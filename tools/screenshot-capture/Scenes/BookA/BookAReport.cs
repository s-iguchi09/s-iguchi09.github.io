using System.Runtime.InteropServices;
using System.Text;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 付録サンプルの 1 つの確認。記事のどの値（<see cref="ArticleRef"/>）と比べ、記事の値（<see cref="Expected"/>）に対して
/// サンプルで何が得られたか（<see cref="Actual"/>）を持つ。
/// </summary>
internal sealed record BookACheck(string Label, string ArticleRef, string Expected, string Actual)
{
    public bool Matches => Expected == Actual;
}

/// <summary>
/// 付録サンプルの検証結果を <c>samples/book-a/verification/&lt;file&gt;</c> に書き出し、
/// 記事の値と 1 つでも食い違えば例外にする。
/// </summary>
internal static class BookAReport
{
    public static void WriteAndAssert(
        string fileName, string slug, string chapter, string articleTitle, string articleSlug, string note, IReadOnlyList<BookACheck> checks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!-- tools/screenshot-capture が実行時に自動生成する。手で編集しない。 -->");
        builder.AppendLine($"<!-- 再生成: dotnet run --project tools/screenshot-capture -c Release -- {slug} -->");
        builder.AppendLine();
        builder.AppendLine($"# {chapter} 付録サンプルと記事の実測の突き合わせ");
        builder.AppendLine();
        builder.AppendLine($"環境: {RuntimeInformation.FrameworkDescription} / {RuntimeInformation.OSDescription}");
        builder.AppendLine();
        builder.AppendLine($"記事: [{articleTitle}](https://s-iguchi09.github.io/ja/articles/{articleSlug}/)。{note}");
        builder.AppendLine();
        builder.AppendLine("| 操作 | 記事の該当箇所 | 記事の値 | サンプルの実測 | 記事と一致 |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (BookACheck check in checks)
        {
            builder.AppendLine($"| {Cell(check.Label)} | {Cell(check.ArticleRef)} | {Cell(check.Expected)} | {Cell(check.Actual)} | {(check.Matches ? "一致" : "不一致")} |");
        }

        string text = builder.ToString();
        string directory = Path.Combine(FindRepositoryRoot(), "samples", "book-a", "verification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), text, new UTF8Encoding(false));
        Console.WriteLine(text);

        var mismatches = checks.Where(c => !c.Matches).ToList();
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"{chapter}の付録サンプルが記事の実測と一致しない: " +
                string.Join(" / ", mismatches.Select(m => $"{m.Label}: 記事 {m.Expected} 実測 {m.Actual}")));
        }
    }

    /// <summary>Markdown の表のセルに入れられるよう、縦線を逃がす。</summary>
    private static string Cell(string value) => value.Replace("|", "\\|");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "_config.yml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("リポジトリルートが見つからない。");
    }
}
