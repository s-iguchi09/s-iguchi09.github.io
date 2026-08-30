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
    public IReadOnlyList<string> Verifies =>
    [
        "4 メソッドが BCL 側に存在するターゲットフレームワークを、実際にコンパイルして調べる",
        "Append / Prepend が .NET Framework 4.7.1 以降で使えること（ポリフィルは衝突を起こす）",
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "TakeLast / SkipLast に 0・要素数超過・負数を渡したときの挙動",
        "空の並びに対する TakeLast",
        "NET471_OR_GREATER が SDK 形式のプロジェクトでしか定義されず、従来形式では同じソースが CS0121 になること",
    ];

    private static readonly string[] Frameworks = ["net46", "net47", "net471", "net48", "net10.0"];

    private static readonly LinqBackportParity.Probe[] BclProbes =
    [
        new("Append", "source.Append(1)"),
        new("Prepend", "source.Prepend(0)"),
        new("TakeLast", "source.TakeLast(2)"),
        new("SkipLast", "source.SkipLast(2)"),
    ];

    public string Slug => "linq-backport-netframework-to-net5";

    private const string Sample = """
            private static readonly int[] Numbers = { 1, 2, 3, 4, 5 };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new("Numbers.Append(6)", "Numbers.Append(6)"),
        new("Numbers.Prepend(0)", "Numbers.Prepend(0)"),
        new("Numbers.TakeLast(2)", "Numbers.TakeLast(2)"),
        new("Numbers.TakeLast(0)", "Numbers.TakeLast(0)"),
        new("Numbers.TakeLast(10)", "Numbers.TakeLast(10)"),
        new("Numbers.TakeLast(-1)", "Numbers.TakeLast(-1)"),
        new("Numbers.SkipLast(2)", "Numbers.SkipLast(2)"),
        new("Numbers.SkipLast(0)", "Numbers.SkipLast(0)"),
        new("Numbers.SkipLast(10)", "Numbers.SkipLast(10)"),
        new("Numbers.SkipLast(-1)", "Numbers.SkipLast(-1)"),
        new("empty.TakeLast(2)", "new int[0].TakeLast(2)"),
    ];

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

        await context.SaveTableAsync(
            "available in the BCL without a polyfill? (compiled per target framework)",
            ["method", .. Frameworks],
            await LinqBackportParity.MeasureAvailabilityAsync(Slug, Frameworks, BclProbes),
            "linq-net5-bcl-availability.svg");

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net5-polyfill-parity.svg");

        await context.SaveTableAsync(
            "is NET471_OR_GREATER defined? (same source, two project formats)",
            ["project format", "symbol", "polyfill", "build result"],
            await ProjectFormatProbe.SymbolAvailabilityAsync(),
            "linq-net5-project-format.svg");
    }
}

/// <summary>.NET 6 相当（Chunk / MaxBy / MinBy / DistinctBy）。</summary>
internal sealed class LinqBackportNet6Scene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "Chunk の端数・ちょうど・source より大きい size・不正な size での挙動",
        "MaxBy / MinBy が空の並びに対して返すもの（値型と参照型で異なる）",
        "DistinctBy が残す要素と、その順序",
    ];

    public string Slug => "linq-backport-netframework-to-net6";

    /// <summary>ドライバー側で使う標本。記事の実装例と同じ形の型を用意する。</summary>
    private const string Sample = """
            private sealed class Item
            {
                public string Name;
                public int Price;
                public string Category;

                public override string ToString()
                {
                    return Name;
                }
            }

            private static readonly int[] Numbers = { 1, 2, 3, 4, 5 };

            private static readonly string[] Words = { "pear", "apple", "fig", "apple", "pear" };

            private static readonly Item[] Items =
            {
                new Item { Name = "pen", Price = 120, Category = "stationery" },
                new Item { Name = "mug", Price = 980, Category = "kitchen" },
                new Item { Name = "pad", Price = 340, Category = "stationery" },
                new Item { Name = "cup", Price = 210, Category = "kitchen" },
            };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new("Numbers.Chunk(2)", "Numbers.Chunk(2)"),
        new("Numbers.Chunk(5)", "Numbers.Chunk(5)"),
        new("Numbers.Chunk(10)", "Numbers.Chunk(10)"),
        new("Numbers.Chunk(0)", "Numbers.Chunk(0).ToArray()"),
        new("Items.MaxBy(Price)", "Items.MaxBy(x => x.Price)"),
        new("Items.MinBy(Price)", "Items.MinBy(x => x.Price)"),
        new("empty int[].MaxBy", "new int[0].MaxBy(x => x)"),
        new("empty Item[].MaxBy", "new Item[0].MaxBy(x => x.Price)"),
        new("Items.DistinctBy(Category)", "Items.DistinctBy(x => x.Category)"),
        new("Words.DistinctBy(Length)", "Words.DistinctBy(w => w.Length)"),
    ];

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

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net6-polyfill-parity.svg");
    }
}

/// <summary>.NET 7 相当（Order / OrderDescending）。</summary>
internal sealed class LinqBackportNet7Scene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "戻り値が IOrderedEnumerable であり、ThenBy を連結できること",
        "比較子で等しくなる要素どうしの順序が保たれること（安定ソート）",
    ];

    public string Slug => "linq-backport-netframework-to-net7";

    private const string Sample = """
            private sealed class ByLength : IComparer<string>
            {
                public int Compare(string x, string y)
                {
                    return x.Length.CompareTo(y.Length);
                }
            }

            private static readonly string[] Words = { "pear", "apple", "fig", "kiwi", "date" };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new("Words.Order()", "Words.Order()"),
        new("Words.OrderDescending()", "Words.OrderDescending()"),
        new("Order(OrdinalIgnoreCase)", "Words.Order(StringComparer.OrdinalIgnoreCase)"),
        new("OrderDescending(ByLength)", "Words.OrderDescending(new ByLength())"),
        new("Order(ByLength), stability", "Words.Order(new ByLength())"),
        new("Order().ThenByDescending(len)", "Words.Order().ThenByDescending(w => w.Length)"),
        new("empty.Order()", "new string[0].Order()"),
    ];

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

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net7-polyfill-parity.svg");
    }
}

/// <summary>.NET 8 相当（セレクタ不要の ToDictionary）。</summary>
internal sealed class LinqBackportNet8Scene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "KeyValuePair 版とタプル版の両方、および IEqualityComparer オーバーロード",
        "キーが重複したときに送出される例外の型",
    ];

    public string Slug => "linq-backport-netframework-to-net8";

    // Dictionary の列挙順は規定されないため、比較の前にキーで整列する。
    private const string Sample = """
            private static readonly KeyValuePair<string, int>[] Pairs =
            {
                new KeyValuePair<string, int>("pear", 1),
                new KeyValuePair<string, int>("apple", 2),
                new KeyValuePair<string, int>("fig", 3),
            };

            private static readonly (string Key, int Value)[] Tuples =
            {
                ("pear", 1),
                ("apple", 2),
                ("fig", 3),
            };

            private static readonly KeyValuePair<string, int>[] Duplicated =
            {
                new KeyValuePair<string, int>("pear", 1),
                new KeyValuePair<string, int>("pear", 2),
            };

            private static readonly KeyValuePair<string, int>[] CaseVariants =
            {
                new KeyValuePair<string, int>("pear", 1),
                new KeyValuePair<string, int>("PEAR", 2),
            };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new("Pairs.ToDictionary()", "Pairs.ToDictionary().OrderBy(kv => kv.Key)"),
        new("Tuples.ToDictionary()", "Tuples.ToDictionary().OrderBy(kv => kv.Key)"),
        new("Pairs.ToDictionary(cmp)", "Pairs.ToDictionary(StringComparer.OrdinalIgnoreCase).OrderBy(kv => kv.Key)"),
        new("Tuples.ToDictionary(cmp)", "Tuples.ToDictionary(StringComparer.OrdinalIgnoreCase).OrderBy(kv => kv.Key)"),
        new("duplicate key", "Duplicated.ToDictionary().OrderBy(kv => kv.Key)"),
        new("case variants, ordinal", "CaseVariants.ToDictionary().OrderBy(kv => kv.Key)"),
        new("case variants, ignore case", "CaseVariants.ToDictionary(StringComparer.OrdinalIgnoreCase).OrderBy(kv => kv.Key)"),
        new("empty.ToDictionary()", "new KeyValuePair<string, int>[0].ToDictionary().OrderBy(kv => kv.Key)"),
    ];

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

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net8-polyfill-parity.svg");
    }
}

/// <summary>.NET 9 相当（CountBy / AggregateBy / Index）。</summary>
internal sealed class LinqBackportNet9Scene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "CountBy / AggregateBy が返すキーの順序（最初に現れた順か）",
        "Index が返すタプルの中身と、空の並びに対する結果",
    ];

    public string Slug => "linq-backport-netframework-to-net9";

    private const string Sample = """
            private static readonly string[] Words = { "pear", "fig", "pear", "PEAR" };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new("CountBy(w => w)", "Words.CountBy(w => w)"),
        new("CountBy(w => w.Length)", "Words.CountBy(w => w.Length)"),
        new("CountBy(w => w, ignore case)", "Words.CountBy(w => w, StringComparer.OrdinalIgnoreCase)"),
        new("AggregateBy(len, 0, +1)", "Words.AggregateBy(w => w.Length, 0, (acc, w) => acc + 1)"),
        new("AggregateBy(w, \"\", concat)", "Words.AggregateBy(w => w.Length, string.Empty, (acc, w) => acc + w[0])"),
        new("Index()", "Words.Index()"),
        new("empty.Index()", "new string[0].Index()"),
        new("empty.CountBy()", "new string[0].CountBy(w => w)"),
    ];

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

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net9-polyfill-parity.svg");
    }
}

/// <summary>.NET 10 相当（LeftJoin / RightJoin / Shuffle）。</summary>
internal sealed class LinqBackportNet10Scene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "記事本文のポリフィル実装をそのまま net48 と net10.0 でビルドし、出力が一致するかを確かめる",
        "相手が居ない行に対して LeftJoin / RightJoin が渡す既定値",
        "Shuffle が元の並びの並べ替えになっていること（乱数のため整列して比較する）",
    ];

    public string Slug => "linq-backport-netframework-to-net10";

    private const string Sample = """
            private static readonly (int Id, string Name)[] Left =
            {
                (1, "pear"),
                (2, "apple"),
                (3, "fig"),
            };

            private static readonly (int Id, string Note)[] Right =
            {
                (2, "ripe"),
                (3, "dry"),
                (4, "unknown"),
            };
        """;

    private static readonly LinqBackportParity.Probe[] Probes =
    [
        new(
            "LeftJoin",
            "Left.LeftJoin(Right, l => l.Id, r => r.Id, (l, r) => l.Name + \":\" + (r.Note ?? \"-\"))"),
        new(
            "RightJoin",
            "Left.RightJoin(Right, l => l.Id, r => r.Id, (l, r) => (l.Name ?? \"-\") + \":\" + r.Note)"),
        new(
            "LeftJoin, no match at all",
            "Left.LeftJoin(new (int Id, string Note)[0], l => l.Id, r => r.Id, (l, r) => l.Name + \":\" + (r.Note ?? \"-\"))"),
        new(
            "RightJoin, empty right",
            "Left.RightJoin(new (int Id, string Note)[0], l => l.Id, r => r.Id, (l, r) => (l.Name ?? \"-\") + \":\" + r.Note)"),
        new("Shuffle, sorted back", "Left.Select(l => l.Name).Shuffle().OrderBy(n => n)"),
        new("Shuffle, count", "Left.Shuffle().Count()"),
        new("empty.Shuffle()", "new int[0].Shuffle()"),
    ];

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

        await context.SaveTableAsync(
            "polyfill on net48 vs built-in on net10.0 — same source, same driver",
            ["expression", "net10.0 (built-in)", "net48 (polyfill)", ""],
            await LinqBackportParity.MeasureAsync(Slug, Probes, Sample),
            "linq-net10-polyfill-parity.svg");
    }
}
