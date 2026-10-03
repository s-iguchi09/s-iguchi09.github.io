using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Label」（apps/wpf-standard-control-demo/label.md と日本語版）の記述を実測する。
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
        "Target を設定したときの、TextBox の UI オートメーションの名前と LabeledBy（公式ドキュメントは名前になると説明している）と、AutomationProperties.LabeledBy を手動で設定したとき。スクリーンリーダーも使う UI オートメーションのクライアント API で、UI スレッドとは別のスレッドから読む",
        "改行を含む文字列の Content の行数（高さ）",
        "ComboBox・編集可能な ComboBox・DatePicker を Target にした Label のアクセスキーで、フォーカスが移る要素",
    ];

    /// <summary>
    /// スクリーンリーダーも使う UI オートメーションのクライアント API で、要素の名前と LabeledBy を読む（同じプロセスの、UI スレッドとは別のスレッドから）。
    /// WPF のオートメーションピアを直接呼ぶと、手動で設定した LabeledBy も空になり、実際に読み上げられる内容と食い違うため。
    /// UI スレッドを止めないよう、呼び出し側は Task.Run（MTA のスレッド）から呼ぶ。
    /// </summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static Loc ReadThroughClient(IntPtr hwnd, string automationId)
    {
        AutomationElement root = AutomationElement.FromHandle(hwnd);
        AutomationElement? element = root.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        if (element is null)
        {
            return "not found";
        }

        var labeledBy = element.GetCurrentPropertyValue(AutomationElement.LabeledByProperty) as AutomationElement;
        string name = WpfProbe.Describe(element.Current.Name);
        return labeledBy is null ? T($"{name} / none", $"{name} / なし") : $"{name} / {WpfProbe.Describe(labeledBy.Current.Name)}";
    }

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Label: defaults, access keys, Target and UI Automation",
            [T("case", "条件"), T("measured", "計測値")],
            await MeasureAsync(),
            "label-behavior.svg");
    }

    private static Loc Focused(params (Loc Name, IInputElement Element)[] candidates)
    {
        IInputElement? focused = Keyboard.FocusedElement;
        foreach ((Loc name, IInputElement element) in candidates)
        {
            if (ReferenceEquals(focused, element))
            {
                return name;
            }
        }

        return focused?.GetType().Name ?? "null";
    }

    private static async Task<List<IReadOnlyList<Loc>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        var defaults = new Label();
        rows.Add([T("base class / Focusable, IsTabStop / Padding / content alignment", "基底クラス / Focusable、IsTabStop / Padding / 内容の配置"),
            T($"{typeof(Label).BaseType!.Name} / {defaults.Focusable}, {defaults.IsTabStop} / (padding below) / {defaults.HorizontalContentAlignment}, {defaults.VerticalContentAlignment}",
              $"{typeof(Label).BaseType!.Name} / {defaults.Focusable}, {defaults.IsTabStop} / (padding below) / {defaults.HorizontalContentAlignment}, {defaults.VerticalContentAlignment}")]);

        {
            var label = new Label { Content = "Label" };
            var before = new TextBox();
            var after = new TextBox();
            var panel = new StackPanel { Width = 200, Children = { before, label, after } };
            await ShowAsync(panel, async () =>
            {
                string padding = label.Padding.ToString();
                rows[0] = [rows[0][0], T(rows[0][1].En.Replace("(padding below)", padding), rows[0][1].Ja.Replace("(padding below)", padding))];
                await FocusAsync(before);
                SendKey(Key.Tab);
                await Capture.SettleAsync(Window.GetWindow(panel)!, 50);
                rows.Add([T("TextBox, Label, TextBox: Tab from the first TextBox goes to", "TextBox、Label、TextBox の順: 最初の TextBox から Tab で移る先"),
                    Focused((T("the Label", "Label"), label), (T("the second TextBox", "2 つ目の TextBox"), after))]);
            }, activate: true);
        }

        {
            // デモアプリの Target の欄と同じ 2 組。
            var nameBox = new TextBox();
            var ageBox = new TextBox();
            AutomationProperties.SetAutomationId(nameBox, "NameBox");
            AutomationProperties.SetAutomationId(ageBox, "AgeBox");
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
                Loc afterN = Focused((T("Name TextBox", "Name の TextBox"), nameBox), (T("Age TextBox", "Age の TextBox"), ageBox), ("Other", other));
                AccessKeyManager.ProcessKey(null, "A", false);
                await Capture.SettleAsync(window, 50);
                Loc afterA = Focused((T("Name TextBox", "Name の TextBox"), nameBox), (T("Age TextBox", "Age の TextBox"), ageBox), ("Other", other));
                rows.Add([T("demo Target labels: focus after access key N / A", "デモの Target 付きの Label: アクセスキー N / A の後のフォーカス"), T($"{afterN.En} / {afterA.En}", $"{afterN.Ja} / {afterA.Ja}")]);

                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                rows.Add([T("  Name TextBox, read by a UI Automation client: name / LabeledBy", "  Name の TextBox を UI オートメーションのクライアントから読む: 名前 / LabeledBy"),
                    await Task.Run(() => ReadThroughClient(hwnd, "NameBox"))]);

                // ヒントで勧める回避策: 入力欄に AutomationProperties.LabeledBy を手動で設定したとき。
                AutomationProperties.SetLabeledBy(ageBox, ageLabel);
                rows.Add([T("  Age TextBox, AutomationProperties.LabeledBy set by hand: name / LabeledBy", "  AutomationProperties.LabeledBy を手で指定した Age の TextBox: 名前 / LabeledBy"),
                    await Task.Run(() => ReadThroughClient(hwnd, "AgeBox"))]);
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
                rows.Add([T("Label \"_Plain\" without Target: focus after access key P", "Target の無い Label \"_Plain\": アクセスキー P の後のフォーカス"),
                    Focused((T("the Label", "Label"), label), (T("the TextBox", "TextBox"), box), (T("Other (unchanged)", "Other（変わらない）"), other))]);
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
                rows.Add([T("Target is a TextBox inside a ToolBar (own focus scope): focus after access key S", "Target が ToolBar の中の TextBox（独自のフォーカススコープ）: アクセスキー S の後のフォーカス"),
                    Focused((T("the TextBox in the ToolBar", "ToolBar の中の TextBox"), inToolBar), (T("Other (unchanged)", "Other（変わらない）"), other))]);
            }, activate: true);
        }

        foreach (string content in new[] { "CONTENT", "CONTENT\nLINE 2" })
        {
            var label = new Label { Content = content, VerticalAlignment = VerticalAlignment.Top };
            Layout(new Grid { Children = { label } }, 200, 200);
            rows.Add([T($"string Content {(content.Contains('\n') ? "with a line break" : "on one line")}: height", $"文字列の Content（{(content.Contains('\n') ? "改行あり" : "1 行")}）: 高さ"), D(label.ActualHeight)]);
        }


        {
            // 使用例「ComboBox や DatePicker を Target にする」。アクセスキーでフォーカスがどこへ移るか。
            var combo = new ComboBox { ItemsSource = new[] { "A", "B" }, Width = 120 };
            var editable = new ComboBox { ItemsSource = new[] { "A", "B" }, IsEditable = true, Width = 120 };
            var picker = new DatePicker { Width = 120 };
            var other = new Button { Content = "Other" };
            var panel = new StackPanel
            {
                Width = 200,
                Children =
                {
                    new Label { Content = "_Combo", Target = combo }, combo,
                    new Label { Content = "_Editable", Target = editable }, editable,
                    new Label { Content = "_Date", Target = picker }, picker,
                    other,
                },
            };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                async Task<string> PressAsync(string key)
                {
                    await FocusAsync(other);
                    AccessKeyManager.ProcessKey(null, key, false);
                    await Capture.SettleAsync(window, 50);
                    return Keyboard.FocusedElement?.GetType().Name ?? "none";
                }

                rows.Add([T("access key, Target ComboBox / editable ComboBox / DatePicker: focus goes to", "アクセスキー、Target が ComboBox / 編集できる ComboBox / DatePicker: フォーカスの移る先"),
                    $"{await PressAsync("C")} / {await PressAsync("E")} / {await PressAsync("D")}"]);
            }, activate: true);
        }

        return rows;
    }
}
