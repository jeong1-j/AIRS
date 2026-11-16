using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

[DataContract]
internal sealed class LocalAccount
{
    [DataMember] public string Id;
    [DataMember] public string Name;
    [DataMember] public bool Admin;
    [DataMember] public byte[] Salt;
    [DataMember] public byte[] Hash;
    [DataMember(EmitDefaultValue = false)] public string Nickname;

    public string DisplayName { get { return string.IsNullOrEmpty(Nickname) ? (Admin ? "관리자" : Name) : Nickname; } }
}

internal sealed class LocalAccounts
{
    private const int Iterations = 600000;
    public const string AdminName = "airs";
    private static readonly DataContractJsonSerializer Serializer =
        new DataContractJsonSerializer(typeof(List<LocalAccount>));
    private readonly string path;
    private readonly List<LocalAccount> users;
    private LocalAccount signedIn;

    public LocalAccounts() : this(HamsterProgress.Folder) { }

    internal LocalAccounts(string folder)
    {
        path = Path.Combine(folder, "accounts.json");
        if (File.Exists(path))
        {
            using (var stream = File.OpenRead(path)) users = (List<LocalAccount>)Serializer.ReadObject(stream);
            if (users == null) throw new SerializationException("AIRS accounts file is empty.");
        }
        else users = new List<LocalAccount>();
        if (!users.Any(user => user.Admin))
        {
            var admin = new LocalAccount
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = AdminName,
                Admin = true,
                Salt = Convert.FromBase64String("BFbCV6N5URTTY2mi1DhvoQ=="),
                Hash = Convert.FromBase64String("R8fJk1YE1Eeef1te1euJHxJbIYs0SOONSOW70Q4FrIA=")
            };
            users.Add(admin);
            WriteJson(path, Serializer, users);
        }
    }

    public LocalAccount Register(string name, string password, string nickname = null)
    {
        name = name.Trim();
        if (!Regex.IsMatch(name, @"^[\p{L}\p{N}._-]{2,24}$"))
            throw new ArgumentException("아이디는 한글·영문·숫자·._-로 2~24자 입력해 주세요.");
        if (string.Equals(name, AdminName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("이 아이디는 관리자용이에요. 학생 아이디를 입력해 주세요.");
        if (password.Length < 10 || password.Length > 128)
            throw new ArgumentException("비밀번호는 10~128자로 입력해 주세요.");
        if (users.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("이미 사용 중인 아이디예요.");

        byte[] salt = new byte[16];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
        var user = new LocalAccount { Id = Guid.NewGuid().ToString("N"), Name = name, Nickname = NormalizeNickname(nickname),
            Salt = salt, Hash = Derive(password, salt) };
        var updated = new List<LocalAccount>(users) { user };
        WriteJson(path, Serializer, updated);
        users.Add(user);
        return user;
    }

    public LocalAccount SignIn(string name, string password)
    {
        signedIn = null;
        if (password.Length > 128) return null;
        var user = users.FirstOrDefault(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        byte[] candidate = Derive(password, user == null ? new byte[16] : user.Salt);
        if (user == null) return null;
        int mismatch = 0;
        for (int i = 0; i < candidate.Length; i++) mismatch |= candidate[i] ^ user.Hash[i];
        if (mismatch == 0) signedIn = user;
        return signedIn;
    }

    public IEnumerable<LocalAccount> Students()
    {
        if (signedIn == null || !signedIn.Admin) return Enumerable.Empty<LocalAccount>();
        return users.Where(user => !user.Admin).OrderBy(user => user.Name).ToArray();
    }

    public void SignOut() { signedIn = null; }

    public void SetNickname(string nickname)
    {
        if (signedIn == null) throw new InvalidOperationException("Sign in before editing a nickname.");
        string updated = NormalizeNickname(nickname);
        string previous = signedIn.Nickname;
        if (updated == previous) return;
        signedIn.Nickname = updated;
        try { WriteJson(path, Serializer, users); }
        catch
        {
            signedIn.Nickname = previous;
            throw;
        }
    }

    private static string NormalizeNickname(string nickname)
    {
        nickname = (nickname ?? "").Trim();
        if (nickname.Length > 80 || new StringInfo(nickname).LengthInTextElements > 20 || nickname.Any(char.IsControl))
            throw new ArgumentException("닉네임은 줄바꿈 없이 20자까지 입력해 주세요.");
        return nickname.Length == 0 ? null : nickname;
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        using (var key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            return key.GetBytes(32);
    }

    internal static void WriteJson(string file, DataContractJsonSerializer serializer, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        string temp = file + ".tmp";
        using (var output = File.Create(temp)) serializer.WriteObject(output, data);
        if (File.Exists(file)) File.Replace(temp, file, null);
        else File.Move(temp, file);
    }
}

internal sealed class AccountWindow : Form
{
    public LocalAccount Account { get; private set; }

    public AccountWindow(LocalAccounts accounts)
    {
        SuspendLayout();
        Text = "AIRS · 이 PC의 계정";
        ClientSize = new Size(447, 396);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = Palette.Paper;
        MaximizeBox = MinimizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Palette.RoundWindow(this);
        var back = Palette.AddHomeButton(this, 24, 23);
        Controls.Add(Palette.Label("AIRS 계정", 125, 17, 295, 44, 21, true, Palette.Ink));
        Controls.Add(Palette.Label("학생 계정은 여기서 만들 수 있어요.", 24, 65, 400, 24, 10, false, Palette.Muted));
        var name = AddField(this, "아이디", 96, 24);
        var secret = AddField(this, "비밀번호", 156, 128, true);
        var nickname = AddField(this, "회원가입 닉네임 (선택)", 216, 80);
        var feedback = Palette.Label("처음이라면 학생 회원가입을 눌러요.",
            24, 275, 399, 38, 9, false, Palette.Muted);
        Controls.Add(feedback);

        var signIn = Palette.Button("로그인", 24, 324, 193, 42, true);
        var join = Palette.Button("학생 회원가입", 229, 324, 193, 42, false);
        bool pending = false;
        FormClosing += delegate(object sender, FormClosingEventArgs args) { if (pending) args.Cancel = true; };
        Action<bool> submit = async register =>
        {
            if (pending) return;
            string id = name.Text.Trim(), password = secret.Text, display = nickname.Text;
            if (id.Length == 0 || password.Length == 0)
            {
                feedback.Text = "아이디와 비밀번호를 입력해 주세요.";
                (id.Length == 0 ? name : secret).Focus();
                return;
            }
            pending = true;
            signIn.Enabled = join.Enabled = name.Enabled = secret.Enabled = nickname.Enabled = back.Enabled = false;
            feedback.Text = register ? "계정을 만드는 중이에요…" : "계정을 확인하는 중이에요…";
            LocalAccount verified = null;
            try
            {
                verified = await Task.Run(() => register ? accounts.Register(id, password, display) : accounts.SignIn(id, password));
                if (register)
                {
                    name.Text = verified.Name;
                    secret.Clear();
                    nickname.Clear();
                    feedback.Text = "학생 계정을 만들었어요. 비밀번호를 다시 입력해 로그인하세요.";
                }
                else if (verified == null)
                {
                    feedback.Text = "아이디와 비밀번호를 확인해 주세요.";
                    secret.SelectAll();
                }
            }
            catch (ArgumentException error) { feedback.Text = error.Message; }
            catch (IOException) { feedback.Text = "계정을 저장할 수 없어요. 잠시 뒤 다시 시도해 주세요."; }
            catch (UnauthorizedAccessException) { feedback.Text = "계정을 저장할 수 없어요. 잠시 뒤 다시 시도해 주세요."; }
            finally
            {
                pending = false;
                signIn.Enabled = join.Enabled = name.Enabled = secret.Enabled = nickname.Enabled = back.Enabled = true;
            }
            if (!register && verified != null)
            {
                Account = verified;
                DialogResult = DialogResult.OK;
            }
            else secret.Focus();
        };
        signIn.Click += delegate { submit(false); };
        AcceptButton = signIn;
        join.Click += delegate { submit(true); };
        Controls.Add(signIn);
        Controls.Add(join);
        ResumeLayout(true);
    }

    internal static TextBox AddField(Form window, string label, int y, int maxLength, bool password = false)
    {
        window.Controls.Add(Palette.Label(label, 24, y, 392, 20, 10, true, Palette.Ink));
        var field = new SurfaceCard(new Rectangle(24, y + 21, 398, 34), Palette.Surface, Palette.Line, 13);
        var input = new TextBox { Bounds = new Rectangle(11, 5, 376, 24), Font = Palette.Font(11), MaxLength = maxLength,
            BackColor = Palette.Surface, ForeColor = Palette.Ink, BorderStyle = BorderStyle.None,
            UseSystemPasswordChar = password, AccessibleName = label };
        field.Controls.Add(input);
        window.Controls.Add(field);
        return input;
    }
}

internal sealed class ProfileWindow : Form
{
    public ProfileWindow(LocalAccounts accounts, LocalAccount account)
    {
        SuspendLayout();
        Text = "AIRS · 내 계정";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(447, 272);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        BackColor = Palette.Paper;
        Palette.RoundWindow(this);
        Palette.AddHomeButton(this, 24, 23);
        Controls.Add(Palette.Label("내 계정", 125, 17, 295, 44, 21, true, Palette.Ink));
        Controls.Add(Palette.Label((account.Admin ? "관리자" : "학생") + " · " + account.Name, 24, 66, 398, 26, 10, false, Palette.Muted));
        var nickname = AccountWindow.AddField(this, "닉네임", 101, 80);
        nickname.Text = account.Nickname ?? "";
        var feedback = Palette.Label("닉네임은 20자까지 설정할 수 있어요.", 24, 162, 398, 30, 9, false, Palette.Muted);
        Controls.Add(feedback);
        var save = Palette.Button("닉네임 저장", 24, 202, 193, 42, true);
        save.Click += delegate
        {
            try
            {
                accounts.SetNickname(nickname.Text);
                feedback.Text = "닉네임을 저장했어요.";
            }
            catch (ArgumentException error) { feedback.Text = error.Message; }
            catch (IOException) { feedback.Text = "닉네임을 저장할 수 없어요."; }
            catch (UnauthorizedAccessException) { feedback.Text = "닉네임을 저장할 수 없어요."; }
        };
        var signOut = Palette.Button("로그아웃", 229, 202, 193, 42, false);
        signOut.DialogResult = DialogResult.Abort;
        Controls.Add(save);
        Controls.Add(signOut);
        AcceptButton = save;
        ResumeLayout(true);
    }
}

internal sealed class AccountOverview : Form
{
    public AccountOverview(IEnumerable<LocalAccount> students)
    {
        SuspendLayout();
        Text = "AIRS · 관리자";
        ClientSize = new Size(734, 437);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Palette.Paper;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Palette.RoundWindow(this);
        Palette.AddHomeButton(this, 24, 23);
        Controls.Add(Palette.Label("이 PC의 학생 활동", 125, 18, 580, 43, 23, true, Palette.Ink));
        Controls.Add(Palette.Label("이 Windows 계정에 등록된 학생의 활동을 보여줘요.", 25, 68, 680, 30, 10, false, Palette.Muted));
        var list = new ListView { Bounds = new Rectangle(24, 109, 686, 300), View = View.Details,
            Font = Palette.Font(10), FullRowSelect = true, AccessibleName = "학생별 활동 집계",
            ShowItemToolTips = true, BackColor = Palette.Surface, ForeColor = Palette.Ink,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
        list.Columns.AddRange(new[] { new ColumnHeader { Text = "닉네임" }, new ColumnHeader { Text = "학생 아이디" },
            new ColumnHeader { Text = "해바라기씨" }, new ColumnHeader { Text = "질문 확인" },
            new ColumnHeader { Text = "과의존 신호" }, new ColumnHeader { Text = "스스로 확인" } });
        int[] columnShares = { 25, 21, 13, 12, 15, 14 };
        Action fitColumns = delegate
        {
            int width = Math.Max(0, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            for (int i = 0; i < columnShares.Length; i++) list.Columns[i].Width = width * columnShares[i] / 100;
        };
        list.SizeChanged += delegate { fitColumns(); };
        foreach (var student in students)
        {
            var state = HamsterProgress.Load(student);
            list.Items.Add(new ListViewItem(new[] { student.DisplayName, student.Name, state.Points + "알",
                state.Days.Sum(day => day.Total) + "건", state.Days.Sum(day => day.Risk) + "건", state.Days.Sum(day => day.Good) + "건" })
                { ToolTipText = student.DisplayName + " · " + student.Name });
        }
        if (list.Items.Count == 0) list.Items.Add(new ListViewItem("등록된 학생이 없어요"));
        Controls.Add(list);
        ResumeLayout(true);
        fitColumns();
    }
}
