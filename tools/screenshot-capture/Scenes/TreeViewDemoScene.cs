using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「TreeView」（apps/wpf-standard-control-demo/treeview.md と日本語版）の記述を実測する。
///
/// SelectedItem が読み取り専用であることと、子のコンテナーが親を展開するまで作られないことは、
/// 記事 wpf-treeview-select-item-programmatically（TreeViewSelectItemScene）で実測済みのため、
/// ここではデモページだけが述べている挙動を確かめる。
/// 展開ボタンは UI オートメーションで切り替える。仮の子を持つノードの展開ボタンだけは、実際のマウス（<see cref="RealMouse"/>）でクリックする。
/// </summary>
internal sealed class TreeViewDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-treeview";

    public string ImageDirectory => DemoProbe.ImageDirectory("treeview");

    public IReadOnlyList<string> Verifies =>
    [
        "TreeView・TreeViewItem の基底クラス、SelectedItem・SelectedValue が読み取り専用か、IsExpanded・IsSelected が既定で TwoWay か",
        "SelectedItem にバインドを設定しようとしたときの例外",
        "SelectedItem が返すもの（XAML の TreeViewItem のとき、ItemsSource のデータのとき）と、SelectedValuePath=\"Header\" のときの SelectedValue",
        "IsSelected を ItemContainerStyle で TwoWay にバインドした 2 つのノードを両方 True にしたときに選ばれるノードとソースの値",
        "IsSelectionActive が、フォーカスの有無と選択の有無でどう変わるか",
        "親が折りたたまれている子ノードの IsExpanded をソースで True にしておき、親を展開したときに子が展開されるか",
        "デモアプリの IsExpanded 欄と同じく Mode 指定なしで IsExpanded にバインドしたノードを、展開ボタン（ToggleButton の UI オートメーション）で折りたたんだときのバインドの状態と、その後の CheckBox の操作が効くか",
        "HierarchicalDataTemplate を ItemTemplate に指定したときにすべての階層に適用されること、暗黙のテンプレートの DataType が型と一致しないときの表示",
        "既定の仮想化の設定（VirtualizingPanel.IsVirtualizing）と、1,000 ノードのときに作られる TreeViewItem の数",
        "キー操作（下矢印・右矢印・左矢印・Space・Enter・テンキーの *）による選択と展開の変化",
        "ItemContainerStyle で設定した ContextMenu の DataContext",
        "スクロールバーの既定値とその出どころ",
        "仮の子を置いた TreeViewItem で、Expanded のハンドラーで子を入れ替えるときに、展開ボタンを実際にクリックする前と後の子",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "TreeView: types, metadata, selection and focus",
            [T("case", "条件"), T("measured", "計測値")],
            await SelectionAsync(),
            "treeview-selection.svg");

        await context.SaveTableAsync(
            "TreeView: expansion, templates, virtualization, keys and context menu",
            [T("case", "条件"), T("measured", "計測値")],
            await StructureAsync(),
            "treeview-structure.svg");
    }

    private sealed class Node : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;

        public Node(string name, params Node[] children)
        {
            Name = name;
            Children = new ObservableCollection<Node>(children);
        }

        public string Name { get; }

        public ObservableCollection<Node> Children { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public override string ToString() => $"ToString:{Name}";

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>デモアプリと同じ Desktop / Mobile の 2 階層。</summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static ObservableCollection<Node> DeviceTree() =>
    [
        new Node("Desktop", new Node("Workstation PC"), new Node("Gaming PC")),
        new Node("Mobile", new Node("Laptop"), new Node("Tablet")),
    ];

    /// <summary>IsSelected と IsExpanded をデータに TwoWay で結ぶ ItemContainerStyle。</summary>
    private static Style BoundContainerStyle()
    {
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding(nameof(Node.IsSelected)) { Mode = BindingMode.TwoWay }));
        style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(Node.IsExpanded)) { Mode = BindingMode.TwoWay }));
        return style;
    }

    private static HierarchicalDataTemplate NodeTemplate() =>
        new(typeof(Node))
        {
            ItemsSource = new Binding(nameof(Node.Children)),
            VisualTree = NameText(),
        };

    private static FrameworkElementFactory NameText()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Node.Name)));
        return text;
    }

    private static TreeViewItem Container(ItemsControl parent, object item) =>
        (TreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(item);

    private static async Task Settle(FrameworkElement element) =>
        await Capture.SettleAsync(Window.GetWindow(element)!);

    private static async Task<List<IReadOnlyList<Loc>>> SelectionAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        rows.Add([T("base class of TreeView / TreeViewItem", "TreeView / TreeViewItem の基底クラス"),
            $"{typeof(TreeView).BaseType!.Name} / {typeof(TreeViewItem).BaseType!.Name}"]);
        rows.Add([T("SelectedItem / SelectedValue read-only", "SelectedItem / SelectedValue が読み取り専用か"),
            $"{TreeView.SelectedItemProperty.ReadOnly} / {TreeView.SelectedValueProperty.ReadOnly}"]);
        rows.Add(["IsExpanded / IsSelected: BindsTwoWayByDefault",
            $"{((FrameworkPropertyMetadata)TreeViewItem.IsExpandedProperty.GetMetadata(typeof(TreeViewItem))).BindsTwoWayByDefault} / " +
            $"{((FrameworkPropertyMetadata)TreeViewItem.IsSelectedProperty.GetMetadata(typeof(TreeViewItem))).BindsTwoWayByDefault}"]);
        rows.Add(["SetBinding(TreeView.SelectedItemProperty, ...)",
            Throws(() => new TreeView().SetBinding(TreeView.SelectedItemProperty, new Binding("X")))]);

        {
            // デモアプリの SelectedValuePath 欄と同じく、XAML の TreeViewItem に Header で値を取る。
            var tree = SceneContext.LoadXaml<TreeView>(
                """
                <TreeView Height="150" Width="200" SelectedValuePath="Header">
                  <TreeViewItem Header="Desktop" IsExpanded="True">
                    <TreeViewItem Header="Workstation PC" />
                    <TreeViewItem Header="Gaming PC" />
                  </TreeViewItem>
                  <TreeViewItem Header="Mobile" IsExpanded="True">
                    <TreeViewItem Header="Laptop" />
                  </TreeViewItem>
                </TreeView>
                """);
            await ShowAsync(tree, async () =>
            {
                var desktop = (TreeViewItem)tree.Items[0]!;
                var gaming = (TreeViewItem)desktop.Items[1]!;
                gaming.IsSelected = true;
                rows.Add([T("XAML TreeViewItems, SelectedValuePath=\"Header\"; select \"Gaming PC\": SelectedItem / SelectedValue", "XAML の TreeViewItem、SelectedValuePath=\"Header\"、\"Gaming PC\" を選択: SelectedItem / SelectedValue"),
                    $"{tree.SelectedItem?.GetType().Name} / {WpfProbe.Describe(tree.SelectedValue)}"]);
                await Task.CompletedTask;
            });
        }

        {
            ObservableCollection<Node> data = DeviceTree();
            var tree = new TreeView
            {
                Width = 200, Height = 150, ItemsSource = data, ItemTemplate = NodeTemplate(),
                ItemContainerStyle = BoundContainerStyle(),
            };
            await ShowAsync(tree, async () =>
            {
                data[0].IsSelected = true;
                await Settle(tree);
                rows.Add([T("ItemsSource data; source Desktop.IsSelected = true: SelectedItem", "ItemsSource のデータ、ソースの Desktop.IsSelected = true: SelectedItem"),
                    WpfProbe.Describe(tree.SelectedItem)]);

                data[1].IsSelected = true;
                await Settle(tree);
                rows.Add([T("  then source Mobile.IsSelected = true: SelectedItem / Desktop.IsSelected / Mobile.IsSelected", "  続けてソースの Mobile.IsSelected = true: SelectedItem / Desktop.IsSelected / Mobile.IsSelected"),
                    $"{WpfProbe.Describe(tree.SelectedItem)} / {data[0].IsSelected} / {data[1].IsSelected}"]);
            });
        }

        {
            // デモアプリの IsSelectionActive 欄と同じ 3 ノードと、フォーカスの移動先の TextBox。
            var tree = SceneContext.LoadXaml<TreeView>(
                """
                <TreeView Height="120" Width="200">
                  <TreeViewItem Header="Node 1" />
                  <TreeViewItem Header="Node 2" />
                  <TreeViewItem Header="Node 3" />
                </TreeView>
                """);
            var other = new TextBox { Width = 200 };
            var panel = new StackPanel();
            panel.Children.Add(tree);
            panel.Children.Add(other);
            await ShowAsync(panel, async () =>
            {
                var node2 = (TreeViewItem)tree.Items[1]!;
                rows.Add([T("Node 2 IsSelectionActive: nothing selected, no focus", "Node 2 の IsSelectionActive: 選択なし、フォーカスなし"), Selector.GetIsSelectionActive(node2).ToString()]);
                node2.IsSelected = true;
                await Settle(tree);
                rows.Add([T("  Node 2 selected from code (IsSelected / IsSelectionActive)", "  コードで Node 2 を選択（IsSelected / IsSelectionActive）"),
                    $"{node2.IsSelected} / {Selector.GetIsSelectionActive(node2)}"]);
                node2.Focus();
                await Settle(tree);
                rows.Add([T("  Node 2 focused", "  Node 2 にフォーカス"), $"{node2.IsSelected} / {Selector.GetIsSelectionActive(node2)}"]);
                other.Focus();
                await Settle(tree);
                rows.Add([T("  focus moved to a TextBox", "  フォーカスを TextBox に移す"), $"{node2.IsSelected} / {Selector.GetIsSelectionActive(node2)}"]);
            }, activate: true);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<Loc>>> StructureAsync()
    {
        var rows = new List<IReadOnlyList<Loc>>();

        {
            // デモアプリの IsExpanded 欄と同じく、CheckBox の IsChecked を Mode 指定なしで IsExpanded にバインドする。
            var panel = SceneContext.LoadXaml<StackPanel>(
                """
                <StackPanel Width="220">
                  <CheckBox x:Name="IsExpandedCheckBox" Content="IsExpanded(Node 1)" IsChecked="True" />
                  <TreeView Height="150">
                    <TreeViewItem x:Name="Node1" Header="Node 1 (Controlled)"
                                  IsExpanded="{Binding IsChecked, ElementName=IsExpandedCheckBox}">
                      <TreeViewItem Header="Child 1-1" />
                      <TreeViewItem Header="Child 1-2" />
                    </TreeViewItem>
                  </TreeView>
                </StackPanel>
                """);
            await ShowAsync(panel, async () =>
            {
                var check = (CheckBox)panel.FindName("IsExpandedCheckBox");
                var node1 = (TreeViewItem)panel.FindName("Node1");
                var expander = (ToggleButton)node1.Template.FindName("Expander", node1);

                // ユーザーが展開ボタンを押すのと同じく、ToggleButton の UI オートメーションで切り替える。
                ((System.Windows.Automation.Provider.IToggleProvider)new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(expander)).Toggle();
                await Settle(panel);
                bool kept = BindingOperations.GetBindingExpression(node1, TreeViewItem.IsExpandedProperty) is not null;
                rows.Add([T("demo IsExpanded section, node collapsed by its expander: IsExpanded / CheckBox / binding", "デモの IsExpanded の欄、展開ボタンでノードを折りたたむ: IsExpanded / CheckBox / バインド"),
                    T($"{node1.IsExpanded} / {check.IsChecked} / {(kept ? "kept" : "removed")}", $"{node1.IsExpanded} / {check.IsChecked} / {(kept ? "残る" : "外れる")}")]);

                check.IsChecked = false;
                check.IsChecked = true;
                await Settle(panel);
                rows.Add([T("  then the CheckBox unchecked and checked again: IsExpanded", "  続けて CheckBox を外してもう一度チェック: IsExpanded"), node1.IsExpanded.ToString()]);
            }, activate: true);
        }

        {
            // 親を折りたたんだまま、子のソースだけ展開状態にしておく。
            var leaf = new Node("Leaf");
            var child = new Node("Child", leaf) { IsExpanded = true };
            var root = new Node("Root", child);
            var tree = new TreeView
            {
                Width = 200, Height = 150, ItemsSource = new[] { root }, ItemTemplate = NodeTemplate(),
                ItemContainerStyle = BoundContainerStyle(),
            };
            await ShowAsync(tree, async () =>
            {
                TreeViewItem rootItem = Container(tree, root);
                Loc before = Container(rootItem, child) is null ? T("not created", "作られていない") : T("created", "作られている");
                root.IsExpanded = true;
                await Settle(tree);
                TreeViewItem? childItem = Container(rootItem, child);
                rows.Add([T("child IsExpanded=true (source) under a collapsed parent: container before / after expanding", "折りたたまれた親の下の子が IsExpanded=true（ソース）: コンテナー（展開前 / 展開後）"),
                    T($"{before.En} / created, IsExpanded {childItem?.IsExpanded}, leaf {(childItem is not null && Container(childItem, leaf) is not null ? "created" : "not created")}", $"{before.Ja} / 作られている、IsExpanded {childItem?.IsExpanded}、葉 {(childItem is not null && Container(childItem, leaf) is not null ? "作られている" : "作られていない")}")]);
            });
        }

        {
            // デモアプリの ItemTemplate 欄と同じく、明示した HierarchicalDataTemplate。
            ObservableCollection<Node> data = DeviceTree();
            data[0].IsExpanded = true;
            var tree = new TreeView
            {
                Width = 200, Height = 150, ItemsSource = data, ItemTemplate = NodeTemplate(),
                ItemContainerStyle = BoundContainerStyle(),
            };
            await ShowAsync(tree, async () =>
            {
                TreeViewItem desktop = Container(tree, data[0]);
                TreeViewItem workstation = Container(desktop, data[0].Children[0]);
                string shown = Descendants(workstation).OfType<TextBlock>().FirstOrDefault()?.Text ?? "(none)";
                rows.Add([T("explicit HierarchicalDataTemplate: text of a second-level node", "明示した HierarchicalDataTemplate: 2 階層目のノードの文字"), $"\"{shown}\""]);
                await Task.CompletedTask;
            });
        }

        foreach (Type dataType in new[] { typeof(Node), typeof(string) })
        {
            // 暗黙のテンプレート（リソースに DataType で登録）の型が合う場合と合わない場合。
            var template = new HierarchicalDataTemplate(dataType)
            {
                ItemsSource = new Binding(nameof(Node.Children)),
                VisualTree = NameText(),
            };
            var tree = new TreeView { Width = 200, Height = 150, ItemsSource = DeviceTree() };
            tree.Resources.Add(template.DataTemplateKey!, template);
            await ShowAsync(tree, async () =>
            {
                var first = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
                string shown = Descendants(first).OfType<TextBlock>().FirstOrDefault()?.Text ?? "(none)";
                rows.Add([T($"implicit HierarchicalDataTemplate, DataType={dataType.Name} (items are Node)", $"暗黙の HierarchicalDataTemplate、DataType={dataType.Name}（項目は Node）"),
                    T($"first node shows \"{shown}\", expandable {first.HasItems}", $"最初のノードの表示 \"{shown}\"、展開できるか {first.HasItems}")]);
                await Task.CompletedTask;
            });
        }

        {
            var many = Enumerable.Range(1, 1000).Select(i => new Node($"Node {i}")).ToList();
            foreach (bool? virtualizing in new bool?[] { null, true })
            {
                var tree = new TreeView { Width = 200, Height = 100, ItemsSource = many, ItemTemplate = NodeTemplate() };
                if (virtualizing is not null)
                {
                    VirtualizingPanel.SetIsVirtualizing(tree, virtualizing.Value);
                }

                await ShowAsync(tree, async () =>
                {
                    rows.Add([T($"1,000 root nodes, IsVirtualizing {(virtualizing is null ? "not set" : virtualizing.ToString())}: value, panel, TreeViewItems", $"ルートのノード 1,000 個、IsVirtualizing {(virtualizing is null ? "指定なし" : virtualizing.ToString())}: 値, パネル, TreeViewItem の数"),
                        $"{VirtualizingPanel.GetIsVirtualizing(tree)}, {Descendants(tree).OfType<Panel>().First(p => p.IsItemsHost).GetType().Name}, " +
                        $"{Descendants(tree).OfType<TreeViewItem>().Count()}"]);
                    await Task.CompletedTask;
                });
            }
        }

        foreach ((Loc label, Key key) in new (Loc, Key)[]
        {
            ("Down", Key.Down), ("Right", Key.Right), (T("Left (after Right)", "Left（Right の後）"), Key.Left),
            ("Space", Key.Space), ("Enter", Key.Enter), (T("numpad *", "テンキーの *"), Key.Multiply),
        })
        {
            ObservableCollection<Node> data =
            [
                new Node("A", new Node("A1", new Node("A1a")), new Node("A2")),
                new Node("B"),
            ];
            var tree = new TreeView
            {
                Width = 200, Height = 150, ItemsSource = data, ItemTemplate = NodeTemplate(),
                ItemContainerStyle = BoundContainerStyle(),
            };
            await ShowAsync(tree, async () =>
            {
                TreeViewItem a = Container(tree, data[0]);
                a.IsSelected = true;
                a.Focus();
                await Settle(tree);
                if (key == Key.Left)
                {
                    PressKey(a, Key.Right);
                    await Settle(tree);
                }

                PressKey(a, key);
                await Settle(tree);
                Node[] watched = [data[0], data[0].Children[0]];
                string expanded = string.Join(", ", watched.Select(n => $"{n.Name} {(n.IsExpanded ? "open" : "closed")}"));
                string expandedJa = string.Join(", ", watched.Select(n => $"{n.Name} {(n.IsExpanded ? "展開" : "折りたたみ")}"));
                rows.Add([T($"key {label.En} on A (selected, focused)", $"A（選択、フォーカスあり）で {label.Ja} キー"),
                    T($"selected {(tree.SelectedItem as Node)?.Name ?? "none"}; {expanded}", $"選択 {(tree.SelectedItem as Node)?.Name ?? "なし"}; {expandedJa}")]);
            }, activate: true);
        }

        {
            ObservableCollection<Node> data = DeviceTree();
            var style = BoundContainerStyle();
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Rename" });
            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
            var tree = new TreeView { Width = 200, Height = 150, ItemsSource = data, ItemTemplate = NodeTemplate(), ItemContainerStyle = style };
            await ShowAsync(tree, async () =>
            {
                TreeViewItem mobile = Container(tree, data[1]);
                ContextMenu contextMenu = mobile.ContextMenu!;
                string before = WpfProbe.Describe(contextMenu.DataContext);
                contextMenu.PlacementTarget = mobile;
                contextMenu.IsOpen = true;
                await Settle(tree);
                string open = WpfProbe.Describe(contextMenu.DataContext);
                contextMenu.IsOpen = false;
                rows.Add([T("ContextMenu from ItemContainerStyle (Mobile): DataContext before / while open", "ItemContainerStyle の ContextMenu（Mobile）: DataContext（開く前 / 開いている間）"),
                    $"{before} / {open}"]);
            });
        }

        {
            var tree = new TreeView();
            var host = new Grid();
            host.Children.Add(tree);
            Layout(host, 200, 100);
            rows.Add([T("ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility (value source)", "ScrollViewer.HorizontalScrollBarVisibility / VerticalScrollBarVisibility（値の出どころ）"),
                $"{WpfProbe.ValueAndSource(tree, ScrollViewer.HorizontalScrollBarVisibilityProperty)} / " +
                WpfProbe.ValueAndSource(tree, ScrollViewer.VerticalScrollBarVisibilityProperty)]);
        }


        {
            // 使用例「フォルダーのツリーで、展開されたときに子を読み込む」。仮の子を置き、Expanded で本来の子に入れ替える。
            var folder = new TreeViewItem { Header = "Folder" };
            folder.Items.Add(new TreeViewItem { Header = "(loading)" });
            int loads = 0;
            folder.Expanded += (_, _) =>
            {
                if (folder.Items.Count == 1 && folder.Items[0] is TreeViewItem { Header: "(loading)" })
                {
                    loads++;
                    folder.Items.Clear();
                    folder.Items.Add(new TreeViewItem { Header = "Child 1" });
                    folder.Items.Add(new TreeViewItem { Header = "Child 2" });
                }
            };
            var tree = new TreeView { Width = 200, Height = 120, Items = { folder } };
            await ShowAsync(tree, async () =>
            {
                Window window = await FrontAsync(tree);
                var expander = (ToggleButton)folder.Template.FindName("Expander", folder);
                string Children() => string.Join(", ", folder.Items.OfType<TreeViewItem>().Select(i => i.Header));
                string before = Children();
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(expander);
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([T("placeholder replaced in Expanded: before; after real click on expander (loads)", "Expanded で仮の項目を置き換える: 前; 展開ボタンを実際にクリックした後（読み込みの回数）"),
                    $"{before}; {Children()} ({loads})"]);
            });
        }

        return rows;
    }
}
