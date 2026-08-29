using System.Runtime.InteropServices;
using System.Text;
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
        new FluentCustomStyleScene(),
        new RelayCommandCanExecuteScene(),
        new StaticVsDynamicResourceScene(),
        new StyleTriggerLocalValueScene(),
        new DataTemplateParentBindingScene(),
        new TreeViewSelectItemScene(),
        new ValidationErrorNotDisplayedScene(),
        new UserControlDependencyPropertyScene(),
        new RadioButtonEnumBindingScene(),
        new NaturalSortScene(),
        new CollectionViewFilterRefreshScene(),
        new LinqBackportNet5Scene(),
        new LinqBackportNet6Scene(),
        new LinqBackportNet7Scene(),
        new LinqBackportNet8Scene(),
        new LinqBackportNet9Scene(),
        new LinqBackportNet10Scene(),
        new CSharpFrameworkMatrixScene(),
        new BitmapImageFileLockScene(),
        new ShutdownLifetimeScene(),
        new CollectionCrossThreadScene(),
        new BindingErrorTraceScene(),
        new UpdateSourcePitfallScene(),
        new ExtensionReceiverMatrixScene(),
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

            string? record = WriteVerificationRecord(repositoryRoot, scene, context);
            if (record is not null)
            {
                Console.WriteLine(Path.GetRelativePath(repositoryRoot, record).Replace('\\', '/'));
            }
        }
    }

    /// <summary>
    /// シーンが宣言した検証内容を <c>docs/verification/&lt;slug&gt;.yml</c> へ書き出す。
    ///
    /// 手で書かず実行のたびに更新することで、「その記事が実測で確かめられているか」を
    /// 記事の書式から推測せずに判定できる。<c>docs</c> は <c>_config.yml</c> の
    /// <c>exclude</c> に入っているため、サイトには出力されない。
    /// 何も検証していないシーン（図を描くだけ）は記録を作らない。
    /// </summary>
    private static string? WriteVerificationRecord(string repositoryRoot, IScene scene, SceneContext context)
    {
        IReadOnlyList<string> claims = scene.Verifies;
        if (claims.Count == 0)
        {
            return null;
        }

        string directory = Path.Combine(repositoryRoot, "docs", "verification");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, scene.Slug + ".yml");

        var builder = new StringBuilder();
        builder.AppendLine("# tools/screenshot-capture が実行時に自動生成する。手で編集しない。");
        builder.AppendLine("# 再生成: dotnet run --project tools/screenshot-capture -c Release -- " + scene.Slug);
        builder.AppendLine($"slug: {Yaml(scene.Slug)}");
        builder.AppendLine($"scene: {Yaml(scene.GetType().Name)}");
        builder.AppendLine("environment:");
        builder.AppendLine($"  runtime: {Yaml(RuntimeInformation.FrameworkDescription)}");
        builder.AppendLine($"  os: {Yaml(RuntimeInformation.OSDescription)}");
        builder.AppendLine("verifies:");
        foreach (string claim in claims)
        {
            builder.AppendLine($"  - {Yaml(claim)}");
        }

        builder.AppendLine("images:");
        foreach (string file in context.SavedFiles)
        {
            builder.AppendLine($"  - {Yaml(Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'))}");
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        return path;
    }

    /// <summary>YAML のスカラーとして安全な形に引用する。</summary>
    private static string Yaml(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

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
