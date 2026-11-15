using System;
using System.Collections.Generic;
using System.Drawing;
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
        SuspendLayout();
        Text = "AIRS · 이 PC의 계정";
        ClientSize = new Size(447, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = Palette.Paper;
        MaximizeBox = MinimizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Palette.RoundWindow(this);
        Controls.Add(Palette.Label("AIRS 계정", 23, 17, 388, 44, 21, true, Palette.Ink));
        Controls.Add(Palette.Label("가입한 계정으로 로그인하세요.", 24, 67, 400, 24, 10, false, Palette.Muted));
        Controls.Add(Palette.Label("아이디", 24, 102, 392, 20, 10, true, Palette.Ink));
        Controls.Add(Palette.Label("비밀번호", 24, 162, 392, 20, 10, true, Palette.Ink));
        var nameField = new SurfaceCard(new Rectangle(24, 123, 398, 34), Palette.Surface, Palette.Line, 13);
        var secretField = new SurfaceCard(new Rectangle(24, 183, 398, 34), Palette.Surface, Palette.Line, 13);
        var name = new TextBox { Bounds = new Rectangle(11, 5, 376, 24), Font = Palette.Font(11), MaxLength = 24,
            BackColor = Palette.Surface, ForeColor = Palette.Ink, BorderStyle = BorderStyle.None, AccessibleName = "아이디" };
        var secret = new TextBox { Bounds = new Rectangle(11, 5, 376, 24), Font = Palette.Font(11), MaxLength = 128,
            BackColor = Palette.Surface, ForeColor = Palette.Ink, BorderStyle = BorderStyle.None, UseSystemPasswordChar = true,
            AccessibleName = "비밀번호" };
        var feedback = Palette.Label("처음이라면 먼저 회원가입을 눌러요.",
            24, 219, 399, 38, 9, false, Palette.Muted);
        nameField.Controls.Add(name);
        secretField.Controls.Add(secret);
        Controls.Add(nameField);
        Controls.Add(secretField);
        Controls.Add(feedback);

        var signIn = Palette.Button("로그인", 24, 262, 193, 42, true);
        var join = Palette.Button("회원가입", 229, 262, 193, 42, false);
        bool pending = false;
        FormClosing += delegate(object sender, FormClosingEventArgs args) { if (pending) args.Cancel = true; };
        Action<bool> submit = async register =>
        {
            if (pending) return;
            string id = name.Text.Trim(), password = secret.Text;
            if (id.Length == 0 || password.Length == 0)
            {
                feedback.Text = "아이디와 비밀번호를 입력해 주세요.";
                (id.Length == 0 ? name : secret).Focus();
                return;
            }
            bool adminName = string.Equals(id, LocalAccounts.AdminName, StringComparison.OrdinalIgnoreCase);
            if (register && adminName && !accounts.HasAdmin && MessageBox.Show(this,
                "이 PC의 관리자 계정을 만들까요? 관리자는 이 PC에 등록된 학생의 활동 집계를 볼 수 있어요.",
                "관리자 계정 만들기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            pending = true;
            signIn.Enabled = join.Enabled = name.Enabled = secret.Enabled = false;
            feedback.Text = register ? "계정을 만드는 중이에요…" : "계정을 확인하는 중이에요…";
            LocalAccount verified = null;
            try
            {
                verified = await Task.Run(() => register ? accounts.Register(id, password) : accounts.SignIn(id, password));
                if (register)
                {
                    name.Text = verified.Name;
                    secret.Clear();
                    feedback.Text = verified.Admin ? "관리자 계정을 만들었어요. 비밀번호를 다시 입력해 로그인하세요."
                        : "학생 계정을 만들었어요. 비밀번호를 다시 입력해 로그인하세요.";
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
                signIn.Enabled = join.Enabled = name.Enabled = secret.Enabled = true;
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
