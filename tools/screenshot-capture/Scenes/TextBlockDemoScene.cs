using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TextBlock」（apps/wpf-standard-control-demo/textblock.md と日本語版）の記述を実測する。
///
/// レイアウトはウィンドウを作らずに Measure / Arrange で行い、TextBlock の大きさから行数を読む。
/// 大きさはフォントと表示スケールで変わるため、本文には比と行数を書き、値は図に持たせる。
/// </summary>
internal sealed class TextBlockDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-textblock";

    public string ImageDirectory => DemoProbe.ImageDirectory("textblock");

    public IReadOnlyList<string> Verifies =>
    [
        "TextBlock の基底クラスと、Focusable・Padding・TextWrapping・TextTrimming・LineStackingStrategy・LineHeight の既定値",
        "Inlines（Run と Bold）を持つ TextBlock の Text の値（レイアウトの前と後）と、そのあと Text を設定したときの例外の有無と Inlines の数",
        "Inlines の中の Run の Text をバインドしたときの値と、ソースの変更への追従",
        "1 語で幅 100 を超える語を含む文を幅 100 で NoWrap / Wrap / WrapWithOverflow にしたときの高さ（行数）と、幅 100 を超えてレイアウトで切り抜かれるか",
        "TextTrimming を設定した TextBlock の幅が、幅 100 の Grid と横の StackPanel（幅の制限なし）でどうなるか",
        "Padding 10 で TextBlock の大きさが増える量",
        "デモアプリと同じ 4 行の文字で、LineHeight（未設定・5・30）と LineStackingStrategy（BlockLineHeight・MaxHeight）ごとの高さ",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "TextBlock: text and inlines, wrapping, trimming, padding",
            ["case", "measured"],
            Measure(),
            "textblock-behavior.svg");

        await context.SaveTableAsync(
            "TextBlock: height of the demo's four lines by LineHeight and LineStackingStrategy",
            ["LineHeight", "BlockLineHeight", "MaxHeight"],
            LineHeightRows(),
            "textblock-lineheight.svg");
        await Task.CompletedTask;
    }

    private static TextBlock Laid(TextBlock text, double width = 100)
    {
        Layout(new Grid { Children = { text } }, width, 1000);
        return text;
    }

    private static List<IReadOnlyList<string>> Measure()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new TextBlock();
        rows.Add(["base class / Focusable / Padding / TextWrapping, TextTrimming",
            $"{typeof(TextBlock).BaseType!.Name} / {defaults.Focusable} / {defaults.Padding} / {defaults.TextWrapping}, {defaults.TextTrimming}"]);

        {
            var text = new TextBlock();
            text.Inlines.Add(new Run("Hello "));
            text.Inlines.Add(new Bold(new Run("world")));
            string before = text.Text;
            Laid(text, 300);
            string laidOut = text.Text;
            string thrown = Throws(() => text.Text = "Replaced");
            rows.Add(["Inlines \"Hello \" + Bold \"world\": Text before / after layout",
                $"\"{before}\" / \"{laidOut}\""]);
            rows.Add(["  then Text = \"Replaced\": exception / Inlines count", $"{thrown} / {text.Inlines.Count}"]);
        }

        {
            // 書式付きの並びの中の Run の Text をバインドする。
            var source = new TextBox { Text = "bound" };
            var run = new Run();
            BindingOperations.SetBinding(run, Run.TextProperty, new Binding(nameof(TextBox.Text)) { Source = source });
            var text = new TextBlock();
            text.Inlines.Add(new Bold(new Run("Name: ")));
            text.Inlines.Add(run);
            Laid(text, 300);
            string first = run.Text;
            source.Text = "changed";
            rows.Add(["Run with bound Text after Bold \"Name: \": Run.Text / after change",
                $"\"{first}\" / \"{run.Text}\""]);
        }

        {
            const string sentence = "Say Supercalifragilisticexpialidocious now";
            var single = Laid(new TextBlock { Text = "Visit", VerticalAlignment = VerticalAlignment.Top });
            double line = single.ActualHeight;
            var heights = new List<string>();
            foreach (TextWrapping wrapping in new[] { TextWrapping.NoWrap, TextWrapping.Wrap, TextWrapping.WrapWithOverflow })
            {
                var text = Laid(new TextBlock { Text = sentence, TextWrapping = wrapping, VerticalAlignment = VerticalAlignment.Top });
                Geometry? clip = LayoutInformation.GetLayoutClip(text);
                heights.Add($"{wrapping} {Math.Round(text.ActualHeight / line)}{(clip is null ? "" : ", clipped")}");
            }

            rows.Add([$"\"{sentence}\", width 100: lines", string.Join(" / ", heights)]);
        }

        {
            const string longText = "A fairly long piece of text that does not fit";
            var inGrid = Laid(new TextBlock { Text = longText, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Top });
            var free = new TextBlock { Text = longText, TextTrimming = TextTrimming.CharacterEllipsis };
            Layout(new StackPanel { Orientation = Orientation.Horizontal, Children = { free } }, 100, 100);
            rows.Add(["CharacterEllipsis: width in Grid 100 / horizontal StackPanel 100",
                $"{D(inGrid.ActualWidth)} / {D(free.ActualWidth)}"]);
        }

        {
            var plain = Laid(new TextBlock { Text = "TEXTBLOCK", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }, 300);
            var padded = Laid(new TextBlock { Text = "TEXTBLOCK", Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }, 300);
            rows.Add(["Padding=10: size without / with",
                $"{D(plain.ActualWidth)} x {D(plain.ActualHeight)} / {D(padded.ActualWidth)} x {D(padded.ActualHeight)}"]);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> LineHeightRows()
    {
        var rows = new List<IReadOnlyList<string>>();
        // デモアプリの LineHeight 欄と同じ 4 行の文字。
        const string text = "TEXTBLOCK\nTEXTBLOCK\nTEXTBLOCK\nTEXTBLOCK";
        foreach (double lineHeight in new[] { double.NaN, 5, 30 })
        {
            var cells = new List<string> { double.IsNaN(lineHeight) ? "not set (NaN)" : D(lineHeight) + (lineHeight == 5 ? " (demo start)" : "") };
            foreach (LineStackingStrategy strategy in new[] { LineStackingStrategy.BlockLineHeight, LineStackingStrategy.MaxHeight })
            {
                var block = Laid(new TextBlock { Text = text, LineHeight = lineHeight, LineStackingStrategy = strategy, VerticalAlignment = VerticalAlignment.Top }, 300);
                cells.Add(D(block.ActualHeight));
            }

            rows.Add(cells);
        }

        return rows;
    }
}
