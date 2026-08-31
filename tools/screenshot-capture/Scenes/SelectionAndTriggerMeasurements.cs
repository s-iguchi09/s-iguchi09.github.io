using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// TreeView の選択・読み取り専用テキスト・UpdateSourceTrigger の既定値を実測する部品。
/// </summary>
internal static class SelectionAndTriggerMeasurements
{
    // ------------------------------------------------------------------
    // TreeView の選択とコンテナ生成
    // ------------------------------------------------------------------

    /// <summary>
    /// 「SelectedItem は書き込めない」「コンテナは展開するまで生成されない」を実測する。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> TreeViewSelectionAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add([
            "TreeView.SelectedItemProperty.ReadOnly",
            WpfProbe.Describe(TreeView.SelectedItemProperty.ReadOnly),
            "-",
        ]);

        // 親 1 つ、その下に子 2 つ。子のコンテナは展開するまで作られない。
        TreeViewItem BuildTree(out TreeViewItem parent, out TreeViewItem child)
        {
            child = new TreeViewItem { Header = "child" };
            parent = new TreeViewItem { Header = "parent" };
            parent.Items.Add(child);
            return parent;
        }

        rows.Add(await MeasureAsync(
            "SetValue(SelectedItemProperty) from outside",
            BuildTree,
            (tree, _, _) =>
            {
                try
                {
                    tree.SetValue(TreeView.SelectedItemProperty, "x");
                    return "no exception";
                }
                catch (Exception ex)
                {
                    return "throws " + ex.GetType().Name;
                }
            }));

        rows.Add(await MeasureAsync(
            "child container before expanding",
            BuildTree,
            (_, parent, _) => Describe(parent.ItemContainerGenerator.ContainerFromIndex(0))));

        // 展開しただけでは足りない。コンテナが作られるのはレイアウトが走った後である。
        rows.Add(await MeasureAsync(
            "right after IsExpanded = true, before a layout pass",
            BuildTree,
            (_, parent, _) =>
            {
                parent.IsExpanded = true;
                return Describe(parent.ItemContainerGenerator.ContainerFromIndex(0));
            }));

        rows.Add(await MeasureAsync(
            "after IsExpanded = true and UpdateLayout()",
            BuildTree,
            (_, parent, _) =>
            {
                parent.IsExpanded = true;
                parent.UpdateLayout();
                return Describe(parent.ItemContainerGenerator.ContainerFromIndex(0));
            }));

        rows.Add(await MeasureAsync(
            "TreeView.SelectedItem after child.IsSelected = true",
            BuildTree,
            (tree, parent, child) =>
            {
                parent.IsExpanded = true;
                parent.UpdateLayout();
                child.IsSelected = true;
                return tree.SelectedItem is TreeViewItem selected
                    ? $"the '{selected.Header}' item"
                    : WpfProbe.Describe(tree.SelectedItem);
            }));

        return rows;
    }

    private static string Describe(DependencyObject? container) =>
        container is null ? "null" : container.GetType().Name;

    private delegate TreeViewItem TreeBuilder(out TreeViewItem parent, out TreeViewItem child);

    private static async Task<IReadOnlyList<string>> MeasureAsync(
        string label, TreeBuilder build, Func<TreeView, TreeViewItem, TreeViewItem, string> read)
    {
        TreeViewItem root = build(out TreeViewItem parent, out TreeViewItem child);
        var tree = new TreeView { Height = 120 };
        tree.Items.Add(root);

        var host = new Grid();
        host.Children.Add(tree);

        List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(label, host, _ => [read(tree, parent, child)]),
        ]);

        return [measured[0][0], measured[0][1], "-"];
    }

    // ------------------------------------------------------------------
    // 編集不可のまま選択・コピーできる表示
    // ------------------------------------------------------------------

    /// <summary>
    /// 表示専用の候補ごとに、テキストを選択できるかとフォーカスの扱いを測る。
    /// </summary>
    public static Task<List<IReadOnlyList<string>>> SelectableTextAsync()
    {
        const string Sample = "selectable text";

        return WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "TextBlock",
                Host(new TextBlock { Text = Sample }),
                root => ReadSelectable(Child<TextBlock>(root))),
            new WpfProbe.Case(
                "TextBox IsReadOnly=True",
                Host(new TextBox { Text = Sample, IsReadOnly = true }),
                root => ReadSelectable(Child<TextBox>(root))),
            new WpfProbe.Case(
                "+ borderless, transparent",
                Host(new TextBox
                {
                    Text = Sample,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(0),
                }),
                root => ReadSelectable(Child<TextBox>(root))),
            new WpfProbe.Case(
                "+ IsTabStop=False",
                Host(new TextBox
                {
                    Text = Sample,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(0),
                    IsTabStop = false,
                }),
                root => ReadSelectable(Child<TextBox>(root))),
        ]);
    }

    private static Grid Host(FrameworkElement element)
    {
        var grid = new Grid();
        grid.Children.Add(element);
        return grid;
    }

    private static T Child<T>(FrameworkElement root) where T : FrameworkElement =>
        (T)((Grid)root).Children[0];

    private static IReadOnlyList<string> ReadSelectable(FrameworkElement element)
    {
        string selected;

        if (element is TextBox box)
        {
            box.Focus();
            box.SelectAll();
            selected = box.SelectedText.Length == 0 ? "(nothing)" : box.SelectedText;
        }
        else
        {
            // TextBlock には選択のための API が無い。
            selected = "no selection API";
        }

        return
        [
            selected,
            WpfProbe.Describe(element.Focusable),
            WpfProbe.Describe(KeyboardNavigation.GetIsTabStop(element)),
        ];
    }

    // ------------------------------------------------------------------
    // UpdateSourceTrigger の既定値
    // ------------------------------------------------------------------

    /// <summary>
    /// プロパティごとの既定の更新タイミングを、メタデータから読み出す。
    ///
    /// 「TextBox.Text だけ LostFocus が既定」という主張は、
    /// 他のプロパティと並べて初めて意味を持つ。
    /// </summary>
    public static List<IReadOnlyList<string>> DefaultUpdateSourceTriggers()
    {
        (string Label, DependencyProperty Property, Type Owner)[] targets =
        [
            ("TextBox.Text", TextBox.TextProperty, typeof(TextBox)),
            ("CheckBox.IsChecked", CheckBox.IsCheckedProperty, typeof(CheckBox)),
            ("ComboBox.SelectedItem", ComboBox.SelectedItemProperty, typeof(ComboBox)),
            ("Slider.Value", Slider.ValueProperty, typeof(Slider)),
            ("PasswordBox.Tag", FrameworkElement.TagProperty, typeof(PasswordBox)),
            ("TextBlock.Text", TextBlock.TextProperty, typeof(TextBlock)),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string label, DependencyProperty property, Type owner) in targets)
        {
            var metadata = property.GetMetadata(owner) as FrameworkPropertyMetadata;
            rows.Add([
                label,
                metadata is null
                    ? "(not FrameworkPropertyMetadata)"
                    : metadata.DefaultUpdateSourceTrigger.ToString(),
                metadata is null ? "-" : WpfProbe.Describe(metadata.BindsTwoWayByDefault),
            ]);
        }

        return rows;
    }

    /// <summary>
    /// 既定・PropertyChanged・Explicit の 3 通りで、入力からソースへ値が渡るタイミングを測る。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> UpdateTimingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (UpdateSourceTrigger trigger in
                 new[] { UpdateSourceTrigger.Default, UpdateSourceTrigger.PropertyChanged, UpdateSourceTrigger.Explicit })
        {
            var source = new TextSource();
            var box = new TextBox { Width = 160 };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(TextSource.Value))
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = trigger,
            });

            var other = new TextBox { Width = 160 };
            var panel = new StackPanel();
            panel.Children.Add(box);
            panel.Children.Add(other);

            string afterInput = string.Empty;
            string afterLostFocus = string.Empty;

            List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    trigger.ToString(),
                    panel,
                    _ =>
                    [
                        afterInput,
                        afterLostFocus,
                        Quote(source.Value),
                    ],
                    Act: _ =>
                    {
                        box.Focus();

                        // TextBox のエディタが通る経路で 1 文字入れる。
                        box.RaiseEvent(new TextCompositionEventArgs(
                            Keyboard.PrimaryDevice,
                            new TextComposition(InputManager.Current, box, "X"))
                        {
                            RoutedEvent = TextCompositionManager.TextInputEvent,
                        });

                        afterInput = Quote(source.Value);

                        // 別のコントロールへフォーカスを移し、LostFocus を起こす。
                        other.Focus();
                        afterLostFocus = Quote(source.Value);

                        if (trigger == UpdateSourceTrigger.Explicit)
                        {
                            BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
                        }

                        return Task.CompletedTask;
                    }),
            ]);

            rows.Add(measured[0]);
        }

        return rows;
    }

    private static string Quote(string value) => value.Length == 0 ? "(empty)" : $"\"{value}\"";

    private sealed class TextSource
    {
        public string Value { get; set; } = string.Empty;
    }
}
