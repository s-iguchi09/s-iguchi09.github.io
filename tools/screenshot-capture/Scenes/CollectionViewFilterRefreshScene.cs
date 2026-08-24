using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の CollectionViewSource でフィルタが再評価されない問題」の図。
/// 同一のフィルタ（Stock &gt; 0）を掛けた 3 つのビューに対して同じプロパティ変更を与え、
/// 何もしない場合・Refresh() を呼ぶ場合・IsLiveFiltering を有効にした場合で
/// 表示内容が変わることを示す。
/// </summary>
internal sealed class CollectionViewFilterRefreshScene : IScene
{
    public string Slug => "wpf-collectionviewsource-filter-not-refreshing";

    public async Task CaptureAsync(SceneContext context)
    {
        (ListBox list, ObservableCollection<Product> source, ICollectionView view) plain = BuildPanel();
        (ListBox list, ObservableCollection<Product> source, ICollectionView view) refreshed = BuildPanel();
        (ListBox list, ObservableCollection<Product> source, ICollectionView view) live = BuildPanel();

        var liveShaping = (ListCollectionView)live.view;
        liveShaping.IsLiveFiltering = true;
        liveShaping.LiveFilteringProperties.Add(nameof(Product.Stock));

        Window window = DemoLayout.BuildPanelWindow(
            "ICollectionView.Filter",
            [
                new DemoLayout.Panel("Filter", plain.list),
                new DemoLayout.Panel("Filter + Refresh()", refreshed.list),
                new DemoLayout.Panel("Filter + IsLiveFiltering", live.list),
            ]);

        await context.ShootAsync(window, "collectionview-filter-refresh.png", async _ =>
        {
            // 3 つのビューへ同じ変更を与える。フィルタ条件は Stock > 0 なので、
            // Stock を 0 から 8 に変えた時点でこの項目はフィルタを通るようになる。
            plain.source[0].Stock = 8;
            refreshed.source[0].Stock = 8;
            live.source[0].Stock = 8;

            refreshed.view.Refresh();

            // ライブフィルタの反映は Dispatcher コールバックで行われるため、
            // メッセージポンプを回してから撮影する。
            await Task.Delay(400);
        });
    }

    private static (ListBox List, ObservableCollection<Product> Source, ICollectionView View) BuildPanel()
    {
        var source = new ObservableCollection<Product>
        {
            new() { Name = "Bolt", Stock = 0 },
            new() { Name = "Nut", Stock = 5 },
            new() { Name = "Washer", Stock = 12 },
            new() { Name = "Screw", Stock = 3 },
        };

        var list = SceneContext.LoadXaml<ListBox>(
            """
            <ListBox Width="176" Height="112" BorderThickness="1">
              <ListBox.ItemTemplate>
                <DataTemplate>
                  <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding Name}" Width="72" />
                    <TextBlock Text="{Binding Stock, StringFormat='Stock = {0}'}" />
                  </StackPanel>
                </DataTemplate>
              </ListBox.ItemTemplate>
            </ListBox>
            """);

        list.ItemsSource = source;

        ICollectionView view = CollectionViewSource.GetDefaultView(source);
        view.Filter = item => ((Product)item).Stock > 0;

        return (list, source, view);
    }

    /// <summary>記事の例と同じく、在庫数の変化でフィルタの通過可否が変わるモデル。</summary>
    private sealed class Product : INotifyPropertyChanged
    {
        private int _stock;

        public string Name { get; init; } = string.Empty;

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
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
