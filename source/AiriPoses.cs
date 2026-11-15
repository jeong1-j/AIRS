using System.Drawing;
using System.Drawing.Drawing2D;

internal static class AiriPoses
{
    private static readonly Rectangle[] Frames = {
        new Rectangle(59, 82, 294, 212),
        new Rectangle(414, 86, 297, 208),
        new Rectangle(760, 81, 292, 212),
        new Rectangle(1115, 84, 302, 209),
        new Rectangle(55, 450, 301, 210),
        new Rectangle(403, 487, 298, 173),
        new Rectangle(754, 470, 302, 190),
        new Rectangle(1146, 378, 229, 287),
        new Rectangle(76, 759, 254, 278),
        new Rectangle(417, 756, 255, 281),
        new Rectangle(762, 752, 259, 285),
        new Rectangle(1100, 848, 308, 189),
        new Rectangle(155, 81, 289, 359),
        new Rectangle(635, 83, 289, 357),
        new Rectangle(1117, 82, 287, 358),
        new Rectangle(159, 577, 277, 358),
        new Rectangle(651, 574, 266, 334),
        new Rectangle(1085, 681, 354, 268),
    };

    // Standing, sniffing, looking up, washing, and napping. Each begins and ends at rest.
    private static readonly int[][] Rests = {
        new[] { 4 },
        new[] { 4, 5, 6, 5, 6, 5, 6, 4 },
        new[] { 4, 7, 7, 7, 7, 7, 4 },
        new[] { 4, 8, 9, 10, 9, 8, 9, 10, 9, 8, 4 },
        new[] { 4, 5, 11, 11, 11, 11, 11, 11, 11, 11, 5, 4 }
    };
    public const double RestFrameSeconds = .27;
    private static readonly Image Atlas = Load("airi-poses.png");
    private static readonly Image Interactions = Load("airi-interactions.png");
    private static readonly Image Treats = Load("airi-treats.png");
    private static readonly PointF[] Paws = { new PointF(.625f, .543f), new PointF(.715f, .458f), new PointF(.61f, .556f) };

    private static Image Load(string name)
    {
        using (var stream = typeof(AiriPoses).Assembly.GetManifestResourceStream(name))
        using (var image = Image.FromStream(stream)) return new Bitmap(image);
    }

    public static int RestFrame(int behavior, double elapsed)
    {
        var frames = Rests[behavior];
        return frames[System.Math.Min(frames.Length - 1, (int)(elapsed / RestFrameSeconds))];
    }

    public static double RestDuration(int behavior)
    {
        return Rests[behavior].Length * RestFrameSeconds;
    }

    public static void DrawTreat(Graphics graphics, string id, RectangleF target)
    {
        int treat = id == "seed" ? 0 : id == "berry" ? 1 : id == "apple" ? 2 : 3;
        graphics.DrawImage(Treats, target, new Rectangle(treat * 64, 0, 64, 64), GraphicsUnit.Pixel);
    }

    public static void Draw(Graphics graphics, Size size, int frame, bool left, string outfit, bool happy, ShopItem food = null)
    {
        var source = Frames[frame];
        float scale = System.Math.Min(size.Width / 330f, size.Height / 370f) * (frame >= 12 ? .78f : 1);
        float w = source.Width * scale, h = source.Height * scale;
        var body = new RectangleF((size.Width - w) / 2, size.Height * .91f - h, w, h);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var state = graphics.Save();
        if (left)
        {
            graphics.TranslateTransform(size.Width, 0);
            graphics.ScaleTransform(-1, 1);
        }
        graphics.DrawImage(frame < 12 ? Atlas : Interactions, body, source, GraphicsUnit.Pixel);
        if (food != null && frame >= 12 && frame <= 14)
        {
            var paw = Paws[frame - 12];
            float side = w * .245f * (frame == 14 ? .76f : 1);
            var held = new RectangleF(body.Left + w * paw.X - side / 2, body.Top + h * paw.Y - side / 2, side, side);
            DrawTreat(graphics, food.Id, held);
        }
        if (frame != 11)
        {
            bool upright = frame >= 7 && frame != 17;
            if (outfit == "scarf")
            {
                using (var brush = new SolidBrush(Color.FromArgb(62, 115, 80)))
                {
                    float x = body.Left + w * (upright ? .36f : .55f), y = body.Top + h * .55f;
                    graphics.FillEllipse(brush, x, y, w * .27f, h * .09f);
                    graphics.FillEllipse(brush, x + w * .06f, y + h * .05f, w * .09f, h * .14f);
                }
            }
            if (outfit == "crown" || outfit == "cap")
                HamsterArt.DrawHeadwear(graphics, outfit, body.Left + w * .56f, body.Top - h * .13f, w * .27f);
            if (outfit == "ribbon")
                HamsterArt.DrawRibbon(graphics, body.Left + w * .43f, body.Top + h * .16f, w * .23f);
        }
        graphics.Restore(state);
        if (happy && frame >= 7 && frame <= 10)
            HamsterArt.DrawBadge(graphics, "♥", body.Right - 5, body.Top - 3, 10, Color.FromArgb(181, 65, 97));
    }
}
