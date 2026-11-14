using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[DataContract]
internal sealed class LocalAccount
{
    [DataMember] public string Id;
    [DataMember] public string Name;
    [DataMember] public bool Admin;
    [DataMember] public byte[] Salt;
    [DataMember] public byte[] Hash;
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
    }

    public bool HasAdmin { get { return users.Any(user => user.Admin); } }

    public LocalAccount Register(string name, string password)
    {
        name = name.Trim();
        if (!Regex.IsMatch(name, @"^[\p{L}\p{N}._-]{2,24}$"))
            throw new ArgumentException("아이디는 한글·영문·숫자·._-로 2~24자 입력해 주세요.");
        bool admin = string.Equals(name, AdminName, StringComparison.OrdinalIgnoreCase);
        int minimum = admin ? 8 : 10;
        if (password.Length < minimum || password.Length > 128)
            throw new ArgumentException("비밀번호는 " + minimum + "~128자로 입력해 주세요.");
        if (admin && HasAdmin) throw new ArgumentException("이미 이 PC에 관리자가 등록되어 있어요.");
        if (users.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("이미 사용 중인 아이디예요.");

        byte[] salt = new byte[16];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
        var user = new LocalAccount { Id = Guid.NewGuid().ToString("N"), Name = name, Admin = admin, Salt = salt, Hash = Derive(password, salt) };
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
        Text = "AIRS · 이 PC의 계정";
        ClientSize = new Size(447, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = Palette.Paper;
        MaximizeBox = MinimizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Palette.RoundWindow(this);
        Controls.Add(Palette.Label("AIRS 로그인", 23, 17, 388, 44, 21, true, Palette.Ink));
        Controls.Add(Palette.Label("학생별 씨앗과 활동 기록을 이 PC에 저장해요.", 24, 67, 400, 24, 10, false, Palette.Muted));
        Controls.Add(Palette.Label("아이디", 24, 102, 392, 20, 10, true, Palette.Ink));
        Controls.Add(Palette.Label("비밀번호", 24, 162, 392, 20, 10, true, Palette.Ink));
        var name = new TextBox { Bounds = new Rectangle(24, 126, 398, 27), Font = Palette.Font(11), MaxLength = 24, AccessibleName = "아이디" };
        var secret = new TextBox { Bounds = new Rectangle(24, 186, 398, 27), Font = Palette.Font(11), MaxLength = 128,
            UseSystemPasswordChar = true, AccessibleName = "비밀번호" };
        var feedback = Palette.Label("처음이라면 아이디와 비밀번호를 적고 회원가입을 눌러 주세요.",
            24, 219, 399, 34, 9, false, Palette.Muted);
        Controls.Add(name);
        Controls.Add(secret);
        Controls.Add(feedback);

        Action<bool> submit = register =>
        {
            try
            {
                if (register)
                {
                    bool adminName = string.Equals(name.Text.Trim(), LocalAccounts.AdminName, StringComparison.OrdinalIgnoreCase);
                    if (adminName && !accounts.HasAdmin && MessageBox.Show(this,
                        "이 PC의 관리자 계정을 만들까요? 관리자는 이 PC에 등록된 학생의 활동 집계를 볼 수 있어요.",
                        "관리자 계정 만들기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    var created = accounts.Register(name.Text, secret.Text);
                    name.Text = created.Name;
                    secret.Clear();
                    feedback.Text = "계정을 만들었어요. 비밀번호를 다시 입력하고 로그인해 주세요.";
                    secret.Focus();
                    return;
                }
                Account = accounts.SignIn(name.Text, secret.Text);
                if (Account == null)
                {
                    feedback.Text = "아이디와 비밀번호를 확인해 주세요.";
                    return;
                }
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException error) { feedback.Text = error.Message; }
            catch (IOException) { feedback.Text = "계정을 저장할 수 없어요. 잠시 뒤 다시 시도해 주세요."; }
            catch (UnauthorizedAccessException) { feedback.Text = "계정을 저장할 수 없어요. 잠시 뒤 다시 시도해 주세요."; }
        };
        var signIn = Palette.Button("로그인", 24, 262, 193, 42, true);
        signIn.Click += delegate { submit(false); };
        AcceptButton = signIn;
        var join = Palette.Button("회원가입", 229, 262, 193, 42, false);
        join.Click += delegate { submit(true); };
        Controls.Add(signIn);
        Controls.Add(join);
    }
}

internal sealed class AccountOverview : Form
{
    public AccountOverview(IEnumerable<LocalAccount> students)
    {
        Text = "AIRS · 관리자";
        ClientSize = new Size(690, 437);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Palette.Paper;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Palette.RoundWindow(this);
        Controls.Add(Palette.Label("이 PC의 학생 활동", 24, 20, 625, 43, 23, true, Palette.Ink));
        Controls.Add(Palette.Label("등록된 계정의 전체 활동 건수예요. 질문 원문은 저장하지 않아요.", 25, 68, 630, 30, 10, false, Palette.Muted));
        var list = new ListView { Bounds = new Rectangle(24, 109, 643, 300), View = View.Details,
            Font = Palette.Font(10), FullRowSelect = true, AccessibleName = "학생별 활동 집계",
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
        list.Columns.Add("학생 아이디", 163);
        list.Columns.Add("해바라기씨", 105);
        list.Columns.Add("질문 확인", 106);
        list.Columns.Add("과의존 신호", 112);
        list.Columns.Add("스스로 확인", 113);
        foreach (var student in students)
        {
            var state = HamsterProgress.Load(student);
            list.Items.Add(new ListViewItem(new[] { student.Name, state.Points + "알",
                state.Days.Sum(day => day.Total) + "건", state.Days.Sum(day => day.Risk) + "건", state.Days.Sum(day => day.Good) + "건" }));
        }
        if (list.Items.Count == 0) list.Items.Add(new ListViewItem("등록된 학생이 없어요"));
        Controls.Add(list);
    }
}
