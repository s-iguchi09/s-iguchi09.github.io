using System.Windows;
using ScreenshotCapture.Scenes;

namespace ScreenshotCapture;

/// <summary>
/// 記事に載せるスクリーンショットを、実際に WPF アプリを起動して取得するツール。
/// 出力先はリポジトリ内の <c>images/articles/&lt;slug&gt;/</c> である。
///
/// 使い方:
///   dotnet run --project tools/screenshot-capture              … 全シーンを取得する
///   dotnet run --project tools/screenshot-capture -- &lt;slug&gt;   … 指定した slug のみ取得する
/// </summary>
internal static class Program
{
    /// <summary>取得対象のシーン。記事を追加したらここへ登録する。</summary>
    private static readonly IScene[] AllScenes =
    [
        new LabelUnderscoreScene(),
        new FluentClearButtonScene(),
        new DataGridSortingScene(),
        new DataGridSortResetScene(),
        new DataGridEditingTemplateScene(),
        new BindingStringFormatScene(),
        new UpdateSourceTriggerScene(),
        new SelectableReadOnlyTextScene(),
        new ScrollViewerNotScrollingScene(),
        new DatePickerFormatScene(),
        new LabelVsTextBlockScene(),
        new ComboBoxItemsSourceScene(),
        new FluentSystemColorsScene(),
        new RelayCommandCanExecuteScene(),
        new StaticVsDynamicResourceScene(),
        new StyleTriggerLocalValueScene(),
        new DataTemplateParentBindingScene(),
        new TreeViewSelectItemScene(),
        new ValidationErrorNotDisplayedScene(),
        new UserControlDependencyPropertyScene(),
        new NaturalSortScene(),
        new LinqBackportNet5Scene(),
        new LinqBackportNet6Scene(),
        new LinqBackportNet7Scene(),
        new LinqBackportNet8Scene(),
        new LinqBackportNet9Scene(),
        new LinqBackportNet10Scene(),
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        int exitCode = 0;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                await RunAsync(args);
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

    private static async Task RunAsync(string[] args)
    {
        string repositoryRoot = FindRepositoryRoot();
        IEnumerable<IScene> scenes = args.Length == 0
            ? AllScenes
            : AllScenes.Where(scene => args.Contains(scene.Slug, StringComparer.OrdinalIgnoreCase));

        var targets = scenes.ToList();
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"該当するシーンが無い。登録済み slug: {string.Join(", ", AllScenes.Select(s => s.Slug))}");
        }

        foreach (IScene scene in targets)
        {
            string outputDirectory = Path.Combine(repositoryRoot, "images", "articles", scene.Slug);
            Directory.CreateDirectory(outputDirectory);

            var context = new SceneContext(scene.Slug, outputDirectory);
            await scene.CaptureAsync(context);

            foreach (string file in context.SavedFiles)
            {
                Console.WriteLine(Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'));
            }
        }
    }

    /// <summary>
    /// 実行ディレクトリから上位をたどり、<c>_config.yml</c> のあるリポジトリルートを探す。
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "_config.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("_config.yml が見つからず、リポジトリルートを特定できない。");
    }
}
