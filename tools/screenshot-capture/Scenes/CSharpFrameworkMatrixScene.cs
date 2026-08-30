using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「C# バージョン別 演算子と初期化構文シンタックスシュガー一覧」の図。
///
/// 各構文を <c>net48</c>（.NET Framework 4.8）に対して実際にコンパイルし、
/// 通るか、通らない場合は何の型が不足するかを実測して表にする。
/// 不足する型を自前定義した場合に通るようになるかも、同じ手順で確かめる。
///
/// 「言語機能だけで足りるのか、BCL 側の型が要るのか」は記事の中心的な主張であり、
/// ドキュメントの読解では確かめられない。撮影のたびにコンパイルし直すため、
/// 図と本文が食い違わない。
/// </summary>
internal sealed class CSharpFrameworkMatrixScene : IScene
{
    /// <summary>検証に使うターゲットフレームワーク。</summary>
    private const string TargetFramework = "net48";

    public IReadOnlyList<string> Verifies =>
    [
        "net48 に対して各構文をコンパイルし、通るか・不足する型は何かを確かめる",
        "with 式が IsExternalInit を要するのは対象が init アクセサを持つ場合に限ること",
        "可変な struct と positional record struct の with は IsExternalInit を要さないこと",
        "required が RequiredMemberAttribute 以外に 2 つの型を要すること",
        "不足する型を自前定義すればコンパイルが通ること",
    ];

    public string Slug => "csharp-operators-initialization-syntax-by-version";

    /// <summary>
    /// 表に載せる構文。<paramref name="Polyfill"/> が空でない場合、
    /// 素の状態で失敗したときにそれを足して再度コンパイルする。
    /// </summary>
    private readonly record struct Case(string Syntax, string Code, string Polyfill = "");

    private static readonly Case[] Cases =
    [
        new("??=", "using System.Collections.Generic;\npublic class C { private List<int> _n; public void M(int v) { _n ??= new List<int>(); } }"),
        new("!", "public class C { public int M(string s) { return s!.Length; } }"),
        new("new()", "using System.Collections.Generic;\npublic class C { private readonly List<int> _n = new(); public int M() => _n.Count; }"),
        new("[1, 2, 3]", "public class C { public int[] M() { int[] a = [1, 2, 3]; return a; } }"),
        new("C(string p)", "public class Logger(string path) { public string Path => path; }"),
        new("a[^1]", "public class C { public int M(int[] a) { return a[^1]; } }", IndexRangePolyfill),
        new("a[1..3]", "public class C { public int[] M(int[] a) { return a[1..3]; } }", IndexRangePolyfill),
        new("{ get; init; }", "public class R { public string Name { get; init; } }", IsExternalInitPolyfill),

        // with 式は「対象が init アクセサを持つか」で結果が分かれる。
        // 可変な struct や positional record struct は init を生成しないため、
        // IsExternalInit が無くてもコンパイルできる。区別せずに一括りにしない。
        new(
            "struct s with { }",
            "public struct S { public int X { get; set; } }\npublic class C { public S M(S s) => s with { X = 9 }; }",
            IsExternalInitPolyfill),
        new(
            "record struct s with { }",
            "public record struct S(int X);\npublic class C { public S M(S s) => s with { X = 9 }; }",
            IsExternalInitPolyfill),
        new(
            "record r with { }",
            "public record R(string Name);\npublic class C { public R M(R r) => r with { Name = \"x\" }; }",
            IsExternalInitPolyfill),
        new("required", "public class R { public required string Name { get; init; } }", RequiredPolyfill),
    ];

    private const string IsExternalInitPolyfill = """
        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    private const string RequiredPolyfill = """
        using System;
        namespace System.Runtime.CompilerServices
        {
            internal static class IsExternalInit { }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
            internal sealed class RequiredMemberAttribute : Attribute { }

            [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
            internal sealed class CompilerFeatureRequiredAttribute : Attribute
            {
                public CompilerFeatureRequiredAttribute(string featureName) { FeatureName = featureName; }
                public string FeatureName { get; }
            }
        }
        """;

    private const string IndexRangePolyfill = """
        using System;
        namespace System
        {
            internal readonly struct Index
            {
                private readonly int _value;
                public Index(int value, bool fromEnd = false) { _value = fromEnd ? ~value : value; }
                public int Value => _value < 0 ? ~_value : _value;
                public bool IsFromEnd => _value < 0;
                public int GetOffset(int length) => IsFromEnd ? length - Value : Value;
                public static implicit operator Index(int value) => new Index(value);
            }

            internal readonly struct Range
            {
                public Index Start { get; }
                public Index End { get; }
                public Range(Index start, Index end) { Start = start; End = end; }
                public (int Offset, int Length) GetOffsetAndLength(int length)
                {
                    int start = Start.GetOffset(length);
                    int end = End.GetOffset(length);

                    // 検査を省くと a[3..1] が負の長さを返す。
                    // 標準の System.Range は ArgumentOutOfRangeException を送出するため、そこへ揃える。
                    if ((uint)end > (uint)length || (uint)start > (uint)end)
                    {
                        throw new ArgumentOutOfRangeException(nameof(length));
                    }

                    return (start, end - start);
                }
            }
        }
        namespace System.Runtime.CompilerServices
        {
            internal static class RuntimeHelpers
            {
                public static T[] GetSubArray<T>(T[] array, Range range)
                {
                    (int offset, int length) = range.GetOffsetAndLength(array.Length);
                    var result = new T[length];
                    Array.Copy(array, offset, result, 0, length);
                    return result;
                }
            }
        }
        """;

    public async Task CaptureAsync(SceneContext context)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "csharp-framework-matrix");
        Directory.CreateDirectory(workspace);
        WriteProject(workspace);

        var rows = new List<IReadOnlyList<string>>();
        foreach (Case item in Cases)
        {
            CompileResult bare = await CompileAsync(workspace, item.Code);
            string afterPolyfill;

            if (bare.Succeeded)
            {
                afterPolyfill = "-";
            }
            else if (string.IsNullOrEmpty(item.Polyfill))
            {
                afterPolyfill = "-";
            }
            else
            {
                CompileResult patched = await CompileAsync(workspace, item.Polyfill + "\n" + item.Code);
                afterPolyfill = patched.Succeeded ? "OK" : "NG";
            }

            rows.Add([item.Syntax, bare.Succeeded ? "OK" : "NG", bare.MissingType, afterPolyfill]);
        }

        await context.SaveTableAsync(
            $"compiled against {TargetFramework}, LangVersion=latest",
            ["", TargetFramework, "missing type", "+ polyfill"],
            rows,
            "csharp-net-framework-matrix.svg");
    }

    private readonly record struct CompileResult(bool Succeeded, string MissingType);

    private static void WriteProject(string workspace)
    {
        File.WriteAllText(
            Path.Combine(workspace, "p.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <LangVersion>latest</LangVersion>
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

    /// <summary>
    /// コードを 1 本コンパイルし、成否と、不足していた型の名前を返す。
    /// </summary>
    private static async Task<CompileResult> CompileAsync(string workspace, string code)
    {
        await File.WriteAllTextAsync(Path.Combine(workspace, "C.cs"), code, new UTF8Encoding(false));

        var startInfo = new ProcessStartInfo("dotnet", "build -v q --nologo")
        {
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("dotnet build を起動できない。");

        string output = await process.StandardOutput.ReadToEndAsync();
        output += await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return new CompileResult(true, "-");
        }

        return new CompileResult(false, ExtractMissingType(output));
    }

    /// <summary>
    /// コンパイラが「定義されていない」と報告した型・メンバーのうち、
    /// 最初のものを短い名前で返す。表に収めるため名前空間は落とす。
    /// </summary>
    private static string ExtractMissingType(string buildOutput)
    {
        var names = new List<string>();
        foreach (Match match in Regex.Matches(buildOutput, @"'(System[A-Za-z0-9_.]*)'"))
        {
            string full = match.Groups[1].Value;
            // 'System.Index..ctor' のようなメンバー表記から型名までを取り出す。
            int ctor = full.IndexOf("..ctor", StringComparison.Ordinal);
            if (ctor >= 0)
            {
                full = full[..ctor];
            }

            string shortName = full.Split('.').Last();
            if (shortName.Length > 0 && !names.Contains(shortName))
            {
                names.Add(shortName);
            }
        }

        if (names.Count == 0)
        {
            return "(unknown)";
        }

        // 複数不足する場合は件数を添える。表の幅を抑えるため先頭 1 件だけ名前を出す。
        return names.Count == 1 ? names[0] : $"{names[0]} +{names.Count - 1}";
    }
}
