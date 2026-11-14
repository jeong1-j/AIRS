using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

internal sealed class ShopItem
{
    public readonly string Id, Name, Kind;
    public readonly int Cost, Bond;
    public ShopItem(string id, string name, string kind, int cost, int bond)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Cost = cost;
        Bond = bond;
    }
}

[DataContract]
internal sealed class HamsterProgress
{
    public const int Reward = 10;
    public const int BondPerLevel = 5;
    public static readonly ShopItem[] Shop = {
        new ShopItem("seed", "씨앗 간식", "snack", 10, 1),
        new ShopItem("berry", "딸기 간식", "snack", 25, 3),
        new ShopItem("apple", "사과 한 조각", "snack", 20, 2),
        new ShopItem("carrot", "당근 간식", "snack", 35, 4),
        new ShopItem("scarf", "초록 스카프", "outfit", 30, 0),
        new ShopItem("crown", "별 왕관", "outfit", 50, 0),
        new ShopItem("ribbon", "복숭아 리본", "outfit", 40, 0),
        new ShopItem("cap", "도토리 모자", "outfit", 60, 0)
    };

    internal static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "THINK BACK");
    private static readonly string SavePath = Path.Combine(Folder, "hamster.json");
    private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(HamsterProgress));

    [DataMember] public int Points;
    [DataMember] public int Bond;
    [DataMember] public bool Roam;
    [DataMember] public string Outfit = "";
    [DataMember] public List<string> Owned = new List<string>();
    [DataMember] public List<string> Earned = new List<string>();
    [DataMember] public List<DailyCount> Days = new List<DailyCount>();
    private string profilePath;
    public bool Unlimited { get; private set; }

    public event EventHandler Changed;
    public event Action<ShopItem> Fed;

    public static HamsterProgress Load()
    {
        return Read(SavePath, false);
    }

    internal static HamsterProgress Load(LocalAccount account, string folder = null)
    {
        return Read(Path.Combine(folder ?? Folder, "profiles", account.Id + ".json"), account.Admin);
    }

    private static HamsterProgress Read(string path, bool unlimited)
    {
        try
        {
            if (File.Exists(path))
            {
                using (var input = File.OpenRead(path))
                {
                    var state = (HamsterProgress)Serializer.ReadObject(input);
                    state.Owned = state.Owned ?? new List<string>();
                    state.Earned = state.Earned ?? new List<string>();
                    state.Days = state.Days ?? new List<DailyCount>();
                    state.profilePath = path;
                    state.Unlimited = unlimited;
                    return state;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SerializationException) { }
        return new HamsterProgress { profilePath = path, Unlimited = unlimited };
    }

    private void Save()
    {
        try
        {
            if (profilePath != null) LocalAccounts.WriteJson(profilePath, Serializer, this);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    public void SetRoam(bool enabled)
    {
        Roam = enabled;
        Save();
    }

    private static string QuestionKey(string question)
    {
        using (var hash = SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(Detector.Compact(question))));
    }

    public void Record(string question, bool risk, bool good)
    {
        DateTime today = DateTime.Today;
        var day = Days.Find(entry => entry.Day == today);
        if (day == null)
        {
            day = new DailyCount { Day = today };
            Days.Add(day);
        }
        if (day.Keys == null) day.Keys = new List<string>();
        string key = QuestionKey(question);
        if (day.Keys.Contains(key)) return;
        day.Keys.Add(key);
        day.Total++;
        if (risk) day.Risk++;
        if (good) day.Good++;
        Save();
    }

    public bool Award(string question)
    {
        if (Unlimited) return false;
        if (!Detector.IsGood(question)) return false;
        string key = QuestionKey(question);
        if (Earned.Contains(key)) return false;
        Earned.Add(key);
        Points += Reward;
        Save();
        return true;
    }

    public bool GetItem(ShopItem item)
    {
        if (item.Kind == "outfit" && Owned.Contains(item.Id))
        {
            Outfit = Outfit == item.Id ? "" : item.Id;
        }
        else
        {
            if (!Unlimited && Points < item.Cost) return false;
            if (!Unlimited) Points -= item.Cost;
            Bond += item.Bond;
            if (item.Kind == "outfit")
            {
                Owned.Add(item.Id);
                Outfit = item.Id;
            }
        }
        Save();
        if (item.Kind == "snack" && Fed != null) Fed(item);
        return true;
    }
}

[DataContract]
internal sealed class DailyCount
{
    [DataMember] public DateTime Day;
    [DataMember] public int Total, Risk, Good;
    [DataMember] public List<string> Keys = new List<string>();
}

internal sealed class HamsterArt : Control
{
    private static readonly Image Idle = Load("hamster.png");
    private static readonly Image Peek = Load("hamster-peek.png");
    public string Outfit = "";
    public bool Alert;
    public bool Happy;
    public int ActionFrame = -1;
    public ShopItem ActionFood;

    public HamsterArt()
    {
        DoubleBuffered = true;
        BackColor = Palette.Paper;
    }

    private static Image Load(string name)
    {
        using (var stream = typeof(HamsterArt).Assembly.GetManifestResourceStream(name))
        using (var maskStream = typeof(HamsterArt).Assembly.GetManifestResourceStream(
            Path.GetFileNameWithoutExtension(name) + "-mask.png"))
        using (var mask = new Bitmap(maskStream))
        using (var image = new Bitmap(stream))
        {
            var sprite = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int alpha = mask.GetPixel(x, y).A;
                    if (alpha > 0) sprite.SetPixel(x, y, Color.FromArgb(alpha, image.GetPixel(x, y)));
                }
            return sprite;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawSprite(e.Graphics, ClientSize);
    }

    public void DrawSprite(Graphics g, Size size)
    {
        if (ActionFrame >= 0)
        {
            AiriPoses.Draw(g, size, ActionFrame, false, Outfit, Happy, ActionFood);
            return;
        }
        var image = Alert ? Peek : Idle;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        float scale = Math.Min(size.Width / (float)image.Width, size.Height / (float)image.Height);
        float w = image.Width * scale, h = image.Height * scale;
        var area = new RectangleF((size.Width - w) / 2f, (size.Height - h) / 2f, w, h);
        g.DrawImage(image, area);
        if (!Alert && Outfit == "scarf")
        {
            using (var scarf = new SolidBrush(Color.FromArgb(62, 115, 80)))
            {
                g.FillEllipse(scarf, area.Left + w * .38f, area.Top + h * .57f, w * .33f, h * .071f);
                g.FillPolygon(scarf, new[] {
                    new PointF(area.Left + w * .59f, area.Top + h * .61f),
                    new PointF(area.Left + w * .68f, area.Top + h * .60f),
                    new PointF(area.Left + w * .66f, area.Top + h * .71f),
                    new PointF(area.Left + w * .57f, area.Top + h * .72f)
                });
            }
        }
        if (!Alert && (Outfit == "crown" || Outfit == "cap"))
            DrawHeadwear(g, Outfit, area.Left + w * .46f, area.Top + h * .09f, w * .27f);
        if (!Alert && Outfit == "ribbon")
            DrawRibbon(g, area.Left + w * .65f, area.Top + h * .27f, w * .21f);
        if (Happy)
            DrawBadge(g, "♥", area.Left + w * .78f, area.Top, 20, Color.FromArgb(181, 65, 97));
    }

    internal static void DrawBadge(Graphics g, string text, float x, float y, int size, Color color)
    {
        using (var font = new Font("Segoe UI Symbol", size * g.DpiX / 96f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
            g.DrawString(text, font, brush, x, y);
    }

    internal static void DrawHeadwear(Graphics g, string outfit, float x, float y, float size)
    {
        if (outfit == "crown")
        {
            DrawBadge(g, "♛", x, y, Math.Max(9, (int)size), Color.FromArgb(171, 105, 31));
            return;
        }
        using (var cap = new SolidBrush(Color.FromArgb(137, 89, 58)))
        using (var rim = new SolidBrush(Color.FromArgb(185, 128, 84)))
        {
            g.FillEllipse(cap, x, y, size, size * .56f);
            g.FillEllipse(rim, x - size * .06f, y + size * .33f, size * 1.12f, size * .25f);
        }
    }

    internal static void DrawRibbon(Graphics g, float x, float y, float size)
    {
        using (var ribbon = new SolidBrush(Color.FromArgb(212, 112, 116)))
        {
            g.FillEllipse(ribbon, x, y, size * .55f, size * .44f);
            g.FillEllipse(ribbon, x + size * .44f, y, size * .55f, size * .44f);
            g.FillEllipse(ribbon, x + size * .37f, y + size * .09f, size * .28f, size * .31f);
        }
    }
}

internal sealed class HamsterWalker : Form
{
    private readonly Timer motion = new Timer { Interval = 25 };
    private readonly Stopwatch clock = new Stopwatch();
    private readonly HamsterWalk path = new HamsterWalk();
    private double previousTime;
    private Screen screen;
    private string outfit;
    private bool happy;
    private int shownPose = -1;
    private bool shownFacing;
    private Bitmap sprite;
    private Point dragOffset;
    private bool dragging;
    private double dropVelocity;
    private double feedTime;
    private ShopItem food;

    public HamsterWalker()
    {
        Text = "AIRS · 햄스터";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(51, 65);
        Cursor = Cursors.Hand;
        motion.Tick += delegate { Walk(); };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var value = base.CreateParams;
            value.ExStyle |= 0x08080080; // Per-pixel transparency leaves empty pixels clickable through.
            return value;
        }
    }

    public void RefreshState(HamsterProgress progress, bool canShow)
    {
        if (outfit != progress.Outfit || happy != (progress.Bond >= 3)) shownPose = -1;
        outfit = progress.Outfit;
        happy = progress.Bond >= 3;
        if (progress.Roam && canShow)
        {
            if (Visible)
            {
                Place();
                return;
            }
            screen = Screen.FromPoint(HasPosition ? Location : Cursor.Position);
            Rectangle bounds = WalkBounds();
            if (!HasPosition)
                path.Start(new PointF(bounds.Left + Math.Min(30, bounds.Width), bounds.Bottom));
            HasPosition = true;
            path.Step(0, bounds);
            Show();
            shownPose = -1;
            Place();
            previousTime = 0;
            clock.Restart();
            motion.Start();
        }
        else
        {
            dragging = false;
            Capture = false;
            dropVelocity = 0;
            Cursor = Cursors.Hand;
            feedTime = 0;
            food = null;
            motion.Stop();
            clock.Stop();
            Hide();
        }
    }

    private bool HasPosition;

    private Rectangle WalkBounds()
    {
        var area = screen.WorkingArea;
        int left = area.Left + 12;
        int bottom = Math.Max(area.Top, area.Bottom - Height - 8);
        return Rectangle.FromLTRB(left, Math.Max(area.Top, bottom - Math.Min(190, area.Height / 4)),
            Math.Max(left, area.Right - Width - 12), bottom);
    }

    private void Walk()
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Min(.08, now - previousTime);
        previousTime = now;
        screen = Screen.FromControl(this);
        if (dragging)
        {
            if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left) MoveWithPointer();
            else ReleaseHamster();
        }
        else if (dropVelocity > 0)
        {
            dropVelocity += 970 * dt;
            var area = WalkBounds();
            float y = Math.Min(area.Bottom, path.Position.Y + (float)(dropVelocity * dt));
            path.Reposition(new PointF(path.Position.X, y));
            if (y >= area.Bottom) { dropVelocity = 0; path.Land(); }
        }
        else if (feedTime > 0)
        {
            feedTime = Math.Max(0, feedTime - dt);
            if (feedTime == 0) { food = null; path.Land(); }
        }
        else path.Step(dt, WalkBounds());
        Place();
    }

    public void Feed(ShopItem item)
    {
        if (!Visible) return;
        food = item;
        feedTime = 2.6;
        shownPose = -1;
        Place();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        dragOffset = e.Location;
        dragging = true;
        dropVelocity = 0;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) MoveWithPointer();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && dragging) ReleaseHamster();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (dragging && !Capture) ReleaseHamster();
    }

    private void MoveWithPointer()
    {
        Point mouse = System.Windows.Forms.Cursor.Position;
        Rectangle work = Screen.FromPoint(mouse).WorkingArea;
        int x = Math.Max(work.Left, Math.Min(mouse.X - dragOffset.X, work.Right - Width));
        int y = Math.Max(work.Top, Math.Min(mouse.Y - dragOffset.Y, work.Bottom - Height - 8));
        path.Reposition(new PointF(x, y));
        Place();
    }

    private void ReleaseHamster()
    {
        dragging = false;
        Capture = false;
        Cursor = Cursors.Hand;
        screen = Screen.FromControl(this);
        dropVelocity = 30;
        Place();
    }

    private void Place()
    {
        var position = Point.Round(path.Position);
        int pose = dragging ? 15 + (int)(clock.Elapsed.TotalSeconds * 4) % 2
            : dropVelocity > 0 ? 16 : feedTime > 0 ? 12 + (int)((2.6 - feedTime) / .24) % 3 : path.Pose;
        if (sprite == null || sprite.Size != ClientSize)
        {
            if (sprite != null) sprite.Dispose();
            sprite = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            shownPose = -1;
        }
        if (pose == shownPose && path.FacingLeft == shownFacing && Location == position) return;
        if (pose != shownPose || path.FacingLeft != shownFacing)
        {
            using (var graphics = Graphics.FromImage(sprite))
            {
                graphics.Clear(Color.Transparent);
                AiriPoses.Draw(graphics, ClientSize, pose, path.FacingLeft, outfit, happy, food);
            }
        }
        if (Location != position) Location = position;
        if (!LayeredSprite.ShowFrame(Handle, sprite, position))
        {
            Debug.WriteLine("AIRS sprite could not be shown: " + Marshal.GetLastWin32Error());
            return;
        }
        shownPose = pose;
        shownFacing = path.FacingLeft;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            motion.Dispose();
            if (sprite != null) sprite.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class LayeredSprite
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Blend
    {
        public byte Operation, Flags, Opacity, AlphaFormat;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref Point position,
        ref Size size, IntPtr source, ref Point origin, int colorKey, ref Blend blend, int flags);

    public static bool ShowFrame(IntPtr window, Bitmap frame, Point position)
    {
        IntPtr bitmap = frame.GetHbitmap(Color.FromArgb(0));
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr previous = SelectObject(dc, bitmap);
        try
        {
            var size = frame.Size;
            var origin = Point.Empty;
            var blend = new Blend { Opacity = 255, AlphaFormat = 1 };
            return UpdateLayeredWindow(window, IntPtr.Zero, ref position, ref size, dc, ref origin, 0, ref blend, 2);
        }
        finally
        {
            SelectObject(dc, previous);
            DeleteObject(bitmap);
            DeleteDC(dc);
        }
    }
}

internal sealed class HamsterWalk
{
    private readonly Random random;
    private PointF target;
    private double pause;
    private double speed;
    private double restElapsed;
    private int behavior;
    private int arrivals;
    private double pace;
    private double landing;

    public PointF Position { get; private set; }
    public bool FacingLeft { get; private set; }
    private double travel;
    public int Pose { get; private set; }

    public HamsterWalk() : this(new Random()) { }

    public HamsterWalk(Random random) { this.random = random; }

    public void Start(PointF position)
    {
        Position = target = position;
        BeginRest(1);
    }

    public void Step(double dt, Rectangle bounds)
    {
        Position = Clamp(Position, bounds);
        target = Clamp(target, bounds);
        if (landing > 0)
        {
            landing -= dt;
            if (landing <= 0) BeginRest(1);
            return;
        }
        double x = target.X - Position.X;
        double y = target.Y - Position.Y;
        double distance = Math.Sqrt(x * x + y * y);
        if (distance <= 1)
        {
            speed = 0;
            restElapsed += dt;
            Pose = AiriPoses.RestFrame(behavior, restElapsed);
            pause -= dt;
            if (pause > 0) return;
            if (bounds.Width < 2 && bounds.Height < 2) return;
            target = PickTarget(bounds);
            travel = 0;
            pace = 40 + random.NextDouble() * 22;
            if (Math.Abs(target.X - Position.X) > 5) FacingLeft = target.X < Position.X;
            return;
        }

        double desired = Math.Min(pace, Math.Sqrt(2 * 104 * distance));
        speed = Math.Min(desired, speed + 104 * dt);
        double step = Math.Min(distance, speed * dt);
        Position = new PointF((float)(Position.X + x / distance * step), (float)(Position.Y + y / distance * step));
        travel += step;
        Pose = (int)(travel / 6) % 4;
        if (distance - step <= 1)
        {
            Position = target;
            speed = 0;
            arrivals++;
            BeginRest(arrivals % 5 == 0 ? 4 : 1 + random.Next(3));
        }
    }

    public void Reposition(PointF position)
    {
        Position = target = position;
        speed = 0;
    }

    public void Land()
    {
        Reposition(Position);
        landing = .29;
        Pose = 17;
    }

    private void BeginRest(int nextBehavior)
    {
        behavior = nextBehavior;
        restElapsed = 0;
        Pose = 4;
        pause = AiriPoses.RestDuration(behavior) + .18 + random.NextDouble() * .55;
    }

    private PointF PickTarget(Rectangle bounds)
    {
        double distance = 100 + random.NextDouble() * 190;
        if (random.Next(2) == 0) distance = -distance;
        float x = (float)(Position.X + distance);
        if (x < bounds.Left || x > bounds.Right) x = (float)(Position.X - distance);
        float y = Position.Y + (float)((random.NextDouble() - .5) * Math.Min(52, bounds.Height));
        return Clamp(new PointF(x, y), bounds);
    }

    private static PointF Clamp(PointF value, Rectangle bounds)
    {
        return new PointF(Math.Max(bounds.Left, Math.Min(bounds.Right, value.X)),
            Math.Max(bounds.Top, Math.Min(bounds.Bottom, value.Y)));
    }
}
