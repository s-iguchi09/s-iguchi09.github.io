using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ch10.UnhandledExceptionRoutes;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 10 章の検証。
///
/// 再現版（Task.Run を待たない）と修正版（AsyncRelayCommand の中で await し、try / catch で受ける）を表示し、
/// 「取り込む」のコマンドを実行して、DispatcherUnhandledException・AppDomain.UnhandledException・TaskScheduler.UnobservedTaskException
/// が呼ばれたかと、画面のエラー表示を読む。値は記事「WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先」の表と比べる。
/// どちらの経路もプロセスを終わらせないので、このプロセスの中で測る（GC は呼ばない。記事の「GC なし」の行に合わせる）。
/// </summary>
internal sealed class BookACh10UnhandledExceptionScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 10 章の再現版（Task.Run を待たない）で、取り込みの例外は DispatcherUnhandledException・AppDomain.UnhandledException・UnobservedTaskException（GC なし）のどれにも届かず、画面にも出ず、プロセスは動き続けること（記事の表の Task.Run(...) を待たない（GC なし）の行と同じ）",
        "付録サンプル第 10 章の修正版（AsyncRelayCommand の中で await し、try / catch で受ける）で、例外は catch で受けられて画面に理由が出て、DispatcherUnhandledException などには届かず、プロセスは動き続けること（記事の表の「async void の Click で await を try / catch」の行と同じ。投げたスレッドの列は比べない）",
    ];

    public string Slug => "book-a-ch10-unhandled-exception-routes";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch10");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>
        {
            new("再現版、「取り込む」のコマンドを実行", "Task.Run(...) を待たない（GC なし）の行",
                "DispatcherUnhandledException —、AppDomain.UnhandledException —、UnobservedTaskException —、画面: （なし）、プロセス: 動き続ける",
                await RunAsync(new ReproView { DataContext = new ReproImportViewModel() })),
            new("修正版、「取り込む」のコマンドを実行", "async void の Click で await を try / catch の行（記事の値は DispatcherUnhandledException「—（catch で受ける）」。サンプルは Click ではなくコマンドから実行し、投げたスレッドの列は比べない）。画面の文言はサンプルの値",
                $"DispatcherUnhandledException —、AppDomain.UnhandledException —、UnobservedTaskException —、画面: {Importer.FailureMessage}、プロセス: 動き続ける",
                await RunAsync(new FixedView { DataContext = new ImportViewModel() })),
        };

        BookAReport.WriteAndAssert(
            "ch10.md", Slug, "第 10 章",
            "WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先",
            "wpf-unhandled-exception-dispatcher-task-thread",
            "「—」は呼ばれなかったことを示す（記事の表と同じ）。「画面」はビューのエラー表示の文字列。修正版は記事の「catch で受ける」に当たり、例外を受けたことを画面の表示で確かめた。",
            checks);
    }

    internal static async Task<string> RunAsync(UserControl view)
    {
        // 前の回で待たずに残した Task が、この回の計測中に回収されて UnobservedTaskException を出さないよう、
        // ハンドラーを付ける前に回収しきっておく（この回で作る Task には GC を呼ばない）。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int dispatcher = 0;
        int appDomain = 0;
        int unobserved = 0;
        DispatcherUnhandledExceptionEventHandler onDispatcher = (_, e) =>
        {
            dispatcher++;
            e.Handled = true; // 計測ツールを落とさないため。サンプルの経路では呼ばれない想定。
        };
        UnhandledExceptionEventHandler onAppDomain = (_, _) => appDomain++;
        EventHandler<UnobservedTaskExceptionEventArgs> onUnobserved = (_, _) => unobserved++;
        Application.Current.DispatcherUnhandledException += onDispatcher;
        AppDomain.CurrentDomain.UnhandledException += onAppDomain;
        TaskScheduler.UnobservedTaskException += onUnobserved;

        string result = "";
        try
        {
            view.Width = 320;
            await ShowAsync(view, async () =>
            {
                var button = (Button)view.FindName("importButton");
                button.Command.Execute(null);
                // 別スレッドの例外が届くまで待つ（記事の計測と同じく GC は呼ばない）。
                await Task.Delay(1000);
                await Capture.SettleAsync(Window.GetWindow(view)!, 100);
                var error = (TextBlock)view.FindName("errorText");
                result = $"DispatcherUnhandledException {Called(dispatcher)}、AppDomain.UnhandledException {Called(appDomain)}、UnobservedTaskException {Called(unobserved)}、画面: {(error.Text.Length == 0 ? "（なし）" : error.Text)}、プロセス: 動き続ける";
            });
        }
        finally
        {
            Application.Current.DispatcherUnhandledException -= onDispatcher;
            AppDomain.CurrentDomain.UnhandledException -= onAppDomain;
            TaskScheduler.UnobservedTaskException -= onUnobserved;
        }

        return result;
    }

    private static string Called(int count) => count == 0 ? "—" : $"呼ばれる（{count} 回）";
}
