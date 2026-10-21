using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
#if NETFRAMEWORK
    // .NET Framework 4.8 での検証（tools/book-a-net48）は、.NET 10 の結果を上書きしないよう net48/ の下に書く。
    public const string Tool = "tools/book-a-net48";
    private const string Subdirectory = "net48";
#else
    public const string Tool = "tools/screenshot-capture";
    private const string Subdirectory = "";
#endif

    /// <summary>レポートの書き出し先（<c>samples/book-a/verification/</c>、.NET Framework では <c>net48/</c> の下）。ディレクトリは作っておく。</summary>
    public static string ReportPath(string fileName)
    {
        string directory = Path.Combine(FindRepositoryRoot(), "samples", "book-a", "verification", Subdirectory);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    public static void WriteAndAssert(
        string fileName, string slug, string chapter, string articleTitle, string articleSlug, string note, IReadOnlyList<BookACheck> checks) =>
        Write(
            fileName, slug,
            $"{chapter} 付録サンプルと記事の実測の突き合わせ",
            $"記事: [{articleTitle}](https://s-iguchi09.github.io/ja/articles/{articleSlug}/)。{note}{ExpectedNote}",
            "| 操作 | 記事の該当箇所 | 記事から期待する値 | サンプルの実測 | 期待と一致 |",
            $"{chapter}の付録サンプルが記事の実測と一致しない", "記事",
            checks);

    /// <summary>
    /// 記事の表の値と、比べる値（<see cref="BookACheck.Expected"/>）の関係。サンプルは色・文言・データを記事と変えている章があるため、
    /// 比べる値は記事の結果をサンプルの値で書き直したものとし、記事の値そのものと違う場合は「記事の該当箇所」に記事の値を書く。
    /// </summary>
    private const string ExpectedNote =
        "「記事から期待する値」は、記事の実測の結果をサンプルの値（色・文言・データなど）で書き直したもの。記事の表の値そのものと違う場合は、「記事の該当箇所」に記事の値を書いている。";

    /// <summary>
    /// MVVM ライブラリ（CommunityToolkit.Mvvm・Prism）で書いた場合の結果を書き出す。記事に値が無いので、
    /// 公式文書などの根拠（<see cref="BookACheck.ArticleRef"/>）から予想した値（<see cref="BookACheck.Expected"/>）と比べる。
    /// </summary>
    public static void WriteLibraryReport(string fileName, string slug, string title, string note, IReadOnlyList<BookACheck> checks) =>
        Write(
            fileName, slug, title, note,
            "| 操作 | 予想の根拠 | 予想 | 実測 | 予想と一致 |",
            $"{title}が予想と一致しない", "予想",
            checks);

    /// <summary>
    /// 実行したランタイムの説明。.NET Framework では FrameworkDescription（4.8.9xxx など）だけでは
    /// 4.8 と 4.8.1 の区別が付かないため、レジストリの Release の値から版を求め、ビルドの対象と並べて書く。
    /// </summary>
    internal static string DescribeRuntime()
    {
        string description = RuntimeInformation.FrameworkDescription;
        if (!description.StartsWith(".NET Framework", StringComparison.Ordinal))
        {
            return description;
        }

        string target = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "不明";

        // 値が取れないことは、どの版で動いたかの証拠にならない。取れたときだけ版を判定する。
        if (ReadFrameworkRelease() is not int release)
        {
            return $"{description}（ランタイムの版は不明: レジストリの Release を読めない。ビルドの対象は {target}）";
        }

        // 版と Release の最小値の対応は Microsoft Learn の「インストールされている .NET Framework のバージョンを確認する」による。
        string version = release switch
        {
            >= 533320 => ".NET Framework 4.8.1",
            >= 528040 => ".NET Framework 4.8",
            >= 461808 => ".NET Framework 4.7.2",
            _ => ".NET Framework 4.7.1 以前",
        };
        return $"{description}（ランタイムは {version}、Release {release}。ビルドの対象は {target}）";
    }

    /// <summary>
    /// レジストリから .NET Framework 4.x の Release を読む。キーや値が無いとき、権限不足などで読めないときは null を返す。
    /// </summary>
    private static int? ReadFrameworkRelease()
    {
        try
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            return key?.GetValue("Release") as int?;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static void Write(string fileName, string slug, string heading, string intro, string header, string mismatchMessage, string expectedWord, IReadOnlyList<BookACheck> checks)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<!-- {Tool} が実行時に自動生成する。手で編集しない。 -->");
        builder.AppendLine($"<!-- 再生成: dotnet run --project {Tool} -c Release -- {slug} -->");
        builder.AppendLine();
        builder.AppendLine($"# {heading}");
        builder.AppendLine();
        builder.AppendLine($"環境: {DescribeRuntime()} / {RuntimeInformation.OSDescription.Trim()}");
        builder.AppendLine();
        builder.AppendLine(intro);
        builder.AppendLine();
        builder.AppendLine(header);
        builder.AppendLine("|---|---|---|---|---|");
        foreach (BookACheck check in checks)
        {
            builder.AppendLine($"| {Cell(check.Label)} | {Cell(check.ArticleRef)} | {Cell(check.Expected)} | {Cell(check.Actual)} | {(check.Matches ? "一致" : "不一致")} |");
        }

        string text = builder.ToString();
        File.WriteAllText(ReportPath(fileName), text, new UTF8Encoding(false));
        Console.WriteLine(text);

        var mismatches = checks.Where(c => !c.Matches).ToList();
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                $"{mismatchMessage}: " +
                string.Join(" / ", mismatches.Select(m => $"{m.Label}: {expectedWord} {m.Expected} 実測 {m.Actual}")));
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
