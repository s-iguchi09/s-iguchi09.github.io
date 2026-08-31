using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「C# でエクスプローラー風の並び順を実装する（StrCmpLogicalW と IComparer）」の図。
/// 同じ文字列リストを既定の比較と StrCmpLogicalW で並べ替え、結果を並べて取得する。
/// </summary>
internal sealed class NaturalSortScene : IScene
{
    /// <summary>記事の再現コードと同じ入力。</summary>
    private static readonly string[] Source = ["item10", "item2", "item1", "item20", "item3"];

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    public IReadOnlyList<string> Verifies =>
    [
        "同じ入力を比較器ごとに並べ替え、結果の並びを比べる",
        "序数比較でもカルチャ比較でも item10 が item2 より前になること",
        "StrCmpLogicalW が数字をまとめて数値として比較すること",
        "StrCmpLogicalW が大文字小文字を区別しないこと",
    ];

    public string Slug => "csharp-natural-sort-strcmplogicalw-icomparer";

    public async Task CaptureAsync(SceneContext context)
    {
        var ordinal = Source.ToList();
        ordinal.Sort();

        var logical = Source.ToList();
        logical.Sort(StrCmpLogicalW);

        Window window = DemoLayout.BuildPanelWindow(
            "Natural sort",
            [
                new DemoLayout.Panel("List<string>.Sort()", BuildList(ordinal)),
                new DemoLayout.Panel("StrCmpLogicalW", BuildList(logical)),
            ]);

        await context.ShootAsync(window, "natural-sort-comparison.png");

        await context.SaveTableAsync(
            "same input sorted by each comparer",
            ["comparer", "resulting order"],
            FormatAndSortMeasurements.SortOrders(),
            "natural-sort-orders.svg");

        await context.SaveTableAsync(
            "comparison result per pair",
            ["pair", "StrCmpLogicalW", "CompareOrdinal"],
            FormatAndSortMeasurements.LogicalComparisons(),
            "natural-sort-pairs.svg");
    }

    private static UIElement BuildList(IEnumerable<string> items) => new ListBox
    {
        ItemsSource = items,
        Width = 150,
        Height = 132,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
}
