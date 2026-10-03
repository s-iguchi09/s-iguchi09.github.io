using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;

namespace ScreenshotCapture;

/// <summary>
/// 1 つのページ（記事、またはコントロール別のデモページ）に対応するキャプチャ手順。
/// </summary>
internal interface IScene
{
    /// <summary>
    /// 対応するページの slug。検証記録 <c>docs/verification/&lt;slug&gt;.yml</c> の名前と、
    /// 既定の <see cref="ImageDirectory"/> に使う。
    /// </summary>
    string Slug { get; }

    /// <summary>
    /// 図の出力先。リポジトリルートからの相対パス。既定は <c>images/articles/&lt;slug&gt;/</c>。
    /// 記事以外のページ（コントロール別のデモページなど）を検証するシーンは上書きする。
    /// </summary>
    string ImageDirectory => Path.Combine("images", "articles", Slug);

    /// <summary>
    /// このシーンが「実際に動かして」確かめている主張。
    ///
    /// 図を描くだけのシーンは空のままでよい。値を返すシーンは、実行結果が
    /// 記事のどの記述を裏づけているかを 1 項目ずつ書く。
    /// 実行するたびに <c>docs/verification/&lt;slug&gt;.yml</c> へ書き出されるため、
    /// 「どの記事が実測で確かめられているか」を毎回調べ直さずに済む。
    /// </summary>
    IReadOnlyList<string> Verifies => [];

    Task CaptureAsync(SceneContext context);
}

/// <summary>
/// シーンから使う保存・生成ユーティリティ。
/// </summary>
internal sealed class SceneContext(string slug, string outputDirectory, string tableDirectory)
{
    private readonly List<string> _saved = [];
    private readonly List<string> _savedTables = [];

    public string Slug { get; } = slug;

    public string OutputDirectory { get; } = outputDirectory;

    /// <summary>
    /// 記事に include する Markdown の表の出力先。<c>_includes/tables/</c> の下に、
    /// <see cref="OutputDirectory"/> の <c>images/</c> 以下と同じ階層を作る。
    /// </summary>
    public string TableDirectory { get; } = tableDirectory;

    public IReadOnlyList<string> SavedFiles => _saved;

    public IReadOnlyList<string> SavedTables => _savedTables;

    /// <summary>
    /// ウィンドウを表示し、描画が安定してから PNG として保存して閉じる。
    /// </summary>
    /// <param name="requireContentRendered">
    /// 真なら、クライアント領域が一色のときに保存せず例外にする。中身が一色の図を意図して撮るときだけ偽にする。
    /// </param>
    public async Task ShootAsync(Window window, string fileName, Func<Window, Task>? beforeCapture = null, bool requireContentRendered = true)
    {
        try
        {
            await Capture.ShowAndSettleAsync(window);

            if (beforeCapture is not null)
            {
                await beforeCapture(window);
                await Capture.SettleAsync(window);
            }

            SaveWindowOrSkip(window, Path.Combine(OutputDirectory, fileName), requireContentRendered);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 既に表示しているウィンドウを、描画が安定してから PNG として保存する。閉じはしない。
    ///
    /// 複数のウィンドウを同時に開いた状態を撮る場合に使う。
    /// <see cref="ShootAsync"/> は表示から始めるため、表示済みのウィンドウには使えない。
    /// </summary>
    public async Task SaveShownWindowAsync(Window window, string fileName, bool requireContentRendered = true)
    {
        await Capture.SettleAsync(window);
        SaveWindowOrSkip(window, Path.Combine(OutputDirectory, fileName), requireContentRendered);
    }

    /// <summary>
    /// ウィンドウを PNG として保存する。撮影の経路はすべてここを通す。
    ///
    /// 画面が消灯しているときなど、ウィンドウを撮れない状況で表の計測だけを行うため、
    /// SCREENSHOT_SKIP_WINDOW_CAPTURE=1 では撮影を飛ばす。既存の図は上書きせず、撮らなかったことを出力に残す。
    /// </summary>
    private void SaveWindowOrSkip(Window window, string path, bool requireContentRendered)
    {
        if (Environment.GetEnvironmentVariable("SCREENSHOT_SKIP_WINDOW_CAPTURE") == "1")
        {
            Console.WriteLine($"skipped window capture (SCREENSHOT_SKIP_WINDOW_CAPTURE=1): {path}");
            if (File.Exists(path))
            {
                // 図そのものは変えていないので、検証記録の画像一覧には残す。
                _saved.Add(path);
            }

            return;
        }

        Capture.SaveWindow(window, path, requireContentRendered);
        _saved.Add(path);
    }

    /// <summary>
    /// 実測値の表を SVG と、記事に include する Markdown の表（<see cref="TableDirectory"/> の下）として保存する。
    ///
    /// 表は文字と罫線だけなので、ウィンドウを撮影せず直接描く。
    /// 拡大しても文字がぼやけず、差分でも値の変化が読める。
    /// 実行して得た値を描く点は <see cref="ShootAsync"/> と変わらない。
    /// </summary>
    public Task SaveTableAsync(
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows,
        string fileName) =>
        SaveTableAsync(
            title,
            headers.Select(header => (Loc)header).ToList(),
            rows.Select(row => (IReadOnlyList<Loc>)row.Select(cell => (Loc)cell).ToList()),
            fileName);

    /// <summary>見出しだけを日英の組で渡し、セルは識別子や値だけで言語に依存しない表の版。</summary>
    public Task SaveTableAsync(
        string title,
        IReadOnlyList<Loc> headers,
        IEnumerable<IReadOnlyList<string>> rows,
        string fileName) =>
        SaveTableAsync(
            title,
            headers,
            rows.Select(row => (IReadOnlyList<Loc>)row.Select(cell => (Loc)cell).ToList()),
            fileName);

    /// <summary>
    /// 見出しとセルを日英の組（<see cref="Loc"/>）で受け取る版。
    /// SVG は英語で描き、Markdown の表は <c>&lt;名前&gt;.en.md</c> と <c>&lt;名前&gt;.ja.md</c> の 2 つを書き出す。
    /// 記事は自分の言語の表を include する。
    /// </summary>
    public async Task SaveTableAsync(
        string title,
        IReadOnlyList<Loc> headers,
        IEnumerable<IReadOnlyList<Loc>> rows,
        string fileName)
    {
        var body = rows.ToList();
        string svg = DemoLayout.BuildTableSvg(
            title,
            headers.Select(header => header.En).ToList(),
            body.Select(row => (IReadOnlyList<string>)row.Select(cell => cell.En).ToList()));
        string path = Path.Combine(OutputDirectory, fileName);
        await File.WriteAllTextAsync(path, svg, new UTF8Encoding(false));
        _saved.Add(path);

        // 同じ値を、記事の本文に include する Markdown の表として、言語ごとに書き出す。
        // 横に広い表は SVG のままだと本文の幅に縮められて読めないため、記事では Markdown の表を使う。
        Directory.CreateDirectory(TableDirectory);
        foreach (string language in new[] { "en", "ja" })
        {
            string tablePath = Path.Combine(TableDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.{language}.md");
            string markdown = DemoLayout.BuildTableMarkdown(
                headers.Select(header => header.In(language)).ToList(),
                body.Select(row => (IReadOnlyList<string>)row.Select(cell => Localize(cell, language)).ToList()));
            await File.WriteAllTextAsync(tablePath, markdown, new UTF8Encoding(false));
            _savedTables.Add(tablePath);
        }
    }

    /// <summary>
    /// 共通の補助関数（DemoProbe.HitName・DemoProbe.Throws・WpfProbe.BindingState など）が返す英語の言葉の日本語。
    /// シーンごとに訳さなくても日本語の表に出ないようにする。どれも紛れのない言い回しなので、セルの中に出てきても置き換える（英語の語の直後に続くものは、英語の文の一部として残す）。
    /// </summary>
    private static readonly Dictionary<string, string> CommonJapanese = new(StringComparer.Ordinal)
    {
        ["(nothing)"] = "（なし）",
        ["(none)"] = "（なし）",
        ["no exception"] = "例外なし",
        ["no binding"] = "バインドなし",
        ["not found"] = "見つからない",
    };

    /// <summary>
    /// セルを指定した言語の文字にする。日本語では、共通の部品が出す英語の言葉
    /// （<see cref="CommonJapanese"/> の言葉と、WpfProbe.Describe の "(empty)"）を置き換える。
    /// </summary>
    private static string Localize(Loc cell, string language)
    {
        if (language != "ja")
        {
            return cell.En;
        }

        string text = cell.Ja;
        foreach ((string english, string japanese) in CommonJapanese)
        {
            // "property not found" のように英語の文の一部になっているもの（引用した WPF のメッセージなど）は置き換えない。
            string pattern = char.IsLetter(english[0])
                ? $@"(?<![A-Za-z] ?){Regex.Escape(english)}(?![A-Za-z])"
                : Regex.Escape(english);
            text = Regex.Replace(text, pattern, japanese.Replace("$", "$$"));
        }

        return text.Replace("((empty))", "(空)", StringComparison.Ordinal).Replace("(empty)", "（空）", StringComparison.Ordinal);
    }

    /// <summary>
    /// XAML 文字列から要素を生成する。記事に載せる XAML をそのまま使うことで、
    /// 図と本文のコードが食い違わないようにする。
    /// 既定の名前空間は <see cref="ParserContext"/> 側で補うため、
    /// 呼び出し側は xmlns を書く必要がない。
    /// シーン側で定義した型（列挙体・コンバーターなど）を参照する XAML では、
    /// <paramref name="extraNamespaces"/> に接頭辞と CLR 名前空間の組を渡す。
    /// </summary>
    public static T LoadXaml<T>(string xaml, params (string Prefix, string Namespace)[] extraNamespaces) where T : class
    {
        var parserContext = new ParserContext();
        parserContext.XmlnsDictionary.Add(string.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        parserContext.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

        foreach ((string prefix, string ns) in extraNamespaces)
        {
            parserContext.XmlnsDictionary.Add(prefix, ns);
        }

        object element = XamlReader.Parse(xaml, parserContext);
        return element as T
            ?? throw new InvalidOperationException($"XAML の型が想定と異なる: {element.GetType()}");
    }
}
