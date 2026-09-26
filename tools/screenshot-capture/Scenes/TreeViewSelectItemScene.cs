using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「TreeView の SelectedItem が読み取り専用でコードから選択できない問題」の図。
/// ItemContainerStyle で <c>IsExpanded</c> / <c>IsSelected</c> を ViewModel と双方向に結び、
/// ViewModel 側のプロパティを変更するだけで祖先が展開され、目的のノードが選択され、
/// 読み取り専用の <c>TreeView.SelectedItem</c> がそれに追従することを実際の描画で示す。
///
/// TreeView・ItemContainerStyle・読み出し用の TextBlock をまとめて解析する必要があるため、
/// ウィンドウの内容を 1 つの XAML で記述する。
/// </summary>
internal sealed class TreeViewSelectItemScene : IScene
{
    private const string ContentXaml =
        """
        <DockPanel Margin="12">
          <TextBlock DockPanel.Dock="Bottom" Margin="4,10,0,0"
                     FontFamily="Consolas, Courier New" FontSize="12" Foreground="#333D4D"
                     Text="{Binding SelectedItem.Name, ElementName=Tree, StringFormat='TreeView.SelectedItem = {0}'}" />
          <TreeView x:Name="Tree" ItemsSource="{Binding Roots}">
            <TreeView.ItemContainerStyle>
              <Style TargetType="TreeViewItem">
                <Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}" />
                <Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}" />
              </Style>
            </TreeView.ItemContainerStyle>
            <TreeView.ItemTemplate>
              <HierarchicalDataTemplate ItemsSource="{Binding Children}">
                <TextBlock Text="{Binding Name}" />
              </HierarchicalDataTemplate>
            </TreeView.ItemTemplate>
          </TreeView>
        </DockPanel>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "TreeView.SelectedItemProperty が読み取り専用として登録されていること",
        "外部からの SetValue が送出する例外の型",
        "子のコンテナが、親を展開するまで生成されないこと",
        "IsExpanded を true にした直後はまだ生成されておらず、レイアウトが走って初めて取得できること",
        "TreeViewItem.IsSelected を true にすると TreeView.SelectedItem に反映されること",
        "記事の XAML で、生成済みのコンテナの間では選択が排他になり、ViewModel にも書き戻されること",
        "コンテナが生成されていないノードとの間では排他にならず、ViewModel に true が 2 つ残ること",
        "ItemContainerStyle の IsSelected が TwoWay と OneWay のとき、コンテナへ代入した後の値の出どころと、ViewModel から選択を戻したときの反映",
        "選択中の項目の背景が、SystemColors のどのブラシと一致するか（フォーカスあり・なし）",
        "IsSelected を true にしてもスクロール位置は変わらず、BringIntoView で初めて動くこと（200 ノード、仮想化なし）",
        "仮想化を有効にすると、コンテナの無いノードを ViewModel から選んでも、スクロールで生成されるまで SelectedItem に届かないこと",
    ];

    public string Slug => "wpf-treeview-select-item-programmatically";

    public async Task CaptureAsync(SceneContext context)
    {
        var content = SceneContext.LoadXaml<DockPanel>(ContentXaml);
        var viewModel = BuildViewModel(out FolderNode target);

        // 記事の添付ビヘイビアと同じ処理。選択されたノードを表示範囲へ入れ、フォーカスを移す。
        // XAML の名前空間宣言を LoadXaml で補えないため、ここではコードから適用している。
        var tree = (TreeView)content.FindName("Tree");
        RevealSelectedItemBehavior.SetIsEnabled(tree, true);

        var window = new Window
        {
            Title = "TreeView / Select From ViewModel",
            Content = content,
            DataContext = viewModel,
            Width = 340,
            Height = 300,
            ResizeMode = ResizeMode.CanMinimize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };

        // 記事の実装例と同じく、コンテナには一切触れず ViewModel 側だけを操作する。
        await context.ShootAsync(
            window,
            "treeview-select-from-viewmodel.png",
            _ =>
            {
                target.SelectAndReveal();
                return Task.CompletedTask;
            });

        await context.SaveTableAsync(
            "TreeView selection and container generation",
            ["what was measured", "result"],
            await SelectionAndTriggerMeasurements.TreeViewSelectionAsync(),
            "treeview-selection-facts.svg");

        await context.SaveTableAsync(
            "the article's XAML: exclusivity, value source, and selection color",
            ["case", "measured"],
            await BehaviorAsync(),
            "treeview-selection-behavior.svg");
    }

    /// <summary>記事の XAML を読み込み、ViewModel を載せて表示したうえで測る。</summary>
    private static async Task<List<IReadOnlyList<string>>> BehaviorAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 生成済みのコンテナの間の排他。C: を展開して、その子 2 つを順に選ぶ。
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml);
            var viewModel = BuildViewModel(out _);
            FolderNode root = viewModel.Roots[0];
            root.IsExpanded = true;
            FolderNode first = root.Children[0];
            FolderNode second = root.Children[1];
            content.DataContext = viewModel;
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "select 'Program Files', then 'Users' (both generated)",
                    content,
                    _ => [$"ViewModel: Program Files {WpfProbe.Describe(first.IsSelected)}, Users {WpfProbe.Describe(second.IsSelected)}"],
                    Act: async _ =>
                    {
                        first.IsSelected = true;
                        await Task.Delay(50);
                        second.IsSelected = true;
                    }),
            ]));
        }

        // 生成されていないノードとの間。drivers は祖先が閉じているのでコンテナが無い。
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml);
            var viewModel = BuildViewModel(out FolderNode hidden);
            FolderNode root = viewModel.Roots[0];
            content.DataContext = viewModel;
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "select 'C:', then 'drivers' (no container yet)",
                    content,
                    _ =>
                    [
                        $"ViewModel: C: {WpfProbe.Describe(root.IsSelected)}, drivers {WpfProbe.Describe(hidden.IsSelected)}; " +
                        $"SelectedItem {((content.FindName("Tree") as TreeView)?.SelectedItem as FolderNode)?.Name ?? "null"}",
                    ],
                    Act: async _ =>
                    {
                        root.IsSelected = true;
                        await Task.Delay(50);
                        hidden.IsSelected = true;
                    }),
            ]));
        }

        // TwoWay と OneWay。コンテナへ代入した後の値の出どころと、ViewModel から選択を外したときの反映。
        foreach (string mode in new[] { "TwoWay", "OneWay" })
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml.Replace("Value=\"{Binding IsSelected, Mode=TwoWay}\"", $"Value=\"{{Binding IsSelected, Mode={mode}}}\""));
            var viewModel = BuildViewModel(out _);
            FolderNode root = viewModel.Roots[0];
            content.DataContext = viewModel;
            string sourceAfterAssign = "";
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    $"{mode}: assign container.IsSelected, then ViewModel false",
                    content,
                    _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(root);
                        return [$"source {sourceAfterAssign}; container.IsSelected {WpfProbe.Describe(container.IsSelected)}"];
                    },
                    Act: async _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(root);
                        container.IsSelected = true;
                        sourceAfterAssign = System.Windows.DependencyPropertyHelper.GetValueSource(container, TreeViewItem.IsSelectedProperty).BaseValueSource.ToString();
                        await Task.Delay(50);
                        root.IsSelected = true;
                        root.IsSelected = false;
                    }),
            ]));
        }

        // 選択中の項目の背景。既定テンプレートの Bd を読み、SystemColors のブラシと比べる。
        foreach (bool focused in new[] { true, false })
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml);
            var viewModel = BuildViewModel(out _);
            FolderNode root = viewModel.Roots[0];
            var other = new Button { Content = "other" };
            DockPanel.SetDock(other, Dock.Top);
            content.Children.Insert(0, other);
            content.DataContext = viewModel;
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    focused ? "selected background, item focused" : "selected background, focus elsewhere",
                    content,
                    _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(root);
                        var border = container.Template.FindName("Bd", container) as Border;
                        Color? color = (border?.Background as SolidColorBrush)?.Color;
                        string match =
                            color == SystemColors.HighlightColor ? "SystemColors.HighlightColor" :
                            color == SystemColors.InactiveSelectionHighlightBrush.Color ? "SystemColors.InactiveSelectionHighlightBrush" :
                            color == SystemColors.AccentColor ? "SystemColors.AccentColor" :
                            "no match";
                        return [$"{color?.ToString() ?? "null"} = {match}"];
                    },
                    Act: async _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(root);
                        root.IsSelected = true;
                        await DemoProbe.FocusAsync(focused ? container : other);
                    }),
            ]));
        }

        rows.AddRange(await ScrollAndVirtualizationAsync());
        return rows;
    }

    /// <summary>
    /// 注意点の 2 つを測る。IsSelected を変えてもスクロールしないこと（BringIntoView で初めて動く）と、
    /// 仮想化を有効にしたとき、コンテナの無いノードの選択はスクロールで生成されるまで TreeView に届かないこと。
    /// ノードは表示範囲を大きく超える 200 個を最上位に並べ、最後のノードを選ぶ。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> ScrollAndVirtualizationAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        static ExplorerViewModel ManyRoots(out FolderNode last)
        {
            var roots = Enumerable.Range(1, 200).Select(i => new FolderNode($"Folder {i}")).ToArray();
            last = roots[^1];
            return new ExplorerViewModel(roots);
        }

        static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer viewer)
                {
                    return viewer;
                }

                if (FindScrollViewerOrNull(child) is { } found)
                {
                    return found;
                }
            }

            throw new InvalidOperationException("ScrollViewer が見つからない。");
        }

        static ScrollViewer? FindScrollViewerOrNull(DependencyObject root)
        {
            try
            {
                return FindScrollViewer(root);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        // 既定（仮想化なし）: IsSelected を true にしてもスクロール位置は変わらず、BringIntoView で初めて動くか。
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml);
            var viewModel = ManyRoots(out FolderNode last);
            content.DataContext = viewModel;
            string afterSelect = "";
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "last of 200 nodes: IsSelected, then BringIntoView",
                    content,
                    _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        double offset = FindScrollViewer(tree).VerticalOffset;
                        return [$"{afterSelect} -> {offset:0.##}"];
                    },
                    Act: async _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        ScrollViewer viewer = FindScrollViewer(tree);
                        last.IsSelected = true;
                        await Task.Delay(100);
                        afterSelect = $"selected {(tree.SelectedItem as FolderNode)?.Name ?? "null"}, offset {viewer.VerticalOffset:0.##}";
                        var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(last);
                        container.BringIntoView();
                        await Task.Delay(100);
                    }),
            ]));
        }

        // 仮想化あり: コンテナの無いノードを ViewModel から選ぶと、スクロールで生成されるまで SelectedItem に届かないか。
        {
            var content = SceneContext.LoadXaml<DockPanel>(ContentXaml.Replace(
                "<TreeView x:Name=\"Tree\" ItemsSource=\"{Binding Roots}\">",
                "<TreeView x:Name=\"Tree\" ItemsSource=\"{Binding Roots}\" VirtualizingPanel.IsVirtualizing=\"True\">"));
            var viewModel = ManyRoots(out FolderNode last);
            content.DataContext = viewModel;
            string beforeScroll = "";
            rows.AddRange(await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    "IsVirtualizing: last of 200 selected in ViewModel",
                    content,
                    _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        bool container = tree.ItemContainerGenerator.ContainerFromItem(last) is not null;
                        return [$"{beforeScroll} -> scrolled: {(container ? "container" : "no container")}, {(tree.SelectedItem as FolderNode)?.Name ?? "null"}"];
                    },
                    Act: async _ =>
                    {
                        var tree = (TreeView)content.FindName("Tree");
                        last.IsSelected = true;
                        await Task.Delay(100);
                        bool container = tree.ItemContainerGenerator.ContainerFromItem(last) is not null;
                        beforeScroll = $"SelectedItem: {(container ? "container" : "no container")}, {(tree.SelectedItem as FolderNode)?.Name ?? "null"}";
                        FindScrollViewer(tree).ScrollToEnd();
                        await Task.Delay(300);
                    }),
            ]));
        }

        return rows;
    }

    /// <summary>
    /// 選択されたノードを表示範囲へスクロールし、フォーカスを移す添付ビヘイビア。
    /// 記事の実装例と同じ内容にしている。
    /// </summary>
    private static class RevealSelectedItemBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(RevealSelectedItemBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value)
            => element.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TreeView treeView)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                treeView.AddHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(OnItemSelected));
            }
            else
            {
                treeView.RemoveHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(OnItemSelected));
            }
        }

        private static void OnItemSelected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TreeViewItem item)
            {
                return;
            }

            item.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    item.BringIntoView();
                    item.Focus();
                }));
        }
    }

    /// <summary>
    /// 図に使うツリーを組み立て、選択対象のノードを返す。
    /// 目的のノードは 3 階層下にあり、初期状態では画面に現れない。
    /// </summary>
    private static ExplorerViewModel BuildViewModel(out FolderNode target)
    {
        var root = new FolderNode("C:");
        root.Add(new FolderNode("Program Files"));

        var users = root.Add(new FolderNode("Users"));
        users.Add(new FolderNode("Public"));

        var windows = root.Add(new FolderNode("Windows"));
        windows.Add(new FolderNode("Fonts"));

        var system32 = windows.Add(new FolderNode("System32"));
        target = system32.Add(new FolderNode("drivers"));
        system32.Add(new FolderNode("spool"));

        return new ExplorerViewModel(root);
    }

    /// <summary>TreeView の <c>ItemsSource</c> に設定する ViewModel 相当。</summary>
    private sealed class ExplorerViewModel(params FolderNode[] roots)
    {
        public ObservableCollection<FolderNode> Roots { get; } = new(roots);
    }

    /// <summary>
    /// 展開状態と選択状態を自分で持つノード。記事の実装例と同じ構造にしている。
    /// </summary>
    private sealed class FolderNode(string name) : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;

        public string Name { get; } = name;

        public FolderNode? Parent { get; private set; }

        public ObservableCollection<FolderNode> Children { get; } = [];

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FolderNode Add(FolderNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        /// <summary>ルートまでの祖先を展開し、自ノードを選択する。</summary>
        public void SelectAndReveal()
        {
            for (FolderNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                ancestor.IsExpanded = true;
            }

            IsSelected = true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
