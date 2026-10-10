using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で Mode を書かないバインドが、ユーザー操作で黙って外れる仕組み」の計測。表を 3 つ出す。
///
/// 1 つ目は、Mode を書かずにバインドされやすいプロパティの既定の向き（メタデータの
/// <see cref="FrameworkPropertyMetadata.BindsTwoWayByDefault"/>）。画面は使わない。
/// 2 つ目は、TreeViewItem と Expander の既定のテンプレートで、開閉のボタンが IsExpanded へどう結ばれているか。画面は使わない。
/// 3 つ目は、バインドしたプロパティを実際のマウスとキーボード（<see cref="RealMouse"/> / <see cref="RealKeyboard"/>）で操作し、
/// 操作のあとにバインドが残るか、ソースへ書き戻されたか、ソースに操作前の値を代入したとき（ClearValue の行は True を代入したとき）に表示が追従するかを読む。
/// バインドのエラーと警告の数もトレースで数え、数え方が働いていることを、存在しないパスへのバインドの行で確かめる。
/// </summary>
internal sealed class BindingModeOmittedScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "TreeViewItem.IsExpanded・ColumnDefinition.Width・RowDefinition.Height・DatePicker.Text・Window.Left・Window.Top・Window の FrameworkElement.Width の BindsTwoWayByDefault が False で、TreeViewItem.IsSelected・Expander.IsExpanded・ToggleButton.IsChecked（ToggleButton と CheckBox）・MenuItem.IsChecked・ComboBox.IsDropDownOpen・Popup.IsOpen・Selector.SelectedIndex・Selector.SelectedItem・TextBox.Text・RangeBase.Value・DatePicker.SelectedDate・Window.WindowState などは True であること",
        "TreeViewItem の既定のテンプレートで、展開ボタン（ToggleButton \"Expander\"）の IsChecked は Mode を書かない {Binding IsExpanded}（RelativeSource TemplatedParent）で、Expander の既定のテンプレートの見出し（ToggleButton \"HeaderSite\"）は Mode=TwoWay であること",
        "ItemContainerStyle の Setter で Mode を書かずに IsExpanded をバインドした TreeView は、実際のマウスで展開ボタンをクリックすると、値の供給元が Local になってバインドが働かなくなり、ソースは更新されず、ソースを戻しても表示は戻らないこと。バインドのエラーと警告は 0 件であること",
        "同じ TreeView で、展開ボタンの IsChecked にコードで True を設定しても、クリックと同じく Local になってソースは更新されないこと",
        "同じ TreeView で、展開ボタンのクリックの後に ClearValue を呼ぶと、値の供給元が Style のバインドの式に戻り、その後にソースを True にすると表示も True になること（Setter のバインドは隠れるだけであること）",
        "同じ TreeView で、実際のキー（→）や見出しのダブルクリックで展開すると、バインドは残り、値は SetCurrentValue の現在値（IsCurrent）になり、ソースは更新されず、ソースが変更を通知すると表示がソースの値に戻ること",
        "ItemContainerStyle の Setter に Mode=TwoWay を書くと、展開ボタンのクリック・→ キー・見出しのダブルクリックのどれでもバインドが残り、ソースが更新され、値の供給元に現在値（IsCurrent）は付かないこと",
        "TreeViewItem に属性で直接 IsExpanded=\"{Binding Value}\" と書いた場合は、展開ボタンのクリックでバインドが外れて Local になり、ClearValue の後は Default になって、ソースを True にしても表示は False のままであること",
        "BindsTwoWayByDefault が True の Expander.IsExpanded・MenuItem.IsChecked・CheckBox.IsChecked は、Mode を書かなくても実際のクリックでソースが更新され、バインドが残ること",
        "Expander の IsExpanded に Mode=OneWay を書くと、テンプレートの見出しが Mode=TwoWay でも、実際のクリックで Local になってソースは更新されないこと",
        "Mode を書かずに ColumnDefinition.Width をバインドした列は、実際のマウスで GridSplitter を 60 DIP ドラッグするとバインドが外れてソースは更新されず、Mode=TwoWay を書くとバインドが残ってソースが更新されること",
        "存在しないパスへのバインドは、同じトレースの数え方で 1 件以上と数えられること",
    ];

    public string Slug => "wpf-binding-mode-omitted-detached";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Default binding direction of properties often bound without Mode",
            [T("property", "プロパティ"), "BindsTwoWayByDefault", "DefaultUpdateSourceTrigger"],
            Defaults(),
            "binding-mode-defaults.svg");

        await context.SaveTableAsync(
            "How the buttons inside the default templates are bound to IsExpanded",
            [T("element in the template", "テンプレートの中の要素"), "Binding", "Mode", "RelativeSource"],
            TemplateBindings(),
            "binding-mode-template-buttons.svg");

        await context.SaveTableAsync(
            "Bindings after real mouse and keyboard input",
            [
                T("binding and input", "バインドと操作"),
                T("after the input: displayed / source", "操作後: 表示 / ソース"),
                T("binding after the input", "操作後のバインド"),
                T("value source after the input", "操作後の値の供給元"),
                T("displayed after the source is assigned", "ソースに代入した後の表示"),
                T("binding errors", "バインドエラー"),
            ],
            await UserInputAsync(),
            "binding-mode-user-input.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    // ---------------------------------------------------------------- 既定の向き

    private static List<IReadOnlyList<Loc>> Defaults()
    {
        (string Name, DependencyProperty Property, Type Owner)[] properties =
        [
            ("TreeViewItem.IsExpanded", TreeViewItem.IsExpandedProperty, typeof(TreeViewItem)),
            ("TreeViewItem.IsSelected", TreeViewItem.IsSelectedProperty, typeof(TreeViewItem)),
            ("Expander.IsExpanded", Expander.IsExpandedProperty, typeof(Expander)),
            ("ToggleButton.IsChecked", ToggleButton.IsCheckedProperty, typeof(ToggleButton)),
            ("ToggleButton.IsChecked (CheckBox)", ToggleButton.IsCheckedProperty, typeof(CheckBox)),
            ("MenuItem.IsChecked", MenuItem.IsCheckedProperty, typeof(MenuItem)),
            ("MenuItem.IsSubmenuOpen", MenuItem.IsSubmenuOpenProperty, typeof(MenuItem)),
            ("ComboBox.IsDropDownOpen", ComboBox.IsDropDownOpenProperty, typeof(ComboBox)),
            ("ComboBox.Text", ComboBox.TextProperty, typeof(ComboBox)),
            ("Popup.IsOpen", Popup.IsOpenProperty, typeof(Popup)),
            ("ContextMenu.IsOpen", ContextMenu.IsOpenProperty, typeof(ContextMenu)),
            ("ToolTip.IsOpen", ToolTip.IsOpenProperty, typeof(ToolTip)),
            ("Selector.SelectedIndex (ListBox)", Selector.SelectedIndexProperty, typeof(ListBox)),
            ("Selector.SelectedItem (ListBox)", Selector.SelectedItemProperty, typeof(ListBox)),
            ("ListBoxItem.IsSelected", ListBoxItem.IsSelectedProperty, typeof(ListBoxItem)),
            ("TabItem.IsSelected", TabItem.IsSelectedProperty, typeof(TabItem)),
            ("TextBox.Text", TextBox.TextProperty, typeof(TextBox)),
            ("RangeBase.Value (Slider)", RangeBase.ValueProperty, typeof(Slider)),
            ("DatePicker.SelectedDate", DatePicker.SelectedDateProperty, typeof(DatePicker)),
            ("DatePicker.Text", DatePicker.TextProperty, typeof(DatePicker)),
            ("DatePicker.IsDropDownOpen", DatePicker.IsDropDownOpenProperty, typeof(DatePicker)),
            ("Calendar.SelectedDate", Calendar.SelectedDateProperty, typeof(Calendar)),
            ("ColumnDefinition.Width", ColumnDefinition.WidthProperty, typeof(ColumnDefinition)),
            ("RowDefinition.Height", RowDefinition.HeightProperty, typeof(RowDefinition)),
            ("Window.WindowState", Window.WindowStateProperty, typeof(Window)),
            ("Window.Left", Window.LeftProperty, typeof(Window)),
            ("Window.Top", Window.TopProperty, typeof(Window)),
            ("FrameworkElement.Width (Window)", FrameworkElement.WidthProperty, typeof(Window)),
        ];

        var rows = new List<IReadOnlyList<Loc>>();
        foreach ((string name, DependencyProperty property, Type owner) in properties)
        {
            // 派生型での上書きを読むため、型の静的コンストラクターを先に走らせる。
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(owner.TypeHandle);
            PropertyMetadata metadata = property.GetMetadata(owner);
            if (metadata is FrameworkPropertyMetadata framework)
            {
                rows.Add([name, framework.BindsTwoWayByDefault.ToString(), framework.DefaultUpdateSourceTrigger.ToString()]);
            }
            else
            {
                rows.Add([name, T($"- ({metadata.GetType().Name})", $"-（{metadata.GetType().Name}）"), "-"]);
            }
        }

        return rows;
    }

    // ---------------------------------------------------------------- テンプレートの中のボタン

    /// <summary>
    /// TreeViewItem と Expander の既定のテンプレートで、開閉のボタン（ToggleButton）の IsChecked が
    /// テンプレートを適用した要素の IsExpanded へどう結ばれているかを読む。画面には出さず、テンプレートを適用するだけで読む。
    /// </summary>
    private static List<IReadOnlyList<Loc>> TemplateBindings()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        var node = new TreeViewItem { Header = "Node" };
        node.Items.Add(new TreeViewItem { Header = "Child" });
        var tree = new TreeView();
        tree.Items.Add(node);
        var expander = new Expander { Header = "Expander", Content = "content" };
        var panel = new StackPanel();
        panel.Children.Add(tree);
        panel.Children.Add(expander);
        Layout(panel, 240, 200);
        node.ApplyTemplate();
        expander.ApplyTemplate();

        foreach ((string Name, Control Owner) owner in new (string, Control)[] { ("TreeViewItem", node), ("Expander", expander) })
        {
            ToggleButton button = Descendants(owner.Owner).OfType<ToggleButton>().First(b => b.TemplatedParent == owner.Owner);
            Binding? binding = BindingOperations.GetBinding(button, ToggleButton.IsCheckedProperty);
            rows.Add(
            [
                $"{owner.Name}: ToggleButton \"{button.Name}\" IsChecked",
                binding is null ? "-" : $"{{Binding {binding.Path?.Path}}}",
                binding?.Mode.ToString() ?? "-",
                binding?.RelativeSource?.Mode.ToString() ?? "-",
            ]);
        }

        return rows;
    }

    // ---------------------------------------------------------------- 実際の操作

    /// <summary>バインドのソース。パスは Value。</summary>
    private sealed class Source<TValue> : INotifyPropertyChanged
    {
        private TValue _value;

        public Source(TValue value) => _value = value;

        public TValue Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>バインドのエラーと警告の数を数える。</summary>
    private sealed class ErrorCounter : TraceListener
    {
        public int Count { get; private set; }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (eventType <= TraceEventType.Warning)
            {
                Count++;
            }
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
        {
            if (eventType <= TraceEventType.Warning)
            {
                Count++;
            }
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }

    /// <summary>トレースに出たバインドのエラーと警告の数。条件ごとに、操作前の数との差を取る。</summary>
    private static readonly ErrorCounter s_errors = new();

    /// <summary>1 つの条件の結果を表の 1 行にする。</summary>
    private static async Task<IReadOnlyList<Loc>> ReportAsync(
        Loc label,
        Window window,
        DependencyObject target,
        DependencyProperty property,
        Func<object?> readSource,
        Action resetSource,
        int errorsBefore)
    {
        int errors = s_errors.Count - errorsBefore;
        string displayed = Describe(target.GetValue(property));
        string sourced = Describe(readSource());
        bool kept = BindingOperations.GetBindingExpressionBase(target, property) is not null;
        ValueSource valueSource = DependencyPropertyHelper.GetValueSource(target, property);
        // IsExpression はバインドの式が値を持っていること、IsCurrent は SetCurrentValue で設定された値であることを示す。
        string from = valueSource.BaseValueSource + (valueSource.IsExpression ? ", expression" : "") + (valueSource.IsCurrent ? ", current" : "");
        string fromJa = valueSource.BaseValueSource + (valueSource.IsExpression ? "、式" : "") + (valueSource.IsCurrent ? "、現在値" : "");

        resetSource();
        await Capture.SettleAsync(window, 100);
        string after = Describe(target.GetValue(property));

        return
        [
            label,
            $"{displayed} / {sourced}",
            kept ? T("kept", "残る") : T("removed", "外れる"),
            T(from, fromJa),
            after,
            errors.ToString(),
        ];
    }

    private static string Describe(object? value) => value switch
    {
        GridLength length => length.ToString(),
        _ => WpfProbe.Describe(value),
    };

    private static async Task<List<IReadOnlyList<Loc>>> UserInputAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();
        SourceLevels level = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        PresentationTraceSources.DataBindingSource.Listeners.Add(s_errors);
        try
        {
            const string Style = "ItemContainerStyle {Binding IsExpanded}";
            const string StyleJa = "ItemContainerStyle で {Binding IsExpanded}";
            const string TwoWay = "ItemContainerStyle {Binding IsExpanded, Mode=TwoWay}";
            const string TwoWayJa = "ItemContainerStyle で {Binding IsExpanded, Mode=TwoWay}";

            rows.Add(await FolderTreeAsync(T($"{Style}, click the expander button", $"{StyleJa}、展開ボタンをクリック"), twoWay: false, FolderInput.ExpanderButton));
            rows.Add(await FolderTreeAsync(T($"{Style}, select the node and press the right arrow key", $"{StyleJa}、ノードを選んで右矢印キー（→）"), twoWay: false, FolderInput.RightArrow));
            rows.Add(await FolderTreeAsync(T($"{Style}, double-click the header", $"{StyleJa}、見出しをダブルクリック"), twoWay: false, FolderInput.DoubleClick));
            rows.Add(await FolderTreeAsync(T($"{Style}, set IsChecked of the expander button to True from code", $"{StyleJa}、展開ボタンの IsChecked にコードで True を設定"), twoWay: false, FolderInput.ButtonFromCode));
            rows.Add(await FolderTreeAsync(T($"{Style}, click the expander button, then ClearValue(IsExpanded)", $"{StyleJa}、展開ボタンをクリックした後に ClearValue(IsExpanded)"), twoWay: false, FolderInput.ExpanderButtonThenClear));
            rows.Add(await FolderTreeAsync(T($"{TwoWay}, click the expander button", $"{TwoWayJa}、展開ボタンをクリック"), twoWay: true, FolderInput.ExpanderButton));
            rows.Add(await FolderTreeAsync(T($"{TwoWay}, select the node and press the right arrow key", $"{TwoWayJa}、ノードを選んで右矢印キー（→）"), twoWay: true, FolderInput.RightArrow));
            rows.Add(await FolderTreeAsync(T($"{TwoWay}, double-click the header", $"{TwoWayJa}、見出しをダブルクリック"), twoWay: true, FolderInput.DoubleClick));
            rows.Add(await TreeExpanderClickAsync(T("TreeViewItem IsExpanded=\"{Binding Value}\" written on the item, click the expander button", "TreeViewItem に IsExpanded=\"{Binding Value}\" を直接書く、展開ボタンをクリック"), clearAfter: false));
            rows.Add(await TreeExpanderClickAsync(T("TreeViewItem IsExpanded=\"{Binding Value}\" written on the item, click, then ClearValue(IsExpanded)", "TreeViewItem に IsExpanded=\"{Binding Value}\" を直接書く、クリックした後に ClearValue(IsExpanded)"), clearAfter: true));
            rows.Add(await ExpanderClickAsync(T("Expander IsExpanded=\"{Binding Value}\", click the header", "Expander IsExpanded=\"{Binding Value}\"、見出しをクリック"), BindingMode.Default));
            rows.Add(await ExpanderClickAsync(T("Expander IsExpanded=\"{Binding Value, Mode=OneWay}\", click the header", "Expander IsExpanded=\"{Binding Value, Mode=OneWay}\"、見出しをクリック"), BindingMode.OneWay));
            rows.Add(await MenuItemClickAsync(T("MenuItem IsCheckable IsChecked=\"{Binding Value}\", click it", "MenuItem IsCheckable IsChecked=\"{Binding Value}\"、クリック")));
            rows.Add(await CheckBoxClickAsync(T("CheckBox IsChecked=\"{Binding Value}\", click it", "CheckBox IsChecked=\"{Binding Value}\"、クリック")));
            rows.Add(await SplitterDragAsync(T("ColumnDefinition Width=\"{Binding Value}\", drag the GridSplitter 60 DIP to the right", "ColumnDefinition Width=\"{Binding Value}\"、GridSplitter を右へ 60 DIP ドラッグ"), BindingMode.Default));
            rows.Add(await SplitterDragAsync(T("ColumnDefinition Width=\"{Binding Value, Mode=TwoWay}\", drag the GridSplitter 60 DIP to the right", "ColumnDefinition Width=\"{Binding Value, Mode=TwoWay}\"、GridSplitter を右へ 60 DIP ドラッグ"), BindingMode.TwoWay));
            rows.Add(await MissingPathAsync(T("check of the error count: TreeViewItem IsExpanded=\"{Binding Missing}\" (no such property), no input", "エラーの数え方の確認: TreeViewItem IsExpanded=\"{Binding Missing}\"（存在しないパス）、操作なし")));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(s_errors);
            PresentationTraceSources.DataBindingSource.Switch.Level = level;
        }

        return rows;
    }

    private static ToggleButton ExpanderButton(TreeViewItem node) =>
        Descendants(node).OfType<ToggleButton>().First(button => button.TemplatedParent == node);

    /// <summary>記事の XAML のデータ。ノードの名前・子・開閉の状態を持つ。</summary>
    private sealed class Folder : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Name { get; init; } = "";

        public List<Folder> Children { get; } = [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>記事の XAML の DataContext。</summary>
    private sealed class FolderPage
    {
        public List<Folder> Folders { get; } = [];
    }

    private enum FolderInput
    {
        ExpanderButton,
        RightArrow,
        DoubleClick,
        ButtonFromCode,
        ExpanderButtonThenClear,
    }

    /// <summary>記事の「現象」の節と同じ XAML（ItemsSource と ItemContainerStyle）。twoWay で Mode=TwoWay を足す。</summary>
    private static TreeView FolderTree(bool twoWay) => SceneContext.LoadXaml<TreeView>($$"""
        <TreeView x:Name="folderTree" ItemsSource="{Binding Folders}" Width="240" Height="120">
          <TreeView.ItemContainerStyle>
            <Style TargetType="TreeViewItem">
              <Setter Property="IsExpanded" Value="{Binding IsExpanded{{(twoWay ? ", Mode=TwoWay" : "")}}}" />
            </Style>
          </TreeView.ItemContainerStyle>
          <TreeView.ItemTemplate>
            <HierarchicalDataTemplate ItemsSource="{Binding Children}">
              <TextBlock Text="{Binding Name}" />
            </HierarchicalDataTemplate>
          </TreeView.ItemTemplate>
        </TreeView>
        """);

    private static async Task<IReadOnlyList<Loc>> FolderTreeAsync(Loc label, bool twoWay, FolderInput input)
    {
        var root = new Folder { Name = "Folder" };
        root.Children.Add(new Folder { Name = "Child" });
        var page = new FolderPage();
        page.Folders.Add(root);
        TreeView tree = FolderTree(twoWay);
        tree.DataContext = page;
        IReadOnlyList<Loc> row = [];
        await ShowAsync(tree, async () =>
        {
            Window window = await FrontCenteredAsync(tree);
            var node = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                switch (input)
                {
                    case FolderInput.ExpanderButton:
                    case FolderInput.ExpanderButtonThenClear:
                        await RealMouse.ClickAsync(window, ExpanderButton(node));
                        if (input == FolderInput.ExpanderButtonThenClear)
                        {
                            node.ClearValue(TreeViewItem.IsExpandedProperty);
                            await Capture.SettleAsync(window, 100);
                        }

                        break;
                    case FolderInput.RightArrow:
                        node.IsSelected = true;
                        node.Focus();
                        await Capture.SettleAsync(window, 100);
                        before = s_errors.Count;
                        await RealKeyboard.PressAsync(window, 0x27); // VK_RIGHT
                        await Capture.SettleAsync(window, 100);
                        break;
                    case FolderInput.DoubleClick:
                        FrameworkElement header = Descendants(node).OfType<ContentPresenter>().First(p => p.Name == "PART_Header");
                        await RealMouse.ClickAsync(window, header);
                        await RealMouse.LeftDownAsync(window, header);
                        await RealMouse.LeftUpAsync(window);
                        break;
                    case FolderInput.ButtonFromCode:
                        // 実際の入力を使わず、テンプレートのボタンの IsChecked を設定する。テンプレートのバインドが IsExpanded へ書き込む経路だけを通す。
                        ExpanderButton(node).IsChecked = true;
                        await Capture.SettleAsync(window, 100);
                        break;
                }
            }

            // ClearValue の行は、ソースを操作前の値（False）に戻しても既定値と同じで違いが出ないため、True にして追従するかを見る。
            bool changeTo = input == FolderInput.ExpanderButtonThenClear;
            row = await ReportAsync(label, window, node, TreeViewItem.IsExpandedProperty, () => root.IsExpanded, () => root.IsExpanded = changeTo, before);
        }, activate: true);
        return row;
    }

    /// <summary>ノードと子を 1 つ持つ TreeView。ノードに IsExpanded="{Binding Value}" を直接書く。</summary>
    private static (TreeView Tree, TreeViewItem Node) NewTree(Source<bool> source, string path = nameof(Source<bool>.Value))
    {
        var node = new TreeViewItem { Header = "Node", DataContext = source };
        node.Items.Add(new TreeViewItem { Header = "Child" });
        node.SetBinding(TreeViewItem.IsExpandedProperty, new Binding(path));
        var tree = new TreeView { Width = 240, Height = 120 };
        tree.Items.Add(node);
        return (tree, node);
    }

    private static async Task<IReadOnlyList<Loc>> TreeExpanderClickAsync(Loc label, bool clearAfter)
    {
        var source = new Source<bool>(false);
        (TreeView tree, TreeViewItem node) = NewTree(source);
        IReadOnlyList<Loc> row = [];
        await ShowAsync(tree, async () =>
        {
            Window window = await FrontCenteredAsync(tree);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(window, ExpanderButton(node));
            }

            if (clearAfter)
            {
                node.ClearValue(TreeViewItem.IsExpandedProperty);
                await Capture.SettleAsync(window, 100);
            }

            // ClearValue の行は、ソースを True にして追従するかを見る（False に戻すだけでは既定値と区別できない）。
            row = await ReportAsync(label, window, node, TreeViewItem.IsExpandedProperty, () => source.Value, () => source.Value = clearAfter, before);
        }, activate: true);
        return row;
    }

    /// <summary>存在しないパスへのバインド。エラーの数え方が働いていることを確かめる（操作はしない）。</summary>
    private static async Task<IReadOnlyList<Loc>> MissingPathAsync(Loc label)
    {
        var source = new Source<bool>(false);
        int before = s_errors.Count;
        (TreeView tree, TreeViewItem node) = NewTree(source, "Missing");
        IReadOnlyList<Loc> row = [];
        await ShowAsync(tree, async () =>
        {
            Window window = Window.GetWindow(tree)!;
            await Capture.SettleAsync(window, 100);
            row = await ReportAsync(label, window, node, TreeViewItem.IsExpandedProperty, () => source.Value, () => source.Value = false, before);
        });
        return row;
    }

    private static async Task<IReadOnlyList<Loc>> ExpanderClickAsync(Loc label, BindingMode mode)
    {
        var source = new Source<bool>(false);
        var expander = new Expander { Header = "Expander", Content = "content", DataContext = source, Width = 240 };
        expander.SetBinding(Expander.IsExpandedProperty, new Binding(nameof(Source<bool>.Value)) { Mode = mode });
        IReadOnlyList<Loc> row = [];
        await ShowAsync(expander, async () =>
        {
            Window window = await FrontCenteredAsync(expander);
            var header = Descendants(expander).OfType<ToggleButton>().First(b => b.TemplatedParent == expander);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(window, header);
            }

            row = await ReportAsync(label, window, expander, Expander.IsExpandedProperty, () => source.Value, () => source.Value = false, before);
        }, activate: true);
        return row;
    }

    private static async Task<IReadOnlyList<Loc>> MenuItemClickAsync(Loc label)
    {
        var source = new Source<bool>(false);
        var item = new MenuItem { Header = "Check", IsCheckable = true, DataContext = source };
        item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(Source<bool>.Value)));
        var menu = new Menu { Width = 240 };
        menu.Items.Add(item);
        IReadOnlyList<Loc> row = [];
        await ShowAsync(menu, async () =>
        {
            Window window = await FrontCenteredAsync(menu);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(window, item);
            }

            row = await ReportAsync(label, window, item, MenuItem.IsCheckedProperty, () => source.Value, () => source.Value = false, before);
        }, activate: true);
        return row;
    }

    private static async Task<IReadOnlyList<Loc>> CheckBoxClickAsync(Loc label)
    {
        var source = new Source<bool>(false);
        var box = new CheckBox { Content = "CheckBox", DataContext = source, Width = 240 };
        box.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Source<bool>.Value)));
        IReadOnlyList<Loc> row = [];
        await ShowAsync(box, async () =>
        {
            Window window = await FrontCenteredAsync(box);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(window, box);
            }

            row = await ReportAsync(label, window, box, ToggleButton.IsCheckedProperty, () => source.Value, () => source.Value = false, before);
        }, activate: true);
        return row;
    }

    private static async Task<IReadOnlyList<Loc>> SplitterDragAsync(Loc label, BindingMode mode)
    {
        var source = new Source<GridLength>(new GridLength(100));
        var grid = new Grid { Width = 320, Height = 80, DataContext = source };
        var first = new ColumnDefinition();
        first.SetBinding(ColumnDefinition.WidthProperty, new Binding(nameof(Source<GridLength>.Value)) { Mode = mode });
        grid.ColumnDefinitions.Add(first);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);
        IReadOnlyList<Loc> row = [];
        await ShowAsync(grid, async () =>
        {
            Window window = await FrontCenteredAsync(grid);
            int before = s_errors.Count;
            using (RealMouse.Preserve())
            {
                // ドラッグ中は列の幅が変わってスプリッターも動くため、動かない Grid の座標（DIP）で位置を決める。
                // 画面の座標への換算は PointToScreen が表示スケールに合わせて行うので、動かす量は表示スケールによらず 60 DIP になる。
                Point start = splitter.TranslatePoint(new Point(splitter.ActualWidth / 2, splitter.ActualHeight / 2), grid);
                await RealMouse.MoveToAsync(grid, start);
                await RealMouse.LeftDownAsync(window, splitter);
                for (int dx = 15; dx <= 60; dx += 15)
                {
                    await RealMouse.MoveToAsync(grid, new Point(start.X + dx, start.Y));
                }

                await RealMouse.LeftUpAsync(window);
            }

            row = await ReportAsync(label, window, first, ColumnDefinition.WidthProperty, () => source.Value, () => source.Value = new GridLength(100), before);
        }, activate: true);
        return row;
    }
}
