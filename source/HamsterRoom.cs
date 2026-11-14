using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class HamsterRoom : Form
{
    private readonly HamsterProgress progress;
    private readonly HamsterArt art;
    private readonly Label wallet, feedback, mood;
    private readonly Label reaction;
    private readonly Button[] shopButtons = new Button[HamsterProgress.Shop.Length];
    private readonly Timer celebration = new Timer { Interval = 2600 };
    private readonly Timer nibble = new Timer { Interval = 235 };
    private Bubble coach;
    private readonly TextBox question;

    public HamsterRoom(HamsterProgress progress)
    {
        this.progress = progress;
        Text = "AIRS · 햄스터 집";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(566, 594);
        BackColor = Palette.Paper;
        Palette.RoundWindow(this);

        Controls.Add(Palette.Label("햄스터 집", 24, 18, 304, 46, 24, true, Palette.Ink));
        wallet = Palette.Label("", 324, 31, 213, 26, 11, true, Palette.Green);
        wallet.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(wallet);
        art = new HamsterArt { Bounds = new Rectangle(53, 118, 126, 169) };
        Controls.Add(art);
        mood = Palette.Label("", 24, 326, 205, 36, 9, true, Palette.Green);
        Controls.Add(mood);
        var play = Palette.Button("놀아주기", 24, 363, 184, 30, false);
        play.Click += delegate
        {
            art.Alert = true;
            Celebrate("빼꼼! 햄스터가 고개를 내밀었어요.");
        };
        Controls.Add(play);
        Controls.Add(Palette.Label("간식과 옷", 235, 75, 303, 26, 13, true, Palette.Ink));
        reaction = Palette.Label("", 27, 294, 180, 38, 10, true, Palette.Green);
        reaction.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(reaction);
        var shop = new Panel { Bounds = new Rectangle(231, 106, 313, 285), AutoScroll = true };
        Controls.Add(shop);

        for (int i = 0; i < HamsterProgress.Shop.Length; i++)
        {
            ShopItem item = HamsterProgress.Shop[i];
            int y = 4 + 63 * i;
            shop.Controls.Add(Palette.Label(item.Name, 5, y, 171, 23, 11, true, Palette.Ink));
            shop.Controls.Add(Palette.Label(item.Bond > 0 ? "호감도 +" + item.Bond : "구매 후 다시 입을 수 있어요", 5, y + 24, 165, 23, 9, false, Palette.Muted));
            var button = Palette.Button("", 172, y + 5, 116, 33, false);
            button.Click += delegate
            {
                int previousLevel = progress.Bond / HamsterProgress.BondPerLevel;
                if (!progress.GetItem(item)) return;
                if (item.Bond > 0)
                {
                    art.ActionFrame = 12;
                    art.ActionFood = item;
                    nibble.Start();
                    int newLevel = progress.Bond / HamsterProgress.BondPerLevel;
                    Celebrate(newLevel > previousLevel ? "호감도 Lv. " + (newLevel + 1) + " 달성!"
                        : "냠냠! " + item.Name + " · 호감도 +" + item.Bond);
                }
                else Celebrate(progress.Outfit == item.Id ? item.Name + " 착용!" : "옷을 벗었어요.");
            };
            shopButtons[i] = button;
            shop.Controls.Add(button);
        }

        Controls.Add(Palette.Label("내가 쓴 질문 확인", 24, 401, 303, 27, 13, true, Palette.Ink));
        question = new TextBox
        {
            Bounds = new Rectangle(24, 437, 514, 61), Font = Palette.Font(10), Multiline = true,
            MaxLength = 1500, ScrollBars = ScrollBars.Vertical, AccessibleName = "AI 질문 초안"
        };
        Controls.Add(question);
        var check = Palette.Button("질문 확인", 406, 514, 132, 38, true);
        check.Click += delegate { CheckQuestion(); };
        Controls.Add(check);
        feedback = Palette.Label("힌트, 내 풀이 검토, 근거 확인을 요청하면 해바라기씨 " + HamsterProgress.Reward + "알을 받아요.", 24, 510, 363, 67, 9.5f, false, Palette.Muted);
        Controls.Add(feedback);
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
        RefreshState();
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
            feedback.Text = signal.Step;
            if (coach != null) coach.Dispose();
            coach = new Bubble();
            Rectangle anchor = RectangleToScreen(question.Bounds);
            coach.Present(signal, Screen.FromControl(this).WorkingArea, anchor);
            return;
        }
        if (!good)
        {
            feedback.Text = "내가 어디까지 했는지 적고, 확인받고 싶은 한 가지를 물어볼래요?";
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
        art.Outfit = progress.Outfit;
        art.Happy = celebration.Enabled || progress.Bond >= 3;
        art.Invalidate();
        for (int i = 0; i < shopButtons.Length; i++)
        {
            var item = HamsterProgress.Shop[i];
            bool owned = progress.Owned.Contains(item.Id);
            shopButtons[i].Text = owned ? progress.Outfit == item.Id ? "벗기" : "입히기" : "씨앗 " + item.Cost + "알";
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
