namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 11 章の検証。
///
/// 再現版（同期のコマンドで LoadAsync().Result）と修正版（AsyncRelayCommand で await）を子プロセスで動かし
/// （<see cref="BookAChildProcess"/>）、記事「WPF で Task.Result を呼ぶと画面が固まる原因と、ConfigureAwait(false) で直らない条件」の表の
/// 「LoadAsync().Result」と「async void の Click で await LoadAsync()」の行と一致するかを確かめる。
/// </summary>
internal sealed class BookACh11TaskResultScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 11 章の再現版（同期のコマンドの中で LoadAsync().Result）で、コマンドの実行後に UI スレッドが止まったまま、後から積んだ処理が 2.5 秒経っても Pending のままで、読み込みの結果も出ないこと（記事の表の LoadAsync().Result の行と同じ）",
        "付録サンプル第 11 章の修正版（AsyncRelayCommand の中で await）で、UI スレッドが止まらず、後から積んだ処理が読み込みの終わる前に走り、その後に結果の 42 が出ること（UI スレッドと積んだ処理は記事の表の async void の Click で await LoadAsync() の行と同じ。結果の 42 はサンプルで足した確認）",
    ];

    public string Slug => "book-a-ch11-task-result-deadlock";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch11");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>
        {
            new("再現版、「読み込む」のコマンドを実行", "LoadAsync().Result の行（2.5 秒後の結果は記事の表に無い列で、サンプルで足した確認）",
                "UI スレッド: 止まったまま、積んだ処理: Pending のまま、2.5 秒後の結果: （なし）", await RunAsync("ch11-repro")),
            new("修正版、「読み込む」のコマンドを実行", "async void の Click で await LoadAsync() の行（記事の値は「止まらない（積んだ処理は 6 ms 後に走る）」「待っている間に走る」。結果の 42 はサンプルの値）",
                "UI スレッド: 止まらない、積んだ処理: 待っている間に走る、2.5 秒後の結果: 42", await RunAsync("ch11-fixed")),
        };

        BookAReport.WriteAndAssert(
            "ch11.md", Slug, "第 11 章",
            "WPF で Task.Result を呼ぶと画面が固まる原因と、ConfigureAwait(false) で直らない条件",
            "wpf-task-result-deadlock-synchronizationcontext",
            "子プロセスでコマンドを UI スレッドで実行し、200 ms 後に別の処理を UI スレッドへ積んで、2.5 秒後に状態を読んだ。「待っている間に走る」は、積んだ処理が走った時点でまだ結果が出ていなかったことを示す。記事の表の「内側の await の後」の列は、サンプルに計測用のコードを入れないため比べない。",
            checks);
    }

    private static async Task<string> RunAsync(string scenario)
    {
        (Dictionary<string, string> values, _, bool exited) = await BookAChildProcess.RunAsync(scenario, TimeSpan.FromSeconds(30));
        if (!exited || !values.TryGetValue("marker", out string? marker))
        {
            return "子プロセスが判定を書かなかった";
        }

        string ui;
        string queued;
        if (marker == "Completed")
        {
            ui = "止まらない";
            queued = values["statusWhenMarkerRan"].Length == 0 ? "待っている間に走る" : "結果の後に走る";
        }
        else
        {
            ui = "止まったまま";
            queued = $"{marker} のまま";
        }

        string status = values["status"].Length == 0 ? "（なし）" : values["status"];
        return $"UI スレッド: {ui}、積んだ処理: {queued}、2.5 秒後の結果: {status}";
    }
}
