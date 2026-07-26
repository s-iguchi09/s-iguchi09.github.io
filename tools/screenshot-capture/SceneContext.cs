using System.Windows;
using System.Windows.Markup;

namespace ScreenshotCapture;

/// <summary>
/// 1 つの記事（slug）に対応するキャプチャ手順。
/// </summary>
internal interface IScene
{
    /// <summary>対応する記事の slug。出力先 <c>images/articles/&lt;slug&gt;/</c> になる。</summary>
    string Slug { get; }

    Task CaptureAsync(SceneContext context);
}

/// <summary>
/// シーンから使う保存・生成ユーティリティ。
/// </summary>
internal sealed class SceneContext(string slug, string outputDirectory)
{
    private readonly List<string> _saved = [];

    public string Slug { get; } = slug;

    public string OutputDirectory { get; } = outputDirectory;

    public IReadOnlyList<string> SavedFiles => _saved;

    /// <summary>
    /// ウィンドウを表示し、描画が安定してから PNG として保存して閉じる。
    /// </summary>
    public async Task ShootAsync(Window window, string fileName, Func<Window, Task>? beforeCapture = null)
    {
        try
        {
            await Capture.ShowAndSettleAsync(window);

            if (beforeCapture is not null)
            {
                await beforeCapture(window);
                await Capture.SettleAsync(window);
            }

            string path = Path.Combine(OutputDirectory, fileName);
            Capture.SaveWindow(window, path);
            _saved.Add(path);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// XAML 文字列から要素を生成する。記事に載せる XAML をそのまま使うことで、
    /// 図と本文のコードが食い違わないようにする。
    /// 既定の名前空間は <see cref="ParserContext"/> 側で補うため、
    /// 呼び出し側は xmlns を書く必要がない。
    /// </summary>
    public static T LoadXaml<T>(string xaml) where T : class
    {
        var parserContext = new ParserContext();
        parserContext.XmlnsDictionary.Add(string.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        parserContext.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

        object element = XamlReader.Parse(xaml, parserContext);
        return element as T
            ?? throw new InvalidOperationException($"XAML の型が想定と異なる: {element.GetType()}");
    }
}
