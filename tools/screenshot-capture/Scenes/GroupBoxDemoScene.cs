using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「GroupBox」（apps/wpf-standard-control-demo/groupbox.md と日本語版）の記述を実測する。
///
/// 見出しの表示は、見出しの ContentPresenter の中の TextBlock / AccessText の文字と大きさで読む。
/// アクセスキーは AccessKeyManager.ProcessKey（Alt と文字キーを押したときに呼ばれる処理）で送る。
/// </summary>
internal sealed class GroupBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-groupbox";

    public string ImageDirectory => DemoProbe.ImageDirectory("groupbox");

    public IReadOnlyList<string> Verifies =>
    [
        "GroupBox の基底クラスと、BorderBrush / BorderThickness / Padding の既定値",
        "Header が null のときの HasHeader と見出しの領域の高さ",
        "HeaderStringFormat が文字列の Header と要素の Header に効くか（デモアプリと同じ \"Header={0}.\"）",
        "改行を含む文字列の Header が何行で表示されるか",
        "Padding 0 と 20 での内容の位置",
        "FontSize・Foreground を GroupBox に設定したときの見出しと内容の文字、Background がどこに塗られ、内容がその範囲に入るか",
        "UI オートメーションでのコントロールの種類と名前",
        "アンダースコア付きの Header のアクセスキーを押したときのフォーカスの移動先",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "GroupBox: header, padding, inherited properties and access key",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureAsync(),
            "groupbox-behavior.svg");
    }

    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static ContentPresenter HeaderPresenter(GroupBox box) =>
        Descendants(box).OfType<ContentPresenter>().First(p => p.ContentSource == "Header");

    private static string HeaderText(GroupBox box)
    {
        DependencyObject? text = Descendants(HeaderPresenter(box)).FirstOrDefault(d => d is TextBlock or AccessText);
        return text switch
        {
            AccessText access => access.Text,
            TextBlock block => block.Text,
            _ => "(no text)",
        };
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            var box = new GroupBox { Header = "GroupBox", Content = "CONTENT", Width = 200 };
            await ShowAsync(box, async () =>
            {
                rows.Add([T("base class / default BorderBrush, BorderThickness, Padding", "基底クラス / BorderBrush、BorderThickness、Padding の既定値"),
                    $"{typeof(GroupBox).BaseType!.Name} / {box.BorderBrush}, {box.BorderThickness}, {box.Padding}"]);
                await Task.CompletedTask;
            });
        }

        foreach (object? header in new object?[] { "GroupBox", null })
        {
            var box = new GroupBox { Header = header, Content = "CONTENT", Width = 200 };
            await ShowAsync(box, async () =>
            {
                rows.Add([T($"Header={WpfProbe.Describe(header)}: HasHeader / header area height", $"Header={WpfProbe.Describe(header)}: HasHeader / 見出しの領域の高さ"),
                    $"{box.HasHeader} / {D(HeaderPresenter(box).ActualHeight)}"]);
                await Task.CompletedTask;
            });
        }

        foreach (object header in new object[] { "HEADER", new TextBlock { Text = "HEADER" } })
        {
            var box = new GroupBox { Header = header, HeaderStringFormat = "Header={0}.", Content = "CONTENT", Width = 200 };
            await ShowAsync(box, async () =>
            {
                rows.Add([T($"HeaderStringFormat=\"Header={{0}}.\", {header.GetType().Name} header: text", $"HeaderStringFormat=\"Header={{0}}.\"、{header.GetType().Name} の見出し: 文字列"),
                    HeaderText(box)]);
                await Task.CompletedTask;
            });
        }

        foreach (string header in new[] { "Line 1", "Line 1\nLine 2" })
        {
            var box = new GroupBox { Header = header, Content = "CONTENT", Width = 200 };
            await ShowAsync(box, async () =>
            {
                rows.Add([T($"string Header {(header.Contains('\n') ? "with a line break" : "on one line")}: header height", $"文字列の Header（{(header.Contains('\n') ? "改行あり" : "1 行")}）: 見出しの高さ"),
                    D(HeaderPresenter(box).ActualHeight)]);
                await Task.CompletedTask;
            });
        }

        foreach (double padding in new[] { 0d, 20d })
        {
            var content = new Border { Width = 80, Height = 30 };
            var box = new GroupBox { Header = "GroupBox", Content = content, Width = 200, Padding = new Thickness(padding) };
            await ShowAsync(box, async () =>
            {
                rows.Add([T($"Padding={D(padding)}: content position", $"Padding={D(padding)}: 内容の位置"), Format(Bounds(content, box))]);
                await Task.CompletedTask;
            });
        }

        {
            var headerText = new TextBlock { Text = "GroupBox" };
            var contentText = new TextBlock { Text = "CONTENT" };
            var inner = new Border { Child = contentText };
            var box = new GroupBox
            {
                Header = headerText,
                Content = inner,
                Width = 200,
                FontSize = 20,
                Foreground = Brushes.Red,
                Background = Brushes.LightYellow,
            };
            await ShowAsync(box, async () =>
            {
                rows.Add([T("FontSize=20, Foreground=Red: header text / content text", "FontSize=20、Foreground=Red: 見出しの文字 / 内容の文字"),
                    $"{D(headerText.FontSize)}, {headerText.Foreground} / {D(contentText.FontSize)}, {contentText.Foreground}"]);

                // Background を描いている要素と、その範囲が見出しを含むか。
                var painted = Descendants(box).OfType<Border>().First(b => ReferenceEquals(b.Background, box.Background));
                Rect area = Bounds(painted, box);
                Rect header = Bounds(HeaderPresenter(box), box);
                Rect body = Bounds(inner, box);
                rows.Add([T("Background: painted area / header area / content inside the painted area", "Background: 塗られる領域 / 見出しの領域 / 内容が塗られる領域の中にあるか"),
                    $"{Format(area)} / {Format(header)} / {area.Contains(body)}"]);
                await Task.CompletedTask;
            });
        }

        {
            var box = new GroupBox { Header = "Personal information", Content = new TextBox(), Width = 200 };
            await ShowAsync(box, async () =>
            {
                AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(box);
                rows.Add([T("UI Automation: control type / name", "UI オートメーション: コントロールの種類 / 名前"), $"{peer.GetAutomationControlType()} / {peer.GetName()}"]);
                await Task.CompletedTask;
            });
        }

        {
            var before = new Button { Content = "Before" };
            var first = new TextBox();
            var second = new TextBox();
            var box = new GroupBox { Header = "_Name", Content = new StackPanel { Children = { first, second } }, Width = 200 };
            var panel = new StackPanel { Children = { before, box } };
            await ShowAsync(panel, async () =>
            {
                await FocusAsync(before);
                bool recognizes = HeaderPresenter(box).RecognizesAccessKey;
                AccessKeyManager.ProcessKey(null, "N", false);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                Loc focused = ReferenceEquals(Keyboard.FocusedElement, first) ? T("first TextBox in the content", "内容の最初の TextBox")
                    : Keyboard.FocusedElement?.GetType().Name ?? "null";
                rows.Add([T("Header=\"_Name\": access keys recognized / focus after access key N", "Header=\"_Name\": アクセスキーを認識するか / アクセスキー N の後のフォーカス"),
                    T($"{recognizes} / {focused.En}", $"{recognizes} / {focused.Ja}")]);
            }, activate: true);
        }

        return rows;
    }
}
