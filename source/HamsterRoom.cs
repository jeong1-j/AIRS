using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class HamsterRoom : Form
{
    private readonly HamsterProgress progress;
    private readonly HamsterArt art;
    private readonly Label wallet, feedback, mood;
    private readonly Label reaction;
    private readonly Panel bondTrack;
    private readonly Button[] shopButtons = new Button[HamsterProgress.Shop.Length];
    private readonly Timer celebration = new Timer { Interval = 2600 };
    private readonly Timer nibble = new Timer { Interval = 235 };
    private Bubble coach;
    private readonly TextBox question;

    public HamsterRoom(HamsterProgress progress)
    {
        SuspendLayout();
        this.progress = progress;
        Text = "AIRS · 햄스터 집";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(752, 608);
        AutoScroll = true;
        BackColor = Palette.Paper;
        Palette.RoundWindow(this);

        var back = Palette.Button("‹  홈으로", 24, 27, 88, 37, false, tint: Palette.Surface);
        back.AccessibleName = "홈으로 돌아가기";
        back.DialogResult = DialogResult.Cancel;
        back.Click += delegate { Close(); };
        CancelButton = back;
        Controls.Add(back);
        Controls.Add(Palette.Label("햄스터 집", 126, 21, 320, 47, 25, true, Palette.Ink));

        var balance = new SurfaceCard(new Rectangle(526, 25, 202, 42), Palette.Oat, Palette.Line);
        balance.Controls.Add(new ShopSticker(HamsterProgress.Shop[0], new Rectangle(9, 7, 28, 28)));
        wallet = Palette.Label("", 41, 3, 150, 35, 10, true, Palette.Earth);
        wallet.TextAlign = ContentAlignment.MiddleLeft;
        balance.Controls.Add(wallet);
        Controls.Add(balance);

        var home = new SurfaceCard(new Rectangle(24, 84, 279, 349), Palette.Surface, Palette.Line, 26);
        home.Controls.Add(Palette.Label("Airi의 방", 17, 13, 226, 27, 14, true, Palette.Ink));
        var habitat = new HamsterHabitat(new Rectangle(14, 49, 251, 181));
        art = new HamsterArt { Bounds = new Rectangle(61, 27, 126, 128) };
        habitat.Controls.Add(art);
        home.Controls.Add(habitat);
        mood = Palette.Label("", 17, 237, 244, 40, 10, true, Palette.Green);
        home.Controls.Add(mood);
        bondTrack = new Panel { Bounds = new Rectangle(19, 282, 241, 8), BackColor = Color.Transparent };
        bondTrack.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float earned = (progress.Bond % HamsterProgress.BondPerLevel) / (float)HamsterProgress.BondPerLevel;
            float radius = bondTrack.Height / 2f;
            RoundedShape.Fill(e.Graphics, bondTrack.ClientRectangle, radius, Palette.Mint);
            if (earned > 0)
                RoundedShape.Fill(e.Graphics, new RectangleF(0, 0, bondTrack.Width * earned, bondTrack.Height), radius, Palette.Pine);
        };
        home.Controls.Add(bondTrack);
        var play = Palette.Button("놀아주기", 17, 301, 245, 33, false, AppIcon.Heart, Palette.Peach);
        play.Click += delegate
        {
            nibble.Stop();
            art.ActionFrame = -1;
            art.ActionFood = null;
            art.Alert = true;
            Celebrate("빼꼼! 햄스터가 고개를 내밀었어요.");
        };
        home.Controls.Add(play);
        reaction = Palette.Label("", 9, 153, 233, 22, 9, true, Palette.Ink);
        reaction.TextAlign = ContentAlignment.MiddleCenter;
        habitat.Controls.Add(reaction);
        reaction.BringToFront();
        Controls.Add(home);

        var shop = new SurfaceCard(new Rectangle(319, 84, 409, 349), Palette.Surface, Palette.Line, 26);
        shop.Controls.Add(Palette.Label("햄스터 상점", 16, 12, 280, 27, 14, true, Palette.Ink));
        shop.Controls.Add(Palette.Label("간식", 16, 45, 160, 19, 9, true, Palette.Earth));
        shop.Controls.Add(Palette.Label("옷", 16, 191, 160, 19, 9, true, Palette.Earth));
        Controls.Add(shop);

        int snackIndex = 0, outfitIndex = 0;
        for (int i = 0; i < HamsterProgress.Shop.Length; i++)
        {
            ShopItem item = HamsterProgress.Shop[i];
            bool snack = item.Kind == "snack";
            int index = snack ? snackIndex++ : outfitIndex++;
            var tile = new SurfaceCard(new Rectangle(14 + 194 * (index % 2),
                (snack ? 66 : 212) + 63 * (index / 2), 186, 58), Palette.Paper, Palette.Line, 16);
            tile.Controls.Add(new ShopSticker(item, new Rectangle(7, 9, 40, 40)));
            tile.Controls.Add(Palette.Label(item.Name, 53, 5, 125, 21, 9.5f, true, Palette.Ink));
            if (snack)
                tile.Controls.Add(Palette.Label("♥ +" + item.Bond, 53, 30, 52, 20, 8, false, Palette.Earth));
            var button = Palette.Button("", snack ? 105 : 53, 27, snack ? 75 : 123, 24, false,
                tint: snack ? Palette.Oat : Palette.Mint);
            button.Click += delegate { UseItem(item); };
            shopButtons[i] = button;
            tile.Controls.Add(button);
            shop.Controls.Add(tile);
        }

        var practice = new SurfaceCard(new Rectangle(24, 448, 704, 139), Palette.Surface, Palette.Line, 26);
        practice.Controls.Add(Palette.Label("내가 쓴 질문 확인", 15, 10, 280, 26, 13, true, Palette.Ink));
        var input = new SurfaceCard(new Rectangle(14, 43, 501, 49), Palette.Paper, Palette.Line, 14);
        question = new TextBox
        {
            Bounds = new Rectangle(11, 8, 479, 33), Font = Palette.Font(10), Multiline = true,
            BackColor = Palette.Paper, ForeColor = Palette.Ink, BorderStyle = BorderStyle.None,
            MaxLength = 1500, ScrollBars = ScrollBars.Vertical, AccessibleName = "AI 질문 초안"
        };
        input.Controls.Add(question);
        practice.Controls.Add(input);
        var check = Palette.Button("질문 확인", 530, 46, 157, 42, true);
        check.Click += delegate { CheckQuestion(); };
        practice.Controls.Add(check);
        feedback = Palette.Label("힌트, 내 풀이 검토, 근거 확인을 요청하면 해바라기씨 " + HamsterProgress.Reward + "알을 받아요.",
            16, 96, 671, 33, 9, false, Palette.Muted);
        practice.Controls.Add(feedback);
        Controls.Add(practice);
        nibble.Tick += delegate
        {
            art.ActionFrame = 12 + (art.ActionFrame - 11) % 3;
            art.Invalidate();
        };
        celebration.Tick += delegate
        {
            celebration.Stop();
            nibble.Stop();
            art.Alert = false;
            art.ActionFrame = -1;
            art.ActionFood = null;
            reaction.Text = "";
            RefreshState();
        };
        progress.Changed += OnProgress;
        ResumeLayout(true);
        RefreshState();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Rectangle desktop = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, desktop.Width), Math.Min(Height, desktop.Height));
    }

    private void UseItem(ShopItem item)
    {
        int previousLevel = progress.Bond / HamsterProgress.BondPerLevel;
        if (!progress.GetItem(item)) return;
        if (item.Bond == 0)
        {
            Celebrate(progress.Outfit == item.Id ? item.Name + " 착용!" : "옷을 벗었어요.");
            return;
        }
        art.Alert = false;
        art.ActionFrame = 12;
        art.ActionFood = item;
        nibble.Start();
        int newLevel = progress.Bond / HamsterProgress.BondPerLevel;
        Celebrate(newLevel > previousLevel ? "호감도 Lv. " + (newLevel + 1) + " 달성!"
            : "냠냠! " + item.Name + " · 호감도 +" + item.Bond);
    }

    private void CheckQuestion()
    {
        string text = question.Text.Trim();
        if (text.Length == 0)
        {
            feedback.Text = "확인할 질문을 입력해 주세요.";
            return;
        }

        var signal = Detector.Find(text);
        bool good = signal == null && Detector.IsGood(text);
        progress.Record(text, signal != null, good);
        if (signal != null)
        {
            feedback.Text = Detector.Direction(signal);
            if (coach != null) coach.Dispose();
            coach = new Bubble();
            Rectangle anchor = question.RectangleToScreen(question.ClientRectangle);
            coach.Present(signal, Screen.FromControl(this).WorkingArea, anchor);
            return;
        }
        if (!good)
        {
            feedback.Text = "직접 해본 부분과 막힌 지점부터 적어봐요.";
            return;
        }
        if (progress.Unlimited) feedback.Text = "좋은 질문이에요. 관리자 모드에서는 씨앗을 제한 없이 써요.";
        else if (progress.Award(text))
        {
            feedback.Text = "좋은 질문이에요! 해바라기씨 " + HamsterProgress.Reward + "알을 받았어요.";
            Celebrate("Airi가 기뻐해요!");
        }
        else feedback.Text = "이미 해바라기씨를 받은 질문이에요.";
    }


    private void Celebrate(string message)
    {
        reaction.Text = message;
        celebration.Stop();
        celebration.Start();
        RefreshState();
    }

    private void OnProgress(object sender, EventArgs args) { RefreshState(); }

    private void RefreshState()
    {
        wallet.Text = progress.Unlimited ? "해바라기씨 무제한" : "해바라기씨 " + progress.Points.ToString("N0") + "알";
        int level = progress.Bond / HamsterProgress.BondPerLevel + 1;
        int remaining = HamsterProgress.BondPerLevel - progress.Bond % HamsterProgress.BondPerLevel;
        mood.Text = "호감도 Lv. " + level + "  ·  누적 " + progress.Bond + "\n다음 레벨까지 " + remaining;
        bondTrack.Invalidate();
        art.Outfit = progress.Outfit;
        art.Happy = celebration.Enabled || progress.Bond >= 3;
        art.Invalidate();
        for (int i = 0; i < shopButtons.Length; i++)
        {
            var item = HamsterProgress.Shop[i];
            bool owned = progress.Owned.Contains(item.Id);
            shopButtons[i].Text = owned ? progress.Outfit == item.Id ? "벗기" : "입히기" : item.Cost + "알";
            shopButtons[i].AccessibleName = item.Name + " · " + (owned ? shopButtons[i].Text : "해바라기씨 " + shopButtons[i].Text);
            shopButtons[i].Enabled = progress.Unlimited || owned || progress.Points >= item.Cost;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            progress.Changed -= OnProgress;
            celebration.Dispose();
            nibble.Dispose();
            if (coach != null) coach.Dispose();
        }
        base.Dispose(disposing);
    }
}
