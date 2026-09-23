using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Label」（apps/wpf-standard-control-demo/label.html と日本語版）の記述を実測する。
///
/// アクセスキーは AccessKeyManager.ProcessKey（Alt と文字キーを押したときに呼ばれる処理）で送り、移ったフォーカスを読む。
/// </summary>
internal sealed class LabelDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-label";

    public string ImageDirectory => DemoProbe.ImageDirectory("label");

    public IReadOnlyList<string> Verifies =>
    [
        "Label の基底クラスと、Focusable・IsTabStop・Padding・HorizontalContentAlignment・VerticalContentAlignment の既定値",
        "Tab キーでフォーカスが Label に止まるか",
        "デモアプリの Target の欄（_Name / _Age）で、アクセスキー N と A を押したときのフォーカスの移動先",
        "Target を設定しない Label のアクセスキーを押したときのフォーカス",
        "ToolBar（別のフォーカススコープ）の中の TextBox を Target にしたときのフォーカスの移動先",
        "Target を設定したときの、TextBox の UI オートメーションの名前と LabeledBy",
        "改行を含む文字列の Content の行数（高さ）",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Label: defaults, access keys, Target and UI Automation",
            ["case", "measured"],
            await MeasureAsync(),
            "label-behavior.svg");
    }

    private static string Focused(params (string Name, IInputElement Element)[] candidates)
    {
        IInputElement? focused = Keyboard.FocusedElement;
        foreach ((string name, IInputElement element) in candidates)
        {
            if (ReferenceEquals(focused, element))
            {
                return name;
            }
        }

        return focused?.GetType().Name ?? "null";
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new Label();
        rows.Add(["base class / Focusable, IsTabStop / Padding / content alignment",
            $"{typeof(Label).BaseType!.Name} / {defaults.Focusable}, {defaults.IsTabStop} / (padding below) / {defaults.HorizontalContentAlignment}, {defaults.VerticalContentAlignment}"]);

        {
            var label = new Label { Content = "Label" };
            var before = new TextBox();
            var after = new TextBox();
            var panel = new StackPanel { Width = 200, Children = { before, label, after } };
            await ShowAsync(panel, async () =>
            {
                rows[0] = [rows[0][0], rows[0][1].Replace("(padding below)", label.Padding.ToString())];
                await FocusAsync(before);
                SendKey(Key.Tab);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                rows.Add(["TextBox, Label, TextBox: Tab from the first TextBox goes to",
                    Focused(("the Label", label), ("the second TextBox", after))]);
            }, activate: true);
        }

        {
            // デモアプリの Target の欄と同じ 2 組。
            var nameBox = new TextBox();
            var ageBox = new TextBox();
            var nameLabel = new Label { Content = "_Name(Press Alt+N)", Target = nameBox };
            var ageLabel = new Label { Content = "_Age(Press Alt+A)", Target = ageBox };
            var other = new Button { Content = "Other" };
            var panel = new StackPanel { Width = 250, Children = { nameLabel, nameBox, ageLabel, ageBox, other } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                await FocusAsync(other);
                AccessKeyManager.ProcessKey(null, "N", false);
                await Capture.SettleAsync(window, 50);
                string afterN = Focused(("Name TextBox", nameBox), ("Age TextBox", ageBox), ("Other", other));
                AccessKeyManager.ProcessKey(null, "A", false);
                await Capture.SettleAsync(window, 50);
                string afterA = Focused(("Name TextBox", nameBox), ("Age TextBox", ageBox), ("Other", other));
                rows.Add(["demo Target labels: focus after access key N / A", $"{afterN} / {afterA}"]);

                AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(nameBox);
                AutomationPeer? labeledBy = peer.GetLabeledBy();
                rows.Add(["  Name TextBox in UI Automation: name / LabeledBy",
                    $"{WpfProbe.Describe(peer.GetName())} / {(labeledBy is null ? "none" : labeledBy.GetName())}"]);
            }, activate: true);
        }

        {
            var box = new TextBox();
            var label = new Label { Content = "_Plain" };
            var other = new Button { Content = "Other" };
            var panel = new StackPanel { Width = 200, Children = { label, box, other } };
            await ShowAsync(panel, async () =>
            {
                await FocusAsync(other);
                AccessKeyManager.ProcessKey(null, "P", false);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                rows.Add(["Label \"_Plain\" without Target: focus after access key P",
                    Focused(("the Label", label), ("the TextBox", box), ("Other (unchanged)", other))]);
            }, activate: true);
        }

        {
            var inToolBar = new TextBox { Width = 80 };
            var toolBar = new ToolBar { Items = { inToolBar } };
            var label = new Label { Content = "_Search", Target = inToolBar };
            var other = new Button { Content = "Other" };
            var panel = new StackPanel { Width = 250, Children = { toolBar, label, other } };
            await ShowAsync(panel, async () =>
            {
                await FocusAsync(other);
                AccessKeyManager.ProcessKey(null, "S", false);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                rows.Add(["Target is a TextBox inside a ToolBar (own focus scope): focus after access key S",
                    Focused(("the TextBox in the ToolBar", inToolBar), ("Other (unchanged)", other))]);
            }, activate: true);
        }

        foreach (string content in new[] { "CONTENT", "CONTENT\nLINE 2" })
        {
            var label = new Label { Content = content, VerticalAlignment = VerticalAlignment.Top };
            Layout(new Grid { Children = { label } }, 200, 200);
            rows.Add([$"string Content {(content.Contains('\n') ? "with a line break" : "on one line")}: height", D(label.ActualHeight)]);
        }

        return rows;
    }
}
