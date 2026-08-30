using System.Diagnostics;
using System.Text;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// プロジェクトの形式によって、TFM 由来のプリプロセッサシンボルが定義されるかどうかが
/// 変わることを実測する部品。
///
/// <c>#if !NET471_OR_GREATER</c> のようなガードは、シンボルが定義されている前提で書く。
/// SDK 形式では TFM から自動で定義されるが、従来形式（非 SDK 形式）のプロジェクトでは
/// 定義されない。ガードが常に成立してポリフィルが有効になり、BCL 側の実装と衝突する。
///
/// 従来形式のプロジェクトは <c>dotnet build</c> では扱えないため、
/// Visual Studio に同梱される MSBuild を使う。見つからない環境では測定できないので
/// 例外にする（測っていないものを検証済みとして記録しないため）。
/// </summary>
internal static class ProjectFormatProbe
{
    /// <summary>ガードの対象にしているシンボル。記事が使っているものと同じ。</summary>
    private const string Symbol = "NET471_OR_GREATER";

    /// <summary>
    /// 同じソースを SDK 形式と従来形式の両方でビルドし、シンボルの定義状況と
    /// ポリフィルを重ねたときの結果を突き合わせる。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> SymbolAvailabilityAsync()
    {
        string msbuild = FindMsBuild();
        string root = Path.Combine(Path.GetTempPath(), "project-format-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            return
            [
                await MeasureSdkStyleAsync(root),
                await MeasureLegacyStyleAsync(root, msbuild, "plain", defineConstants: null),

                // 従来形式でも、シンボルを自分で定義すれば SDK 形式と同じ結果になる。
                // 対処法として記事に載せるため、効くことを確かめておく。
                await MeasureLegacyStyleAsync(root, msbuild, "defined", Symbol),
            ];
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>SDK 形式。TFM から <c>NET471_OR_GREATER</c> が自動で定義される。</summary>
    private static async Task<IReadOnlyList<string>> MeasureSdkStyleAsync(string root)
    {
        string workspace = Path.Combine(root, "sdk-style");
        Directory.CreateDirectory(workspace);

        var utf8 = new UTF8Encoding(true);
        File.WriteAllText(Path.Combine(workspace, "Program.cs"), ProbeSource, utf8);
        File.WriteAllText(
            Path.Combine(workspace, "sdk-style.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net48</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>disable</Nullable>
                <AssemblyName>probe</AssemblyName>
                <RootNamespace>probe</RootNamespace>
              </PropertyGroup>
            </Project>
            """,
            utf8);

        (int exitCode, string output) = await RunAsync("dotnet", "build -c Release -v m --nologo", workspace);
        return ["SDK-style, <TargetFramework>net48</TargetFramework>", .. Interpret(exitCode, output)];
    }

    /// <summary>
    /// 従来形式。<c>TargetFrameworkVersion</c> で対象を指定するため、
    /// TFM 由来のシンボルは定義されない。
    /// </summary>
    private static async Task<IReadOnlyList<string>> MeasureLegacyStyleAsync(
        string root, string msbuild, string variant, string? defineConstants)
    {
        string workspace = Path.Combine(root, "legacy-style-" + variant);
        Directory.CreateDirectory(workspace);

        string defineElement = defineConstants is null
            ? string.Empty
            : $"{Environment.NewLine}    <DefineConstants>$(DefineConstants);{defineConstants}</DefineConstants>";

        var utf8 = new UTF8Encoding(true);
        File.WriteAllText(Path.Combine(workspace, "Program.cs"), ProbeSource, utf8);
        File.WriteAllText(
            Path.Combine(workspace, "legacy-style.csproj"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
                <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
                <ProjectGuid>{6F4C6D3E-4E6B-4C1E-9D9C-2C1B9A6E0F11}</ProjectGuid>
                <OutputType>Exe</OutputType>
                <RootNamespace>probe</RootNamespace>
                <AssemblyName>probe</AssemblyName>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                <OutputPath>bin\</OutputPath>
                <LangVersion>latest</LangVersion>{{defineElement}}
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Core" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include="Program.cs" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """,
            utf8);

        (int exitCode, string output) = await RunAsync(
            msbuild, "legacy-style.csproj /t:Build /p:Configuration=Release /nologo /v:m", workspace);

        string label = defineConstants is null
            ? "legacy (non-SDK), <TargetFrameworkVersion>v4.8"
            : $"legacy (non-SDK) + <DefineConstants>{defineConstants}";

        return [label, .. Interpret(exitCode, output)];
    }

    /// <summary>
    /// ビルド出力から、シンボルの定義状況とビルド結果を読み取る。
    ///
    /// 定義されているかどうかはビルドの成否から推し量らず、ソース側に置いた
    /// <c>#warning</c> がどちらの分岐から出たかで判定する。
    /// ビルドが通った理由は他にもありうるためである。
    /// </summary>
    private static IReadOnlyList<string> Interpret(int exitCode, string output)
    {
        bool defined = output.Contains("PROBE_SYMBOL_DEFINED", StringComparison.Ordinal);
        bool notDefined = output.Contains("PROBE_SYMBOL_NOT_DEFINED", StringComparison.Ordinal);

        if (defined == notDefined)
        {
            throw new InvalidOperationException(
                $"シンボルの定義状況を判定できない。{Environment.NewLine}{output}");
        }

        string symbol = defined ? $"{Symbol} defined" : $"{Symbol} not defined";
        string polyfill = defined ? "skipped" : "compiled in";
        string result = exitCode == 0
            ? "build succeeded"
            : output.Contains("CS0121", StringComparison.Ordinal)
                ? "CS0121 (ambiguous call)"
                : throw new InvalidOperationException(
                    $"想定しないビルド失敗。CS0121 ではない。{Environment.NewLine}{output}");

        return [symbol, polyfill, result];
    }

    /// <summary>
    /// 記事のポリフィルと同じ形を置き、BCL の <c>Append</c> を呼ぶ。
    ///
    /// 置き場所は記事に合わせて <c>System.Linq</c> にする。拡張メソッドは
    /// 近い名前空間から探されるため、置き場所が違うと結果が変わる。
    /// グローバル名前空間に置いた場合は <c>using System.Linq</c> より近いところで
    /// 解決が済み、あいまいにならずにポリフィル側が使われてしまう。
    /// </summary>
    private const string ProbeSource =
        """
        using System;
        using System.Collections.Generic;
        using System.Linq;

        #if !NETCOREAPP

        namespace System.Linq
        {
        #if NET471_OR_GREATER
        #warning PROBE_SYMBOL_DEFINED
        #else
        #warning PROBE_SYMBOL_NOT_DEFINED
        #endif

        #if !NET471_OR_GREATER
            public static class LinqExtensions
            {
                public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
                {
                    foreach (var item in source)
                    {
                        yield return item;
                    }

                    yield return element;
                }
            }
        #endif
        }

        #endif

        internal static class Program
        {
            private static void Main()
            {
                Console.WriteLine(string.Join(",", new[] { 1, 2 }.Append(3)));
            }
        }
        """;

    /// <summary>
    /// 従来形式のプロジェクトをビルドできる MSBuild を探す。
    /// <c>dotnet build</c> は SDK 形式しか扱えないため、Visual Studio 同梱のものを使う。
    /// </summary>
    private static string FindMsBuild()
    {
        string vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");

        if (File.Exists(vswhere))
        {
            using var process = Process.Start(new ProcessStartInfo(
                vswhere,
                "-latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is not null)
            {
                string found = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string? first = found
                    .Split('\n')
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => line.Length > 0 && File.Exists(line));

                if (first is not null)
                {
                    return first;
                }
            }
        }

        throw new InvalidOperationException(
            "従来形式のプロジェクトをビルドできる MSBuild が見つからない。"
            + "Visual Studio（Microsoft.Component.MSBuild）が必要である。");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} を起動できない。");

        // 片方だけを読み切ろうとすると、もう片方のパイプが埋まって子プロセスが止まる。
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + stderr);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // 後片付けに失敗しても測定結果には影響しない。
        }
    }
}
