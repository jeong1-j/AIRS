using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Media.Ocr;

internal static class Program
{
    public const string Name = "AIRS";
    public const string FullName = "AIRS (AI Reliance Regulation System)";
    internal static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [STAThread]
    private static void Main()
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool firstInstance;
        using (var instance = new Mutex(true, @"Local\ThinkBackScreenCompanion", out firstInstance))
        {
            if (firstInstance)
            {
                Application.Run(new ControlWindow());
            }
            else
            {
                MessageBox.Show("AIRS가 실행 중이에요. 작업 표시줄 오른쪽의 아이콘을 눌러 열 수 있어요.",
                    Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

internal sealed class ControlWindow : Form
{
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem monitorMenu;
    private readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer { Interval = 1450 };
    private readonly System.Windows.Forms.Timer hideBubble = new System.Windows.Forms.Timer { Interval = 25000 };
    private readonly Button toggle;
    private readonly Label status;
    private HamsterProgress progress = HamsterProgress.Load();
    private LocalAccount activeAccount;
    private LocalAccounts accounts;
    private readonly Button accountButton, adminButton;
    private readonly ToolStripMenuItem accountMenu, adminMenu;
    private readonly HamsterWalker walker = new HamsterWalker();
    private readonly Button roam;
    private readonly ToolStripMenuItem roamMenu;
    private readonly Bubble bubble = new Bubble();
    private readonly int processId = Process.GetCurrentProcess().Id;
    private OcrEngine engine;
    private bool monitoring;
    private bool busy;
    private bool exitRequested;
    private bool trayHintShown;
    private int session;
    private IntPtr lastWindow;
    private DateTime lastSeen;
    private IntPtr coachingWindow;
    private string coachingBaseline;
    private DateTime coachingUntil;
    private string lastRiskKey;

    public ControlWindow()
    {
        Text = Program.FullName;
        Icon = Program.AppIcon;
        Font = Palette.Font(10f);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(482, 607);
        BackColor = Palette.Paper;
        ForeColor = Palette.Ink;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Palette.RoundWindow(this);

        var backdrop = new Panel { Bounds = new Rectangle(336, 52, 119, 119), BackColor = Palette.PaleGreen };
        Palette.ClipCorners(backdrop, 59);
        var mascot = new HamsterArt { Bounds = new Rectangle(12, 5, 100, 106), BackColor = Palette.PaleGreen };
        backdrop.Controls.Add(mascot);
        Controls.Add(backdrop);
        Controls.Add(Palette.Label(Program.FullName, 30, 27, 305, 26, 10f, true, Palette.Green));
        Controls.Add(Palette.Label("AI 질문 앞에서\n한 번 더 생각하기", 28, 64, 320, 92, 25f, true, Palette.Ink));
        Controls.Add(Palette.Label("지금 보고 있는 창에서 AI에게 답이나 과제를 맡기는 표현을 찾으면 캐릭터가 알려줘요.", 30, 171, 420, 57, 11f, false, Palette.Muted));

        var line = new Panel { Bounds = new Rectangle(30, 240, 422, 1), BackColor = Palette.Line };
        Controls.Add(line);
        status = Palette.Label("●  감지 꺼짐", 30, 254, 420, 44, 10f, true, Palette.Muted);
        Controls.Add(status);
        toggle = Palette.Button("화면 감지 시작", 30, 305, 203, 46, true);
        toggle.Click += delegate { SetMonitoring(!monitoring); };
        Controls.Add(toggle);
        var room = Palette.Button("햄스터 집", 247, 305, 205, 46, false);
        room.Click += delegate { OpenRoom(); };
        Controls.Add(room);
        roam = Palette.Button("", 30, 364, 203, 40, false);
        roam.Click += delegate { progress.SetRoam(!progress.Roam); };
        Controls.Add(roam);
        var demo = Palette.Button("브라우저 시연 ↗", 247, 364, 205, 40, false);
        demo.Click += delegate { OpenDemo(); };
        Controls.Add(demo);
        var stats = Palette.Button("주간 통계", 30, 415, 422, 40, false);
        stats.Click += delegate { OpenStats(); };
        Controls.Add(stats);
        accountButton = Palette.Button("로그인 · 이 PC 계정", 30, 466, 203, 40, false);
        accountButton.Click += delegate { OpenAccount(); };
        Controls.Add(accountButton);
        adminButton = Palette.Button("관리자 · 학생 활동", 247, 466, 205, 40, false);
        adminButton.Click += delegate { OpenAdmin(); };
        adminButton.Visible = false;
        Controls.Add(adminButton);
        Controls.Add(Palette.Label("창을 닫아도 켜 둔 감지는 계속돼요. 작업 표시줄 오른쪽 아이콘에서 감지를 멈추거나 앱을 종료할 수 있어요. 화면은 이 PC에서만 읽어요.", 30, 521, 420, 62, 9f, false, Palette.Muted));

        var menu = new ContextMenuStrip();
        menu.Items.Add("AIRS 열기", null, delegate { Restore(); });
        monitorMenu = new ToolStripMenuItem("화면 감지 시작", null, delegate { SetMonitoring(!monitoring); });
        menu.Items.Add(monitorMenu);
        menu.Items.Add("햄스터 집", null, delegate { OpenRoom(); });
        menu.Items.Add("주간 통계", null, delegate { OpenStats(); });
        accountMenu = new ToolStripMenuItem("로그인 · 이 PC 계정", null, delegate { OpenAccount(); });
        adminMenu = new ToolStripMenuItem("관리자 · 학생 활동", null, delegate { OpenAdmin(); }) { Visible = false };
        menu.Items.Add(accountMenu);
        menu.Items.Add(adminMenu);
        roamMenu = new ToolStripMenuItem("", null, delegate { progress.SetRoam(!progress.Roam); });
        menu.Items.Add(roamMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, delegate { Exit(); });
        tray = new NotifyIcon { Icon = Icon, Text = "AIRS · 감지 꺼짐", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) Restore(); };
        poll.Tick += async delegate { await Scan(); };
        hideBubble.Tick += delegate { DismissBubble(); };
        bubble.Dismissed += delegate { DismissBubble(); };
        progress.Changed += OnProgressChanged;
        progress.Fed += walker.Feed;
        Shown += delegate { RefreshHamster(); };
        Resize += delegate { if (WindowState == FormWindowState.Minimized) HideToTray(); };
    }

    private void OnProgressChanged(object sender, EventArgs e) { RefreshHamster(); }

    private void OpenAccount()
    {
        if (!Enabled) return;
        Restore();
        if (activeAccount != null)
        {
            if (MessageBox.Show(this, activeAccount.Name + " 계정에서 로그아웃할까요?", "AIRS 계정",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) SwitchAccount(null);
            return;
        }
        try
        {
            if (accounts == null) accounts = new LocalAccounts();
            using (var login = new AccountWindow(accounts))
                if (login.ShowDialog(this) == DialogResult.OK) SwitchAccount(login.Account);
        }
        catch (IOException) { ShowAccountError(); }
        catch (UnauthorizedAccessException) { ShowAccountError(); }
        catch (System.Runtime.Serialization.SerializationException) { ShowAccountError(); }
    }

    private void SwitchAccount(LocalAccount account)
    {
        SetMonitoring(false);
        progress.Changed -= OnProgressChanged;
        progress.Fed -= walker.Feed;
        activeAccount = account;
        if (account == null && accounts != null) accounts.SignOut();
        progress = account == null ? HamsterProgress.Load() : HamsterProgress.Load(account);
        progress.Changed += OnProgressChanged;
        progress.Fed += walker.Feed;
        bool admin = account != null && account.Admin;
        accountButton.Text = account == null ? "로그인 · 이 PC 계정" : "로그아웃 · " + (admin ? "관리자" : account.Name);
        accountMenu.Text = accountButton.Text;
        adminButton.Visible = adminMenu.Visible = admin;
        RefreshHamster();
    }

    private void OpenAdmin()
    {
        if (!Enabled || activeAccount == null || !activeAccount.Admin) return;
        Restore();
        try
        {
            using (var overview = new AccountOverview(accounts.Students()))
                overview.ShowDialog(this);
        }
        catch (IOException) { ShowAccountError(); }
        catch (UnauthorizedAccessException) { ShowAccountError(); }
        catch (System.Runtime.Serialization.SerializationException) { ShowAccountError(); }
    }

    private void ShowAccountError() { MessageBox.Show(this, "이 PC의 계정 정보를 읽을 수 없어요.", Program.Name); }

    private void OpenDemo()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dist", "index.html");
        if (!File.Exists(path))
        {
            MessageBox.Show("dist 폴더에 있는 index.html을 찾을 수 없어요.", Program.Name);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            MessageBox.Show("브라우저를 열 수 없어요. dist 폴더의 index.html을 직접 열어 주세요.", Program.Name);
        }
    }

    private void RefreshHamster()
    {
        roam.Text = progress.Roam ? "햄스터 숨기기" : "햄스터 산책시키기";
        roamMenu.Text = roam.Text;
        walker.RefreshState(progress, !bubble.Visible);
    }

    private void OpenRoom()
    {
        if (!Enabled) return;
        Restore();
        using (var room = new HamsterRoom(progress)) room.ShowDialog(this);
    }

    private void OpenStats()
    {
        if (!Enabled) return;
        Restore();
        using (var stats = new WeeklyStats(progress)) stats.ShowDialog(this);
    }

    private void SetMonitoring(bool enabled)
    {
        if (enabled)
        {
            try { engine = ScreenReader.KoreanEngine(); }
            catch (ExternalException)
            {
                MessageBox.Show("Windows 글자 인식을 시작할 수 없어요. 잠시 뒤 다시 시도해 주세요.", Program.Name);
                return;
            }
            if (engine == null)
            {
                MessageBox.Show("Windows 설정의 언어 옵션에서 한국어 기본 입력을 설치한 뒤 다시 실행해 주세요.", "한국어 글자 인식 필요");
                return;
            }
        }

        session++;
        monitoring = enabled;
        lastWindow = IntPtr.Zero;
        lastRiskKey = null;
        coachingBaseline = null;
        toggle.Text = enabled ? "화면 감지 중지" : "화면 감지 시작";
        monitorMenu.Text = toggle.Text;
        status.Text = enabled ? "●  감지 켜짐 · 지금 보고 있는 창" : "●  감지 꺼짐";
        status.ForeColor = enabled ? Palette.Green : Palette.Muted;
        tray.Text = enabled ? "AIRS · 감지 켜짐" : "AIRS · 감지 꺼짐";
        if (enabled)
        {
            poll.Start();
            HideToTray();
        }
        else
        {
            poll.Stop();
            DismissBubble();
        }
    }

    private async Task Scan()
    {
        if (!monitoring || busy || bubble.Visible) return;
        IntPtr window = ScreenReader.GetForegroundWindow();
        Rectangle bounds;
        if (!ScreenReader.TryGetBounds(window, processId, out bounds)) return;
        int scanSession = session;
        var scanEngine = engine;
        busy = true;
        try
        {
            var capture = await Task.Run(async delegate
            {
                using (var image = ScreenReader.Capture(bounds))
                    return await ScreenReader.Read(scanEngine, image);
            });
            if (scanSession != session || IsDisposed || window != ScreenReader.GetForegroundWindow()) return;
            string text = capture.Text;
            DateTime now = DateTime.UtcNow;
            if (coachingBaseline != null && window == coachingWindow && now < coachingUntil)
            {
                string revised = text.Split('\n').Reverse().FirstOrDefault(line =>
                    Detector.IsGood(line) && !coachingBaseline.Contains(Detector.Compact(line)));
                if (revised != null)
                {
                    coachingBaseline = null;
                    progress.Record(revised, false, true);
                    if (progress.Award(revised))
                        tray.ShowBalloonTip(2200, "Airi가 기뻐해요!", "해바라기씨 " + HamsterProgress.Reward + "알을 받았어요.", ToolTipIcon.None);
                    return;
                }
            }
            else if (now >= coachingUntil)
            {
                coachingBaseline = null;
            }
            var flagged = FindFlaggedLine(capture.Lines);
            if (flagged == null) return;
            var signal = FindScreenSignal(flagged.Text);

            string riskKey = Detector.Compact(flagged.Text);
            bool repeated = window == lastWindow && riskKey == lastRiskKey && now - lastSeen < TimeSpan.FromSeconds(28);
            lastWindow = window;
            lastRiskKey = riskKey;
            lastSeen = now;
            if (repeated) return;

            progress.Record(flagged.Text, true, false);
            coachingWindow = window;
            coachingBaseline = Detector.Compact(text);
            coachingUntil = now.AddMinutes(2);
            var anchor = flagged.Area;
            anchor.Offset(bounds.Location);
            bubble.Present(signal, Screen.FromHandle(window).WorkingArea, anchor);
            RefreshHamster();
            hideBubble.Start();
        }
        catch (Exception error)
        {
            // A window can disappear or become unavailable between bounds lookup and capture.
            if (!(error is ExternalException || error is InvalidOperationException || error is ArgumentException)) throw;
            Debug.WriteLine(error.GetType().Name);
        }
        finally
        {
            busy = false;
        }
    }

    private static ScreenLine FindFlaggedLine(ScreenLine[] lines)
    {
        for (int index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index];
            string joined = line.Text;
            var area = line.Area;
            int first = index;
            while (first > 0 && index - first < 2 && Adjacent(lines[first - 1], lines[first]))
            {
                first--;
                joined = lines[first].Text + " " + joined;
                area = Rectangle.Union(lines[first].Area, area);
            }
            if (FindScreenSignal(joined) != null)
                return new ScreenLine { Text = joined, Area = area };
            index = first;
        }
        return null;
    }

    private static bool Adjacent(ScreenLine above, ScreenLine below)
    {
        int gap = below.Area.Top - above.Area.Bottom;
        return gap >= 0 && gap <= Math.Max(above.Area.Height, below.Area.Height)
            && Math.Abs(above.Area.Left - below.Area.Left) <= 28;
    }

    private static Signal FindScreenSignal(string text)
    {
        // Windows OCR sometimes reads the last syllable of "알려 줘" as "주의".
        return Detector.Find(System.Text.RegularExpressions.Regex.Replace(
            text, @"알려\s*주의(?=\s*[.!?]?\s*$)", "알려 줘"));
    }

    private void Restore()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        if (trayHintShown) return;

        trayHintShown = true;
        tray.ShowBalloonTip(3500, Program.Name,
            monitoring
                ? "화면 감지가 계속돼요. 알림 영역의 아이콘에서 멈추거나 창을 다시 열 수 있어요."
                : "알림 영역의 아이콘에서 창을 다시 열거나 앱을 종료할 수 있어요.",
            ToolTipIcon.Info);
    }

    private void Exit()
    {
        exitRequested = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void DismissBubble()
    {
        hideBubble.Stop();
        bubble.Hide();
        RefreshHamster();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        monitoring = false;
        session++;
        poll.Dispose();
        hideBubble.Dispose();
        bubble.Dispose();
        walker.Dispose();
        tray.Visible = false;
        tray.ContextMenuStrip.Dispose();
        tray.Dispose();
        base.OnFormClosed(e);
    }
}

internal sealed class Bubble : Form
{
    private readonly Label category, reason, evidence;
    private readonly Label step;
    private readonly Label suggestion;
    private readonly Button copy;
    private Signal current;
    private bool pointerUp = true;
    public event EventHandler Dismissed;

    public Bubble()
    {
        Font = Palette.Font(10f);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Palette.Paper;
        ClientSize = new Size(480, 405);
        SizeChanged += delegate { ShapeBubble(); };
        ShapeBubble();
        Text = "AIRS · AI 과의존 신호";

        Controls.Add(new HamsterArt { Bounds = new Rectangle(23, 21, 77, 67), Alert = true });
        category = Palette.Label("", 111, 22, 325, 24, 10f, true, Palette.Green);
        Controls.Add(category);
        Controls.Add(Palette.Label("Airi의 질문 코칭", 111, 48, 329, 35, 16f, true, Palette.Ink));
        evidence = Palette.Label("", 23, 94, 434, 46, 10f, true, Palette.Ink);
        evidence.BackColor = Color.FromArgb(247, 233, 209);
        evidence.Padding = new Padding(9, 5, 9, 3);
        Palette.ClipCorners(evidence, 10);
        Controls.Add(evidence);
        reason = Palette.Label("", 23, 149, 434, 38, 10f, false, Palette.Muted);
        Controls.Add(reason);
        step = Palette.Label("", 23, 190, 434, 57, 10.5f, true, Palette.Ink);
        Controls.Add(step);
        Controls.Add(Palette.Label("이렇게 물어보면 어때요?", 23, 250, 405, 23, 10f, true, Palette.Green));
        suggestion = Palette.Label("", 23, 276, 434, 67, 10.5f, true, Palette.Ink);
        suggestion.BackColor = Palette.PaleGreen;
        suggestion.Padding = new Padding(10, 6, 8, 4);
        Palette.ClipCorners(suggestion, 13);
        Controls.Add(suggestion);
        copy = Palette.Button("추천 질문 복사", 23, 354, 151, 35, true);
        copy.Click += delegate
        {
            try
            {
                Clipboard.SetText(current.Suggestion);
                copy.Text = "복사했어요";
                if (Dismissed != null) Dismissed(this, EventArgs.Empty);
            }
            catch (ExternalException) { copy.Text = "다시 복사"; }
        };
        Controls.Add(copy);
        var dismiss = Palette.Button("닫기", 184, 354, 77, 35, false);
        dismiss.Click += delegate { if (Dismissed != null) Dismissed(this, EventArgs.Empty); else Close(); };
        Controls.Add(dismiss);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE keeps typing focus in the observed app.
            return parameters;
        }
    }

    public void Present(Signal signal, Rectangle workArea, Rectangle anchor = default(Rectangle))
    {
        current = signal;
        category.Text = "AI 과의존 신호 · " + signal.Label;
        evidence.Text = "“" + signal.Evidence + "”";
        reason.Text = signal.Reason;
        step.Text = signal.Step;
        suggestion.Text = signal.Suggestion;
        copy.Text = "추천 질문 복사";
        Position(workArea, anchor);
        Show();
        Position(workArea, anchor);
    }

    private void Position(Rectangle workArea, Rectangle anchor)
    {
        int left = anchor.IsEmpty ? workArea.Right - Width - 24 : anchor.Left;
        int top = anchor.IsEmpty ? workArea.Bottom - Height - 24 : anchor.Bottom + 10;
        bool above = !anchor.IsEmpty && top + Height > workArea.Bottom;
        if (above) top = anchor.Top - Height - 10;
        if (pointerUp == above)
        {
            pointerUp = !above;
            ShapeBubble();
        }
        Location = new Point(Math.Max(workArea.Left, Math.Min(left, workArea.Right - Width)),
            Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - Height)));
    }

    private void ShapeBubble()
    {
        if (ClientSize.Width < 60 || ClientSize.Height < 60) return;
        int height = ClientSize.Height, width = ClientSize.Width;
        int top = pointerUp ? 9 : 0, bottom = pointerUp ? height : height - 9;
        using (var shape = new GraphicsPath())
        {
            const int diameter = 43;
            shape.AddArc(0, top, diameter, diameter, 180, 90);
            if (pointerUp) shape.AddLines(new[] { new Point(46, top), new Point(58, 0), new Point(70, top) });
            shape.AddArc(width - diameter, top, diameter, diameter, 270, 90);
            shape.AddArc(width - diameter, bottom - diameter, diameter, diameter, 0, 90);
            if (!pointerUp) shape.AddLines(new[] { new Point(70, bottom), new Point(58, height), new Point(46, bottom) });
            shape.AddArc(0, bottom - diameter, diameter, diameter, 90, 90);
            shape.CloseFigure();
            Region = new Region(shape);
        }
    }
}

internal static class Palette
{
    public static readonly Color Paper = Color.FromArgb(252, 250, 243);
    public static readonly Color Ink = Color.FromArgb(57, 42, 32);
    public static readonly Color Green = Color.FromArgb(35, 85, 68);
    public static readonly Color Muted = Color.FromArgb(71, 91, 80);
    public static readonly Color Lime = Color.FromArgb(218, 169, 92);
    public static readonly Color PaleGreen = Color.FromArgb(230, 242, 232);
    public static readonly Color Line = Color.FromArgb(218, 230, 218);

    public static Font Font(float size, bool bold = false)
    {
        return new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular);
    }

    public static Label Label(string text, int x, int y, int width, int height, float size, bool bold, Color color)
    {
        return new Label
        {
            Text = text, Bounds = new Rectangle(x, y, width, height), Font = Font(size, bold),
            ForeColor = color, UseMnemonic = false
        };
    }

    public static Button Button(string text, int x, int y, int width, int height, bool primary)
    {
        var button = new Button
        {
            Text = text, Bounds = new Rectangle(x, y, width, height), Font = Font(10f, true),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = primary ? Green : PaleGreen, ForeColor = primary ? Color.White : Green
        };
        button.FlatAppearance.BorderColor = primary ? Green : Line;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(43, 102, 81) : Color.FromArgb(215, 235, 220);
        ClipCorners(button, 20);
        return button;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void RoundWindow(Form window)
    {
        if (Program.AppIcon != null) window.Icon = Program.AppIcon;
        window.HandleCreated += delegate
        {
            int round = 2;
            DwmSetWindowAttribute(window.Handle, 33, ref round, sizeof(int));
        };
    }

    public static void ClipCorners(Control control, int radius)
    {
        Action reshape = delegate
        {
            float diameter = Math.Min(radius * 2, Math.Min(control.Width, control.Height));
            if (diameter < 2) return;
            using (var path = new GraphicsPath())
            {
                float right = control.Width - diameter;
                float bottom = control.Height - diameter;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(right, 0, diameter, diameter, 270, 90);
                path.AddArc(right, bottom, diameter, diameter, 0, 90);
                path.AddArc(0, bottom, diameter, diameter, 90, 90);
                path.CloseFigure();
                control.Region = new Region(path);
            }
        };
        control.SizeChanged += delegate { reshape(); };
        reshape();
    }
}

