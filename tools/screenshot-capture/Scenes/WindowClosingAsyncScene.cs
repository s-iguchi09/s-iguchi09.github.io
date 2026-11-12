using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の Window.Closing で await すると e.Cancel が効かない理由と、
/// 先に止めて閉じ直す実装」の計測。
///
/// 条件だけを変えた検証用アプリを別プロセスで起動し、× ボタンと同じ WM_CLOSE を送る
/// （条件によっては、代わりに Application.Shutdown を呼ぶ、別のメインウィンドウや Owner を閉じる、
/// 終了ボタンを押す、ShowDialog で開いて DialogResult を設定する）。
/// Closing・Closed・判定（CanCloseAsync）の開始と完了・未処理の例外を、起きた順にログへ書かせて読む。
/// Application.Shutdown を呼ぶとプロセスが終わるため、撮影ツールとは別のプロセスで測る。
/// </summary>
internal sealed class WindowClosingAsyncScene : IScene
{
    /// <summary>これを超えても終わらない検証用アプリは止める。各条件は 1.5 秒で自分から終わる。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public IReadOnlyList<string> Verifies =>
    [
        "Closing を AsyncRelayCommand<CancelEventArgs>（CommunityToolkit.Mvvm 8.4.0）に渡し、await した判定の後で e.Cancel = true にすると、判定が 300 ms かかる場合は判定の完了より前に Closed が発生してウィンドウが閉じ、判定がすぐに完了する（同期で完了する）場合は閉じないこと",
        "記事の ClosingGuard は、判定が false なら Closing 1 回でウィンドウを開いたまま残し、true なら判定の完了後に 2 回目の Closing を経て閉じること。判定が同期で完了しても例外にならず閉じること",
        "先に e.Cancel = true にして判定の後で Close() を呼ぶだけの実装は、判定が true でも閉じず、Closing と判定を繰り返すこと",
        "判定済みのフラグを持たせて Close() を直接呼ぶ実装は、判定に 300 ms かかる場合は閉じ、判定が同期で完了する場合は InvalidOperationException になってウィンドウが開いたまま残ること",
        "判定中に WM_CLOSE をもう一度送ると、判定中かを見ない実装では判定が 2 回始まり、記事の ClosingGuard では 1 回だけであること",
        "記事の ClosingGuard を付けても DataContext に IClosingGuard を設定しなければ、Closing は 1 回発生するが判定は始まらず、ウィンドウが閉じること",
        "記事の ClosingGuard で判定の途中に DataContext を別のビューモデルに替えると、古い判定が true を返してもウィンドウは閉じず、新しいビューモデルの判定も始まらないこと。替わったかを確かめない実装では、古い判定の後に閉じること",
        "記事の ClosingGuard で 1 回目の判定が例外を投げると、DispatcherUnhandledException に届いてウィンドウは開いたまま残り、もう一度 WM_CLOSE を送ると判定をやり直して（2 回目は true）閉じること",
        "記事の ClosingGuard を付けたウィンドウでも、Application.Shutdown を呼ぶと e.Cancel = true が無視されてウィンドウが閉じ、判定は始まるが完了しないままプロセスが終わること",
        "ShowDialog で開いたウィンドウに DialogResult = true を設定して閉じると、閉じ直さなければ ShowDialog が True を返し、Cancel してから Close() で閉じ直すと False を返し、記事の ClosingGuard（判定が true）では DialogResult を設定し直して True を返すこと",
        "ShutdownMode が OnMainWindowClose のときに ClosingGuard の無い別のメインウィンドウを閉じると、ClosingGuard のウィンドウ（判定が false）も閉じ、判定は始まるが完了しないままプロセスが終わること",
        "ClosingGuard のウィンドウ（判定が false）の Owner を閉じると、そのウィンドウのウィンドウプロシージャに WM_CLOSE が届かず、Closing も発生せず、判定も始まらないまま閉じること。ほかの非モーダルの条件では WM_CLOSE が届くこと",
        "記事の ClosingGuard で判定の途中に DataContext を null にしてからもう一度 WM_CLOSE を送っても、2 回目の Closing も止まり、古い判定が true を返してもウィンドウは開いたまま残ること",
        "記事の ClosingGuard で判定が同期で完了し、閉じ直しを予約した後、それより先に DataContext を替えて Close() を呼ぶと、その Close は止まり、予約した閉じ直しも閉じず、新しいビューモデルの判定も始まらないこと",
        "記事の ClosingGuard の閉じ直しを、後から登録したほかの Closing ハンドラーが止めると、ウィンドウは開いたまま残り、もう一度 WM_CLOSE を送ると判定をやり直して（2 回目も true）閉じること",
        "記事の ClosingGuard で判定が同期で完了し、後から登録した Closing ハンドラーが入れ子のメッセージループを回すと、予約した閉じ直しの Close が Closing の処理中に実行されて InvalidOperationException になり（ClosingGuard が捕まえ、未処理の例外にはならない）、ウィンドウは開いたまま残り、もう一度 WM_CLOSE を送ると判定をやり直して閉じること",
        "判定が 300 ms かかり、後から登録した Closing ハンドラーが 500 ms の入れ子のメッセージループを回す場合も、閉じ直しの Close がそのループの中で InvalidOperationException になり、ウィンドウは開いたまま残り、もう一度 WM_CLOSE を送ると判定をやり直して閉じること",
        "ClosingGuard より先に登録した Closing ハンドラーが閉じ直しの Closing で DataContext を替えると、記事の ClosingGuard はその Closing を止めて新しいビューモデルの判定を始め、新しい判定が false ならウィンドウは開いたまま残ること",
        "ClosingGuard より後に登録した Closing ハンドラーが閉じ直しの Closing で DataContext を替えても、記事の ClosingGuard は閉じ直しの前に付け直して最後に呼ばれるので、その Closing を止めて新しいビューモデルの判定を始め、新しい判定が false ならウィンドウは開いたまま残ること",
        "閉じ直しの前に付け直す部分だけを除いた ClosingGuard（記事のコードから機械的に作る）では、後から登録したハンドラーが閉じ直しの Closing で DataContext を替えると、新しいビューモデルの判定は始まらず、ウィンドウが閉じること",
        "表示する前に Close() を呼び、閉じ直しをほかのハンドラーが止めると、記事の ClosingGuard は判定の前の状態に戻り、その後に表示して WM_CLOSE を送ると判定をやり直して閉じること",
        "ShowDialog で開いたウィンドウの判定の途中で Owner を閉じると、ウィンドウの Closed が発生し、記事の ClosingGuard は閉じ直しで何もせず、未処理の例外は起きないこと（このとき ShowDialog は 1.5 秒以内に戻らなかった）。ClosingGuard の無いダイアログで Owner を閉じても、ShowDialog は 1.5 秒以内に戻らないこと",
        "表の各行の値が、条件ごとに書いた期待値と一致すること（一致しなければ、どの表も書き換えない）",
        "終了ボタンに ApplicationCommands.Close を割り当てると、ClosingGuard の判定を通り、判定が false なら閉じず、true なら閉じてプロセスが終わること",
    ];

    public string Slug => "wpf-window-closing-async-cancel";

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>
    /// 条件。<c>Handler</c> は Closing を処理する書き方、<c>Result</c> は判定の結果（allow・deny・throw-once）、
    /// <c>DelayMs</c> は判定にかかる時間（0 は同期で完了する）、<c>Action</c> は閉じ方。
    /// <c>Expected</c> は、表の行（条件の列を除く日本語のセル）を「|」でつないだ期待値。
    /// 「*」で始まるセルは、その後ろの文字列（空にしない）で終わればよい（回数が実行環境で変わるセル）。
    /// 「~repeat」は「判定の開始 / 完了」の列で、開始が 3 回以上、完了が開始と同じか 1 少ないことを表す。
    /// 記事の本文と Verifies はこの期待値を前提に書いているので、食い違えば表を作らずに止める。
    /// </summary>
    private sealed record Condition(string Handler, string Result, int DelayMs, string Action, Loc Label, string Expected);

    private static readonly Condition[] Naive =
    [
        new("naive", "deny", 300, "close", T("Cancel after await (check takes 300 ms)", "await の後で Cancel（判定に 300 ms）"),
            "1|1 / 0|判定の完了より前|なし|閉じてプロセスも終了"),
        new("naive", "deny", 0, "close", T("Cancel after await (check completes synchronously)", "await の後で Cancel（判定が同期で完了）"),
            "1|1 / 1|発生しない|なし|開いたまま"),
        new("guard", "deny", 300, "close", T("ClosingGuard, check returns false", "ClosingGuard・判定が false"),
            "1|1 / 1|発生しない|なし|開いたまま"),
        new("guard", "allow", 300, "close", T("ClosingGuard, check returns true", "ClosingGuard・判定が true"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard-no-datacontext", "deny", 300, "close", T("ClosingGuard, DataContext not set", "ClosingGuard・DataContext を設定しない"),
            "1|0 / 0|発生する（判定は始まらない）|なし|閉じてプロセスも終了"),
    ];

    private static readonly Condition[] Pitfalls =
    [
        new("cancel-first", "allow", 300, "close", T("Cancel first, then Close() (no flag)", "先に Cancel、判定後に Close()（フラグなし）"),
            "*（繰り返す）|~repeat|発生しない|なし|開いたまま"),
        new("direct-close", "allow", 300, "close", T("+ approved flag, Close() directly (300 ms)", "+ 判定済みフラグ、Close() を直接（300 ms）"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
        new("direct-close", "allow", 0, "close", T("+ approved flag, Close() directly (synchronous)", "+ 判定済みフラグ、Close() を直接（同期で完了）"),
            "1|1 / 1|発生しない|InvalidOperationException|開いたまま"),
        new("guard", "allow", 0, "close", T("ClosingGuard: BeginInvoke(Close) (synchronous)", "ClosingGuard: BeginInvoke(Close)（同期で完了）"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
        new("no-checking", "allow", 300, "close-twice", T("× twice, no in-progress check", "× を 2 回、判定中かを見ない"),
            "3|2 / 2|1 回目の判定の完了後・2 回目の完了より前|なし|閉じた"),
        new("guard", "allow", 300, "close-twice", T("× twice, ClosingGuard", "× を 2 回、ClosingGuard"),
            "3|1 / 1|判定の完了後|なし|閉じた"),
        new("guard", "throw-once", 300, "close-retry", T("ClosingGuard: check throws, × again", "ClosingGuard: 判定が例外、もう一度 ×"),
            "3|2 / 2|判定の完了後|IOException|閉じてプロセスも終了"),
        new("guard", "allow", 300, "datacontext-change", T("ClosingGuard: DataContext replaced during the check (true)", "ClosingGuard: 判定中に DataContext を替える（判定が true）"),
            "1|1 / 1|発生しない|なし|開いたまま"),
        new("no-identity-check", "allow", 300, "datacontext-change", T("DataContext replaced during the check, no identity check (true)", "判定中に DataContext を替える・替わったかを確かめない（判定が true）"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard", "allow", 300, "datacontext-null", T("ClosingGuard: DataContext set to null during the check, × again (true)", "ClosingGuard: 判定中に DataContext を null にして、もう一度 ×（判定が true）"),
            "2|1 / 1|発生しない|なし|開いたまま"),
        new("guard", "allow", 0, "close-before-reclose", T("ClosingGuard: DataContext replaced and Close() called before closing again (synchronous)", "ClosingGuard: 閉じ直す前に DataContext を替えて Close()（判定が同期で完了）"),
            "2|1 / 1|発生しない|なし|開いたまま"),
        new("guard", "allow", 300, "other-cancel", T("ClosingGuard: another handler cancels closing again, × again", "ClosingGuard: 閉じ直しをほかのハンドラーが止め、もう一度 ×"),
            "4|2 / 2|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard", "allow", 0, "nested-loop", T("ClosingGuard: a later Closing handler runs a nested message loop, × again (synchronous)", "ClosingGuard: 後のハンドラーが Closing 中に入れ子のメッセージループを回し、もう一度 ×（同期で完了）"),
            "3|2 / 2|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard", "allow", 300, "nested-loop-long", T("ClosingGuard: nested loop (500 ms) longer than the check (300 ms), × again", "ClosingGuard: 入れ子のループ（500 ms）が判定（300 ms）より長い、もう一度 ×"),
            "3|2 / 2|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard-late", "allow", 300, "close", T("ClosingGuard: an earlier Closing handler replaces DataContext while closing again (new check false)", "ClosingGuard: 先に登録したハンドラーが閉じ直しの Closing で DataContext を替える（新しい判定は false）"),
            "2|2 / 2|発生しない|なし|開いたまま"),
        new("guard", "allow", 300, "datacontext-change-later", T("ClosingGuard: a later Closing handler replaces DataContext while closing again (new check false)", "ClosingGuard: 後に登録したハンドラーが閉じ直しの Closing で DataContext を替える（新しい判定は false）"),
            "2|2 / 2|発生しない|なし|開いたまま"),
        new("guard-no-resubscribe", "allow", 300, "datacontext-change-later", T("same as above, without re-subscribing before closing again", "同上・閉じ直しの前に付け直さない"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
        new("guard", "allow", 300, "close-before-show", T("ClosingGuard: Close() before Show, closing again cancelled, then shown and × (true)", "ClosingGuard: 表示前に Close()、閉じ直しが止められ、表示して ×（判定が true）"),
            "4|2 / 2|判定の完了後|なし|閉じてプロセスも終了"),
    ];

    private static readonly Condition[] Shutdown =
    [
        new("guard", "deny", 300, "shutdown", T("Application.Shutdown(), check returns false", "Application.Shutdown()・判定が false"),
            "1|1 / 0|判定の完了より前|なし|閉じてプロセスも終了"),
        new("guard", "deny", 300, "main-close", T("OnMainWindowClose, another main window closed, false", "OnMainWindowClose で別のメインウィンドウを閉じる・false"),
            "1|1 / 0|判定の完了より前|なし|閉じてプロセスも終了"),
        new("guard", "deny", 300, "owner-close", T("Owner window closed, false", "Owner のウィンドウを閉じる・false"),
            "0|0 / 0|発生する（判定は始まらない）|なし|閉じてプロセスも終了"),
        new("guard", "deny", 300, "exit-button", T("exit button (ApplicationCommands.Close), false", "終了ボタン（ApplicationCommands.Close）・false"),
            "1|1 / 1|発生しない|なし|開いたまま"),
        new("guard", "allow", 300, "exit-button", T("exit button (ApplicationCommands.Close), true", "終了ボタン（ApplicationCommands.Close）・true"),
            "2|1 / 1|判定の完了後|なし|閉じてプロセスも終了"),
    ];

    /// <summary>ダイアログの条件。<c>Expected</c> は、表の行（条件の列を除く）を「|」でつないだ期待値。</summary>
    private static readonly Condition[] Dialog =
    [
        new("plain", "allow", 300, "dialog-ok", T("ShowDialog, DialogResult = true (no ClosingGuard)", "ShowDialog・DialogResult = true（ClosingGuard なし）"),
            "1|0 / 0|True"),
        new("direct-close", "allow", 300, "dialog-ok", T("ShowDialog, DialogResult = true, Cancel then Close() again", "ShowDialog・DialogResult = true・Cancel して Close() で閉じ直す"),
            "2|1 / 1|False"),
        new("guard", "allow", 300, "dialog-ok", T("ShowDialog, DialogResult = true, ClosingGuard (true)", "ShowDialog・DialogResult = true・ClosingGuard（判定が true）"),
            "2|1 / 1|True"),
        new("guard", "allow", 300, "dialog-owner-close", T("ShowDialog, DialogResult = true, ClosingGuard, Owner closed during the check", "ShowDialog・DialogResult = true・ClosingGuard・判定中に Owner を閉じる"),
            "1|1 / 1|1.5 秒以内に戻らない"),
        new("plain", "allow", 300, "dialog-owner-close", T("ShowDialog, no ClosingGuard, Owner closed (DialogResult not set)", "ShowDialog・ClosingGuard なし・Owner を閉じる（DialogResult は設定しない）"),
            "0|0 / 0|1.5 秒以内に戻らない"),
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "window-closing-async-scene");
        Directory.CreateDirectory(workspace);
        string executable = await BuildProbeAsync(workspace);

        Loc[] headers =
        [
            T("condition", "条件"),
            T("Closing raised", "Closing の回数"),
            T("checks started / completed", "判定の開始 / 完了"),
            T("Closed raised", "Closed の発生"),
            T("unhandled exception", "未処理の例外"),
            T("window after 1.5 s", "1.5 秒後のウィンドウ"),
        ];

        // すべての条件を測って期待値と照合してから、まとめて保存する（途中で食い違えば、どの表も書き換えない）。
        var naiveRows = await MeasureAsync(executable, workspace, Naive, "closing-cancel-after-await");
        var pitfallRows = await MeasureAsync(executable, workspace, Pitfalls, "closing-close-again");
        var shutdownRows = await MeasureAsync(executable, workspace, Shutdown, "closing-shutdown");

        var dialogRows = new List<IReadOnlyList<Loc>>();
        for (int i = 0; i < Dialog.Length; i++)
        {
            string[] log = await RunAsync(executable, workspace, Dialog[i], $"closing-dialog-{i}");
            if (log.Any(l => l.StartsWith("exception:", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"{Dialog[i]}: 未処理の例外が起きた: {string.Join(", ", log)}");
            }

            // ShowDialog が戻らなかった場合（Owner を閉じた条件で起きた）も、そのまま表に載せる。期待値と照合する。
            string? returned = log.FirstOrDefault(l => l.StartsWith("dialog-result:", StringComparison.Ordinal))?["dialog-result:".Length..];
            Loc result = returned is null ? T("not returned within 1.5 s", "1.5 秒以内に戻らない") : returned;
            IReadOnlyList<Loc> row = [Dialog[i].Label, log.Count(l => l == "closing").ToString(), $"{log.Count(l => l == "guard-start")} / {log.Count(IsCheckEnd)}", result];
            VerifyExpected(Dialog[i], row, log);
            dialogRows.Add(row);
        }

        await context.SaveTableAsync("Cancel set after await vs. ClosingGuard", headers, naiveRows, "closing-cancel-after-await.svg");
        await context.SaveTableAsync("Closing again after the check", headers, pitfallRows, "closing-close-again.svg");
        await context.SaveTableAsync("Application.Shutdown ignores Cancel", headers, shutdownRows, "closing-shutdown.svg");
        await context.SaveTableAsync(
            "DialogResult after ClosingGuard cancels once",
            [T("condition", "条件"), T("Closing raised", "Closing の回数"), T("checks started / completed", "判定の開始 / 完了"), T("ShowDialog() result", "ShowDialog() の結果")],
            dialogRows,
            "closing-dialog-result.svg");
    }

    /// <summary>条件を順に測り、期待値と照合した表の行を返す。</summary>
    private static async Task<List<IReadOnlyList<Loc>>> MeasureAsync(string executable, string workspace, Condition[] conditions, string name)
    {
        var rows = new List<IReadOnlyList<Loc>>();
        for (int i = 0; i < conditions.Length; i++)
        {
            Condition condition = conditions[i];
            string[] log = await RunAsync(executable, workspace, condition, $"{name}-{i}");
            IReadOnlyList<Loc> row = Describe(condition, log);

            // 入れ子のメッセージループの条件では、閉じ直しの Close が例外になったこと（ClosingGuard が捕まえた）を確かめる。
            if (condition.Action is "nested-loop" or "nested-loop-long" && !ThrewOnceBeforeRetry(log))
            {
                throw new InvalidOperationException($"{condition.Label.Ja}: 閉じ直しの Close が例外になっていない: {string.Join(", ", log)}");
            }

            VerifyExpected(condition, row, log);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>表の行が、条件に書いた期待値と一致するかを確かめる。一致しなければ例外にして、表を作らない。</summary>
    private static void VerifyExpected(Condition condition, IReadOnlyList<Loc> row, string[] log)
    {
        string[] expected = condition.Expected.Split('|');
        string[] actual = row.Skip(1).Select(cell => cell.Ja).ToArray();
        bool matches = expected.Length == actual.Length && expected.Zip(actual).All(pair => CellMatches(pair.First, pair.Second));
        if (!matches)
        {
            throw new InvalidOperationException(
                $"{condition.Label.Ja}: 期待値と違う。期待: {condition.Expected} / 実際: {string.Join("|", actual)} / ログ: {string.Join(", ", log)}");
        }
    }

    /// <summary>
    /// 入れ子のメッセージループの条件で、閉じ直しの Close の例外が、1 回目の判定の完了から 2 回目の WM_CLOSE までの間に
    /// ちょうど 1 回だけ起きたか。プロセスのほかの場所の例外を、閉じ直しの例外と取り違えないために位置で絞る。
    /// </summary>
    private static bool ThrewOnceBeforeRetry(string[] log)
    {
        int firstEnd = Array.FindIndex(log, IsCheckEnd);
        int secondClose = Array.FindIndex(log, firstEnd + 1, l => l == "wm-close");
        return firstEnd >= 0 && secondClose > firstEnd
            && log[firstEnd..secondClose].Count(l => l == "first-chance:InvalidOperationException") == 1
            && log.Count(l => l == "first-chance:InvalidOperationException") == 1;
    }

    /// <summary>期待値の 1 セルと、表の 1 セルを照合する。</summary>
    private static bool CellMatches(string expected, string actual)
    {
        if (expected == "~repeat")
        {
            string[] counts = actual.Split(" / ");
            return counts.Length == 2 && int.TryParse(counts[0], out int started) && int.TryParse(counts[1], out int completed)
                && started >= 3 && (completed == started || completed == started - 1);
        }

        if (expected.StartsWith('*'))
        {
            if (expected.Length == 1)
            {
                throw new InvalidOperationException("期待値の「*」には、後ろに照合する文字列を書く。");
            }

            return actual.EndsWith(expected[1..], StringComparison.Ordinal);
        }

        return expected == actual;
    }

    /// <summary>判定が終わったことを示すログか。結果を返した場合と、例外を投げた場合の両方を数える。</summary>
    private static bool IsCheckEnd(string line) => line is "guard-end" or "guard-threw";

    /// <summary>ログ 1 本を表の 1 行にする。想定と食い違う組み合わせは例外にして、表に載せない。</summary>
    private static IReadOnlyList<Loc> Describe(Condition condition, string[] log)
    {
        int closing = log.Count(l => l == "closing");
        int started = log.Count(l => l == "guard-start");
        int completed = log.Count(IsCheckEnd);
        int closedIndex = Array.IndexOf(log, "closed");
        string[] exceptions = log.Where(l => l.StartsWith("exception:", StringComparison.Ordinal)).Select(l => l["exception:".Length..]).ToArray();
        bool runReturned = log.Contains("run-returned");
        bool finalOpen = log.Contains("final:open");

        // Closing が 0 回でよいのは、Closing を通らずに閉じた場合（Owner を閉じた条件）だけである。
        if ((closing == 0 && (closedIndex < 0 || condition.Action != "owner-close")) || (closedIndex < 0) != finalOpen
            || (condition.Action == "owner-close") == log.Contains("wm-close"))
        {
            throw new InvalidOperationException($"{condition}: ログが想定と合わない: {string.Join(", ", log)}");
        }

        Loc closed = DescribeClosed(log, closedIndex, started, completed);

        Loc window = finalOpen
            ? T("open", "開いたまま")
            : runReturned ? T("closed, process ended", "閉じてプロセスも終了") : T("closed", "閉じた");

        return
        [
            condition.Label,
            // 閉じないまま Closing が 3 回以上続いたら、閉じ直しが繰り返されている。回数自体は判定の時間とタイマーの精度で変わる。
            closedIndex < 0 && closing >= 3 ? T($"{closing} (keeps repeating)", $"{closing}（繰り返す）") : closing.ToString(),
            $"{started} / {completed}",
            closed,
            exceptions.Length == 0 ? T("none", "なし") : string.Join(", ", exceptions),
            window,
        ];
    }

    /// <summary>Closed が、判定の完了に対していつ発生したか。</summary>
    private static Loc DescribeClosed(string[] log, int closedIndex, int started, int completed)
    {
        if (closedIndex < 0)
        {
            return T("not raised", "発生しない");
        }

        if (started == 0)
        {
            return T("raised (no check started)", "発生する（判定は始まらない）");
        }

        // 判定がいくつ完了した後に Closed が発生したか。
        int completedBefore = log.Take(closedIndex).Count(IsCheckEnd);
        if (completedBefore == completed && completed == started)
        {
            return T("after the check completes", "判定の完了後");
        }

        return completedBefore == 0
            ? T("before the check completes", "判定の完了より前")
            : T($"after check {completedBefore} completes, before check {completedBefore + 1} completes", $"{completedBefore} 回目の判定の完了後・{completedBefore + 1} 回目の完了より前");
    }

    /// <summary>検証用アプリを 1 条件ぶん実行し、ログの行（時刻を除いた見出し）を返す。</summary>
    private static async Task<string[]> RunAsync(string executable, string workspace, Condition condition, string name)
    {
        string log = Path.Combine(workspace, $"log-{name}.txt");
        // 前回の実行のログを読まないよう、起動前に消す。
        File.Delete(log);
        string arguments = $"{condition.Handler} {condition.Result} {condition.DelayMs} {condition.Action} \"{log}\"";

        using var process = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("検証用アプリを起動できない。");

        using var cancellation = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidOperationException($"{arguments}: 検証用アプリが終わらない。");
        }

        string content = File.Exists(log) ? await File.ReadAllTextAsync(log) : string.Empty;
        Console.WriteLine($"{arguments}:\n{content}");

        // 検証用アプリは、Run() が戻るか Environment.Exit(0) で終わる。途中で落ちた場合のログは表に載せない。
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{arguments}: 検証用アプリが終了コード {process.ExitCode} で終わった:\n{content}");
        }

        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('\t')[0]).ToArray();
    }

    /// <summary>
    /// 記事の ClosingGuard から、閉じ直しの前にハンドラーを付け直す部分だけを除いた変種（ClosingGuardNoResubscribe）を作る。
    /// 手で写すと記事のコードとずれるので、検証用アプリのソースから機械的に作る。
    /// </summary>
    private static string WithoutResubscribe(string source)
    {
        // 検証用アプリのソースは行頭の字下げが外れた形で、ファイル スコープの名前空間（ClosingProbe）にある。
        string text = source.Replace("\r\n", "\n");
        int a = text.IndexOf("\npublic static class ClosingGuard\n", StringComparison.Ordinal);
        int b = a < 0 ? -1 : text.IndexOf("\n#nullable restore", a, StringComparison.Ordinal);
        if (a < 0 || b < 0)
        {
            throw new InvalidOperationException("検証用アプリのソースに、記事の ClosingGuard が見つからない。");
        }

        string guard = text[a..b];
        const string resubscribe =
            "            if (GetIsEnabled(window))\n" +
            "            {\n" +
            "                window.Closing -= OnClosing;\n" +
            "                window.Closing += OnClosing;\n" +
            "            }\n";
        if (!guard.Contains(resubscribe, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("記事の ClosingGuard に、付け直しの部分が見つからない。");
        }

        guard = guard.Replace(resubscribe, "").Replace("class ClosingGuard", "class ClosingGuardNoResubscribe").Replace("typeof(ClosingGuard)", "typeof(ClosingGuardNoResubscribe)");
        return "\n#nullable enable\n" + guard + "\n#nullable restore\n";
    }

    private static async Task<string> BuildProbeAsync(string workspace)
    {
        await File.WriteAllTextAsync(Path.Combine(workspace, "probe.csproj"), ProjectFile, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(workspace, "Program.cs"), ProbeSource + WithoutResubscribe(ProbeSource), new UTF8Encoding(false));

        using var build = Process.Start(new ProcessStartInfo("dotnet", "build -c Release -v q --nologo")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("dotnet build を起動できない。");

        Task<string> stdout = build.StandardOutput.ReadToEndAsync();
        Task<string> stderr = build.StandardError.ReadToEndAsync();
        await build.WaitForExitAsync();

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException("検証用アプリのビルドに失敗した:\n" + await stdout + await stderr);
        }

        return Path.Combine(workspace, "bin", "Release", "net10.0-windows", "ClosingProbe.exe");
    }

    private const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWPF>true</UseWPF>
            <Nullable>disable</Nullable>
            <LangVersion>latest</LangVersion>
            <AssemblyName>ClosingProbe</AssemblyName>
            <RootNamespace>ClosingProbe</RootNamespace>
            <StartupObject>ClosingProbe.Program</StartupObject>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup><Compile Include="Program.cs" /></ItemGroup>
          <ItemGroup><PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" /></ItemGroup>
        </Project>
        """;

    /// <summary>
    /// 検証用アプリの本体。<c>ClosingGuard</c> と <c>IClosingGuard</c> は記事の実装例と同じコードである。
    /// 判定（CanCloseAsync）は、保存や確認ダイアログの代わりに指定した時間だけ待って、指定した結果を返す。
    /// </summary>
    private const string ProbeSource = """
        using System;
        using System.ComponentModel;
        using System.Diagnostics;
        using System.IO;
        using System.Runtime.InteropServices;
        using System.Threading.Tasks;
        using System.Windows;
        using System.Windows.Automation.Peers;
        using System.Windows.Automation.Provider;
        using System.Windows.Controls;
        using System.Windows.Input;
        using System.Windows.Interop;
        using System.Windows.Markup;
        using System.Windows.Threading;

        namespace ClosingProbe;

        internal static class Log
        {
            public static string FilePath;
            private static readonly Stopwatch Clock = Stopwatch.StartNew();

            public static void Write(string label)
                => File.AppendAllText(FilePath, $"{label}\t{Clock.Elapsed.TotalMilliseconds:F0}\n");
        }

        // ---- 記事の実装例（ここから） ----
        // 記事のコードは、WPF のプロジェクトテンプレートの既定（Nullable 有効）で警告が出ない形にしている。
        #nullable enable

        public interface IClosingGuard
        {
            // 閉じてよければ true を返す。保存や確認ダイアログはここで行う。
            Task<bool> CanCloseAsync();
        }

        public static class ClosingGuard
        {
            public static readonly DependencyProperty IsEnabledProperty =
                DependencyProperty.RegisterAttached(
                    "IsEnabled", typeof(bool), typeof(ClosingGuard),
                    new PropertyMetadata(false, OnIsEnabledChanged));

            public static bool GetIsEnabled(Window window) => (bool)window.GetValue(IsEnabledProperty);

            public static void SetIsEnabled(Window window, bool value) => window.SetValue(IsEnabledProperty, value);

            // ウィンドウごとの状態。判定中か、判定を終えて閉じてよいと決まったか。
            private enum State { Idle, Checking, Approved }

            private static readonly DependencyProperty StateProperty =
                DependencyProperty.RegisterAttached(
                    "State", typeof(State), typeof(ClosingGuard),
                    new PropertyMetadata(State.Idle));

            // 閉じてよいと判定したビューモデル。Approved のときに、DataContext がまだこれかを確かめる。
            private static readonly DependencyProperty ApprovedGuardProperty =
                DependencyProperty.RegisterAttached(
                    "ApprovedGuard", typeof(IClosingGuard), typeof(ClosingGuard),
                    new PropertyMetadata(null));

            // Closed が発生したか。ウィンドウが本当に閉じたかは、HWND の有無ではなくこれで判定する
            // （表示する前に Close を呼んだウィンドウには、閉じていなくても HWND が無い）。
            private static readonly DependencyProperty IsClosedProperty =
                DependencyProperty.RegisterAttached(
                    "IsClosed", typeof(bool), typeof(ClosingGuard),
                    new PropertyMetadata(false));

            private static readonly CommandBinding CloseBinding =
                new(ApplicationCommands.Close, (sender, _) => ((Window)sender).Close());

            private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var window = (Window)d;
                window.Closing -= OnClosing;
                window.CommandBindings.Remove(CloseBinding);

                // Closed は一度しか起きないので、無効にした後も購読したままにして、閉じたかを覚え続ける。
                window.Closed -= OnClosed;
                window.Closed += OnClosed;

                if ((bool)e.NewValue)
                {
                    window.Closing += OnClosing;
                    // 終了ボタンなどの Command="Close" も、× と同じ Close の経路に通す。
                    window.CommandBindings.Add(CloseBinding);
                }
            }

            private static void OnClosed(object? sender, EventArgs e) => ((Window)sender!).SetValue(IsClosedProperty, true);

            private static async void OnClosing(object? sender, CancelEventArgs e)
            {
                var window = (Window)sender!;
                var state = (State)window.GetValue(StateProperty);

                if (state == State.Approved && ReferenceEquals(window.DataContext, window.GetValue(ApprovedGuardProperty)))
                {
                    return; // 判定を終えた 2 回目の Close は、そのまま閉じる（その間に DataContext が替わったら判定し直す）
                }

                if (state == State.Checking)
                {
                    e.Cancel = true; // 判定中にもう一度押された × は、DataContext によらず止める
                    return;
                }

                if (window.DataContext is not IClosingGuard guard)
                {
                    return; // 判定する相手がいなければ、そのまま閉じる
                }

                e.Cancel = true; // 最初の await より前に止める

                // Cancel すると WPF が DialogResult を null に戻すので、閉じ直すときのために控えておく。
                bool? dialogResult = window.DialogResult;
                window.SetValue(StateProperty, State.Checking);
                bool canClose = false;
                try
                {
                    // 判定の間に DataContext が別のビューモデルに替わったら、古い判定の結果では閉じない。
                    canClose = await guard.CanCloseAsync() && ReferenceEquals(window.DataContext, guard);
                }
                finally
                {
                    if (!canClose)
                    {
                        window.SetValue(StateProperty, State.Idle);
                    }
                }

                if (!canClose)
                {
                    return;
                }

                // Closing の処理中に Close を呼ぶと例外になるため、閉じ直しを後回しにする
                // （入れ子のメッセージループがあると Closing の途中で実行されることがある。下の catch）。
                // 閉じ直すまでは Checking のままにして、その間に届いたほかの Close も止める。
                // BeginInvoke にするのは、ここで捕まえない例外を DispatcherUnhandledException に届けるためである。
                _ = window.Dispatcher.BeginInvoke(() =>
                {
                    if ((bool)window.GetValue(IsClosedProperty))
                    {
                        return; // 判定の間にほかの経路（Owner の終了など）で閉じていれば、何もしない
                    }

                    if (!ReferenceEquals(window.DataContext, guard))
                    {
                        window.SetValue(StateProperty, State.Idle); // 閉じ直す前に替わった場合も閉じない
                        return;
                    }

                    window.SetValue(ApprovedGuardProperty, guard);
                    window.SetValue(StateProperty, State.Approved);

                    // 付け直して最後に呼ばれるようにし、ほかのハンドラーが閉じ直しの Closing で DataContext を替えても確かめられるようにする。
                    if (GetIsEnabled(window))
                    {
                        window.Closing -= OnClosing;
                        window.Closing += OnClosing;
                    }
                    try
                    {
                        if (dialogResult is bool result)
                        {
                            window.DialogResult = result; // ShowDialog の戻り値を戻す（設定すると閉じる）
                        }
                        else
                        {
                            window.Close();
                        }
                    }
                    catch (InvalidOperationException) when (!(bool)window.GetValue(IsClosedProperty))
                    {
                        // ほかの Closing ハンドラーが入れ子のメッセージループ（MessageBox など）を回すと、
                        // Closing の処理中にここが実行され、Close が例外になる。閉じずに、次の × で判定し直す。
                        // ウィンドウが閉じた後の例外は捕まえない。
                    }
                    finally
                    {
                        // ほかのハンドラーが閉じるのを止めた場合など、ウィンドウが残ったら判定の前の状態に戻す。
                        // 閉じ直しの Closing で新しい判定が始まっていれば（State が Checking なら）、そのままにする。
                        if (!(bool)window.GetValue(IsClosedProperty) && (State)window.GetValue(StateProperty) == State.Approved)
                        {
                            window.SetValue(StateProperty, State.Idle);
                        }
                    }
                });
            }
        }

        #nullable restore
        // ---- 記事の実装例（ここまで） ----

        // よく見る書き方: Closing を ICommand に渡す添付プロパティ。記事の「現象」の節のコードと同じ。
        #nullable enable
        public static class ClosingEvent
        {
            public static readonly DependencyProperty CommandProperty =
                DependencyProperty.RegisterAttached(
                    "Command", typeof(ICommand), typeof(ClosingEvent),
                    new PropertyMetadata(null, OnCommandChanged));

            public static ICommand? GetCommand(Window window) => (ICommand?)window.GetValue(CommandProperty);

            public static void SetCommand(Window window, ICommand? value) => window.SetValue(CommandProperty, value);

            private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var window = (Window)d;
                window.Closing -= OnClosing;
                if (e.NewValue is not null)
                {
                    window.Closing += OnClosing;
                }
            }

            private static void OnClosing(object? sender, CancelEventArgs e) => GetCommand((Window)sender!)?.Execute(e);
        }
        #nullable restore

        public sealed class EditorViewModel : IClosingGuard
        {
            public bool Allow { get; init; }

            public int DelayMs { get; init; }

            // よく見る書き方: 判定を await した後で Cancel を立てる。
            public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<CancelEventArgs> ClosingCommand { get; }

            public EditorViewModel()
            {
                ClosingCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<CancelEventArgs>(async e =>
                {
                    if (!await CanCloseAsync())
                    {
                        e!.Cancel = true;
                    }
                });
            }

            // 真なら、1 回目の判定は例外を投げ、2 回目からは Allow を返す。
            public bool ThrowOnce { get; init; }

            private int _calls;

            // 保存や確認ダイアログの代わり。DelayMs が 0 なら待たずに（同期で）完了する。
            public async Task<bool> CanCloseAsync()
            {
                Log.Write("guard-start");
                _calls++;
                if (DelayMs > 0)
                {
                    await Task.Delay(DelayMs);
                }

                if (ThrowOnce && _calls == 1)
                {
                    Log.Write("guard-threw");
                    throw new IOException("save failed");
                }

                Log.Write("guard-end");
                return Allow;
            }
        }

        // 記事の「経路から導ける帰結」で比べる、閉じ直し方の違う実装。
        internal static class Variants
        {
            private static bool _approved;
            private static bool _checking;

            // ClosingGuard から、判定の後に DataContext が替わっていないかの確認だけを除いたもの。
            public static async void NoIdentityCheck(object sender, CancelEventArgs e)
            {
                var window = (Window)sender;
                if (_approved)
                {
                    return;
                }

                if (_checking)
                {
                    e.Cancel = true;
                    return;
                }

                if (window.DataContext is not IClosingGuard guard)
                {
                    return;
                }

                e.Cancel = true;
                _checking = true;
                bool canClose;
                try
                {
                    canClose = await guard.CanCloseAsync();
                }
                finally
                {
                    _checking = false;
                }

                if (canClose)
                {
                    _approved = true;
                    _ = window.Dispatcher.BeginInvoke(window.Close);
                }
            }

            // 先に Cancel して、判定の後で Close を呼ぶだけ。2 回目の Closing を区別しない。
            public static async void CancelFirst(object sender, CancelEventArgs e)
            {
                var window = (Window)sender;
                e.Cancel = true;
                if (await ((IClosingGuard)window.DataContext).CanCloseAsync())
                {
                    window.Close();
                }
            }

            // 判定済みのフラグはあるが、Close を直接呼ぶ。
            public static async void DirectClose(object sender, CancelEventArgs e)
            {
                if (_approved)
                {
                    return;
                }

                var window = (Window)sender;
                e.Cancel = true;
                if (await ((IClosingGuard)window.DataContext).CanCloseAsync())
                {
                    _approved = true;
                    window.Close();
                }
            }

            // 判定済みのフラグと BeginInvoke はあるが、判定中かを見ない。
            public static async void NoCheckingGuard(object sender, CancelEventArgs e)
            {
                if (_approved)
                {
                    return;
                }

                var window = (Window)sender;
                e.Cancel = true;
                bool canClose = await ((IClosingGuard)window.DataContext).CanCloseAsync();
                if (canClose)
                {
                    _approved = true;
                    _ = window.Dispatcher.BeginInvoke(window.Close);
                }
            }
        }

        internal static class Program
        {
            [DllImport("user32.dll")]
            private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            private const int WM_CLOSE = 0x0010;

            private const string GuardXaml = @"
        <Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                xmlns:local=""clr-namespace:ClosingProbe;assembly=ClosingProbe""
                Title=""Editor"" Width=""320"" Height=""200""
                local:ClosingGuard.IsEnabled=""True"">
          <DockPanel>
            <Button x:Name=""ExitButton"" DockPanel.Dock=""Bottom"" Content=""Exit"" Command=""Close"" />
            <TextBox />
          </DockPanel>
        </Window>";

            private const string NaiveXaml = @"
        <Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:local=""clr-namespace:ClosingProbe;assembly=ClosingProbe""
                Title=""Editor"" Width=""320"" Height=""200""
                local:ClosingEvent.Command=""{Binding ClosingCommand}"">
          <TextBox />
        </Window>";

            private const string PlainXaml = @"
        <Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                Title=""Editor"" Width=""320"" Height=""200"">
          <TextBox />
        </Window>";

            [STAThread]
            private static int Main(string[] args)
            {
                string handler = args[0];
                bool allow = args[1] != "deny";
                bool throwOnce = args[1] == "throw-once";
                int delay = int.Parse(args[2]);
                string action = args[3];
                Log.FilePath = args[4];
                File.WriteAllText(Log.FilePath, "");

                // × を 2 回押す条件では、2 回目の判定が完了するかを読むため、閉じてもプロセスを終わらせない。
                // 既定の OnLastWindowClose では、2 回目の判定の完了とプロセスの終了のどちらが先かが回によって変わる。
                // ダイアログの条件では、ダイアログが閉じた時点でアプリが終わり、ShowDialog の戻り値を書く前に終わるのを避ける。
                var app = new Application
                {
                    ShutdownMode = action switch
                    {
                        "close-twice" or "dialog-ok" or "dialog-owner-close" => ShutdownMode.OnExplicitShutdown,
                        "main-close" => ShutdownMode.OnMainWindowClose,
                        _ => ShutdownMode.OnLastWindowClose,
                    },
                };
                AppDomain.CurrentDomain.FirstChanceException += (_, e) => Log.Write("first-chance:" + e.Exception.GetType().Name);
                app.DispatcherUnhandledException += (_, e) =>
                {
                    Log.Write("exception:" + e.Exception.GetType().Name);
                    e.Handled = true;
                };

                var window = (Window)XamlReader.Parse(handler switch
                {
                    "guard" or "guard-no-datacontext" => GuardXaml,
                    "naive" => NaiveXaml,
                    _ => PlainXaml,
                });

                if (handler == "guard-no-resubscribe")
                {
                    ClosingGuardNoResubscribe.SetIsEnabled(window, true);
                }

                if (handler == "guard-late")
                {
                    // ClosingGuard より先に登録したハンドラーが、2 回目の Closing（閉じ直し）で DataContext を替える。
                    // 新しいビューモデルの判定は false を返す。
                    int seen = 0;
                    window.Closing += (_, _) =>
                    {
                        if (++seen == 2)
                        {
                            window.DataContext = new EditorViewModel { Allow = false, DelayMs = delay };
                        }
                    };
                    ClosingGuard.SetIsEnabled(window, true);
                }

                switch (handler)
                {
                    case "cancel-first": window.Closing += Variants.CancelFirst; break;
                    case "direct-close": window.Closing += Variants.DirectClose; break;
                    case "no-checking": window.Closing += Variants.NoCheckingGuard; break;
                    case "no-identity-check": window.Closing += Variants.NoIdentityCheck; break;
                }

                bool closed = false;
                window.Closing += (_, _) => Log.Write("closing");
                window.Closed += (_, _) => { closed = true; Log.Write("closed"); };
                if (handler != "guard-no-datacontext")
                {
                    window.DataContext = new EditorViewModel { Allow = allow, DelayMs = delay, ThrowOnce = throwOnce };
                }
                // メインウィンドウ・Owner の条件では、ClosingGuard の無い別のウィンドウを先に開き、それを閉じる。
                Window other = null;
                if (action is "main-close" or "owner-close" or "dialog-owner-close")
                {
                    other = new Window { Title = "other", Width = 200, Height = 150, Left = 40, Top = 40 };
                    other.Show();
                    if (action == "main-close")
                    {
                        app.MainWindow = other;
                    }
                    else
                    {
                        window.Owner = other;
                    }
                }

                if (action is "dialog-ok" or "dialog-owner-close")
                {
                    // ShowDialog で開き、OK ボタンの処理と同じく DialogResult = true にして閉じる。
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        if (!(action == "dialog-owner-close" && handler == "plain"))
                        {
                            After(200, () => window.DialogResult = true);
                        }

                        if (action == "dialog-owner-close")
                        {
                            After(350, () => other.Close()); // 判定（300 ms）の途中で Owner を閉じる
                        }

                        bool? result = window.ShowDialog();
                        Log.Write("dialog-result:" + (result?.ToString() ?? "null"));
                    });
                }
                else if (action != "close-before-show")
                {
                    window.Show();
                }

                // ウィンドウプロシージャに WM_CLOSE が届いたかを記録する（Owner を閉じた条件で、届かないことを確かめる）。
                IntPtr Hook()
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    HwndSource.FromHwnd(handle).AddHook((IntPtr _, int msg, IntPtr _, IntPtr _, ref bool _) =>
                    {
                        if (msg == WM_CLOSE)
                        {
                            Log.Write("wm-close");
                        }

                        return IntPtr.Zero;
                    });
                    return handle;
                }

                // ダイアログの条件と表示前に閉じる条件では、まだハンドルが無い（WM_CLOSE もこの時点では送らない）。
                IntPtr hwnd = action is "dialog-ok" or "dialog-owner-close" or "close-before-show" ? IntPtr.Zero : Hook();

                After(200, () =>
                {
                    switch (action)
                    {
                        case "close":
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            break;
                        case "close-twice":
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(100, () => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
                            break;
                        case "datacontext-change":
                            // 判定（300 ms）の途中で、別の編集対象のビューモデルに替える。
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(100, () => window.DataContext = new EditorViewModel { Allow = allow, DelayMs = delay });
                            break;
                        case "datacontext-null":
                            // 判定（300 ms）の途中で DataContext を null にし、その後にもう一度 × を押す。
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(100, () => window.DataContext = null);
                            After(150, () => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
                            break;
                        case "close-before-reclose":
                            // 判定は同期で完了し、閉じ直しは BeginInvoke（Normal）で予約される。
                            // それより先に（Send で）DataContext を替えて Close を呼ぶ。
                            bool armed = true;
                            window.Closing += (_, _) =>
                            {
                                if (!armed)
                                {
                                    return;
                                }

                                armed = false;
                                window.Dispatcher.InvokeAsync(() =>
                                {
                                    window.DataContext = new EditorViewModel { Allow = allow, DelayMs = delay };
                                    window.Close();
                                }, DispatcherPriority.Send);
                            };
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            break;
                        case "other-cancel":
                            // ClosingGuard の後に登録したハンドラーが、2 回目の Closing（閉じ直し）だけを止める。
                            int closings = 0;
                            window.Closing += (_, e) =>
                            {
                                if (++closings == 2)
                                {
                                    e.Cancel = true;
                                }
                            };
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(700, () => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
                            break;
                        case "nested-loop":
                        case "nested-loop-long":
                            // ClosingGuard の後に登録したハンドラーが、1 回目の Closing の中で入れ子のメッセージループを
                            // 100 ms 回す（MessageBox.Show を開いている間と同じく、予約済みの処理が実行される）。
                            bool pushed = false;
                            window.Closing += (_, _) =>
                            {
                                if (pushed)
                                {
                                    return;
                                }

                                pushed = true;
                                var frame = new DispatcherFrame();
                                // nested-loop-long は、判定（300 ms）より長く回す。
                                var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(action == "nested-loop" ? 100 : 500) };
                                stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
                                stop.Start();
                                Dispatcher.PushFrame(frame);
                            };
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(700, () => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
                            break;
                        case "datacontext-change-later":
                            // ClosingGuard より後に登録したハンドラーが、2 回目の Closing（閉じ直し）で DataContext を替える。
                            // 1 回目の判定は true、新しいビューモデルの判定は false を返す。
                            int later = 0;
                            window.Closing += (_, _) =>
                            {
                                if (++later == 2)
                                {
                                    window.DataContext = new EditorViewModel { Allow = false, DelayMs = delay };
                                }
                            };
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            break;
                        case "close-before-show":
                            // 表示する前に Close を呼ぶ。閉じ直し（2 回目の Closing）はほかのハンドラーが止める。
                            // その後に表示して、もう一度 × を押す。
                            int beforeShow = 0;
                            window.Closing += (_, e) =>
                            {
                                if (++beforeShow == 2)
                                {
                                    e.Cancel = true;
                                }
                            };
                            window.Close();
                            After(500, () =>
                            {
                                window.Show();
                                PostMessage(Hook(), WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            });
                            break;
                        case "close-retry":
                            // 1 回目の判定（300 ms で例外）が終わった後に、もう一度 × を押す。
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            After(600, () => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
                            break;
                        case "shutdown":
                            app.Shutdown();
                            break;
                        case "main-close":
                        case "owner-close":
                            other.Close();
                            break;
                        case "exit-button":
                            var button = (Button)window.FindName("ExitButton");
                            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
                            break;
                    }
                });

                After(1500, () =>
                {
                    Log.Write(closed ? "final:closed" : "final:open");
                    Environment.Exit(0);
                });

                int code = app.Run();
                Log.Write("run-returned");
                return code;
            }

            private static void After(int milliseconds, Action action)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
                timer.Tick += (_, _) => { timer.Stop(); action(); };
                timer.Start();
            }
        }
        """;
}
