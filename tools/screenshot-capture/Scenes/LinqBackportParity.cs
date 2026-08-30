using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// LINQ バックポート記事群が載せているポリフィル実装が、.NET の組み込みと
/// 同じ結果を返すかを確かめる。
///
/// 記事の主張は「この実装を足せば .NET Framework でも同じように使える」である。
/// .NET 10 での実行結果だけを図にしても、この主張を確かめたことにはならない。
///
/// そこで、記事本文から実装コードをそのまま取り出し、同一のドライバーを
/// net48 と net10.0 の両方でビルドして実行し、出力を突き合わせる。
/// net10.0 では #if !NETx_0_OR_GREATER によってポリフィルが無効化されるため、
/// 組み込み側が呼ばれる。両者が一致すれば、実装が等価であることと、
/// 移行ガードが効いていることの両方が確かめられる。
/// </summary>
internal static class LinqBackportParity
{
    private const string LegacyTarget = "net48";
    private const string ModernTarget = "net10.0";

    /// <summary>1 つの検証項目。<paramref name="Expression"/> は両方の環境で評価される。</summary>
    internal sealed record Probe(string Label, string Expression);

    /// <summary>記事のポリフィルを両環境で走らせ、表の行を返す。</summary>
    public static async Task<List<IReadOnlyList<string>>> MeasureAsync(
        string slug,
        IReadOnlyList<Probe> probes,
        string sampleSource)
    {
        string polyfill = ExtractPolyfill(slug);
        string driver = BuildDriver(probes, sampleSource);

        string root = Path.Combine(Path.GetTempPath(), "linq-backport-parity", slug);
        string[] legacy = await BuildAndRunAsync(Path.Combine(root, LegacyTarget), LegacyTarget, polyfill, driver);
        string[] modern = await BuildAndRunAsync(Path.Combine(root, ModernTarget), ModernTarget, polyfill, driver);

        Dictionary<string, string> legacyByLabel = Parse(legacy);
        Dictionary<string, string> modernByLabel = Parse(modern);

        var rows = new List<IReadOnlyList<string>>();
        foreach (Probe probe in probes)
        {
            string built = modernByLabel.GetValueOrDefault(probe.Label, "(no output)");
            string back = legacyByLabel.GetValueOrDefault(probe.Label, "(no output)");
            rows.Add([probe.Label, built, back, built == back ? "same" : "DIFFERS"]);
        }

        return rows;
    }

    /// <summary>
    /// メソッドが BCL 側に存在するターゲットフレームワークを、実際にコンパイルして調べる。
    ///
    /// 「.NET Core で追加されたメソッドは .NET Framework には無い」は成り立たない。
    /// .NET Framework 4.7.1 は .NET Standard 2.0 に対応しており、
    /// そこに含まれるメソッドは 4.7.1 以降で使える。ポリフィルを無条件に足すと
    /// BCL 側と衝突して CS0121 になるため、どこから使えるのかを確かめる必要がある。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> MeasureAvailabilityAsync(
        string slug,
        IReadOnlyList<string> targetFrameworks,
        IReadOnlyList<Probe> methods)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (Probe method in methods)
        {
            var cells = new List<string> { method.Label };

            foreach (string tfm in targetFrameworks)
            {
                string workspace = Path.Combine(
                    Path.GetTempPath(), "linq-bcl-availability", slug, method.Label, tfm);
                cells.Add(await CompilesAsync(workspace, tfm, method.Expression) ? "yes" : "no");
            }

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>ポリフィルを足さずに、その式がコンパイルできるかだけを見る。</summary>
    private static async Task<bool> CompilesAsync(string workspace, string targetFramework, string expression)
    {
        Directory.CreateDirectory(workspace);
        var utf8 = new UTF8Encoding(true);

        await File.WriteAllTextAsync(
            Path.Combine(workspace, "C.cs"),
            $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;

            internal static class Probe
            {
                private static void M(IEnumerable<int> source)
                {
                    object result = {{expression}};
                }
            }
            """,
            utf8);

        await File.WriteAllTextAsync(
            Path.Combine(workspace, "p.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>{targetFramework}</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="C.cs" />
              </ItemGroup>
            </Project>
            """,
            utf8);

        (int exitCode, _) = await RunAsync("dotnet", "build -v q --nologo", workspace);
        return exitCode == 0;
    }

    private static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string>();
        foreach (string line in lines)
        {
            int tab = line.IndexOf('\t');
            if (tab > 0)
            {
                map[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
        }

        return map;
    }

    /// <summary>
    /// 記事本文から実装コードを取り出す。
    ///
    /// 移行ガード #if !NET を含む csharp ブロックのうち最も長いものを実装とみなす。
    /// 記事の見出しは記事ごとに異なるため、見出し名では探さない。
    /// </summary>
    private static string ExtractPolyfill(string slug)
    {
        string path = Path.Combine(RepositoryRoot(), "_articles_ja", slug + ".md");
        string[] lines = File.ReadAllLines(path);

        string? best = null;
        StringBuilder? current = null;

        foreach (string line in lines)
        {
            if (current is null)
            {
                if (line.StartsWith("```csharp", StringComparison.Ordinal))
                {
                    current = new StringBuilder();
                }

                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                string block = current.ToString();
                if (block.Contains("#if !NET", StringComparison.Ordinal)
                    && (best is null || block.Length > best.Length))
                {
                    best = block;
                }

                current = null;
                continue;
            }

            current.AppendLine(line);
        }

        return best ?? throw new InvalidOperationException(
            $"{slug} の本文に、移行ガードを含む実装コードのブロックが見つからない。");
    }

    private static string BuildDriver(IReadOnlyList<Probe> probes, string sampleSource)
    {
        var emits = new StringBuilder();
        foreach (Probe probe in probes)
        {
            // ラベルは文字列リテラルとして埋め込むため、引用符とバックスラッシュを逃がす。
            string label = probe.Label.Replace("\\", "\\\\").Replace("\"", "\\\"");
            emits.AppendLine($"        Emit(\"{label}\", () => Fmt({probe.Expression}));");
        }

        var driver = new StringBuilder();
        driver.AppendLine("using System;");
        driver.AppendLine("using System.Collections;");
        driver.AppendLine("using System.Collections.Generic;");
        driver.AppendLine("using System.Linq;");
        driver.AppendLine();
        driver.AppendLine("internal static class Driver");
        driver.AppendLine("{");
        driver.AppendLine(sampleSource);
        driver.AppendLine("    private static void Main()");
        driver.AppendLine("    {");
        driver.AppendLine("        Console.OutputEncoding = new System.Text.UTF8Encoding(false);");
        driver.Append(emits);
        driver.AppendLine("    }");
        driver.AppendLine();
        driver.AppendLine("    private static void Emit(string label, Func<string> probe)");
        driver.AppendLine("    {");
        driver.AppendLine("        string result;");
        driver.AppendLine("        try");
        driver.AppendLine("        {");
        driver.AppendLine("            result = probe();");
        driver.AppendLine("        }");
        driver.AppendLine("        catch (Exception ex)");
        driver.AppendLine("        {");
        driver.AppendLine("            // 例外も結果のうち。型名まで一致するかを見る。");
        driver.AppendLine("            result = \"throws \" + ex.GetType().Name;");
        driver.AppendLine("        }");
        driver.AppendLine();
        driver.AppendLine("        Console.WriteLine(label + \"\\t\" + result);");
        driver.AppendLine("    }");
        driver.AppendLine();
        driver.AppendLine("    // 両環境で同じ書式にするため、整形もドライバー側で行う。");
        driver.AppendLine("    private static string Fmt(object value)");
        driver.AppendLine("    {");
        driver.AppendLine("        if (value == null)");
        driver.AppendLine("        {");
        driver.AppendLine("            return \"null\";");
        driver.AppendLine("        }");
        driver.AppendLine();
        driver.AppendLine("        if (value is string text)");
        driver.AppendLine("        {");
        driver.AppendLine("            return text;");
        driver.AppendLine("        }");
        driver.AppendLine();
        driver.AppendLine("        if (value is IEnumerable items)");
        driver.AppendLine("        {");
        driver.AppendLine("            var parts = new List<string>();");
        driver.AppendLine("            foreach (object item in items)");
        driver.AppendLine("            {");
        driver.AppendLine("                parts.Add(Fmt(item));");
        driver.AppendLine("            }");
        driver.AppendLine();
        driver.AppendLine("            return \"[\" + string.Join(\", \", parts) + \"]\";");
        driver.AppendLine("        }");
        driver.AppendLine();
        driver.AppendLine("        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);");
        driver.AppendLine("    }");
        driver.AppendLine("}");

        return driver.ToString();
    }

    private static async Task<string[]> BuildAndRunAsync(
        string workspace, string targetFramework, string polyfill, string driver)
    {
        Directory.CreateDirectory(workspace);
        // BOM を付ける。付けないと、コンパイラが既定のコードページで読み、日本語コメントが壊れる。
        var utf8 = new UTF8Encoding(true);

        await File.WriteAllTextAsync(Path.Combine(workspace, "Polyfill.cs"), polyfill, utf8);
        await File.WriteAllTextAsync(Path.Combine(workspace, "Driver.cs"), driver, utf8);
        await File.WriteAllTextAsync(
            Path.Combine(workspace, "p.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{targetFramework}</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>disable</Nullable>
                <AssemblyName>parity</AssemblyName>
                <RootNamespace>parity</RootNamespace>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Polyfill.cs" />
                <Compile Include="Driver.cs" />
              </ItemGroup>
            </Project>
            """,
            utf8);

        (int exitCode, string output) = await RunAsync("dotnet", "run -c Release -v q --nologo", workspace);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"{targetFramework} のビルドまたは実行が失敗した。{Environment.NewLine}{output}");
        }

        return output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // .NET Framework 側はリダイレクト時に OEM コードページで書くため、明示する。
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} を起動できない。");

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + stderr);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "_articles_ja")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("リポジトリのルートが見つからない。");
    }
}
