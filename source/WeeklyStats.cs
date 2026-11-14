using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class WeeklyStats : Form
{
    private readonly HamsterProgress progress;
    private readonly Label range, headline, comparison, counts;
    private readonly Button next;
    private readonly Panel chart;
    private DateTime monday;
    private DailyCount[] week;

    public WeeklyStats(HamsterProgress progress)
    {
        this.progress = progress;
        monday = Monday(DateTime.Today);
        Text = "AIRS · 주간 통계";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(567, 465);
        BackColor = Palette.Paper;
        Palette.RoundWindow(this);

        Controls.Add(Palette.Label("주간 통계", 24, 17, 310, 48, 23, true, Palette.Ink));
        var previous = Palette.Button("이전 주", 24, 78, 81, 33, false);
        previous.Click += delegate { monday = monday.AddDays(-7); RefreshWeek(); };
        Controls.Add(previous);
        next = Palette.Button("다음 주", 462, 78, 81, 33, false);
        next.Click += delegate { monday = monday.AddDays(7); RefreshWeek(); };
        Controls.Add(next);
        range = Palette.Label("", 112, 82, 339, 27, 11, true, Palette.Green);
        range.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(range);
        headline = Palette.Label("", 25, 131, 514, 39, 20, true, Palette.Ink);
        comparison = Palette.Label("", 25, 174, 514, 31, 10, true, Palette.Muted);
        counts = Palette.Label("", 25, 210, 514, 24, 10, false, Palette.Muted);
        Controls.Add(headline);
        Controls.Add(comparison);
        Controls.Add(counts);

        chart = new Panel { Bounds = new Rectangle(24, 244, 519, 155), BackColor = Color.White };
        Palette.ClipCorners(chart, 18);
        chart.Paint += PaintChart;
        Controls.Add(chart);
        Controls.Add(Palette.Label("화면에서 감지한 신호·수정과 햄스터 집에서 확인한 질문을 집계해요. 같은 날 같은 문장은 한 번만 세어요.", 25, 413, 514, 42, 9, false, Palette.Muted));
        progress.Changed += OnChanged;
        RefreshWeek();
    }

    private static DateTime Monday(DateTime date)
    {
        return date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    }

    private DailyCount[] ReadWeek(DateTime start)
    {
        return Enumerable.Range(0, 7).Select(i =>
        {
            DateTime day = start.AddDays(i);
            return progress.Days.Find(entry => entry.Day == day) ?? new DailyCount { Day = day };
        }).ToArray();
    }

    private void RefreshWeek()
    {
        week = ReadWeek(monday);
        var last = ReadWeek(monday.AddDays(-7));
        int total = week.Sum(day => day.Total);
        int risk = week.Sum(day => day.Risk);
        int good = week.Sum(day => day.Good);
        int priorTotal = last.Sum(day => day.Total);
        double rate = total == 0 ? 0 : 100d * risk / total;
        range.Text = monday.ToString("yyyy.MM.dd") + " ~ " + monday.AddDays(6).ToString("MM.dd");
        next.Enabled = monday < Monday(DateTime.Today);
        headline.Text = total == 0 ? "아직 기록이 없어요" : "과의존 신호 " + rate.ToString("0.#") + "%";
        if (total == 0 || priorTotal == 0)
            comparison.Text = "두 주에 기록이 있으면 신호 비율을 비교할 수 있어요.";
        else
        {
            double delta = Math.Round(rate - 100d * last.Sum(day => day.Risk) / priorTotal, 1);
            comparison.Text = delta == 0 ? "전주와 신호 비율이 같아요."
                : "전주보다 " + Math.Abs(delta).ToString("0.#") + "%p " + (delta > 0 ? "높아요." : "낮아요.");
        }
        counts.Text = "확인 " + total + "건   ·   과의존 신호 " + risk + "건   ·   스스로 확인하는 질문 " + good + "건";
        chart.AccessibleName = "요일별 확인과 과의존 신호. " + string.Join(", ", week.Select(day =>
            day.Day.ToString("MM.dd") + " 확인 " + day.Total + "건, 신호 " + day.Risk + "건"));
        chart.Invalidate();
    }

    private void PaintChart(object sender, PaintEventArgs e)
    {
        int max = Math.Max(1, week.Max(day => day.Total));
        float column = chart.ClientSize.Width / 7f;
        float baseline = chart.ClientSize.Height - 30;
        using (var signal = new SolidBrush(Color.FromArgb(176, 112, 61)))
        using (var other = new SolidBrush(Color.FromArgb(190, 210, 179)))
        using (var ink = new SolidBrush(Palette.Ink))
        using (var font = Palette.Font(9))
        using (var center = new StringFormat { Alignment = StringAlignment.Center })
        {
            for (int i = 0; i < week.Length; i++)
            {
                var day = week[i];
                float x = column * (i + .5f);
                float unit = (baseline - 28) / max;
                float total = day.Total * unit;
                float risk = day.Risk * unit;
                e.Graphics.FillRectangle(other, x - 13, baseline - total, 26, Math.Max(2, total));
                e.Graphics.FillRectangle(signal, x - 13, baseline - risk, 26, risk);
                e.Graphics.DrawString(day.Risk + "/" + day.Total, font, ink, x, baseline - total - 20, center);
                e.Graphics.DrawString("월화수목금토일"[i].ToString(), font, ink, x, baseline + 8, center);
            }
        }
    }

    private void OnChanged(object sender, EventArgs args) { RefreshWeek(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) progress.Changed -= OnChanged;
        base.Dispose(disposing);
    }
}
