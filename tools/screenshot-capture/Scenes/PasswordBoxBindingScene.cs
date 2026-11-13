using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ScreenshotCapture.Scenes.PasswordBoxBinding;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の PasswordBox.Password をバインドできない理由と、MVVM でビューモデルへ渡す 2 つの方法」の計測。
///
/// 文字の入力は、キーボードからの入力と同じく 1 文字ずつ TextComposition を送って再現する（PasswordBoxDemoScene と同じ経路）。
/// ボタンの押下は、クリックと同じ ButtonBase.OnClick を呼んで Command を実行する。
///
/// 平文が残るかどうかは、このプロセスのコミット済みメモリをすべて読み、パスワードの UTF-16 の並びを探して確かめる。
/// パスワードは条件ごとに乱数で作り直し、前の条件の残りを数えないようにする。
/// 探す側がパスワードの並びを持つと自分自身を見つけてしまうため、パスワードは常に乱数のマスクと XOR した形で持ち、
/// 1 文字ずつ取り出して入力し、照合も 1 文字ずつ XOR を外して行う。完全な並びをどこにも作らない。
/// </summary>
internal sealed class PasswordBoxBindingScene : IScene
{
    private const string LocalNamespace = "clr-namespace:ScreenshotCapture.Scenes.PasswordBoxBinding;assembly=ScreenshotCapture";

    public string Slug => "wpf-passwordbox-binding-mvvm";

    public IReadOnlyList<string> Verifies =>
    [
        "XAML で Password=\"{Binding Password}\" と書くと XamlParseException になり、その内側の例外のメッセージ",
        "PasswordBox が sealed であること。PasswordProperty と SecurePasswordProperty の識別子が無く、INotifyPropertyChanged も実装していないこと",
        "添付プロパティの変更コールバックの中で PasswordChanged を購読する書き方は、ビューモデルの初期値が添付プロパティの既定値と同じ（空文字列）だと購読されず、入力がビューモデルに届かないこと",
        "PasswordChanged の中でビューモデルへ書き戻し、変更コールバックで値を比べずに Password を設定し直す書き方は、\"abc\" を 1 文字ずつ入力した結果の並び",
        "記事の添付プロパティ（購読用の bool と、値を比べてから設定する BoundPassword）で、入力がビューモデルへ届き、ビューモデルから空文字列を設定すると PasswordBox も空になること。ボタンの IsEnabled が入力の前後で変わること",
        "CommandParameter を {Binding SecurePassword, ElementName=…} にすると、入力した後に実行しても渡る SecureString の長さが 0 であること。{Binding ElementName=…} で PasswordBox を渡して実行時に SecurePassword を読むと入力した長さになること",
        "PasswordBox を CommandParameter で渡し、CanExecute で SecurePassword の長さを見る場合、入力の前後でボタンの IsEnabled が変わらないこと",
        "PasswordBox に入力しただけ、SecurePassword を読んだだけ、SecureString を非マネージドのメモリへ展開して ZeroFreeGlobalAllocUnicode で消した後、Password を 1 回読んだ後、添付プロパティでビューモデルの string に同期した後、その後ビューモデルを空にして GC を実行した後に、プロセスのメモリにパスワードの UTF-16 の並びが残っているか",
    ];

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    public async Task CaptureAsync(SceneContext context)
    {
        // Behavior() は、原因（3 行）・添付プロパティ（5 行）・CommandParameter（3 行）の順に行を返す。記事の節ごとに表を分ける。
        List<IReadOnlyList<Loc>> rows = Behavior();
        if (rows.Count != 11) throw new InvalidOperationException($"行の数が想定と異なる: {rows.Count}");
        Loc[] headers = [T("case", "条件"), T("measured", "計測値")];

        await context.SaveTableAsync("PasswordBox: binding to Password", headers, rows[..3], "passwordbox-binding-error.svg");
        await context.SaveTableAsync("PasswordBox: syncing with an attached property", headers, rows[3..8], "passwordbox-attached-property.svg");
        await context.SaveTableAsync("PasswordBox: passing it as CommandParameter", headers, rows[8..], "passwordbox-command-parameter.svg");

        await context.SaveTableAsync(
            "PasswordBox: is the whole password left in process memory as UTF-16?",
            [T("what the code did", "コードが行ったこと"), T("found in memory", "メモリ上に見つかったか")],
            Memory(),
            "passwordbox-plaintext-in-memory.svg");
    }

    // ------------------------------------------------------------------
    // バインドの可否と、2 つの受け渡し方の挙動
    // ------------------------------------------------------------------

    private static List<IReadOnlyList<Loc>> Behavior()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // 1. Password へのバインド。
        // 例外のメッセージは UI カルチャで変わるため、英語と日本語でそれぞれ読む。
        string BindingError(string culture)
        {
            var saved = System.Globalization.CultureInfo.CurrentUICulture;
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
            try
            {
                SceneContext.LoadXaml<PasswordBox>("""<PasswordBox Password="{Binding Password}" />""");
                return "(no exception)";
            }
            catch (XamlParseException ex)
            {
                return $"{ex.GetType().Name}: {ex.Message.Split(Environment.NewLine)[0]}";
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentUICulture = saved;
            }
        }

        rows.Add([T("XAML: Password=\"{Binding Password}\"", "XAML: Password=\"{Binding Password}\""), T(BindingError("en-US"), BindingError("ja-JP"))]);
        rows.Add([T("PasswordBox is sealed", "PasswordBox が sealed か"), typeof(PasswordBox).IsSealed.ToString()]);
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
        rows.Add([T("has PasswordProperty / SecurePasswordProperty / implements INotifyPropertyChanged",
                    "PasswordProperty / SecurePasswordProperty があるか / INotifyPropertyChanged を実装しているか"),
                  $"{typeof(PasswordBox).GetField("PasswordProperty", flags) is not null} / " +
                  $"{typeof(PasswordBox).GetField("SecurePasswordProperty", flags) is not null} / " +
                  $"{typeof(System.ComponentModel.INotifyPropertyChanged).IsAssignableFrom(typeof(PasswordBox))}"]);

        // 2. 添付プロパティ: 変更コールバックの中で購読する書き方（初期値が既定値と同じ）。
        {
            var (box, vm, _) = BuildLogin("""<PasswordBox local:NaivePasswordAssist.BoundPassword="{Binding Password}" />""");
            Type(box, "abc");
            rows.Add([T("subscribe in the change callback, ViewModel starts with \"\": type \"abc\"",
                        "変更コールバックの中で購読・ビューモデルの初期値 \"\": \"abc\" を入力"),
                      T($"ViewModel.Password \"{vm.Password}\"", $"ViewModel.Password \"{vm.Password}\"")]);
        }

        // 3. 添付プロパティ: 購読はするが、値を比べずに Password を設定し直す書き方。
        {
            var (box, vm, _) = BuildLogin(
                """<PasswordBox local:UncheckedPasswordAssist.Attach="True" local:UncheckedPasswordAssist.BoundPassword="{Binding Password}" />""");
            Type(box, "abc");
            rows.Add([T("set Password back without comparing: type \"abc\"", "値を比べずに Password を設定し直す: \"abc\" を入力"),
                      $"PasswordBox \"{box.Password}\" / ViewModel \"{vm.Password}\""]);
        }

        // 4. 記事の添付プロパティ。
        {
            var (box, vm, button) = BuildLogin(
                """<PasswordBox local:PasswordBoxAssist.Attach="True" local:PasswordBoxAssist.BoundPassword="{Binding Password}" />""");
            bool before = button.IsEnabled;
            Type(box, "abc");
            bool after = button.IsEnabled;
            rows.Add([T("article's attached property: type \"abc\"", "記事の添付プロパティ: \"abc\" を入力"),
                      $"PasswordBox \"{box.Password}\" / ViewModel \"{vm.Password}\""]);
            vm.Password = string.Empty;
            rows.Add([T("then the ViewModel sets Password = \"\"", "続けてビューモデルが Password = \"\" を設定"),
                      $"PasswordBox \"{box.Password}\""]);
            rows.Add([T("button IsEnabled before / after typing", "ボタンの IsEnabled: 入力の前 / 後"), $"{before} / {after}"]);
        }

        // 5. CommandParameter で渡す。
        {
            var (box, vm, button) = BuildCommandParameter("""CommandParameter="{Binding SecurePassword, ElementName=PasswordInput}" """, requireInput: false);
            Type(box, "abc");
            Invoke(button);
            rows.Add([T("CommandParameter=\"{Binding SecurePassword, ElementName=PasswordInput}\": type \"abc\", then click",
                        "CommandParameter=\"{Binding SecurePassword, ElementName=PasswordInput}\": \"abc\" を入力して押す"),
                      $"SecureString.Length {vm.LastLength}"]);
        }

        {
            var (box, vm, button) = BuildCommandParameter("""CommandParameter="{Binding ElementName=PasswordInput}" """, requireInput: false);
            Type(box, "abc");
            Invoke(button);
            rows.Add([T("CommandParameter=\"{Binding ElementName=PasswordInput}\": type \"abc\", then click (read SecurePassword in Execute)",
                        "CommandParameter=\"{Binding ElementName=PasswordInput}\": \"abc\" を入力して押す（Execute で SecurePassword を読む）"),
                      $"SecureString.Length {vm.LastLength}"]);
            rows.Add([T("same, with CanExecute checking SecurePassword.Length > 0: button IsEnabled before / after typing",
                        "同じ渡し方で CanExecute が SecurePassword.Length > 0 を見る: ボタンの IsEnabled 入力の前 / 後"),
                      Enabled()]);
        }

        return rows;

        string Enabled()
        {
            var (box, _, button) = BuildCommandParameter("""CommandParameter="{Binding ElementName=PasswordInput}" """, requireInput: true);
            bool before = button.IsEnabled;
            Type(box, "abc");
            return $"{before} / {button.IsEnabled}";
        }
    }

    private const string LoginXaml = """
        <StackPanel>
          {0}
          <Button Content="Sign in" Command="{Binding SignInCommand}" />
        </StackPanel>
        """;

    private static (PasswordBox Box, LoginViewModel Vm, Button Button) BuildLogin(string passwordBoxXaml)
    {
        var panel = SceneContext.LoadXaml<StackPanel>(LoginXaml.Replace("{0}", passwordBoxXaml), ("local", LocalNamespace));
        var vm = new LoginViewModel();
        panel.DataContext = vm;
        DemoProbe.Layout(panel, 300, 100);
        return ((PasswordBox)panel.Children[0], vm, (Button)panel.Children[1]);
    }

    private const string CommandParameterXaml = """
        <StackPanel>
          <PasswordBox x:Name="PasswordInput" />
          <Button Content="Sign in" Command="{Binding SignInCommand}" {0}/>
        </StackPanel>
        """;

    private static (PasswordBox Box, CommandParameterViewModel Vm, Button Button) BuildCommandParameter(string parameter, bool requireInput)
    {
        var panel = SceneContext.LoadXaml<StackPanel>(CommandParameterXaml.Replace("{0}", parameter));
        var vm = new CommandParameterViewModel(requireInput);
        panel.DataContext = vm;
        DemoProbe.Layout(panel, 300, 100);
        return ((PasswordBox)panel.Children[0], vm, (Button)panel.Children[1]);
    }

    private static void Type(PasswordBox box, string text)
    {
        box.Focus();
        foreach (char c in text)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, c.ToString()));
        }
    }

    /// <summary>クリックと同じ経路（ButtonBase.OnClick）で Command を実行する。AutomationPeer の Invoke は非同期に実行されるため使わない。</summary>
    private static void Invoke(Button button) =>
        typeof(System.Windows.Controls.Primitives.ButtonBase)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(button, null);

    // ------------------------------------------------------------------
    // 平文がメモリに残るか
    // ------------------------------------------------------------------

    private static List<IReadOnlyList<Loc>> Memory()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        using var scanner = new MemoryScanner();

        void Row(Loc label, Action<PasswordBox> act, Func<PasswordBox>? build = null)
        {
            var secret = MaskedSecret.Create(12);
            PasswordBox box = build?.Invoke() ?? DemoProbe.Layout(new PasswordBox(), 200, 30);
            TypeMasked(box, secret);
            act(box);
            int count = scanner.Count(secret);
            rows.Add([label, count == 0 ? T("no", "なし") : T("yes", "あり")]);
            box.Clear();
        }

        Row(T("typed into a PasswordBox only", "PasswordBox に入力しただけ"), _ => { });

        Row(T("read SecurePassword (and disposed it)", "SecurePassword を読んだ（Dispose した）"), box =>
        {
            using SecureString s = box.SecurePassword;
            _ = s.Length;
        });

        Row(T("copied SecurePassword to unmanaged memory, then ZeroFreeGlobalAllocUnicode",
              "SecurePassword を非マネージドのメモリへ展開し、ZeroFreeGlobalAllocUnicode で消した"), box =>
        {
            using SecureString s = box.SecurePassword;
            nint p = Marshal.SecureStringToGlobalAllocUnicode(s);
            Marshal.ZeroFreeGlobalAllocUnicode(p);
        });

        Row(T("read Password once (string, reference dropped)", "Password を 1 回読んだ（string。参照は捨てた）"), box =>
        {
            ReadOnce(box);
        });

        LoginViewModel? vm = null;
        PasswordBox BuildBound()
        {
            var (box, model, _) = BuildLogin(
                """<PasswordBox local:PasswordBoxAssist.Attach="True" local:PasswordBoxAssist.BoundPassword="{Binding Password}" />""");
            vm = model;
            return box;
        }

        Row(T("synced to a ViewModel string with the attached property", "添付プロパティでビューモデルの string に同期した"),
            _ => { }, BuildBound);

        Row(T("same, then ViewModel.Password = \"\" and GC.Collect", "同じ同期の後、ViewModel.Password = \"\" にして GC.Collect を実行した"),
            _ =>
            {
                vm!.Password = string.Empty;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            },
            BuildBound);

        return rows;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ReadOnce(PasswordBox box) => _ = box.Password.Length;

    private static void TypeMasked(PasswordBox box, MaskedSecret secret)
    {
        box.Focus();
        for (int i = 0; i < secret.Length; i++)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, secret.CharAt(i).ToString()));
        }
    }

    /// <summary>パスワードを、乱数のマスクと XOR した形でだけ持つ。完全な並びをメモリ上に作らないため。</summary>
    private sealed class MaskedSecret
    {
        private readonly ushort[] _masked;
        private readonly ushort[] _mask;

        private MaskedSecret(ushort[] masked, ushort[] mask) => (_masked, _mask) = (masked, mask);

        public int Length => _masked.Length;

        public static MaskedSecret Create(int length)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var masked = new ushort[length];
            var mask = new ushort[length];
            for (int i = 0; i < length; i++)
            {
                mask[i] = (ushort)RandomNumberGenerator.GetInt32(1, 0x10000);
                masked[i] = (ushort)(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)] ^ mask[i]);
            }

            return new MaskedSecret(masked, mask);
        }

        public char CharAt(int index) => (char)(_masked[index] ^ _mask[index]);
    }

    /// <summary>
    /// このプロセスのコミット済みで読み取り可能なメモリを、ReadProcessMemory で読んで探す。
    /// 読み込み先のバッファー自身の範囲は読まない（前に読んだ内容の写しを数えないため）。読み終えたらバッファーを消す。
    /// </summary>
    private sealed class MemoryScanner : IDisposable
    {
        private const int ChunkSize = 1 << 20;
        private readonly byte[] _buffer = GC.AllocateArray<byte>(ChunkSize, pinned: true);
        private readonly nint _bufferStart;
        private readonly nint _bufferEnd;

        public MemoryScanner()
        {
            _bufferStart = Marshal.UnsafeAddrOfPinnedArrayElement(_buffer, 0);
            _bufferEnd = _bufferStart + ChunkSize;
        }

        public int Count(MaskedSecret secret)
        {
            int byteLength = secret.Length * 2;
            int count = 0;
            nint process = GetCurrentProcess();
            nint address = 0;
            while (VirtualQuery(address, out var info, Marshal.SizeOf<MemoryBasicInformation>()) != 0)
            {
                nint regionEnd = info.BaseAddress + info.RegionSize;
                bool readable = info.State == MemCommit
                    && info.Protect != 0
                    && (info.Protect & (PageNoAccess | PageGuard)) == 0;
                if (readable)
                {
                    nint start = info.BaseAddress;
                    while (start < regionEnd)
                    {
                        if (start >= _bufferStart && start < _bufferEnd)
                        {
                            start = _bufferEnd;
                            continue;
                        }

                        nint limit = start < _bufferStart && _bufferStart < regionEnd ? _bufferStart : regionEnd;
                        nint size = Math.Min(ChunkSize, limit - start);
                        if (ReadProcessMemory(process, start, _bufferStart, size, out nint read) && read >= byteLength)
                        {
                            count += CountIn(secret, (int)read);
                        }

                        // 境目にまたがる並びも見つけられるよう、次の読み込みを少し重ねる。
                        start += size >= limit - start ? size : size - byteLength;
                    }
                }

                if (regionEnd <= address) break;
                address = regionEnd;
            }

            Array.Clear(_buffer);
            return count;
        }

        private int CountIn(MaskedSecret secret, int length)
        {
            ReadOnlySpan<ushort> chars = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(_buffer.AsSpan(0, length));
            ushort first = secret.CharAt(0);
            int found = 0;
            int offset = 0;
            while (offset < chars.Length)
            {
                int index = chars[offset..].IndexOf(first);
                if (index < 0) break;
                int at = offset + index;
                if (at + secret.Length > chars.Length) break;
                bool match = true;
                for (int i = 1; i < secret.Length; i++)
                {
                    if (chars[at + i] != secret.CharAt(i))
                    {
                        match = false;
                        break;
                    }
                }

                if (match) found++;
                offset = at + 1;
            }

            return found;
        }

        public void Dispose()
        {
        }

        private const uint MemCommit = 0x1000;
        private const uint PageNoAccess = 0x01;
        private const uint PageGuard = 0x100;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public ushort PartitionId;
            public nint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll")]
        private static extern nint VirtualQuery(nint address, out MemoryBasicInformation buffer, nint length);

        [DllImport("kernel32.dll")]
        private static extern nint GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(nint process, nint baseAddress, nint buffer, nint size, out nint read);
    }
}
