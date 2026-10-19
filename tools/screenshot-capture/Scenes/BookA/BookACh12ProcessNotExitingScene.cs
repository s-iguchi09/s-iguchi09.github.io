namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 12 章の検証。
///
/// 再現版（監視のスレッドを既定のフォアグラウンドで起動）と修正版（IsBackground = true）を子プロセスで動かし
/// （<see cref="BookAChildProcess"/>）、記事「WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い」の
/// 表の new Thread(...) と + IsBackground = true の行と同じ振る舞いになるかを確かめる。
/// 記事と同じく、監視は 6 秒のスリープ、ウィンドウは起動から 2 秒後に閉じる。所要時間は環境に依存するため、
/// 記事の値そのもの（6.3 秒・2.9 秒）ではなく、「スリープが尽きるまで残る（6 秒以上）」「閉じた直後に終わる（4 秒未満）」で比べる。
/// </summary>
internal sealed class BookACh12ProcessNotExitingScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 12 章の再現版（監視のスレッドを new Thread の既定のまま起動）で、起動から 2 秒後にウィンドウを閉じると Application.Exit が発生して Run が戻るが、プロセスは 6 秒のスリープが尽きるまで残ること（記事の表の new Thread(...) の行と同じ）",
        "付録サンプル第 12 章の修正版（IsBackground = true）で、Application.Exit が発生して Run が戻り、プロセスはウィンドウを閉じた直後に終わること（記事の表の + IsBackground = true の行と同じ）",
    ];

    public string Slug => "book-a-ch12-process-not-exiting";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch12");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>
        {
            new("再現版、監視を始めて 2 秒後にウィンドウを閉じる", "new Thread(...) の行",
                "Exit 発生する、Run 戻る、プロセス: スリープが尽きるまで残る", await RunAsync("ch12-repro")),
            new("修正版、監視を始めて 2 秒後にウィンドウを閉じる", "+ IsBackground = true の行",
                "Exit 発生する、Run 戻る、プロセス: 閉じた直後に終わる", await RunAsync("ch12-fixed")),
        };

        BookAReport.WriteAndAssert(
            "ch12.md", Slug, "第 12 章",
            "WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い",
            "wpf-application-not-exiting-shutdownmode-threads",
            "子プロセスで、起動直後に監視（6 秒のスリープ）を始め、起動から 2 秒後にウィンドウを閉じた。起動から終了までが 6 秒以上なら「スリープが尽きるまで残る」、4 秒未満なら「閉じた直後に終わる」とした（記事の実測は 6.3 秒と 2.9 秒）。",
            checks);
    }

    private static async Task<string> RunAsync(string scenario)
    {
        (Dictionary<string, string> values, TimeSpan elapsed, bool exited) = await BookAChildProcess.RunAsync(scenario, TimeSpan.FromSeconds(20));
        string process = !exited ? "終わらない"
            : elapsed >= TimeSpan.FromSeconds(6) ? "スリープが尽きるまで残る"
            : elapsed < TimeSpan.FromSeconds(4) ? "閉じた直後に終わる"
            : $"判定できない（{elapsed.TotalSeconds:F1} 秒）";
        Console.WriteLine($"{scenario}: {elapsed.TotalSeconds:F1} 秒");
        return $"Exit {values.GetValueOrDefault("exit", "発生しない")}、Run {values.GetValueOrDefault("runReturned", "戻らない")}、プロセス: {process}";
    }
}
