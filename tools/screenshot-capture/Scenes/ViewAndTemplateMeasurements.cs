using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// ビューの再評価と、テンプレート内のバインド解決を実測する部品。
/// </summary>
internal static class ViewAndTemplateMeasurements
{
    // ------------------------------------------------------------------
    // ICollectionView のフィルタが再評価される契機
    // ------------------------------------------------------------------

    /// <summary>フィルタ述語の呼び出し回数を数えられる項目。</summary>
    private sealed class Row : INotifyPropertyChanged
    {
        private int _stock;

        public required string Name { get; init; }

        public int Stock
        {
            get => _stock;
            set
            {
                if (_stock == value)
                {
                    return;
                }

                _stock = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stock)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 操作ごとに、フィルタ述語が何回呼ばれ、ビューの件数がどうなるかを数える。
    ///
    /// 「値は変わったのに一覧から出入りしない」という症状は、
    /// 呼び出し回数が 0 であることを示せば原因まで説明できる。
    /// </summary>
    public static async Task<List<IReadOnlyList<string>>> FilterRefreshAsync()
    {
        const int ItemCount = 1000;

        var rows = new List<IReadOnlyList<string>>();

        (string Label, Action<ObservableCollection<Row>, ICollectionView> Operate)[] operations =
        [
            ("Add 1 item (passes filter)", (items, _) => items.Add(new Row { Name = "added", Stock = 1 })),
            ("Add 1 item (fails filter)", (items, _) => items.Add(new Row { Name = "added", Stock = 0 })),
            ("Remove 1 item", (items, _) => items.RemoveAt(0)),
            ("item.Stock = 0 (leaves filter)", (items, _) => items[0].Stock = 0),
            ("item.Stock = 1 (enters filter)", (items, _) => items[^1].Stock = 1),
            ("view.Refresh()", (_, view) => view.Refresh()),
        ];

        foreach ((string label, Action<ObservableCollection<Row>, ICollectionView> operate) in operations)
        {
            // 前半は在庫あり、後半は在庫なし。どちらの向きの変化も測れるようにする。
            var items = new ObservableCollection<Row>(
                Enumerable.Range(0, ItemCount)
                    .Select(i => new Row { Name = $"item {i}", Stock = i < ItemCount / 2 ? 1 : 0 }));

            var source = new CollectionViewSource { Source = items };
            ICollectionView view = source.View;

            int calls = 0;
            int collectionChanged = 0;
            view.Filter = item => { calls++; return ((Row)item).Stock > 0; };
            ((INotifyCollectionChanged)view).CollectionChanged += (_, _) => collectionChanged++;

            var host = new ItemsControl { ItemsSource = view };
            List<IReadOnlyList<string>> measured = await WpfProbe.MeasureAsync(
            [
                new WpfProbe.Case(
                    label,
                    host,
                    _ => [calls.ToString(), collectionChanged.ToString(), view.Cast<object>().Count().ToString()],
                    Act: _ =>
                    {
                        // フィルタ設定時の初回評価を数から外し、操作ぶんだけを見る。
                        calls = 0;
                        collectionChanged = 0;
                        operate(items, view);
                        return Task.CompletedTask;
                    }),
            ]);

            rows.Add(measured[0]);
        }

        return rows;
    }

    // ------------------------------------------------------------------
    // ScrollViewer がスクロールしない理由
    // ------------------------------------------------------------------

    /// <summary>
    /// 親のレイアウトを変えて、<see cref="ScrollViewer"/> に渡る高さがどうなるかを測る。
    ///
    /// <c>ScrollableHeight</c> が 0 のままなら、スクロールバーは出ない。
    /// </summary>
    public static Task<List<IReadOnlyList<string>>> ScrollViewerHeightAsync()
    {
        const int ItemCount = 40;

        return WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case("inside StackPanel", BuildScrollCase("StackPanel", ItemCount), ReadScroll),
            new WpfProbe.Case("inside Grid", BuildScrollCase("Grid", ItemCount), ReadScroll),
            new WpfProbe.Case("inside DockPanel (LastChildFill)", BuildScrollCase("DockPanel", ItemCount), ReadScroll),
            new WpfProbe.Case("StackPanel + explicit Height", BuildScrollCase("StackPanelFixed", ItemCount), ReadScroll),
        ]);
    }

    private static IReadOnlyList<string> ReadScroll(FrameworkElement root)
    {
        var scroll = (ScrollViewer)root.FindName("Target");
        return
        [
            scroll.ExtentHeight.ToString("0"),
            scroll.ViewportHeight.ToString("0"),
            scroll.ScrollableHeight.ToString("0"),
            scroll.ComputedVerticalScrollBarVisibility.ToString(),
        ];
    }

    private static FrameworkElement BuildScrollCase(string layout, int itemCount)
    {
        string items = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, itemCount).Select(i => $"""        <TextBlock Text="row {i}" Height="20" />"""));

        string scroller = $"""
              <ScrollViewer x:Name="Target" VerticalScrollBarVisibility="Auto">
                <StackPanel>
            {items}
                </StackPanel>
              </ScrollViewer>
            """;

        string xaml = layout switch
        {
            "StackPanel" => $"""<StackPanel Height="200">{scroller}</StackPanel>""",
            "StackPanelFixed" => $"""<StackPanel Height="200"><ScrollViewer x:Name="Target" VerticalScrollBarVisibility="Auto" Height="200"><StackPanel>{items}</StackPanel></ScrollViewer></StackPanel>""",
            "DockPanel" => $"""<DockPanel Height="200" LastChildFill="True">{scroller}</DockPanel>""",
            _ => $"""<Grid Height="200">{scroller}</Grid>""",
        };

        return SceneContext.LoadXaml<FrameworkElement>(xaml);
    }

    // ------------------------------------------------------------------
    // DataTemplate 内から親の DataContext を参照できるか
    // ------------------------------------------------------------------

    private sealed class ParentViewModel
    {
        public string Title { get; } = "parent";

        public ICommand DeleteCommand { get; } = new NoOpCommand();

        public ObservableCollection<Row> Items { get; } =
        [
            new Row { Name = "a", Stock = 1 },
        ];
    }

    private sealed class NoOpCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    /// <summary>
    /// テンプレート内のバインドの書き方を変え、<c>Command</c> が解決されるかを測る。
    ///
    /// 記事の要点は「解決に失敗してもボタンは無効化されない」ことなので、
    /// <c>Command</c> の有無と <c>IsEnabled</c> の両方を出す。
    /// </summary>
    public static Task<List<IReadOnlyList<string>>> TemplateBindingScopeAsync() =>
        WpfProbe.MeasureAsync(
        [
            new WpfProbe.Case(
                "{Binding DeleteCommand}",
                BuildTemplateCase("{Binding DeleteCommand}"),
                ReadTemplateButton),
            new WpfProbe.Case(
                "RelativeSource AncestorType=ItemsControl",
                BuildTemplateCase("{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"),
                ReadTemplateButton),
            new WpfProbe.Case(
                "ElementName=Root",
                BuildTemplateCase("{Binding DataContext.DeleteCommand, ElementName=Root}"),
                ReadTemplateButton),
        ]);

    private static IReadOnlyList<string> ReadTemplateButton(FrameworkElement root)
    {
        Button? button = Descendants(root).OfType<Button>().FirstOrDefault();
        if (button is null)
        {
            return ["(button not realized)", "-"];
        }

        return [button.Command is null ? "null" : "resolved", WpfProbe.Describe(button.IsEnabled)];
    }

    private static FrameworkElement BuildTemplateCase(string commandBinding)
    {
        var grid = SceneContext.LoadXaml<Grid>($$"""
            <Grid x:Name="Root">
              <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate>
                    <Button Content="Delete" Command="{{commandBinding}}" Width="80" />
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </Grid>
            """);

        grid.DataContext = new ParentViewModel();
        return grid;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (DependencyObject descendant in Descendants(VisualTreeHelper.GetChild(root, i)))
            {
                yield return descendant;
            }
        }
    }
}
