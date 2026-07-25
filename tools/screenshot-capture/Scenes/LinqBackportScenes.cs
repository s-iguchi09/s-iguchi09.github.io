using System.Windows;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// LINQ バックポート記事群の図。
/// いずれも .NET 10 上で実際にメソッドを評価し、その結果をそのまま描画する。
/// シリーズで図が重複しないよう、各記事が扱うメソッドだけを対象にする。
/// </summary>
internal static class LinqSample
{
    public static readonly int[] Numbers = [1, 2, 3, 4, 5];

    public static readonly string[] Words = ["pear", "apple", "fig", "apple", "pear", "apple"];

    /// <summary>並びの各要素を 1 セルとして描画するための文字列化。</summary>
    public static IReadOnlyList<string> Cells<T>(IEnumerable<T> source) =>
        source.Select(value => value?.ToString() ?? "null").ToList();
}

/// <summary>.NET 5 相当（Append / Prepend / TakeLast / SkipLast）。</summary>
internal sealed class LinqBackportNet5Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net5";

    public async Task CaptureAsync(SceneContext context)
    {
        int[] source = LinqSample.Numbers;

        Window window = DemoLayout.BuildSequenceWindow("Append / Prepend / TakeLast / SkipLast",
        [
            new DemoLayout.Sequence("source", LinqSample.Cells(source)),
            new DemoLayout.Sequence("source.Append(6)", LinqSample.Cells(source.Append(6))),
            new DemoLayout.Sequence("source.Prepend(0)", LinqSample.Cells(source.Prepend(0))),
            new DemoLayout.Sequence("source.TakeLast(2)", LinqSample.Cells(source.TakeLast(2))),
            new DemoLayout.Sequence("source.SkipLast(2)", LinqSample.Cells(source.SkipLast(2))),
        ]);

        await context.ShootAsync(window, "linq-append-prepend-takelast-skiplast.png");
    }
}

/// <summary>.NET 6 相当（Chunk / MaxBy / MinBy / DistinctBy）。</summary>
internal sealed class LinqBackportNet6Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net6";

    public async Task CaptureAsync(SceneContext context)
    {
        Product[] products = [.. SampleData.Products()];

        Window window = DemoLayout.BuildSequenceWindow("Chunk / MaxBy / MinBy / DistinctBy",
        [
            new DemoLayout.Sequence("source", LinqSample.Cells(products.Select(p => p.Name))),
            new DemoLayout.Sequence("source.Chunk(2)",
                products.Chunk(2).Select(chunk => string.Join(", ", chunk.Select(p => p.Name))).ToList()),
            new DemoLayout.Sequence("source.MaxBy(p => p.Price)", [products.MaxBy(p => p.Price)!.Name]),
            new DemoLayout.Sequence("source.MinBy(p => p.Price)", [products.MinBy(p => p.Price)!.Name]),
            new DemoLayout.Sequence("source.DistinctBy(p => p.Category)",
                LinqSample.Cells(products.DistinctBy(p => p.Category).Select(p => p.Name))),
        ]);

        await context.ShootAsync(window, "linq-chunk-maxby-minby-distinctby.png");
    }
}

/// <summary>.NET 7 相当（Order / OrderDescending）。</summary>
internal sealed class LinqBackportNet7Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net7";

    public async Task CaptureAsync(SceneContext context)
    {
        string[] source = ["pear", "apple", "fig"];

        Window window = DemoLayout.BuildSequenceWindow("Order / OrderDescending",
        [
            new DemoLayout.Sequence("source", LinqSample.Cells(source)),
            new DemoLayout.Sequence("source.OrderBy(x => x)", LinqSample.Cells(source.OrderBy(x => x))),
            new DemoLayout.Sequence("source.Order()", LinqSample.Cells(source.Order())),
            new DemoLayout.Sequence("source.OrderDescending()", LinqSample.Cells(source.OrderDescending())),
        ]);

        await context.ShootAsync(window, "linq-order-orderdescending.png");
    }
}

/// <summary>.NET 8 相当（セレクタ不要の ToDictionary）。</summary>
internal sealed class LinqBackportNet8Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net8";

    public async Task CaptureAsync(SceneContext context)
    {
        KeyValuePair<string, int>[] pairs = [new("a", 1), new("b", 2)];
        (string Key, int Value)[] tuples = [("a", 1), ("b", 2)];

        Window window = DemoLayout.BuildSequenceWindow("ToDictionary without selectors",
        [
            new DemoLayout.Sequence("KeyValuePair<string,int>[]",
                pairs.Select(p => $"[{p.Key}, {p.Value}]").ToList()),
            new DemoLayout.Sequence(".ToDictionary()",
                pairs.ToDictionary().Select(p => $"{p.Key} => {p.Value}").ToList()),
            new DemoLayout.Sequence("(string, int)[]",
                tuples.Select(t => $"({t.Key}, {t.Value})").ToList()),
            new DemoLayout.Sequence(".ToDictionary()",
                tuples.ToDictionary().Select(p => $"{p.Key} => {p.Value}").ToList()),
        ]);

        await context.ShootAsync(window, "linq-todictionary-without-selectors.png");
    }
}

/// <summary>.NET 9 相当（CountBy / AggregateBy / Index）。</summary>
internal sealed class LinqBackportNet9Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net9";

    public async Task CaptureAsync(SceneContext context)
    {
        string[] source = LinqSample.Words;

        Window window = DemoLayout.BuildSequenceWindow("CountBy / AggregateBy / Index",
        [
            new DemoLayout.Sequence("source", LinqSample.Cells(source)),
            new DemoLayout.Sequence("source.CountBy(x => x)",
                source.CountBy(x => x).Select(p => $"{p.Key} => {p.Value}").ToList()),
            new DemoLayout.Sequence("source.AggregateBy(x => x, 0, (a, x) => a + x.Length)",
                source.AggregateBy(x => x, 0, (acc, item) => acc + item.Length)
                      .Select(p => $"{p.Key} => {p.Value}").ToList()),
            new DemoLayout.Sequence("source.Index()",
                source.Index().Take(3).Select(p => $"({p.Index}, {p.Item})").ToList()),
        ]);

        await context.ShootAsync(window, "linq-countby-aggregateby-index.png");
    }
}

/// <summary>.NET 10 相当（LeftJoin / RightJoin / Shuffle）。</summary>
internal sealed class LinqBackportNet10Scene : IScene
{
    public string Slug => "linq-backport-netframework-to-net10";

    public async Task CaptureAsync(SceneContext context)
    {
        (int Id, string Name)[] left = [(1, "a"), (2, "b"), (3, "c")];
        (int Id, string Tag)[] right = [(2, "x"), (3, "y"), (4, "z")];

        var leftJoin = left
            .LeftJoin(right, l => l.Id, r => r.Id, (l, r) => $"{l.Name}-{(r == default ? "null" : r.Tag)}")
            .ToList();

        var rightJoin = left
            .RightJoin(right, l => l.Id, r => r.Id, (l, r) => $"{(l == default ? "null" : l.Name)}-{r.Tag}")
            .ToList();

        Window window = DemoLayout.BuildSequenceWindow("LeftJoin / RightJoin / Shuffle",
        [
            new DemoLayout.Sequence("left", left.Select(l => $"({l.Id}, {l.Name})").ToList()),
            new DemoLayout.Sequence("right", right.Select(r => $"({r.Id}, {r.Tag})").ToList()),
            new DemoLayout.Sequence("left.LeftJoin(right, ...)", leftJoin),
            new DemoLayout.Sequence("left.RightJoin(right, ...)", rightJoin),
            new DemoLayout.Sequence("source.Shuffle()", LinqSample.Cells(LinqSample.Numbers.Shuffle())),
        ]);

        await context.ShootAsync(window, "linq-leftjoin-rightjoin-shuffle.png");
    }
}
