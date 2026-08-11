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
