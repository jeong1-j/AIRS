using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal enum AppIcon { None, Home, Paw, Chart, Account, Flower, Heart }

internal static class RoundedShape
{
    public static GraphicsPath Path(RectangleF bounds, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        if (diameter <= 0) return path;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Fill(Graphics g, RectangleF bounds, float radius, Color color)
    {
        using (var path = Path(bounds, radius))
        using (var brush = new SolidBrush(color))
            g.FillPath(brush, path);
    }
}

internal class SurfaceCard : Panel
{
    private readonly Color fill, border;
    private readonly float radius;
    public Color FillColor { get { return fill; } }

    public SurfaceCard(Rectangle bounds, Color fill, Color border, float radius = 22)
    {
        this.fill = fill;
        this.border = border;
        this.radius = radius;
        Bounds = bounds;
        DoubleBuffered = true;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RoundedShape.Path(new RectangleF(.6f, .6f, Width - 1.2f, Height - 1.2f), radius * g.DpiX / 96))
        {
            DrawSurface(g, path);
            if (border.A > 0)
                using (var pen = new Pen(border)) g.DrawPath(pen, path);
        }
        base.OnPaint(e);
    }

    protected virtual void DrawSurface(Graphics g, GraphicsPath path)
    {
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
    }
}

internal sealed class PillButton : Button
{
    private readonly bool primary;
    private readonly AppIcon mark;
    private readonly Color tint;
    private bool hover, pressed;

    public PillButton(bool primary, AppIcon mark, Color tint)
    {
        this.primary = primary;
        this.mark = mark;
        this.tint = tint;
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        FlatAppearance.BorderSize = 0;
        ForeColor = primary ? Color.White : Palette.Green;
        Cursor = Cursors.Hand;
        UseMnemonic = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var card = Parent as SurfaceCard;
        using (var background = new SolidBrush(card != null ? card.FillColor : Parent == null ? Palette.Paper : Parent.BackColor))
            g.FillRectangle(background, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = g.DpiX / 96;
        var area = new RectangleF(1, 1, Width - 2, Height - 2);
        Color fill = !Enabled ? Palette.Hush
            : primary ? (pressed || hover ? Palette.DeepGreen : Palette.Green)
            : pressed ? Palette.Mint : hover ? Palette.Wash : tint;
        using (var path = RoundedShape.Path(area, 20 * scale))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(primary && Enabled ? fill : Palette.Line))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
        var textArea = Rectangle.Round(area);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        if (mark != AppIcon.None)
        {
            int inset = (int)(10 * scale), side = Math.Min(Height - 8, (int)(33 * scale));
            var badge = new Rectangle(inset, (Height - side) / 2, side, side);
            RoundedShape.Fill(g, badge, side / 2f, Palette.Surface);
            MarkArt.Draw(g, mark, Rectangle.Inflate(badge, -side / 5, -side / 5));
            textArea = Rectangle.FromLTRB(badge.Right + (int)(7 * scale), 1, Width - inset, Height - 1);
            flags = (flags & ~TextFormatFlags.HorizontalCenter) | TextFormatFlags.Left;
        }
        TextRenderer.DrawText(g, Text, Font, textArea, Enabled ? ForeColor : Palette.Muted, flags);
        if (Focused && ShowFocusCues)
        {
            area.Inflate(-3 * scale, -3 * scale);
            using (var path = RoundedShape.Path(area, 17 * scale))
            using (var pen = new Pen(primary ? Color.White : Palette.Green, Math.Max(1, scale)))
                g.DrawPath(pen, path);
        }
    }
}

internal static class MarkArt
{
    public static void Draw(Graphics g, AppIcon mark, Rectangle area)
    {
        var state = g.Save();
        g.TranslateTransform(area.X, area.Y);
        g.ScaleTransform(area.Width / 32f, area.Height / 32f);
        switch (mark)
        {
            case AppIcon.Home:
                RoundedShape.Fill(g, new RectangleF(5, 13, 22, 16), 5, Palette.Honey);
                using (var roof = new Pen(Palette.Clay, 4) { StartCap = LineCap.Round, LineJoin = LineJoin.Round, EndCap = LineCap.Round })
                    g.DrawLines(roof, new[] { new Point(2, 15), new Point(16, 4), new Point(30, 15) });
                RoundedShape.Fill(g, new RectangleF(13, 18, 7, 11), 3.5f, Palette.Surface);
                break;
            case AppIcon.Paw:
                using (var ink = new SolidBrush(Palette.Pine))
                {
                    g.FillEllipse(ink, 7, 15, 18, 13);
                    g.FillEllipse(ink, 3, 8, 7, 8);
                    g.FillEllipse(ink, 10, 3, 6, 8);
                    g.FillEllipse(ink, 17, 3, 6, 8);
                    g.FillEllipse(ink, 24, 8, 6, 8);
                }
                break;
            case AppIcon.Chart:
                RoundedShape.Fill(g, new RectangleF(3, 17, 7, 11), 3.5f, Palette.Pine);
                RoundedShape.Fill(g, new RectangleF(13, 11, 7, 17), 3.5f, Palette.Honey);
                RoundedShape.Fill(g, new RectangleF(23, 4, 7, 24), 3.5f, Palette.Clay);
                break;
            case AppIcon.Account:
                using (var ink = new SolidBrush(Palette.Pine))
                {
                    g.FillEllipse(ink, 10, 4, 12, 12);
                    g.FillEllipse(ink, 4, 17, 24, 13);
                }
                break;
            case AppIcon.Flower:
                using (var petals = new SolidBrush(Palette.Honey))
                using (var center = new SolidBrush(Palette.Earth))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float x = 16 + 9 * (float)Math.Cos(i * Math.PI / 4);
                        float y = 16 + 9 * (float)Math.Sin(i * Math.PI / 4);
                        g.FillEllipse(petals, x - 5, y - 5, 10, 10);
                    }
                    g.FillEllipse(center, 10, 10, 12, 12);
                }
                break;
            case AppIcon.Heart:
                using (var path = new GraphicsPath())
                using (var ink = new SolidBrush(Palette.Clay))
                {
                    path.AddBezier(16, 27, 2, 19, -1, 6, 8, 6);
                    path.AddBezier(8, 6, 12, 6, 15, 8, 16, 11);
                    path.AddBezier(16, 11, 22, 0, 34, 7, 29, 17);
                    path.AddBezier(29, 17, 27, 21, 21, 25, 16, 27);
                    g.FillPath(ink, path);
                }
                break;
        }
        g.Restore(state);
    }

    public static void Sparkle(Graphics g, float x, float y, float radius, Color color)
    {
        using (var brush = new SolidBrush(color))
            g.FillPolygon(brush, new[] {
                new PointF(x, y - radius), new PointF(x + radius * .24f, y - radius * .25f),
                new PointF(x + radius, y), new PointF(x + radius * .24f, y + radius * .26f),
                new PointF(x, y + radius), new PointF(x - radius * .24f, y + radius * .26f),
                new PointF(x - radius, y), new PointF(x - radius * .24f, y - radius * .25f)
            });
    }
}

internal sealed class WelcomeCard : SurfaceCard
{
    public WelcomeCard(Rectangle bounds) : base(bounds, Palette.Oat, Palette.Line, 31) { }

    protected override void DrawSurface(Graphics g, GraphicsPath path)
    {
        using (var light = new LinearGradientBrush(ClientRectangle, Palette.Oat, Palette.Mint, 15f))
            g.FillPath(light, path);
        var state = g.Save();
        g.SetClip(path);
        g.ScaleTransform(Width / 440f, Height / 205f);
        using (var halo = new SolidBrush(Color.FromArgb(255, 248, 218)))
            g.FillEllipse(halo, 279, 29, 140, 143);
        using (var shade = new SolidBrush(Color.FromArgb(42, 137, 104, 67)))
            g.FillEllipse(shade, 305, 156, 92, 12);
        MarkArt.Sparkle(g, 286, 62, 10, Palette.Honey);
        MarkArt.Sparkle(g, 410, 111, 8, Palette.Clay);
        MarkArt.Sparkle(g, 397, 45, 5, Palette.Pine);
        g.Restore(state);
    }
}

internal sealed class HamsterHabitat : SurfaceCard
{
    public HamsterHabitat(Rectangle bounds) : base(bounds, Palette.Oat, Palette.Line, 20) { }

    protected override void DrawSurface(Graphics g, GraphicsPath path)
    {
        var state = g.Save();
        g.SetClip(path);
        using (var wall = new LinearGradientBrush(ClientRectangle, Palette.Oat, Palette.Peach, 90f))
            g.FillPath(wall, path);
        g.ScaleTransform(Width / 250f, Height / 181f);
        using (var floor = new SolidBrush(Color.FromArgb(238, 210, 177)))
        using (var rug = new SolidBrush(Color.FromArgb(231, 170, 143)))
        using (var rugCenter = new SolidBrush(Color.FromArgb(246, 199, 174)))
        {
            g.FillRectangle(floor, 0, 125, 250, 58);
            g.FillEllipse(rug, 48, 130, 154, 39);
            g.FillEllipse(rugCenter, 54, 133, 142, 30);
        }
        RoundedShape.Fill(g, new RectangleF(20, 21, 51, 62), 20, Color.FromArgb(205, 156, 111));
        RoundedShape.Fill(g, new RectangleF(24, 25, 43, 54), 16, Color.FromArgb(227, 240, 221));
        using (var sun = new SolidBrush(Palette.Honey))
            g.FillEllipse(sun, 31, 33, 13, 13);
        using (var frame = new Pen(Color.FromArgb(205, 156, 111), 2.5f))
        {
            g.DrawLine(frame, 45.5f, 27, 45.5f, 80);
            g.DrawLine(frame, 24, 54, 67, 54);
        }
        RoundedShape.Fill(g, new RectangleF(16, 78, 59, 7), 3, Palette.Clay);
        using (var leaves = new SolidBrush(Palette.Pine))
        {
            g.FillEllipse(leaves, 200, 97, 17, 12);
            g.FillEllipse(leaves, 212, 93, 15, 13);
            g.FillEllipse(leaves, 210, 82, 10, 25);
        }
        RoundedShape.Fill(g, new RectangleF(204, 105, 20, 24), 6, Palette.Clay);
        RoundedShape.Fill(g, new RectangleF(201, 104, 26, 7), 3, Color.FromArgb(230, 173, 145));
        MarkArt.Sparkle(g, 186, 39, 8, Palette.Honey);
        MarkArt.Sparkle(g, 203, 27, 4, Palette.Honey);
        g.Restore(state);
    }
}

internal sealed class ShopSticker : Control
{
    private readonly ShopItem item;

    public ShopSticker(ShopItem item, Rectangle bounds)
    {
        this.item = item;
        Bounds = bounds;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color color = item.Id == "berry" || item.Id == "ribbon" ? Palette.Peach
            : item.Id == "apple" || item.Id == "scarf" ? Palette.Mint : Palette.Oat;
        RoundedShape.Fill(g, new RectangleF(1, 1, Width - 2, Height - 2), Width * .36f, color);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var inner = Rectangle.Inflate(ClientRectangle, -Width / 7, -Height / 7);
        if (item.Id == "seed") MarkArt.Draw(g, AppIcon.Flower, inner);
        else if (item.Kind == "snack") AiriPoses.DrawTreat(g, item.Id, inner);
        else DrawOutfit(g, inner);
    }

    private void DrawOutfit(Graphics g, Rectangle area)
    {
        var state = g.Save();
        g.TranslateTransform(area.X, area.Y);
        g.ScaleTransform(area.Width / 32f, area.Height / 32f);
        switch (item.Id)
        {
            case "scarf":
                RoundedShape.Fill(g, new RectangleF(6, 8, 21, 10), 5, Palette.Pine);
                RoundedShape.Fill(g, new RectangleF(18, 13, 8, 15), 4, Palette.Pine);
                RoundedShape.Fill(g, new RectangleF(7, 11, 17, 2), 1, Color.FromArgb(147, 186, 144));
                break;
            case "ribbon":
                HamsterArt.DrawRibbon(g, 1, 9, 30);
                break;
            case "crown":
                using (var gold = new SolidBrush(Palette.Honey))
                    g.FillPolygon(gold, new[] { new Point(4, 9), new Point(11, 15), new Point(16, 4),
                        new Point(21, 15), new Point(28, 9), new Point(25, 25), new Point(7, 25) });
                RoundedShape.Fill(g, new RectangleF(7, 22, 18, 5), 2, Palette.Clay);
                break;
            case "cap":
                using (var nut = new SolidBrush(Color.FromArgb(196, 132, 79)))
                using (var cap = new SolidBrush(Palette.Earth))
                {
                    g.FillEllipse(nut, 7, 8, 18, 21);
                    g.FillEllipse(cap, 5, 6, 22, 12);
                    g.FillEllipse(cap, 14, 2, 5, 8);
                }
                break;
        }
        g.Restore(state);
    }
}
