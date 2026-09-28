using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「C# 14 の extension ブロックで静的クラスを対象にしたときの制限」の図。
///
/// レシーバーの書き方とメンバーの種類を組み合わせて実際にコンパイルし、
/// 通るか、通らない場合はどのエラーコードになるかを記録する。
/// エラーコードは取り違えやすいため、撮影のたびにコンパイルし直す。
/// </summary>
internal sealed class ExtensionReceiverMatrixScene : IScene
{
    private const string TargetFramework = "net10.0";

    private const string LanguageVersion = "14.0";

    public IReadOnlyList<string> Verifies =>
    [
        "レシーバーの書き方とメンバーの種類の全組み合わせをコンパイルする",
        "extension(Directory) + 静的メンバーが通り、インスタンスメンバーが CS9303 になること",
        "extension(Directory directory) がメンバーの種類によらず CS0721 になること",
        "静的でない型なら名前付きレシーバーにインスタンスメンバーを置けること",
        "記事の DeleteIfExists の形でも、extension(Directory) の静的メンバーが通り、インスタンスメンバーが CS9303、extension(Directory dir) が CS0721 になること",
    ];

    public string Slug => "csharp14-extension-members-static-class-limitation";

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "extension-receiver-matrix");
        Directory.CreateDirectory(workspace);
        WriteProject(workspace);

        Loc staticMember = Loc.Of("static member", "静的メンバー");
        Loc instanceMember = Loc.Of("instance member", "インスタンスメンバー");
        (string Receiver, Loc Member, string Body)[] cases =
        [
            ("extension(Directory)", staticMember, StaticOnTypeOnly),
            ("extension(Directory)", instanceMember, InstanceOnTypeOnly),
            ("extension(Directory directory)", staticMember, StaticOnNamedReceiver),
            ("extension(Directory directory)", instanceMember, InstanceOnNamedReceiver),
            ("extension(DirectoryInfo info)", instanceMember, InstanceOnInstanceType),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string receiver, Loc member, string body) in cases)
        {
            string result = await CompileAsync(workspace, body);
            rows.Add([receiver, member, result == Compiles ? Loc.Of("compiles", "コンパイルが通る") : result]);
        }

        await context.SaveTableAsync(
            $"extension block, {TargetFramework}, LangVersion={LanguageVersion}",
            [Loc.Of("receiver", "レシーバー"), Loc.Of("member kind", "メンバーの種類"), Loc.Of("result", "結果")],
            rows,
            "extension-receiver-matrix.svg");

        // 記事の冒頭の表。記事のコード例と同じ DeleteIfExists の形でコンパイルし、呼び出し方か失敗の理由を添える。
        (string Receiver, string Member, string Body)[] forms =
        [
            ("extension(Directory)", "public static void DeleteIfExists(string path)", DeleteIfExistsStatic),
            ("extension(Directory)", "public void DeleteIfExists()", DeleteIfExistsInstance),
            ("extension(Directory dir)", "public static void DeleteIfExists(string path)", DeleteIfExistsNamedReceiver),
        ];

        var formRows = new List<IReadOnlyList<Loc>>();
        foreach ((string receiver, string member, string body) in forms)
        {
            string result = await CompileAsync(workspace, body);
            formRows.Add([
                receiver,
                member,
                result == Compiles ? Loc.Of("✓ compiles", "✓ コンパイルが通る") : $"✕ {result}",
                result switch
                {
                    Compiles => "Directory.DeleteIfExists(path)",
                    "CS9303" => Loc.Of("instance member needs a named receiver parameter", "インスタンスメンバーには名前付きのレシーバーが要る"),
                    "CS0721" => Loc.Of("static types cannot be used as parameters", "静的な型は引数に使えない"),
                    _ => result,
                },
            ]);
        }

        await context.SaveTableAsync(
            $"extension block on a static class, {TargetFramework}, LangVersion={LanguageVersion}",
            [Loc.Of("receiver", "レシーバー"), Loc.Of("member declaration", "メンバーの宣言"), Loc.Of("result", "結果"), Loc.Of("call or reason", "呼び出し方・理由")],
            formRows,
            "extension-receiver-form-matrix.svg");
    }

    private const string Compiles = "compiles";

    /// <summary>記事と同じ、型だけを書いたレシーバーの静的な DeleteIfExists。</summary>
    private const string DeleteIfExistsStatic = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory)
            {
                public static void DeleteIfExists(string path)
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }
            }
        }
        """;

    /// <summary>型だけを書いたレシーバーに、インスタンスの DeleteIfExists を置く。</summary>
    private const string DeleteIfExistsInstance = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory)
            {
                public void DeleteIfExists() { }
            }
        }
        """;

    /// <summary>名前付きのレシーバーに、静的な DeleteIfExists を置く。</summary>
    private const string DeleteIfExistsNamedReceiver = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory dir)
            {
                public static void DeleteIfExists(string path)
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }
            }
        }
        """;

    /// <summary>型だけを書いたレシーバーに静的メンバーを置く。</summary>
    private const string StaticOnTypeOnly = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory)
            {
                public static bool IsEmpty(string path) => Directory.GetFileSystemEntries(path).Length == 0;
            }
        }
        """;

    /// <summary>型だけを書いたレシーバーにインスタンスメンバーを置く。</summary>
    private const string InstanceOnTypeOnly = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory)
            {
                public bool IsEmpty(string path) => Directory.GetFileSystemEntries(path).Length == 0;
            }
        }
        """;

    /// <summary>パラメーター名まで書いたレシーバーに静的メンバーを置く。</summary>
    private const string StaticOnNamedReceiver = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory directory)
            {
                public static bool IsEmpty(string path) => Directory.GetFileSystemEntries(path).Length == 0;
            }
        }
        """;

    /// <summary>パラメーター名まで書いたレシーバーにインスタンスメンバーを置く。</summary>
    private const string InstanceOnNamedReceiver = """
        using System.IO;

        public static class DirectoryExtensions
        {
            extension(Directory directory)
            {
                public bool IsEmpty => true;
            }
        }
        """;

    /// <summary>対照。静的でない型なら、名前付きレシーバーにインスタンスメンバーを置ける。</summary>
    private const string InstanceOnInstanceType = """
        using System.IO;
        using System.Linq;

        public static class DirectoryInfoExtensions
        {
            extension(DirectoryInfo info)
            {
                public bool IsEmpty => !info.EnumerateFileSystemInfos().Any();
            }
        }
        """;

    private static void WriteProject(string workspace)
    {
        File.WriteAllText(
            Path.Combine(workspace, "p.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <LangVersion>{LanguageVersion}</LangVersion>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="C.cs" />
              </ItemGroup>
            </Project>
            """,
            new UTF8Encoding(false));
    }

    /// <summary>コードを 1 本コンパイルし、成否とエラーコードを返す。</summary>
    private static async Task<string> CompileAsync(string workspace, string code)
    {
        await File.WriteAllTextAsync(Path.Combine(workspace, "C.cs"), code, new UTF8Encoding(false));

        using var process = Process.Start(new ProcessStartInfo("dotnet", "build -v q --nologo")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("dotnet build を起動できない。");

        string output = await process.StandardOutput.ReadToEndAsync();
        output += await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return Compiles;
        }

        var codes = Regex.Matches(output, @"error (CS\d+)")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        return codes.Count == 0 ? "(failed)" : string.Join(", ", codes);
    }
}
