using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal sealed class WeeklyStats : Form
{
    private static readonly Color Good = Color.FromArgb(84, 129, 105);
    private static readonly Color Risk = Color.FromArgb(188, 114, 75);
    private static readonly Color Other = Color.FromArgb(229, 193, 133);
    private readonly HamsterProgress progress;
    private readonly Label range, totalLabel, goodLabel, riskLabel, activity, comparison;
    private readonly Button next;
    private readonly Chart bars, donut, calendar;
    private DateTime monday;
    private DailyCount[] week;
    private DailyCount[][] month;
    private int total, risk, good;

    public WeeklyStats(HamsterProgress progress)
    {
        SuspendLayout();
        this.progress = progress;
        monday = Monday(DateTime.Today);
        Text = "AIRS · 주간 통계";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(692, 630);
        MinimumSize = new Size(560, 390);
        AutoScroll = true;
        BackColor = Palette.Paper;
        Palette.RoundWindow(this);

        Palette.AddHomeButton(this, 24, 28);
        Controls.Add(Palette.Label("주간 통계", 125, 17, 247, 43, 24, true, Palette.Ink));
        Controls.Add(Palette.Label("질문 습관을 한 주씩 돌아봐요.", 126, 64, 261, 23, 10, false, Palette.Muted));
        var previous = Palette.Button("‹", 394, 33, 42, 35, false);
        previous.AccessibleName = "이전 주";
        previous.Click += delegate { monday = monday.AddDays(-7); RefreshWeek(); };
        next = Palette.Button("›", 625, 33, 42, 35, false);
        next.AccessibleName = "다음 주";
        next.Click += delegate { monday = monday.AddDays(7); RefreshWeek(); };
        range = Palette.Label("", 440, 35, 181, 29, 10, true, Palette.Green);
        range.TextAlign = ContentAlignment.MiddleCenter;
        Controls.AddRange(new Control[] { previous, next, range });

        AddMetric("확인한 질문", 24, Color.FromArgb(252, 242, 226), out totalLabel, out activity);
        Label caption;
        AddMetric("스스로 확인하는 질문", 243, Color.FromArgb(237, 244, 235), out goodLabel, out caption);
        caption.Text = "힌트 · 풀이 검토 · 근거";
        AddMetric("과의존 신호 비율", 462, Color.FromArgb(249, 237, 228), out riskLabel, out comparison);

        bars = AddChart(new Rectangle(24, 204, 391, 202), "요일별 질문", PaintBars);
        donut = AddChart(new Rectangle(429, 204, 239, 202), "질문 구성", PaintDonut);
        calendar = AddChart(new Rectangle(24, 418, 644, 167), "4주 활동 달력", PaintCalendar);
        Controls.Add(Palette.Label("창에서 감지한 신호와 앱에서 확인한 질문을 집계해요. 같은 날 같은 문장은 한 번만 세어요.",
            25, 595, 646, 29, 9, false, Palette.Muted));
        progress.Changed += OnChanged;
        ResumeLayout(true);
        RefreshWeek();
    }

    private void AddMetric(string title, int x, Color background, out Label value, out Label note)
    {
        var panel = new SurfaceCard(new Rectangle(x, 97, 206, 94), background, Palette.Line);
        panel.Controls.Add(Palette.Label(title, 13, 10, 181, 20, 10, true, Palette.Muted));
        value = Palette.Label("", 12, 32, 181, 36, 18, true, Palette.Ink);
        note = Palette.Label("", 13, 68, 181, 19, 8, false, Palette.Muted);
        panel.Controls.Add(value);
        panel.Controls.Add(note);
        Controls.Add(panel);
    }

    private Chart AddChart(Rectangle bounds, string title, PaintEventHandler paint)
    {
        var card = new Chart(bounds) { AccessibleName = title };
        card.Paint += paint;
        Controls.Add(card);
        return card;
    }

    private static DateTime Monday(DateTime date) { return date.AddDays(-((int)date.DayOfWeek + 6) % 7); }

    private DailyCount[] ReadWeek(DateTime start)
    {
        return Enumerable.Range(0, 7).Select(i =>
        {
            DateTime date = start.AddDays(i);
            return progress.Days.Find(day => day.Day.Date == date) ?? new DailyCount { Day = date };
        }).ToArray();
    }

    private void RefreshWeek()
    {
        week = ReadWeek(monday);
        var previous = ReadWeek(monday.AddDays(-7));
        month = Enumerable.Range(-3, 4).Select(i => ReadWeek(monday.AddDays(i * 7))).ToArray();
        total = week.Sum(day => day.Total);
        risk = week.Sum(day => day.Risk);
        good = week.Sum(day => day.Good);
        int priorTotal = previous.Sum(day => day.Total);
        double rate = total == 0 ? 0 : 100d * risk / total;
        range.Text = monday.ToString("yyyy.MM.dd") + " – " + monday.AddDays(6).ToString("MM.dd");
        next.Enabled = monday < Monday(DateTime.Today);
        totalLabel.Text = total + "건";
        activity.Text = "활동 " + week.Count(day => day.Total > 0) + "일";
        goodLabel.Text = good + "건";
        riskLabel.Text = total == 0 ? "—" : rate.ToString("0.#") + "%";
        comparison.Text = total == 0 ? "아직 기록이 없어요" : "비교할 전주 기록이 없어요";
        comparison.ForeColor = Palette.Muted;
        if (total > 0 && priorTotal > 0)
        {
            double delta = Math.Round(rate - 100d * previous.Sum(day => day.Risk) / priorTotal, 1);
            comparison.Text = delta == 0 ? "전주와 같아요" : "전주보다 " + Math.Abs(delta).ToString("0.#")
                + "%p " + (delta > 0 ? "높아요" : "낮아요");
            comparison.ForeColor = delta > 0 ? Color.FromArgb(136, 76, 48) : Palette.Green;
        }
        bars.AccessibleDescription = string.Join(", ", week.Select(day => day.Day.ToString("MM.dd") + " " + day.Total + "건"));
        donut.AccessibleDescription = "스스로 확인 " + good + "건, 과의존 신호 " + risk + "건, 그 외 " + Math.Max(0, total - good - risk) + "건";
        calendar.AccessibleDescription = string.Join(", ", month.SelectMany(period => period).Select(day => day.Day.ToString("MM.dd") + " " + day.Total + "건"));
        bars.Invalidate();
        donut.Invalidate();
        calendar.Invalidate();
    }

    private void PaintBars(object sender, PaintEventArgs e)
    {
        var g = Canvas(e.Graphics, bars, 391, 202);
        TextAt(g, "요일별 질문", 16, 13, 12, true, Palette.Ink);
        TextAt(g, "색상별 질문 수를 비교해요.", 16, 39, 8, false, Palette.Muted);
        int peak = Math.Max(1, week.Max(day => day.Total));
        const float baseline = 164, height = 91;
        using (var grid = new Pen(Color.FromArgb(237, 227, 215)))
        using (var goodBrush = new SolidBrush(Good))
        using (var riskBrush = new SolidBrush(Risk))
        using (var otherBrush = new SolidBrush(Other))
        {
            for (int i = 0; i <= 4; i++) g.DrawLine(grid, 17, baseline - height * i / 4, 375, baseline - height * i / 4);
            for (int i = 0; i < week.Length; i++)
            {
                var day = week[i];
                float x = 42 + i * 51, y = baseline;
                foreach (var part in new[] {
                    new { count = Math.Max(0, day.Total - day.Good - day.Risk), brush = otherBrush },
                    new { count = day.Risk, brush = riskBrush }, new { count = day.Good, brush = goodBrush } })
                {
                    float segment = height * part.count / peak;
                    y -= segment;
                    if (segment > 0) g.FillRectangle(part.brush, x - 13, y, 26, segment);
                }
                CenterAt(g, day.Total == 0 ? "–" : day.Total.ToString(), x - 20, y - 18, 40, 8, Palette.Muted);
                CenterAt(g, "월화수목금토일"[i].ToString(), x - 20, 171, 40, 9, Palette.Ink);
            }
        }
    }

    private void PaintDonut(object sender, PaintEventArgs e)
    {
        var g = Canvas(e.Graphics, donut, 239, 202);
        TextAt(g, "질문 구성", 16, 13, 12, true, Palette.Ink);
        var circle = new Rectangle(79, 47, 81, 81);
        using (var pen = new Pen(Color.FromArgb(238, 231, 221), 13))
        {
            g.DrawEllipse(pen, circle);
            if (total > 0)
            {
                float start = -90;
                foreach (var part in new[] { new { count = good, color = Good }, new { count = risk, color = Risk },
                    new { count = Math.Max(0, total - good - risk), color = Other } })
                {
                    if (part.count <= 0) continue;
                    float arc = 360f * part.count / total;
                    pen.Color = part.color;
                    g.DrawArc(pen, circle, start, arc);
                    start += arc;
                }
            }
        }
        CenterAt(g, total.ToString(), 80, 70, 79, 18, Palette.Ink);
        CenterAt(g, "질문", 80, 97, 79, 8, Palette.Muted);
        DrawLegend(g, Good, "스스로 확인", good, 138);
        DrawLegend(g, Risk, "과의존 신호", risk, 157);
        DrawLegend(g, Other, "그 외", Math.Max(0, total - good - risk), 176);
    }

    private void PaintCalendar(object sender, PaintEventArgs e)
    {
        var g = Canvas(e.Graphics, calendar, 644, 167);
        TextAt(g, "4주 활동 달력", 16, 12, 12, true, Palette.Ink);
        TextAt(g, "색이 진할수록 확인한 질문이 많아요.", 392, 16, 8, false, Palette.Muted);
        int peak = Math.Max(1, month.SelectMany(period => period).Max(day => day.Total));
        for (int i = 0; i < 7; i++) CenterAt(g, "월화수목금토일"[i].ToString(), 88 + i * 76, 40, 65, 8, Palette.Muted);
        var colors = new[] { Color.FromArgb(244, 236, 225), Color.FromArgb(226, 238, 223),
            Color.FromArgb(198, 223, 196), Color.FromArgb(156, 191, 161), Color.FromArgb(83, 126, 100) };
        for (int row = 0; row < month.Length; row++)
        {
            int y = 59 + row * 24;
            TextAt(g, month[row][0].Day.ToString("M.d"), 27, y + 2, 8, row == 3, Palette.Muted);
            for (int column = 0; column < 7; column++)
            {
                DailyCount day = month[row][column];
                int level = day.Total == 0 ? 0 : (int)Math.Ceiling(4d * day.Total / peak);
                int x = 88 + column * 76;
                using (var fill = new SolidBrush(day.Day > DateTime.Today ? Palette.Paper : colors[level]))
                    g.FillRectangle(fill, x, y, 65, 18);
                CenterAt(g, day.Total == 0 ? "·" : day.Total.ToString(), x, y, 65, 8, level == 4 ? Color.White : Palette.Ink);
            }
        }
    }

    private static void DrawLegend(Graphics g, Color color, string label, int count, int y)
    {
        using (var brush = new SolidBrush(color)) g.FillEllipse(brush, 22, y + 3, 8, 8);
        TextAt(g, label, 35, y, 8, false, Palette.Muted);
        CenterAt(g, count + "건", 167, y, 55, 8, Palette.Ink);
    }

    private static Graphics Canvas(Graphics g, Control control, int width, int height)
    {
        g.PageUnit = GraphicsUnit.Pixel;
        g.ScaleTransform(control.ClientSize.Width / (float)width, control.ClientSize.Height / (float)height);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        return g;
    }

    private static void TextAt(Graphics g, string text, float x, float y, float size, bool bold, Color color)
    {
        using (var font = new Font("Malgun Gothic", size * 96 / 72, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y);
    }

    private static void CenterAt(Graphics g, string text, float x, float y, float width, float size, Color color)
    {
        using (var font = new Font("Malgun Gothic", size * 96 / 72, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var center = new StringFormat { Alignment = StringAlignment.Center })
            g.DrawString(text, font, brush, new RectangleF(x, y, width, 35), center);
    }

    private sealed class Chart : SurfaceCard
    {
        public Chart(Rectangle bounds) : base(bounds, Palette.Surface, Palette.Line) { }
    }
    private void OnChanged(object sender, EventArgs e) { RefreshWeek(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) progress.Changed -= OnChanged;
        base.Dispose(disposing);
    }
}
