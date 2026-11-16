using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenshotCapture.Scenes.MvvmDialogService;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF MVVM でダイアログを出す: IDialogService の最小実装と Owner・戻り値の落とし穴」の計測。
///
/// 1. テストのスレッドからコマンドを実行したとき、ビューモデルが MessageBox.Show を直接呼ぶと、誰かが閉じるまで戻らないこと。
/// 2. Owner の有無で、ダイアログの所有者（Win32）・メインを Z オーダーの先頭へ移した後の順序・矩形の重なり・出る位置が変わること。
/// 3. DialogService を通したときに、ビューモデルが受け取る値（確認の結果・ファイルのパス・子ウィンドウの結果）。
///
/// ダイアログはモーダルのループで待つため、開く前に Dispatcher へ計測の処理を積み、ループの中で観測して閉じる。
/// ダイアログを見つけられなかった場合も、開いた新しいウィンドウを閉じてから失敗にする（ループに残ったまま止まらないため）。
/// </summary>
internal sealed class MvvmDialogServiceScene : IScene
{
    public string Slug => "wpf-mvvm-dialog-service";

    public IReadOnlyList<string> Verifies =>
    [
        "ビューモデルの中で MessageBox.Show を呼ぶ書き方では、別に作った MTA のスレッドから DeleteCommand.Execute を呼ぶと、1 秒の時点でそのスレッドがメッセージボックス（#32770）を開いたまま Execute が戻っておらず、外からメッセージボックスに OK（IDOK）を送って初めて戻ること",
        "IDialogService を受け取るビューモデルにテスト用の実装（決めた答えを返す）を渡すと、同じ方法で作った MTA のスレッドから呼んだ Execute が 1 秒以内に戻り、Confirm が 1 回呼ばれ、答えが true なら削除し、false なら削除しないこと",
        "Owner を設定せずに Window.ShowDialog で開いた子ウィンドウは Win32 の所有者を持たず、メインを SetWindowPos で Z オーダーの先頭へ移すとメインより下になること。WindowStartupLocation=CenterOwner でもメインの中央に出ないこと",
        "DialogService.ShowDialog で開いた子ウィンドウは、メインと別のウィンドウ（検索）のどちらがアクティブでも所有者がメインになり、メインを Z オーダーの先頭へ移してもメインより上に残り、メインの中央に出ること",
        "MessageBox.Show と OpenFileDialog.ShowDialog は、所有者の引数を省くと、メインがアクティブならメイン、Owner を持たない別のウィンドウ（検索）がアクティブならそのウィンドウが所有者になり、DialogService を通すとどちらの場合もメインになること。所有者がメインでないダイアログは、メインを Z オーダーの先頭へ移すとメインより下になること",
        "DialogService を通すと、DeleteCommand の確認が OK で true（削除する）、キャンセルで false（削除しない）になり、ImportCommand のファイル選択がキャンセルで null、ファイル名を入れて開くとそのファイルの完全なパスになること",
        "EditCommand の子ウィンドウを OK で閉じると ShowDialog が true を返して写しの編集が一覧の Customer に入り、キャンセルや ×（WM_CLOSE）で閉じると false を返して元の値のまま残ること。一覧の Customer そのものをバインドすると、キャンセルしても編集が残ること",
        "呼び出したビューモデルを DataContext に持つウィンドウが無いと、DeleteCommand・EditCommand とも InvalidOperationException になり、表示中のウィンドウの数と Application.Windows の数が呼ぶ前と変わらず、顧客も消えないこと。ウィンドウを作ってから所有者を探す順序のサービスでは、表示はされないまま Application.Windows が 1 つ増えること",
        "Window.ShowDialog で開いた子ウィンドウ（Owner の有無にかかわらず）と、所有者がメインのメッセージボックス・ファイル選択を開いている間は、メインウィンドウが無効（IsWindowEnabled が false）になること。所有者の引数を省いたメッセージボックスとファイル選択は、所有者が別のウィンドウ（検索）になると、メインを無効にしないこと",
        "Owner を設定した子ウィンドウを × で閉じると、Window.ShowDialog() そのものは false を返すこと（DialogResult は設定しない）",
    ];

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // 各表の期待値。行の日本語のセルを「|」でつないだもの。「*」のセルは照合しない。
    // ダイアログの出る位置（メインとの重なり）はシステムが決める（Owner の無い子ウィンドウは Windows の既定の位置、
    // メッセージボックスとファイル選択はそれぞれの決め方）ため、記事が位置を前提にしていない行は照合しない。
    // 検索がアクティブなときの所有者の引数なしの 2 行も、位置はシステムが決めるため照合しない。本文はこの 2 行の重なりを表の回の値として書いている。
    private static readonly string[] ExpectedTestCalls =
    [
        "MessageBox.Show を直接呼ぶ|メッセージボックス（#32770）|いいえ（外から OK（IDOK）を送った後に戻った）|1",
        "IDialogService（テスト用の実装が true を返す）|（戻っていた）|はい|1",
        "IDialogService（テスト用の実装が false を返す）|（戻っていた）|はい|2",
    ];

    private static readonly string[] ExpectedOwners =
    [
        "Window.ShowDialog()・Owner を設定しない|（なし）|ダイアログがメインより下|*|いいえ|無効",
        "DialogService.ShowDialog（Owner = メイン）|メインウィンドウ|ダイアログがメインより上|全体がメインの内側|はい|無効",
        "MessageBox.Show(message, …)・所有者の引数なし|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
        "DialogService.Confirm（MessageBox.Show(owner, …)）|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
        "OpenFileDialog.ShowDialog()・所有者の引数なし|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
        "DialogService.OpenFile（ShowDialog(owner)）|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
        "DialogService.ShowDialog・検索がアクティブ|メインウィンドウ|ダイアログがメインより上|全体がメインの内側|はい|無効",
        "MessageBox.Show(message, …)・所有者の引数なし・検索がアクティブ|別のウィンドウ（検索）|ダイアログがメインより下|*|いいえ|有効",
        "DialogService.Confirm・検索がアクティブ|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
        "OpenFileDialog.ShowDialog()・所有者の引数なし・検索がアクティブ|別のウィンドウ（検索）|ダイアログがメインより下|*|いいえ|有効",
        "DialogService.OpenFile・検索がアクティブ|メインウィンドウ|ダイアログがメインより上|*|いいえ|無効",
    ];

    private static readonly string[] ExpectedResults =
    [
        "DeleteCommand → OK|Confirm: true|顧客 2 件 → 1 件",
        "DeleteCommand → キャンセル|Confirm: false|顧客 2 件 → 2 件",
        "ImportCommand → キャンセル|OpenFile: null|ImportPath: null",
        "ImportCommand → ファイル名を入れて開く|OpenFile: ファイルの完全なパス|ImportPath: 同じパス",
        "EditCommand →「x」を入力して OK|ShowDialog: true|Customer.Name: \"Alicex\"",
        "EditCommand →「x」を入力してキャンセル|ShowDialog: false|Customer.Name: \"Alice\"",
        "EditCommand →「x」を入力して ×|ShowDialog: false|Customer.Name: \"Alice\"",
        "Window.ShowDialog() そのもの → ×|ShowDialog(): false|DialogResult は設定していない",
        "Customer そのものをバインド →「x」を入力してキャンセル|ShowDialog: false|Customer.Name: \"Alicex\"",
        "どのウィンドウの DataContext でもないビューモデルの DeleteCommand|例外 InvalidOperationException|表示中のウィンドウ * → *、Application.Windows 1 → 1、顧客 1 → 1 件",
        "どのウィンドウの DataContext でもないビューモデルの EditCommand|例外 InvalidOperationException|表示中のウィンドウ * → *、Application.Windows 1 → 1、顧客 1 → 1 件",
        "同じ EditCommand・ウィンドウを作ってから所有者を探す順序のサービス|例外 InvalidOperationException|表示中のウィンドウ * → *、Application.Windows 1 → 2、顧客 1 → 1 件",
    ];

    /// <summary>
    /// 表の各行（日本語のセル）を期待値と照合する。セルが「*」なら照合しない。セルの中の「*」は、
    /// 前後と同じ数（表示中のウィンドウの数）を表し、同じ行の中で前と後が等しいことを確かめる。
    /// </summary>
    private static void Expect(string table, IReadOnlyList<IReadOnlyList<Loc>> rows, string[] expected)
    {
        if (rows.Count != expected.Length)
        {
            throw new InvalidOperationException($"{table}: 行数が期待と違う（{rows.Count} 行、期待 {expected.Length} 行）。");
        }

        for (int i = 0; i < rows.Count; i++)
        {
            string[] want = expected[i].Split('|');
            if (rows[i].Count != want.Length)
            {
                throw new InvalidOperationException($"{table} の {i + 1} 行目: 列数が期待と違う。");
            }

            for (int j = 0; j < want.Length; j++)
            {
                string actual = rows[i][j].Ja;
                if (want[j] == "*")
                {
                    continue;
                }

                bool ok = want[j].Contains("* → *", StringComparison.Ordinal)
                    ? MatchesSameCount(actual, want[j])
                    : actual == want[j];
                if (!ok)
                {
                    throw new InvalidOperationException($"{table} の {i + 1} 行目 {j + 1} 列目が期待と違う: 実測「{actual}」、期待「{want[j]}」。");
                }
            }
        }
    }

    /// <summary>「* → *」の部分が同じ数どうしで、残りが期待と一致するか。</summary>
    private static bool MatchesSameCount(string actual, string pattern)
    {
        // Regex.Escape は空白もエスケープするため、「* → *」で分けてから各部分をエスケープしてつなぐ。
        // 後方参照を 1 つしか使わないので、「* → *」は 1 つだけにする（2 つ以上は誤判定になる）。
        string[] parts = pattern.Split("* → *");
        if (parts.Length != 2)
        {
            throw new ArgumentException($"「* → *」は 1 つだけ書ける: {pattern}", nameof(pattern));
        }

        string regex = "^" + string.Join(@"(\d+) → \1", parts.Select(System.Text.RegularExpressions.Regex.Escape)) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(actual, regex);
    }

    public async Task CaptureAsync(SceneContext context)
    {
        var testRows = MeasureTestCalls();
        var ownerRows = await MeasureOwnersAsync(context);
        var resultRows = await MeasureResultsAsync();

        // 記事の本文と Verifies はこの値を前提に書いている。食い違えば、どの表も記録も書き換えずに止める。
        // 画面の撮影（PNG）は計測の途中で行うため、撮る直前にその行全体を ExpectedOwners の同じ行と照合している（Measure の expectedRow）。
        Expect("test-call", testRows, ExpectedTestCalls);
        Expect("owner", ownerRows, ExpectedOwners);
        Expect("results", resultRows, ExpectedResults);

        await context.SaveTableAsync(
            "Executing DeleteCommand from a separate MTA thread (standing in for a test)",
            [
                T("view model", "ビューモデル"),
                T("windows the thread had open at 1 s", "1 秒の時点でスレッドが開いていたウィンドウ"),
                T("Execute returned within 1 s", "1 秒以内に Execute が戻った"),
                T("customers left (of 2)", "残った顧客（2 件中）"),
            ],
            testRows,
            "dialog-service-test-call.svg");

        await context.SaveTableAsync(
            "Owner of each dialog, and z-order after the main window is moved to the top",
            [
                T("how the dialog is opened", "開き方"),
                T("Win32 owner", "Win32 の所有者"),
                T("after main is moved to the top of the z-order", "メインを Z オーダーの先頭へ移した後"),
                T("rectangle vs. main", "メインとの重なり"),
                T("centered on main", "メインの中央に出た"),
                T("main enabled", "メインは有効か"),
            ],
            ownerRows,
            "dialog-service-owner.svg");

        await context.SaveTableAsync(
            "What the view model receives through DialogService",
            [
                T("operation", "操作"),
                T("return value", "戻り値"),
                T("state after the call", "呼び出し後の状態"),
            ],
            resultRows,
            "dialog-service-results.svg");
    }

    // ------------------------------------------------------------------
    // 1. テストのスレッドから実行する
    // ------------------------------------------------------------------

    private static List<IReadOnlyList<Loc>> MeasureTestCalls()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        // MessageBox.Show を直接呼ぶビューモデル。
        {
            var vm = new DirectMessageBoxViewModel();
            vm.Customers.Add(new Customer("Alice"));
            vm.Customers.Add(new Customer("Bob"));
            vm.Selected = vm.Customers[0];

            (Loc shown, bool returnedEarly) = RunOnTestThread(() => vm.DeleteCommand.Execute(null), closeWithOk: true);
            rows.Add([T("calls MessageBox.Show directly", "MessageBox.Show を直接呼ぶ"), shown, YesNo(returnedEarly), vm.Customers.Count.ToString()]);
        }

        // IDialogService を受け取るビューモデルに、テスト用の実装を渡す。
        foreach (bool answer in new[] { true, false })
        {
            var fake = new FakeDialogService { ConfirmResult = answer };
            var vm = new CustomerListViewModel(fake);
            vm.Customers.Add(new Customer("Alice"));
            vm.Customers.Add(new Customer("Bob"));
            vm.Selected = vm.Customers[0];

            (Loc shown, bool returnedEarly) = RunOnTestThread(() => vm.DeleteCommand.Execute(null), closeWithOk: false);
            if (fake.ConfirmCount != 1)
            {
                throw new InvalidOperationException($"テスト用の Confirm が 1 回呼ばれていない（{fake.ConfirmCount} 回）。");
            }

            rows.Add([
                T($"IDialogService (test double returns {answer.ToString().ToLowerInvariant()})", $"IDialogService（テスト用の実装が {answer.ToString().ToLowerInvariant()} を返す）"),
                shown, YesNo(returnedEarly), vm.Customers.Count.ToString(),
            ]);
        }

        return rows;
    }

    /// <summary>
    /// テストのスレッドの代わりに、UI のメッセージループを持たない MTA のスレッドを作って処理を実行する（実際のテストランナーでは測っていない）。
    /// 1 秒待っても戻らず、そのスレッドがウィンドウを出していれば、OK（IDOK）を送って閉じてから戻りを待つ。
    /// </summary>
    private static (Loc Shown, bool ReturnedWithinOneSecond) RunOnTestThread(Action action, bool closeWithOk)
    {
        uint nativeThreadId = 0;
        Exception? error = null;
        var started = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            nativeThreadId = Native.GetCurrentThreadId();
            started.Set();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.IsBackground = true;
        thread.Start();
        started.Wait();

        bool returned = thread.Join(TimeSpan.FromSeconds(1));
        var windows = Native.ThreadWindows(nativeThreadId);

        // 戻ったスレッドはウィンドウを持てないので、数えた値ではなく「戻っていた」と書く。
        Loc shown = returned
            ? T("(already returned)", "（戻っていた）")
            : windows.Count == 0
                ? T("none", "なし")
                : string.Join(", ", windows.Select(Native.ClassName).Distinct());

        if (!returned)
        {
            if (!closeWithOk || windows.Count == 0)
            {
                throw new InvalidOperationException($"テストのスレッドが 1 秒で戻らないが、閉じるウィンドウが無い（{shown.En}）。");
            }

            foreach (IntPtr window in windows)
            {
                Native.PostMessage(window, Native.WM_COMMAND, Native.IDOK, IntPtr.Zero);
            }

            if (!thread.Join(TimeSpan.FromSeconds(5)))
            {
                // 画面に残さないよう、閉じる要求も送ってから失敗にする。
                foreach (IntPtr window in Native.ThreadWindows(nativeThreadId))
                {
                    Native.PostMessage(window, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                throw new InvalidOperationException("メッセージボックスに OK を送っても、テストのスレッドが戻らない。");
            }

            if (shown.En == "#32770")
            {
                shown = T("message box (#32770)", "メッセージボックス（#32770）");
            }
        }

        if (error is not null)
        {
            throw new InvalidOperationException("テストのスレッドで例外が起きた。", error);
        }

        return (shown, returned);
    }

    private static Loc YesNo(bool value) => value ? T("yes", "はい") : T("no (returned after OK (IDOK) was sent from outside)", "いいえ（外から OK（IDOK）を送った後に戻った）");

    // ------------------------------------------------------------------
    // 2. Owner の有無
    // ------------------------------------------------------------------

    private const string EditWindowXaml = """
        <Window Title="顧客の編集" Width="320" SizeToContent="Height"
                WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
          <StackPanel Margin="12">
            <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" />
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
              <Button Content="OK" IsDefault="True" Width="80" Command="{Binding OkCommand}" />
              <Button Content="キャンセル" Width="80" Margin="8,0,0,0" Command="{Binding CancelCommand}" />
            </StackPanel>
          </StackPanel>
        </Window>
        """;

    private static Window CreateEditWindow() => SceneContext.LoadXaml<Window>(EditWindowXaml);

    /// <summary>メインウィンドウ。一覧と、ビューモデルのコマンドを呼ぶボタンだけを置く。DataContext は呼び出し側で設定する。</summary>
    private static async Task<Window> ShowMainAsync(object viewModel)
    {
        var window = SceneContext.LoadXaml<Window>("""
            <Window Title="顧客台帳" Width="520" Height="360" WindowStartupLocation="Manual">
              <DockPanel Margin="12">
                <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,12,0,0">
                  <Button Content="編集" Width="80" Command="{Binding EditCommand}" />
                  <Button Content="削除" Width="80" Margin="8,0,0,0" Command="{Binding DeleteCommand}" />
                  <Button Content="読み込み" Width="80" Margin="8,0,0,0" Command="{Binding ImportCommand}" />
                </StackPanel>
                <ListBox ItemsSource="{Binding Customers}" DisplayMemberPath="Name" SelectedItem="{Binding Selected}" />
              </DockPanel>
            </Window>
            """);
        window.DataContext = viewModel;

        // 画面の中央から外した位置に置き、「メインの中央」と「画面の中央」を見分けられるようにする。
        Rect area = SystemParameters.WorkArea;
        window.Left = area.Left + 60;
        window.Top = area.Top + 60;

        await Capture.ShowAndSettleAsync(window);
        window.Activate();
        await Capture.SettleAsync(window, 100);
        if (!window.IsActive)
        {
            window.Close();
            throw new InvalidOperationException("メインウィンドウをアクティブにできない（ほかのウィンドウが前面を保持している）。");
        }

        return window;
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureOwnersAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var service = new DialogService();
        service.Register<CustomerEditViewModel>(CreateEditWindow);
        var vm = new CustomerListViewModel(service);
        vm.Customers.Add(new Customer("Alice"));
        vm.Customers.Add(new Customer("Bob"));
        vm.Selected = vm.Customers[0];

        Window main = await ShowMainAsync(vm);
        Window? other = null;
        try
        {
            IntPtr mainHandle = new WindowInteropHelper(main).Handle;

            // 同じアプリの、Owner を持たない別のウィンドウ（検索画面など）。メインと重ならない右側に置く。これがアクティブなときにも測る。
            other = new Window { Title = "検索", Width = 300, Height = 200, Left = main.Left + 600, Top = main.Top };
            await Capture.ShowAndSettleAsync(other);
            IntPtr otherHandle = new WindowInteropHelper(other).Handle;

            async Task Measure(Loc label, Action open, bool otherActive = false, string? screenshot = null, int expectedRow = -1)
            {
                Window active = otherActive ? other! : main;
                active.Activate();
                await Capture.SettleAsync(active, 100);
                if (!active.IsActive)
                {
                    throw new InvalidOperationException($"計測の前に {active.Title} をアクティブにできない。");
                }

                Loc owner = "", afterTop = "", overlap = "", centered = "", mainEnabled = "";
                await WhileModalAsync(open, async dialog =>
                {
                    IntPtr ownerHandle = Native.GetWindow(dialog, Native.GW_OWNER);
                    owner = ownerHandle == IntPtr.Zero ? T("(none)", "（なし）")
                        : ownerHandle == mainHandle ? T("main window", "メインウィンドウ")
                        : ownerHandle == otherHandle ? T("the other window (Search)", "別のウィンドウ（検索）")
                        : $"other ({Native.ClassName(ownerHandle)})";

                    centered = DescribeCentered(dialog, mainHandle);
                    overlap = DescribeOverlap(dialog, mainHandle);
                    mainEnabled = Native.IsWindowEnabled(mainHandle) ? T("enabled", "有効") : T("disabled", "無効");

                    // メインを Z オーダーの先頭へ移す（アクティブにはしない）。
                    // タスクバーや Alt+Tab でメインを選ぶ操作の代わりで、それらと同じ結果になるかは確かめていない。
                    Native.SetWindowPos(mainHandle, Native.HWND_TOP, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    afterTop = Native.IsAbove(dialog, mainHandle)
                        ? T("dialog above main", "ダイアログがメインより上")
                        : T("dialog below main", "ダイアログがメインより下");

                    if (screenshot is not null)
                    {
                        // 記事と逆の図で上書きしないよう、撮る前にこの行全体を ExpectedOwners の同じ行と照合する（表と同じ照合）。
                        Expect("owner (before capture)", [[label, owner, afterTop, overlap, centered, mainEnabled]], [ExpectedOwners[expectedRow]]);

                        // 子ウィンドウの枠の色もメインと同じ既定の配色に揃えてから、画面に見えているとおりにメインの範囲を撮る。
                        Window? dialogWindow = Application.Current.Windows.OfType<Window>()
                            .SingleOrDefault(w => new WindowInteropHelper(w).Handle == dialog);
                        if (dialogWindow is not null)
                        {
                            Capture.ApplyNeutralChrome(dialogWindow);
                        }

                        // 画面の撮影はほかのアプリのウィンドウも写すため、撮る間だけメインを Topmost にして、
                        // メインの範囲にほかのアプリが重ならないようにする。Z オーダーの計測は、この前に済ませている。
                        // 測った上下関係を保つため、子ウィンドウがメインより上だった場合だけ、メインの後に子ウィンドウも Topmost にする。
                        bool dialogAbove = Native.IsAbove(dialog, mainHandle);
                        main.Topmost = true;
                        if (dialogAbove && dialogWindow is not null) dialogWindow.Topmost = true;
                        try
                        {
                            await Task.Delay(400);
                            context.SaveScreenArea(main, screenshot, dialogAbove && dialogWindow is not null ? [main, dialogWindow] : [main]);
                        }
                        finally
                        {
                            if (dialogWindow is not null) dialogWindow.Topmost = false;
                            main.Topmost = false;
                        }
                    }
                });

                rows.Add([label, owner, afterTop, overlap, centered, mainEnabled]);
            }

            await Measure(T("Window.ShowDialog(), Owner not set", "Window.ShowDialog()・Owner を設定しない"), () =>
            {
                Window window = CreateEditWindow();
                window.DataContext = new CustomerEditViewModel("Alice");
                window.ShowDialog();
            }, screenshot: "owner-not-set.png", expectedRow: 0);

            await Measure(T("DialogService.ShowDialog (Owner = main)", "DialogService.ShowDialog（Owner = メイン）"),
                () => service.ShowDialog(vm, new CustomerEditViewModel("Alice")), screenshot: "owner-main.png", expectedRow: 1);

            await Measure(T("MessageBox.Show(message, …), no owner argument", "MessageBox.Show(message, …)・所有者の引数なし"),
                () => MessageBox.Show("Alice を削除しますか。", "削除の確認", MessageBoxButton.OKCancel, MessageBoxImage.Question));

            await Measure(T("DialogService.Confirm (MessageBox.Show(owner, …))", "DialogService.Confirm（MessageBox.Show(owner, …)）"),
                () => service.Confirm(vm, "Alice を削除しますか。", "削除の確認"));

            await Measure(T("OpenFileDialog.ShowDialog(), no owner argument", "OpenFileDialog.ShowDialog()・所有者の引数なし"),
                () => new OpenFileDialog { Filter = "JSON ファイル (*.json)|*.json" }.ShowDialog());

            await Measure(T("DialogService.OpenFile (ShowDialog(owner))", "DialogService.OpenFile（ShowDialog(owner)）"),
                () => service.OpenFile(vm, "JSON ファイル (*.json)|*.json"));

            await Measure(T("DialogService.ShowDialog, Search window active", "DialogService.ShowDialog・検索がアクティブ"),
                () => service.ShowDialog(vm, new CustomerEditViewModel("Alice")),
                otherActive: true);

            await Measure(T("MessageBox.Show(message, …), no owner argument, Search window active", "MessageBox.Show(message, …)・所有者の引数なし・検索がアクティブ"),
                () => MessageBox.Show("Alice を削除しますか。", "削除の確認", MessageBoxButton.OKCancel, MessageBoxImage.Question),
                otherActive: true);

            await Measure(T("DialogService.Confirm, Search window active", "DialogService.Confirm・検索がアクティブ"),
                () => service.Confirm(vm, "Alice を削除しますか。", "削除の確認"),
                otherActive: true);

            await Measure(T("OpenFileDialog.ShowDialog(), no owner argument, Search window active", "OpenFileDialog.ShowDialog()・所有者の引数なし・検索がアクティブ"),
                () => new OpenFileDialog { Filter = "JSON ファイル (*.json)|*.json" }.ShowDialog(),
                otherActive: true);

            await Measure(T("DialogService.OpenFile, Search window active", "DialogService.OpenFile・検索がアクティブ"),
                () => service.OpenFile(vm, "JSON ファイル (*.json)|*.json"),
                otherActive: true);

        }
        finally
        {
            other?.Close();
            main.Close();
        }

        return rows;
    }

    /// <summary>
    /// ダイアログの矩形が、メインの矩形の内側に収まるか、一部だけ重なるか、重ならないか。
    /// GetWindowRect はサイズ変更できるウィンドウの見えない枠を含むため、DWM の拡張フレームの矩形（見えている枠）で比べる。
    /// </summary>
    private static Loc DescribeOverlap(IntPtr dialog, IntPtr main)
    {
        Native.RECT d = Native.VisibleBounds(dialog);
        Native.RECT m = Native.VisibleBounds(main);
        if (d.Left >= m.Left && d.Top >= m.Top && d.Right <= m.Right && d.Bottom <= m.Bottom)
        {
            return T("entirely inside main", "全体がメインの内側");
        }

        bool intersects = d.Left < m.Right && m.Left < d.Right && d.Top < m.Bottom && m.Top < d.Bottom;
        return intersects ? T("partly overlaps main", "一部がメインと重なる") : T("does not overlap main", "メインと重ならない");
    }

    /// <summary>ダイアログの中心がメインウィンドウの中心と一致するか（2 px 以内）。</summary>
    private static Loc DescribeCentered(IntPtr dialog, IntPtr main)
    {
        Native.GetWindowRect(dialog, out Native.RECT d);
        Native.GetWindowRect(main, out Native.RECT m);
        return Math.Abs(d.CenterX - m.CenterX) <= 2 && Math.Abs(d.CenterY - m.CenterY) <= 2
            ? T("yes", "はい")
            : T("no", "いいえ");
    }

    // ------------------------------------------------------------------
    // 3. ビューモデルが受け取る値
    // ------------------------------------------------------------------

    private static async Task<List<IReadOnlyList<Loc>>> MeasureResultsAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var inner = new DialogService();
        inner.Register<CustomerEditViewModel>(CreateEditWindow);
        inner.Register<SharedCustomerEditViewModel>(CreateEditWindow);

        // 記事の DialogService をそのまま使い、ビューモデルへ返した値だけを横で記録する。
        var service = new RecordingDialogService(inner);
        var vm = new CustomerListViewModel(service);

        Window main = await ShowMainAsync(vm);
        try
        {
            // 確認（MessageBox）。ビューモデルの DeleteCommand から呼ぶ。
            foreach ((int button, Loc name) in new[] { (Native.IDOK, T("OK", "OK")), (Native.IDCANCEL, T("Cancel", "キャンセル")) })
            {
                vm.Customers.Clear();
                vm.Customers.Add(new Customer("Alice"));
                vm.Customers.Add(new Customer("Bob"));
                vm.Selected = vm.Customers[0];
                await WhileModalAsync(() => vm.DeleteCommand.Execute(null), dialog =>
                {
                    Native.PostMessage(dialog, Native.WM_COMMAND, (IntPtr)button, IntPtr.Zero);
                    return Task.CompletedTask;
                }, closeAfterProbe: false);

                rows.Add([
                    T($"DeleteCommand → {name.En}", $"DeleteCommand → {name.Ja}"),
                    T($"Confirm: {service.LastResult}", $"Confirm: {service.LastResult}"),
                    T($"customers: {vm.Customers.Count} of 2", $"顧客 2 件 → {vm.Customers.Count} 件"),
                ]);
            }

            // ファイルの選択（OpenFileDialog）。ビューモデルの ImportCommand から呼ぶ。
            string file = Path.Combine(Path.GetTempPath(), "mvvm-dialog-service-customers.json");
            await File.WriteAllTextAsync(file, "[]");
            try
            {
                await WhileModalAsync(() => vm.ImportCommand.Execute(null), dialog =>
                {
                    Native.PostMessage(dialog, Native.WM_COMMAND, (IntPtr)Native.IDCANCEL, IntPtr.Zero);
                    return Task.CompletedTask;
                }, closeAfterProbe: false);
                rows.Add([
                    T("ImportCommand → Cancel", "ImportCommand → キャンセル"),
                    $"OpenFile: {service.LastResult}",
                    $"ImportPath: {Describe(vm.ImportPath)}",
                ]);

                await WhileModalAsync(() => vm.ImportCommand.Execute(null), async dialog =>
                {
                    // ファイル名の欄（コントロール ID 1148）にパスを入れ、開く（IDOK）を押す。
                    IntPtr edit = Native.FindFileNameEdit(dialog);
                    if (edit == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("ファイルを開くダイアログのファイル名の欄が見つからない。");
                    }

                    Native.SendMessage(edit, Native.WM_SETTEXT, IntPtr.Zero, file);
                    await Task.Delay(100);
                    Native.PostMessage(dialog, Native.WM_COMMAND, (IntPtr)Native.IDOK, IntPtr.Zero);
                }, closeAfterProbe: false);

                bool returnedPath = string.Equals(service.LastResult, $"\"{file}\"", StringComparison.OrdinalIgnoreCase);
                bool storedPath = string.Equals(vm.ImportPath, file, StringComparison.OrdinalIgnoreCase);
                rows.Add([
                    T("ImportCommand → type a file name, Open", "ImportCommand → ファイル名を入れて開く"),
                    returnedPath ? T("OpenFile: the full path of the file", "OpenFile: ファイルの完全なパス") : $"OpenFile: {service.LastResult}",
                    storedPath ? T("ImportPath: the same path", "ImportPath: 同じパス") : $"ImportPath: {Describe(vm.ImportPath)}",
                ]);
            }
            finally
            {
                File.Delete(file);
            }

            // 子ウィンドウ。写しを編集させる記事の実装（CustomerListViewModel.Edit）。
            foreach ((string how, Loc label) in new[]
            {
                ("ok", T("EditCommand → type \"x\", OK", "EditCommand →「x」を入力して OK")),
                ("cancel", T("EditCommand → type \"x\", Cancel", "EditCommand →「x」を入力してキャンセル")),
                ("close", T("EditCommand → type \"x\", × (close)", "EditCommand →「x」を入力して ×")),
            })
            {
                vm.Customers.Clear();
                vm.Customers.Add(new Customer("Alice"));
                vm.Selected = vm.Customers[0];

                await WhileModalAsync(() => vm.EditCommand.Execute(null),
                    dialog => EditAndCloseAsync(dialog, how), closeAfterProbe: false);

                rows.Add([label, $"ShowDialog: {service.LastResult}", $"Customer.Name: \"{vm.Customers[0].Name}\""]);
            }

            // Window.ShowDialog() を × で閉じたときの生の戻り値（bool?）。サービスは == true で bool にするため、ここで別に読む。
            {
                Window window = CreateEditWindow();
                window.Owner = main;
                window.DataContext = new CustomerEditViewModel("Alice");
                bool? raw = true;
                await WhileModalAsync(() => raw = window.ShowDialog(), dialog =>
                {
                    Native.PostMessage(dialog, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return Task.CompletedTask;
                }, closeAfterProbe: false);
                rows.Add([
                    T("Window.ShowDialog() itself → × (close)", "Window.ShowDialog() そのもの → ×"),
                    $"ShowDialog(): {(raw is null ? "null" : raw.Value ? "true" : "false")}",
                    T("DialogResult was never set", "DialogResult は設定していない"),
                ]);
            }

            // 比較: 一覧の Customer そのものを編集させる。
            {
                var customer = new Customer("Alice");
                vm.Customers.Clear();
                vm.Customers.Add(customer);
                await WhileModalAsync(() => service.ShowDialog(vm, new SharedCustomerEditViewModel(customer)),
                    dialog => EditAndCloseAsync(dialog, "cancel"), closeAfterProbe: false);
                rows.Add([
                    T("Customer itself bound → type \"x\", Cancel", "Customer そのものをバインド →「x」を入力してキャンセル"),
                    $"ShowDialog: {service.LastResult}",
                    $"Customer.Name: \"{customer.Name}\"",
                ]);
            }

            // 呼び出したビューモデルを DataContext に持つウィンドウが無い場合。
            // 呼ぶ前後で、表示中のウィンドウと Application.Windows（作っただけのウィンドウも入る）の数を比べる。
            // 比較の "edit-old-order" は、ウィンドウを作ってから所有者を探す順序のサービスで測る。
            var oldOrder = new CreateFirstDialogService();
            oldOrder.Register<CustomerEditViewModel>(CreateEditWindow);
            foreach ((string command, Loc label) in new[]
            {
                ("delete", T("DeleteCommand of a view model that is no window's DataContext", "どのウィンドウの DataContext でもないビューモデルの DeleteCommand")),
                ("edit", T("EditCommand of a view model that is no window's DataContext", "どのウィンドウの DataContext でもないビューモデルの EditCommand")),
                ("edit-old-order", T("Same EditCommand, service that creates the window before finding the owner", "同じ EditCommand・ウィンドウを作ってから所有者を探す順序のサービス")),
            })
            {
                var orphan = new CustomerListViewModel(command == "edit-old-order" ? oldOrder : service);
                var windowsBeforeSet = Application.Current.Windows.OfType<Window>().ToHashSet();
                orphan.Customers.Add(new Customer("Alice"));
                orphan.Selected = orphan.Customers[0];
                uint threadId = Native.GetCurrentThreadId();
                int shownBefore = Native.ThreadWindows(threadId).Count;
                int windowsBefore = Application.Current.Windows.Count;
                string thrown = DemoProbe.Throws(() =>
                {
                    if (command == "delete") orphan.DeleteCommand.Execute(null);
                    else orphan.EditCommand.Execute(null);
                });

                // 残ったウィンドウ（作っただけで表示していないもの）は、後の計測に持ち越さないよう閉じる。
                var leftovers = Application.Current.Windows.OfType<Window>().Where(w => !windowsBeforeSet.Contains(w)).ToList();
                int shownAfter = Native.ThreadWindows(threadId).Count;
                int windowsAfter = Application.Current.Windows.Count;
                foreach (Window leftover in leftovers) leftover.Close();
                rows.Add([
                    label,
                    T($"throws {thrown}", $"例外 {thrown}"),
                    T($"shown windows: {shownBefore} → {shownAfter}, Application.Windows: {windowsBefore} → {windowsAfter}, customers: 1 → {orphan.Customers.Count}",
                      $"表示中のウィンドウ {shownBefore} → {shownAfter}、Application.Windows {windowsBefore} → {windowsAfter}、顧客 1 → {orphan.Customers.Count} 件"),
                ]);
            }
        }
        finally
        {
            main.Close();
        }

        return rows;
    }

    /// <summary>子ウィンドウの TextBox に「x」を打ち、OK・キャンセル・×（WM_CLOSE）のどれかで閉じる。</summary>
    private static async Task EditAndCloseAsync(IntPtr dialog, string how)
    {
        Window window = Application.Current.Windows.OfType<Window>()
            .Single(w => new WindowInteropHelper(w).Handle == dialog);
        var box = DemoProbe.Descendants(window).OfType<TextBox>().Single();
        await DemoProbe.FocusAsync(box);
        box.CaretIndex = box.Text.Length;
        DemoProbe.TypeInto(box, "x");

        switch (how)
        {
            case "ok":
                DemoProbe.Click(DemoProbe.Descendants(window).OfType<Button>().Single(b => (string)b.Content == "OK"));
                break;
            case "cancel":
                DemoProbe.Click(DemoProbe.Descendants(window).OfType<Button>().Single(b => (string)b.Content == "キャンセル"));
                break;
            default:
                Native.PostMessage(dialog, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                break;
        }
    }

    private static string Describe(string? value) => value is null ? "null" : $"\"{value}\"";

    // ------------------------------------------------------------------
    // モーダルのループの中で観測する
    // ------------------------------------------------------------------

    /// <summary>
    /// <paramref name="open"/> でモーダルのダイアログを開き、そのループの中で <paramref name="probe"/> を実行する。
    /// <paramref name="closeAfterProbe"/> が真なら、観測の後に WM_CLOSE で閉じる。偽なら probe が閉じる。
    /// 開いたダイアログを見つけられない、または probe が失敗した場合も、開いた新しいウィンドウを閉じてから例外にする。
    /// </summary>
    private static async Task WhileModalAsync(Action open, Func<IntPtr, Task> probe, bool closeAfterProbe = true)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        uint threadId = Native.GetCurrentThreadId();
        var before = Native.ThreadWindows(threadId).ToHashSet();
        Exception? failure = null;
        bool probed = false;
        bool openReturned = false;

        // 観測の処理は、open() から戻った後に必ず待ち終える。open() が例外で戻った場合も、探し続けた処理が
        // 次の計測のダイアログを自分のものとして扱わないよう、openReturned を見て探すのをやめる。
        Task probeTask = dispatcher.InvokeAsync(async () =>
        {
            IntPtr dialog = IntPtr.Zero;
            try
            {
                for (int i = 0; i < 100 && dialog == IntPtr.Zero && !openReturned; i++)
                {
                    await Task.Delay(50);
                    dialog = Native.ThreadWindows(threadId).FirstOrDefault(h => !before.Contains(h));
                }

                if (dialog == IntPtr.Zero)
                {
                    throw new InvalidOperationException(openReturned
                        ? "ダイアログが見つかる前に、開く処理が戻った。"
                        : "開いたダイアログが 5 秒以内に見つからない。");
                }

                // 表示と配置が落ち着くのを待つ。
                await Task.Delay(400);

                // 新しく表示されたウィンドウがちょうど 1 つで、ダイアログのクラス（Win32 のダイアログか WPF のウィンドウ）であることを確かめる。
                // 補助のウィンドウを取り違えて測らないため。
                var opened = Native.ThreadWindows(threadId).Where(h => !before.Contains(h)).ToList();
                if (opened.Count != 1 || opened[0] != dialog)
                {
                    throw new InvalidOperationException($"新しく表示されたウィンドウが 1 つではない（{string.Join(", ", opened.Select(Native.ClassName))}）。");
                }

                string className = Native.ClassName(dialog);
                if (className != "#32770" && !className.StartsWith("HwndWrapper", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"ダイアログではないウィンドウを見つけた: {className}");
                }

                await probe(dialog);
                probed = true;
                if (closeAfterProbe)
                {
                    Native.PostMessage(dialog, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach (IntPtr window in Native.ThreadWindows(threadId).Where(h => !before.Contains(h)))
                {
                    Native.PostMessage(window, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }, DispatcherPriority.Background).Task.Unwrap();

        try
        {
            open();
        }
        finally
        {
            openReturned = true;
            await probeTask;
        }

        // ループを抜けた後に、閉じたダイアログの後始末が済むのを待つ。
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        if (failure is not null)
        {
            throw new InvalidOperationException("モーダルのダイアログの観測に失敗した。", failure);
        }

        if (!probed)
        {
            throw new InvalidOperationException("観測する前にダイアログが閉じた。");
        }
    }

    private static class Native
    {
        public const int WM_CLOSE = 0x0010;
        public const int WM_SETTEXT = 0x000C;
        public const int WM_COMMAND = 0x0111;
        public const int IDOK = 1;
        public const int IDCANCEL = 2;
        public const uint GW_OWNER = 4;
        public const uint GW_HWNDPREV = 3;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly double CenterX => (Left + Right) / 2.0;

            public readonly double CenterY => (Top + Bottom) / 2.0;
        }

        private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumProc proc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowEnabled(IntPtr hwnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

        /// <summary>DWMWA_EXTENDED_FRAME_BOUNDS の矩形（見えている枠）。取れなければ GetWindowRect。</summary>
        public static RECT VisibleBounds(IntPtr hwnd)
        {
            if (DwmGetWindowAttribute(hwnd, 9, out RECT frame, Marshal.SizeOf<RECT>()) == 0 && frame.Right > frame.Left)
            {
                return frame;
            }

            GetWindowRect(hwnd, out RECT window);
            return window;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);

        [DllImport("user32.dll")]
        private static extern int GetDlgCtrlID(IntPtr hwnd);

        /// <summary>スレッドが持つ、表示中のトップレベルのウィンドウ。</summary>
        public static List<IntPtr> ThreadWindows(uint threadId)
        {
            var windows = new List<IntPtr>();
            EnumThreadWindows(threadId, (h, _) =>
            {
                if (IsWindowVisible(h)) windows.Add(h);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        public static string ClassName(IntPtr hwnd)
        {
            var name = new StringBuilder(256);
            GetClassName(hwnd, name, name.Capacity);
            return name.ToString();
        }

        /// <summary><paramref name="upper"/> が Z オーダーで <paramref name="lower"/> より上にあるか。</summary>
        public static bool IsAbove(IntPtr upper, IntPtr lower)
        {
            for (IntPtr h = GetWindow(lower, GW_HWNDPREV); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDPREV))
            {
                if (h == upper) return true;
            }

            return false;
        }

        /// <summary>ファイルを開くダイアログの、ファイル名の欄（コントロール ID 1148 のコンボボックスの中の Edit）。</summary>
        public static IntPtr FindFileNameEdit(IntPtr dialog)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(dialog, (h, _) =>
            {
                if (ClassName(h) == "Edit" && GetDlgCtrlID(GetParentOrSelf(h)) == 1148)
                {
                    found = h;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hwnd);

        private static IntPtr GetParentOrSelf(IntPtr hwnd)
        {
            // Edit は ComboBox（1148）の子、ComboBox は ComboBoxEx32（1148）の子にある。Edit 自身が 1148 の場合もある。
            if (GetDlgCtrlID(hwnd) == 1148) return hwnd;
            IntPtr parent = GetParent(hwnd);
            return parent;
        }
    }
}
