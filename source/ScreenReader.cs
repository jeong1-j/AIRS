using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AsyncStatus = Windows.Foundation.AsyncStatus;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

internal static class ScreenReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);

    public static int WindowProcessId(IntPtr window)
    {
        int process;
        GetWindowThreadProcessId(window, out process);
        return process;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);

    public static bool TryGetBounds(IntPtr window, int ownProcessId, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (window == IntPtr.Zero || window == GetDesktopWindow() || window == GetShellWindow()) return false;
        if (WindowProcessId(window) == ownProcessId) return false;

        const int ExtendedFrameBounds = 9;
        NativeRect rect;
        if (DwmGetWindowAttribute(window, ExtendedFrameBounds, out rect, Marshal.SizeOf(typeof(NativeRect))) != 0
            && !GetWindowRect(window, out rect)) return false;

        bounds = Rectangle.Intersect(
            Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom),
            System.Windows.Forms.Screen.FromHandle(window).Bounds);
        return bounds.Width >= 100 && bounds.Height >= 70;
    }

    public static OcrEngine KoreanEngine()
    {
        var language = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(
            item => item.LanguageTag.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
        return language == null ? null : OcrEngine.TryCreateFromLanguage(language);
    }

    public static Bitmap Capture(Rectangle bounds)
    {
        var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
        try
        {
            using (var graphics = Graphics.FromImage(image))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static async Task<ScreenText> Read(OcrEngine engine, Bitmap image)
    {
        var full = await ReadScaled(engine, image, 1).ConfigureAwait(false);
        // Chat composers are usually near the bottom and short, small Hangul is easy to misread at 1x.
        int height = Math.Min(image.Height, Math.Max(130, Math.Min(260, image.Height / 3)));
        var region = new Rectangle(0, image.Height - height, image.Width, height);
        using (var strip = image.Clone(region, PixelFormat.Format32bppRgb))
        {
            var detail = await ReadScaled(engine, strip, 2).ConfigureAwait(false);
            if (detail.Lines.Length == 0) return full;
            foreach (var line in detail.Lines)
                line.Area.Offset(0, region.Top);
            var smallText = detail.Lines.Where(line => line.Area.Top > region.Top + 3
                && !full.Lines.Any(original => original.Area.Height >= 24 && SameRow(line.Area, original.Area)))
                .ToArray();
            var rest = full.Lines.Where(line => !smallText.Any(extra => SameRow(line.Area, extra.Area)));
            return new ScreenText(rest.Concat(smallText).OrderBy(line => line.Area.Top).ThenBy(line => line.Area.Left));
        }
    }

    public static async Task<ScreenText> ReadNearEditor(OcrEngine engine, Bitmap image, Rectangle editor)
    {
        editor.Inflate(10, 8);
        var region = Rectangle.Intersect(editor, new Rectangle(Point.Empty, image.Size));
        if (region.Width == 0 || region.Height == 0) return await Read(engine, image).ConfigureAwait(false);
        using (var field = image.Clone(region, PixelFormat.Format32bppRgb))
        {
            var draft = await ReadScaled(engine, field, 1.75).ConfigureAwait(false);
            if (draft.Lines.Length == 0)
                draft = await ReadScaled(engine, field, 1).ConfigureAwait(false);
            foreach (var line in draft.Lines) line.Area.Offset(region.Location);
            return draft;
        }
    }

    private static bool SameRow(Rectangle first, Rectangle second)
    {
        return first.Right > second.Left && second.Right > first.Left
            && Math.Abs(first.Top - second.Top) <= Math.Max(10, Math.Max(first.Height, second.Height) / 2);
    }

    private static async Task<ScreenText> ReadScaled(OcrEngine engine, Bitmap image, double requestedScale)
    {
        double scale = Math.Min(requestedScale, (double)OcrEngine.MaxImageDimension / Math.Max(image.Width, image.Height));
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        using (var scaled = scale != 1 ? new Bitmap(width, height, PixelFormat.Format32bppRgb) : null)
        using (var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore))
        {
            if (scaled != null)
            {
                using (var graphics = Graphics.FromImage(scaled))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, width, height);
                }
            }

            var source = scaled ?? image;
            var data = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                byte[] pixels = new byte[width * 4 * height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                using (var writer = new DataWriter())
                {
                    writer.WriteBytes(pixels);
                    bitmap.CopyFromBuffer(writer.DetachBuffer());
                }
            }
            finally
            {
                source.UnlockBits(data);
            }

            var operation = engine.RecognizeAsync(bitmap);
            try
            {
                while (operation.Status == AsyncStatus.Started) await Task.Delay(10).ConfigureAwait(false);
                var result = operation.GetResults();
                return new ScreenText(result.Lines.Select(line => new ScreenLine
                {
                    Text = line.Text,
                    Area = line.Words.Select(word => Rectangle.FromLTRB(
                        (int)(word.BoundingRect.Left / scale), (int)(word.BoundingRect.Top / scale),
                        (int)Math.Ceiling(word.BoundingRect.Right / scale),
                        (int)Math.Ceiling(word.BoundingRect.Bottom / scale))).Aggregate(Rectangle.Empty,
                            (area, word) => area.IsEmpty ? word : Rectangle.Union(area, word))
                }));
            }
            finally
            {
                operation.Close();
            }
        }
    }
}

internal sealed class ScreenLine
{
    public string Text;
    public Rectangle Area;
}

internal sealed class ScreenText
{
    public readonly ScreenLine[] Lines;
    public readonly string Text;

    public ScreenText(IEnumerable<ScreenLine> lines)
    {
        Lines = lines.ToArray();
        Text = string.Join("\n", Lines.Select(line => line.Text));
    }
}
