using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法」の図。
///
/// 選択状態の同期方法を変えて、仮想化された <see cref="ListBox"/> で
/// <c>SelectedItems</c> とデータ側の <c>IsSelected</c> がどこまで一致するかを実測する。
/// 図の数値は撮影のたびに実行して求めるため、本文の記述と食い違わない。
/// </summary>
internal sealed class ListBoxSelectionSyncScene : IScene
{
    private const int ItemCount = 10_000;

    /// <summary>レイアウト時間の計測を繰り返す回数。最小値を採る。</summary>
    private const int MeasureIterations = 5;

    /// <summary>スクロールでコンテナを作り直させる回数。</summary>
    private const int PageDownCount = 10;

    public IReadOnlyList<string> Verifies =>
    [
        "仮想化した ListBox をスクロールさせ、SelectedItems とデータ側の IsSelected が何件一致するかを測る",
        "ItemContainerStyle のバインドだけでは、スクロールで選択が失われること",
        "VirtualizingStackPanel の仮想化を切った場合の visual 要素数とレイアウト時間",
        "バインドと SelectionChanged を併用したとき、スクロールでコンテナが実体化されるたびに SelectionChanged が発生するか",
        "画面外の項目をデータ側で選んだとき SelectedItems に載るか、ScrollIntoView で実体化されると載るか",
        "実際のキー入力（Shift+End）で画面外まで範囲選択したとき、SelectionChanged の AddedItems に画面外の項目が含まれるか",
    ];

    public string Slug => "wpf-listbox-virtualization-selecteditems";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildSelectionSyncWindow(), "listbox-selection-sync-measurement.png");
        await context.ShootAsync(BuildVirtualizationCostWindow(), "listbox-virtualization-cost.png");

        await context.SaveTableAsync(
            $"ListBox, {ItemCount:N0} items, virtualized: when SelectionChanged is raised",
            ["", "", "measured"],
            MeasureSelectionEvents(),
            "listbox-selection-events.svg");
    }

    /// <summary>SelectionChanged の発生回数と、AddedItems / RemovedItems の件数の累計。</summary>
    private sealed class SelectionLog
    {
        public int Raised { get; set; }

        public int Added { get; set; }

        public int Removed { get; set; }

        public void Reset() => (Raised, Added, Removed) = (0, 0, 0);

        public override string ToString() => $"SelectionChanged {Raised:N0} (added {Added:N0}, removed {Removed:N0})";
    }

    /// <summary>
    /// SelectionChanged を数える。<paramref name="writeBack"/> が真のとき、記事の解決策どおり
    /// 選択の変化をデータ側へ反映する。
    /// </summary>
    private static SelectionLog Attach(ListBox listBox, bool writeBack)
    {
        var log = new SelectionLog();
        listBox.SelectionChanged += (_, e) =>
        {
            log.Raised++;
            log.Added += e.AddedItems.Count;
            log.Removed += e.RemovedItems.Count;

            if (!writeBack)
            {
                return;
            }

            foreach (RowItemViewModel item in e.AddedItems)
            {
                item.IsSelected = true;
            }

            foreach (RowItemViewModel item in e.RemovedItems)
            {
                item.IsSelected = false;
            }
        };
        return log;
    }

    /// <summary>
    /// SelectionChanged がどの操作で発生するかを測る。
    /// 範囲選択は、ListBox が Keyboard.Modifiers で Shift を判定するため、実際のキー入力で行う。
    /// </summary>
    private static List<IReadOnlyList<string>> MeasureSelectionEvents()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 1. 併用の構成で全件を選び、スクロールでコンテナを実体化させる。
        {
            List<RowItemViewModel> items = CreateRows();
            ListBox listBox = CreateListBox(items, bindIsSelected: true);
            SelectionLog log = Attach(listBox, writeBack: true);
            using var host = new HostWindow(listBox);

            listBox.SelectAll();
            host.Settle();
            log.Reset();
            PageDown(listBox, host);
            rows.Add(["both", $"SelectAll(), then PageDown x{PageDownCount}", log.ToString()]);
        }

        // 2. 併用の構成で、画面外の 1 件をデータ側で選び、その行までスクロールする。
        {
            List<RowItemViewModel> items = CreateRows();
            ListBox listBox = CreateListBox(items, bindIsSelected: true);
            SelectionLog log = Attach(listBox, writeBack: true);
            using var host = new HostWindow(listBox);

            RowItemViewModel target = items[ItemCount / 2];
            target.IsSelected = true;
            host.Settle();
            rows.Add(["both", $"{target.Name}.IsSelected = true (off screen)", $"SelectedItems {listBox.SelectedItems.Count}, {log}"]);

            listBox.ScrollIntoView(target);
            host.Settle();
            rows.Add(["both", $"then ScrollIntoView({target.Name})", $"SelectedItems {listBox.SelectedItems.Count}, {log}"]);
        }

        // 3. 先頭の行を選び、Shift+End で末尾まで範囲選択する。
        foreach ((string label, bool bind, bool writeBack) in new[] { ("SelectionChanged", false, true), ("ItemContainerStyle", true, false) })
        {
            List<RowItemViewModel> items = CreateRows();
            ListBox listBox = CreateListBox(items, bind);
            SelectionLog log = Attach(listBox, writeBack);
            using var host = new HostWindow(listBox);

            listBox.SelectedIndex = 0;
            host.Settle();
            var first = (ListBoxItem)listBox.ItemContainerGenerator.ContainerFromIndex(0);
            host.Activate();
            first.Focus();
            host.Settle();
            log.Reset();

            // 実際のキー入力はフォアグラウンドのウィンドウへ届く。Activate や Focus は失敗しうるので、
            // 送る前に確かめ、満たせなければ誤った件数を記録せずに計測を失敗させる。
            if (!host.IsActive || !first.IsKeyboardFocused)
            {
                throw new InvalidOperationException(
                    $"Shift+End を送る前提が満たせない（IsActive={host.IsActive}, IsKeyboardFocused={first.IsKeyboardFocused}）。画面を表示し、ほかのウィンドウを前面に出さずに撮り直す。");
            }

            PressShiftEnd(host);
            rows.Add(
            [
                label,
                "Row 1 selected, then Shift+End",
                $"SelectedItems {Format(listBox.SelectedItems.Count)}, IsSelected {Format(items.Count(x => x.IsSelected))}, {log}",
            ]);
        }

        return rows;
    }

    private static void PageDown(ListBox listBox, HostWindow host)
    {
        ScrollViewer scrollViewer = FindFirst<ScrollViewer>(listBox)
            ?? throw new InvalidOperationException("ScrollViewer が見つからない。");
        for (int i = 0; i < PageDownCount; i++)
        {
            scrollViewer.PageDown();
            host.Settle();
        }
    }

    private const byte VkShift = 0x10;
    private const byte VkEnd = 0x23;
    private const uint KeyEventExtendedKey = 0x1;
    private const uint KeyEventKeyUp = 0x2;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>実際のキー入力として Shift+End を送り、ListBox が処理し終えるまで待つ。</summary>
    private static void PressShiftEnd(HostWindow host)
    {
        keybd_event(VkShift, 0, 0, UIntPtr.Zero);
        keybd_event(VkEnd, 0, KeyEventExtendedKey, UIntPtr.Zero);
        keybd_event(VkEnd, 0, KeyEventExtendedKey | KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(VkShift, 0, KeyEventKeyUp, UIntPtr.Zero);

        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(50);
            host.Settle();
        }
    }

    /// <summary>各行を表す ViewModel。記事の実装例と同じ形にする。</summary>
    private sealed class RowItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public required string Name { get; init; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static List<RowItemViewModel> CreateRows(int count = ItemCount)
    {
        var rows = new List<RowItemViewModel>(count);
        for (int i = 1; i <= count; i++)
        {
            rows.Add(new RowItemViewModel { Name = $"Row {i}" });
        }

        return rows;
    }

    /// <summary>
    /// 仮想化を有効にした <see cref="ListBox"/> を組み立てる。
    /// <paramref name="bindIsSelected"/> が真のとき、記事の解決策どおり
    /// <c>ItemContainerStyle</c> で <c>ListBoxItem.IsSelected</c> を TwoWay バインドする。
    /// </summary>
    private static ListBox CreateListBox(List<RowItemViewModel> rows, bool bindIsSelected, bool canContentScroll = true)
    {
        var listBox = new ListBox
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.Extended,
            Width = 400,
            Height = 600,
            ItemTemplate = SceneContext.LoadXaml<DataTemplate>(
                """<DataTemplate><TextBlock Text="{Binding Name}" /></DataTemplate>"""),
        };

        if (bindIsSelected)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(
                ListBoxItem.IsSelectedProperty,
                new Binding(nameof(RowItemViewModel.IsSelected))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                }));
            listBox.ItemContainerStyle = style;
        }

        ScrollViewer.SetCanContentScroll(listBox, canContentScroll);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);
        return listBox;
    }

    /// <summary>
    /// <c>SelectAll</c> の直後と、スクロールでコンテナを作り直した後で、
    /// <c>SelectedItems</c> とデータ側の <c>IsSelected</c> がどれだけ一致するかを測る。
    /// </summary>
    private static Window BuildSelectionSyncWindow()
    {
        (string Label, bool Bind, bool Handle)[] configurations =
        [
            ("ItemContainerStyle", true, false),
            ("SelectionChanged", false, true),
            ("both", true, true),
        ];

        var rows = new List<IReadOnlyList<string>>();

        foreach ((string label, bool bind, bool handle) in configurations)
        {
            List<RowItemViewModel> items = CreateRows();
            ListBox listBox = CreateListBox(items, bind);

            if (handle)
            {
                // 選択の変化をデータ側へ明示的に反映する。
                // 実体化済みのコンテナに限られるバインドと違い、全件に届く。
                listBox.SelectionChanged += (_, e) =>
                {
                    foreach (RowItemViewModel item in e.AddedItems)
                    {
                        item.IsSelected = true;
                    }

                    foreach (RowItemViewModel item in e.RemovedItems)
                    {
                        item.IsSelected = false;
                    }
                };
            }

            using var host = new HostWindow(listBox);

            listBox.SelectAll();
            host.Settle();
            rows.Add([label, "SelectAll()", Format(listBox.SelectedItems.Count), Format(items.Count(x => x.IsSelected))]);

            var scrollViewer = FindFirst<ScrollViewer>(listBox)
                ?? throw new InvalidOperationException("ScrollViewer が見つからない。");
            for (int i = 0; i < PageDownCount; i++)
            {
                scrollViewer.PageDown();
                host.Settle();
            }

            rows.Add([label, $"+ PageDown x{PageDownCount}", Format(listBox.SelectedItems.Count), Format(items.Count(x => x.IsSelected))]);
        }

        return DemoLayout.BuildTableWindow(
            $"ListBox, {ItemCount:N0} items, virtualized",
            ["", "", "SelectedItems", "IsSelected"],
            rows);
    }

    /// <summary>
    /// 実体化される <see cref="ListBoxItem"/> の数が件数に依存しないことと、
    /// <c>CanContentScroll</c> を <c>False</c> にすると仮想化が止まることを測る。
    /// 前者を示すため、件数を変えた 3 通りを同じ条件で計測する。
    /// </summary>
    private static Window BuildVirtualizationCostWindow()
    {
        // CanContentScroll="False" は全件分のコンテナを作るため、
        // 件数を増やした組み合わせは測らない。
        (int Items, bool CanContentScroll)[] conditions =
        [
            (100, true),
            (ItemCount, true),
            (100_000, true),
            (ItemCount, false),
        ];

        var rows = new List<IReadOnlyList<string>>();

        // ウィンドウの生成コストを計測に含めないよう、空のウィンドウを先に表示しておく。
        using var host = new HostWindow();

        // JIT とテンプレート初期化の影響が最初の条件だけに乗ると、
        // 件数が多いほど速いという誤解を招く並びになるため、先に暖機する。
        for (int i = 0; i < 2; i++)
        {
            host.MeasureMount(CreateListBox(CreateRows(), bindIsSelected: true));
            host.Clear();
        }

        foreach ((int items, bool canContentScroll) in conditions)
        {
            double best = double.MaxValue;
            int containers = 0;
            int visuals = 0;

            // 単発では GC やレンダリングの影響でばらつくため、複数回試して最小値を採る。
            for (int i = 0; i < MeasureIterations; i++)
            {
                List<RowItemViewModel> data = CreateRows(items);
                ListBox listBox = CreateListBox(data, bindIsSelected: true, canContentScroll);

                best = Math.Min(best, host.MeasureMount(listBox));
                containers = FindAll<ListBoxItem>(listBox).Count;
                visuals = CountVisuals(listBox);

                host.Clear();
            }

            rows.Add(
            [
                Format(items),
                canContentScroll.ToString(),
                Format(containers),
                Format(visuals),
                best.ToString("N0"),
            ]);
        }

        return DemoLayout.BuildTableWindow(
            "ListBox",
            ["items", "CanContentScroll", "ListBoxItem", "visuals", "layout ms"],
            rows);
    }

    private static string Format(int value) => value.ToString("N0");

    /// <summary>
    /// 計測対象を実際のウィンドウに載せる。仮想化はレイアウトが走らないと働かないため、
    /// オフスクリーンではなく表示済みのウィンドウを使う。
    /// </summary>
    private sealed class HostWindow : IDisposable
    {
        private readonly Window _window;

        public HostWindow(FrameworkElement? content = null)
        {
            _window = new Window
            {
                Content = content,
                Width = 440,
                Height = 640,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Brushes.White,
            };
            _window.Show();
            Settle();
        }

        /// <summary>
        /// 表示済みのウィンドウへ内容を載せ、レイアウトが終わるまでの時間を返す。
        /// ウィンドウ自体の生成コストは含めない。
        /// </summary>
        public double MeasureMount(FrameworkElement content)
        {
            var stopwatch = Stopwatch.StartNew();
            _window.Content = content;
            Settle();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        public bool IsActive => _window.IsActive;

        public void Activate()
        {
            _window.Activate();
            Settle();
        }

        public void Clear()
        {
            _window.Content = null;
            Settle();
        }

        /// <summary>レイアウトとコンテナ生成が終わるまでディスパッチャを回す。</summary>
        public void Settle()
        {
            _window.UpdateLayout();
            for (int i = 0; i < 3; i++)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                _window.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ContextIdle,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
        }

        public void Dispose()
        {
            _window.Content = null;
            _window.Close();
        }
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit)
            {
                return hit;
            }

            T? deep = FindFirst<T>(child);
            if (deep is not null)
            {
                return deep;
            }
        }

        return null;
    }

    private static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        var result = new List<T>();

        void Walk(DependencyObject node)
        {
            int children = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < children; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child is T hit)
                {
                    result.Add(hit);
                }

                Walk(child);
            }
        }

        Walk(root);
        return result;
    }

    private static int CountVisuals(DependencyObject root)
    {
        int count = 1;
        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            count += CountVisuals(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }
}
