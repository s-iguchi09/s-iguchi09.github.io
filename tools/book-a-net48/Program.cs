using System.Windows;
using ScreenshotCapture.Scenes.BookA;

namespace ScreenshotCapture;

/// <summary>
/// 本の付録サンプルを .NET Framework 4.8 で検証する入口。
///
/// 使い方（リポジトリのルートで）:
///   dotnet run --project tools/book-a-net48 -c Release                … 12 章すべて
///   dotnet run --project tools/book-a-net48 -c Release -- &lt;slug&gt; …   … 指定した章だけ
///
/// .NET 10 の検証（tools/screenshot-capture）と違い、ある章が記事の値と食い違っても残りの章を続け、
/// 最後に食い違った章をまとめて出す。.NET Framework での差を洗い出すのが目的だからである。
/// </summary>
internal static class Program
{
    private static readonly IScene[] AllScenes =
    [
        new BookACh01BindingModeOmittedScene(),
        new BookACh02InputLimitsScene(),
        new BookACh03DataGridCommitOnSaveScene(),
        new BookACh04CanExecuteScene(),
        new BookACh05ValidationScene(),
        new BookACh06StyleTriggerScene(),
        new BookACh07DataTemplateParentScene(),
        new BookACh08UserControlScene(),
        new BookACh09StaticResourceScene(),
        new BookACh10UnhandledExceptionScene(),
        new BookACh11TaskResultScene(),
        new BookACh12ProcessNotExitingScene(),
        new BookALibrariesScene(),
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == BookAChildProcess.Flag)
        {
            return BookAChildProcess.Run(args[1]);
        }

        int exitCode = 0;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                exitCode = await RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                exitCode = 1;
            }
            finally
            {
                app.Shutdown();
            }
        };

        app.Run();
        return exitCode;
    }

    private static async Task<int> RunAsync(string[] args)
    {
        // 知らない slug が 1 つでもあれば、残りを測らずに失敗にする（測っていない章を測ったと見誤らないため）。
        var unknown = args.Where(arg => !AllScenes.Any(scene => string.Equals(scene.Slug, arg, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"登録されていない slug: {string.Join(", ", unknown)}。登録済み slug: {string.Join(", ", AllScenes.Select(s => s.Slug))}");
        }

        var targets = args.Length == 0
            ? AllScenes.ToList()
            : AllScenes.Where(scene => args.Contains(scene.Slug, StringComparer.OrdinalIgnoreCase)).ToList();

        var failures = new List<string>();
        foreach (IScene scene in targets)
        {
            try
            {
                await scene.CaptureAsync(new SceneContext());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{scene.Slug}] {ex.Message}");
                failures.Add(scene.Slug);
            }
        }

        Console.WriteLine(failures.Count == 0
            ? "すべての章が記事の値と一致した。"
            : $"記事の値と食い違った章: {string.Join(", ", failures)}");
        return failures.Count == 0 ? 0 : 1;
    }
}
