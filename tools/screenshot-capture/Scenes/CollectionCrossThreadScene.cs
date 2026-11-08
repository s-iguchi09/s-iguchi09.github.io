using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の ObservableCollection をバックグラウンドスレッドから更新すると
/// 例外になる原因と対処」の図。
///
/// バインドの有無と対処の有無を変えて、実際にバックグラウンドスレッドから
/// <c>Add</c> を呼び、送出される例外を記録する。
/// 「コレクションを別スレッドで触ったこと自体は問題ではない」という記事の主張は、
/// バインドしていないコレクションを同じ手順で触ってみないと確かめられない。
/// </summary>
internal sealed class CollectionCrossThreadScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "バックグラウンドスレッドからの Add で送出される例外を確かめる",
        "バインドしていない ObservableCollection では例外にならないこと（原因が CollectionView 側にある証拠）",
        "Dispatcher.Invoke と EnableCollectionSynchronization のいずれでも例外が消えること",
        "EnableCollectionSynchronization は、UI スレッドでバインド前に登録し、登録したのと同じロックで Add を包んだ構成で測っている",
        "Add のあと、コレクションの Count、ItemsControl の Items.Count、ビューが受け取った CollectionChanged の数（件数だけではビューへの反映を確かめられないため）",
        "バックグラウンドスレッドから 5,000 件を Add したときの所要時間を、Dispatcher.Invoke と EnableCollectionSynchronization で比べる",
        "バックグラウンドで 50,000 件が一気に発生したときの描画フレームの最長間隔と、すべて反映されるまでの時間を、更新方法ごとに 5 回ずつ測って比べる（中央値と最小〜最大。暖機の 1 巡のあとに 5 巡、巡ごとに順番をずらし、各回の前に GC。計測区間は基準が 1,000 ms、ほかは発生から 1,000 ms 以上）",
        "キューにためて UI スレッドで反映するとき、1 件ずつ Add と Reset 通知 1 回とで描画フレームの最長間隔を比べる。タイマー 1 回で反映した最大件数も記録する",
    ];

    public string Slug => "wpf-observablecollection-cross-thread-update";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<Loc>>();

        async Task AddRowAsync(Loc collection, string countermeasure, Bound bound, Fix fix)
        {
            (string result, string counts) = await RunDetailedAsync(bound, fix);
            rows.Add([collection, countermeasure, result, counts]);
        }

        await AddRowAsync(T("ObservableCollection alone", "ObservableCollection だけ"), "-", Bound.No, Fix.None);
        await AddRowAsync(T("bound to ItemsControl", "ItemsControl にバインド"), "-", Bound.Yes, Fix.None);
        await AddRowAsync(T("bound to ItemsControl", "ItemsControl にバインド"), "Dispatcher.Invoke", Bound.Yes, Fix.Dispatcher);
        await AddRowAsync(T("bound to ItemsControl", "ItemsControl にバインド"), "EnableCollectionSynchronization", Bound.Yes, Fix.Synchronization);

        await context.SaveTableAsync(
            "Add() from a background thread",
            [T("collection", "コレクション"), T("countermeasure", "対策"), T("result", "結果"), T("Count / Items.Count / view notifications", "Count / Items.Count / ビューへの通知の数")],
            rows,
            "collection-cross-thread-matrix.svg");

        await context.SaveTableAsync(
            $"{BulkCount:N0} Add() calls from a background thread",
            [T("countermeasure", "対策"), T("background loop ms", "バックグラウンドのループ（ms）"), T("view notifications after await", "await の後のビューへの通知の数"), T("until all notified ms", "すべて通知されるまで（ms）")],
            [
                await MeasureBulkAsync(Fix.Dispatcher),
                await MeasureBulkAsync(Fix.Synchronization),
            ],
            "collection-cross-thread-bulk.svg");

        await context.SaveTableAsync(
            $"UI thread stalls while {StallCount:N0} items arrive at once (median of {StallRuns}, interleaved, after one warm-up round)",
            [T("how the items are applied", "反映の仕方"), T("longest frame interval ms (median)", "描画フレームの最長間隔（ms、中央値）"), T("longest frame interval min–max of runs ms", "最長間隔の各回の最小〜最大（ms）"), T("until all applied ms (median)", "すべて反映されるまで（ms、中央値）"), T("most items applied in one timer tick", "タイマー 1 回で反映した最大件数")],
            await MeasureStallRowsAsync(),
            "collection-cross-thread-stall.svg");
    }

    private const int BulkCount = 5_000;

    private enum Burst
    {
        None,
        DispatcherPerItem,
        SynchronizationPerItem,
        QueueDrainPerItem,
        QueueDrainReset,
    }

    /// <summary>AddRange で要素をまとめて足し、通知を Reset 1 回にするコレクション。</summary>
    private sealed class RangeObservableCollection<TItem> : ObservableCollection<TItem>
    {
        public void AddRange(IEnumerable<TItem> source)
        {
            CheckReentrancy();
            foreach (TItem item in source)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>
    /// <paramref name="count"/> 件がバックグラウンドで一気に発生したとき、描画がどれだけ止まるかを測る。
    /// 描画フレームの間隔は <see cref="CompositionTarget.Rendering"/> の呼ばれた時刻の差で測る。
    /// Input 優先度の処理の待ち時間も試したが、更新しない基準でもマウス操作などで 24〜164 ms とぶれ、
    /// 更新方法の差と区別できなかったので測らない。
    /// </summary>
    private static async Task<(double FrameGap, double Applied, bool Reached, int MaxDrained, int DrainTicks)> MeasureStallOnceAsync(Burst burst, int count)
    {
        var items = new RangeObservableCollection<string>();
        var gate = new object();
        if (burst == Burst.SynchronizationPerItem)
        {
            BindingOperations.EnableCollectionSynchronization(items, gate);
        }

        var list = new ListBox { ItemsSource = items };
        var host = new Window
        {
            Content = list,
            Width = 240,
            Height = 160,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };
        host.Show();
        Settle(host);

        int viewEvents = 0;
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => viewEvents++;

        Dispatcher ui = Dispatcher.CurrentDispatcher;
        var clock = Stopwatch.StartNew();

        // 計測区間は、発生の開始から、全件の反映の 100 ms 後と開始の 1,000 ms 後の遅いほうまで（基準は開始から 1,000 ms ちょうど）。区間の外の値は捨てる。
        double windowStart = double.MaxValue;
        double windowEnd = double.MaxValue;
        bool InWindow(double t) => t >= windowStart && t <= windowEnd;

        double lastFrame = -1;
        double maxFrameGap = 0;
        void OnRendering(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalMilliseconds;
            if (lastFrame >= 0 && InWindow(now) && now - lastFrame > maxFrameGap)
            {
                maxFrameGap = now - lastFrame;
            }

            lastFrame = now;
        }

        CompositionTarget.Rendering += OnRendering;

        // 計測の仕組みが落ち着くまで待ってから発生させる。
        await Task.Delay(300);

        var queue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        DispatcherTimer? timer = null;
        int maxDrained = 0;
        int drainTicks = 0;
        if (burst is Burst.QueueDrainPerItem or Burst.QueueDrainReset)
        {
            // 既定の DispatcherTimer（Background 優先度）で 100 ms ごとにキューを空にする。
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                var drained = new List<string>();
                while (queue.TryDequeue(out string? row))
                {
                    drained.Add(row);
                }

                if (drained.Count == 0)
                {
                    return;
                }

                drainTicks++;
                maxDrained = Math.Max(maxDrained, drained.Count);

                if (burst == Burst.QueueDrainReset)
                {
                    items.AddRange(drained);
                }
                else
                {
                    foreach (string row in drained)
                    {
                        items.Add(row);
                    }
                }
            };
            timer.Start();
        }

        windowStart = clock.Elapsed.TotalMilliseconds;
        Task producer = Task.Run(() =>
        {
            for (int i = 0; i < count; i++)
            {
                switch (burst)
                {
                    case Burst.DispatcherPerItem:
                        ui.Invoke(() => items.Add("row"));
                        break;
                    case Burst.SynchronizationPerItem:
                        lock (gate)
                        {
                            items.Add("row");
                        }

                        break;
                    case Burst.QueueDrainPerItem:
                    case Burst.QueueDrainReset:
                        queue.Enqueue("row");
                        break;
                }
            }
        });

        bool Done() => burst == Burst.None
            ? clock.Elapsed.TotalMilliseconds - windowStart >= 1_000
            : list.Items.Count == count && (burst == Burst.QueueDrainReset || viewEvents == count);

        while (!Done() && clock.Elapsed.TotalMilliseconds - windowStart < 60_000)
        {
            await Task.Delay(1);
        }

        double applied = clock.Elapsed.TotalMilliseconds - windowStart;
        bool reached = Done();
        // 区間が短いほど最大値は小さく出やすいので、どの方法も発生の開始から 1,000 ms 以上は測る。
        windowEnd = burst == Burst.None ? windowStart + 1_000 : Math.Max(clock.Elapsed.TotalMilliseconds + 100, windowStart + 1_000);
        while (clock.Elapsed.TotalMilliseconds < windowEnd + 100)
        {
            await Task.Delay(5);
        }

        await producer;

        timer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        host.Content = null;
        host.Close();
        if (burst == Burst.SynchronizationPerItem)
        {
            BindingOperations.DisableCollectionSynchronization(items);
        }

        return (maxFrameGap, applied, reached, maxDrained, drainTicks);
    }

    private const int StallRuns = 5;

    /// <summary>予備の計測では、5,000 件だとどの方法でも描画フレームの最長間隔の差が小さかったので、10 倍にしている。</summary>
    private const int StallCount = 50_000;

    /// <summary>
    /// 各方法を <see cref="StallRuns"/> 回測り、列ごとの中央値を返す。1 回だけではほかのプロセスの影響で値がぶれる。
    /// 時間による環境の変化が特定の方法に偏らないよう、1 回ごとに全方法を巡回し、巡ごとに開始位置をずらし、各回の前に GC を済ませる。暖機の 1 巡のあとに測る。
    /// </summary>
    private static async Task<IReadOnlyList<IReadOnlyList<Loc>>> MeasureStallRowsAsync()
    {
        Burst[] bursts = [Burst.None, Burst.DispatcherPerItem, Burst.SynchronizationPerItem, Burst.QueueDrainPerItem, Burst.QueueDrainReset];
        var runs = bursts.ToDictionary(b => b, _ => new List<(double FrameGap, double Applied, bool Reached, int MaxDrained, int DrainTicks)>());
        for (int round = -1; round < StallRuns; round++)
        {
            // 実行順の影響が特定の方法に偏らないよう、巡ごとに開始位置をずらす。
            int offset = Math.Max(round, 0) % bursts.Length;
            foreach (Burst burst in bursts.Skip(offset).Concat(bursts.Take(offset)))
            {
                // 前の回のウィンドウや通知の後始末が次の回の区間に入らないよう、回の前に回収しておく。
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var result = await MeasureStallOnceAsync(burst, StallCount);
                if (round >= 0)
                {
                    runs[burst].Add(result);
                }
            }
        }

        static double Median(IEnumerable<double> values)
        {
            double[] sorted = values.Order().ToArray();
            return sorted[sorted.Length / 2];
        }

        var rows = new List<IReadOnlyList<Loc>>();
        foreach (Burst burst in bursts)
        {
            var r = runs[burst];
            Console.WriteLine($"stall {burst} n={StallCount}: " + string.Join(" | ", r.Select(x => $"{x.FrameGap:F0}/{x.Applied:F0}/{x.MaxDrained}x{x.DrainTicks}{(x.Reached ? "" : "!")}")));
            Loc label = burst switch
            {
                Burst.None => T("no updates (baseline)", "更新しない（基準）"),
                Burst.DispatcherPerItem => T("Dispatcher.Invoke per item", "1 件ごとに Dispatcher.Invoke"),
                Burst.SynchronizationPerItem => "EnableCollectionSynchronization + lock",
                Burst.QueueDrainPerItem => T("queue, drain every 100 ms, Add per item", "キューにためて 100 ms ごとに 1 件ずつ Add"),
                _ => T("queue, drain every 100 ms, one Reset", "キューにためて 100 ms ごとに Reset 通知 1 回"),
            };
            Loc until = burst == Burst.None ? "-" : r.All(x => x.Reached) ? Median(r.Select(x => x.Applied)).ToString("N0") : T("not reached", "届かない");
            string range = $"{r.Min(x => x.FrameGap):N0}–{r.Max(x => x.FrameGap):N0}";
            string drained = burst is Burst.QueueDrainPerItem or Burst.QueueDrainReset
                ? (r.Min(x => x.MaxDrained) == r.Max(x => x.MaxDrained) ? r[0].MaxDrained.ToString("N0") : $"{r.Min(x => x.MaxDrained):N0}–{r.Max(x => x.MaxDrained):N0}")
                : "-";
            rows.Add([label, Median(r.Select(x => x.FrameGap)).ToString("N0"), range, until, drained]);
        }

        return rows;
    }

    /// <summary>
    /// バックグラウンドスレッドから <see cref="BulkCount"/> 件を 1 件ずつ Add し、
    /// ループにかかった時間、await のあと UI スレッドへ戻った時点でビューが受け取っていた通知の数、
    /// ビューが全件分の通知を受け取るまでの時間を返す。
    /// Dispatcher.Invoke は 1 件ごとに UI スレッドでの実行を待つ。
    /// </summary>
    /// <summary>表のセルの英語と日本語。識別子と値は両方に同じものを書く。</summary>
    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    private static async Task<IReadOnlyList<Loc>> MeasureBulkAsync(Fix fix)
    {
        var items = new ObservableCollection<string>();
        var gate = new object();

        if (fix == Fix.Synchronization)
        {
            BindingOperations.EnableCollectionSynchronization(items, gate);
        }

        // 反映の件数だけを見たいので、コンテナの生成が件数に比例しない仮想化した ListBox に載せる。
        var list = new ListBox { ItemsSource = items };
        var host = new Window
        {
            Content = list,
            Width = 240,
            Height = 160,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.White,
        };
        host.Show();
        Settle(host);

        // 同期を登録していないビューは件数を元のリストから読むことがあるので、Items.Count だけでは
        // ビューへの反映を確かめられない。ビューが受け取った CollectionChanged の数も数える。
        int viewEvents = 0;
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => viewEvents++;

        Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;
        var total = Stopwatch.StartNew();
        double elapsed = await Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < BulkCount; i++)
            {
                if (fix == Fix.Dispatcher)
                {
                    uiDispatcher.Invoke(() => items.Add("row"));
                }
                else
                {
                    lock (gate)
                    {
                        items.Add("row");
                    }
                }
            }

            return stopwatch.Elapsed.TotalMilliseconds;
        });

        // ループはワーカースレッドで回るが、ここで読むのは await のあと UI スレッドへ戻った時点の値である。
        // その間にも UI スレッドは通知を処理できるので、「ループを抜けた瞬間」の値ではない。
        int eventsAfterAwait = viewEvents;

        // 反映は UI スレッドで非同期に進む。ビューが全件分の通知を受け取るまで回す。
        while (viewEvents < BulkCount && total.Elapsed < TimeSpan.FromSeconds(60))
        {
            Settle(host);
        }

        total.Stop();
        Loc until = viewEvents == BulkCount ? total.Elapsed.TotalMilliseconds.ToString("N0") : T($"not reached ({viewEvents:N0})", $"届かない（{viewEvents:N0}）");
        host.Content = null;
        host.Close();

        if (fix == Fix.Synchronization)
        {
            BindingOperations.DisableCollectionSynchronization(items);
        }

        return [fix == Fix.Dispatcher ? T("Dispatcher.Invoke per item", "1 件ごとに Dispatcher.Invoke") : "EnableCollectionSynchronization + lock", elapsed.ToString("N0"), eventsAfterAwait.ToString("N0"), until];
    }

    private enum Bound
    {
        No,
        Yes,
    }

    private enum Fix
    {
        None,
        Dispatcher,
        Synchronization,
    }

    /// <summary>
    /// <c>Add</c> の結果に加えて、コレクションの件数、ItemsControl の Items.Count、ビューが受け取った通知の数を返す。
    /// 同期のないビューは件数を元のリストから読むことがあるため、反映の確認は通知の数で行う。
    /// </summary>
    private static async Task<(string Result, string Counts)> RunDetailedAsync(Bound bound, Fix fix)
    {
        var items = new ObservableCollection<string>();
        var gate = new object();
        Window? host = null;
        ItemsControl? list = null;
        int viewEvents = 0;

        if (fix == Fix.Synchronization)
        {
            BindingOperations.EnableCollectionSynchronization(items, gate);
        }

        if (bound == Bound.Yes)
        {
            list = new ItemsControl { ItemsSource = items };
            host = new Window
            {
                Content = list,
                Width = 240,
                Height = 160,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Brushes.White,
            };
            host.Show();
            Settle(host);
            ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => viewEvents++;
        }

        Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;
        string result = await Task.Run(() =>
        {
            try
            {
                switch (fix)
                {
                    case Fix.Dispatcher:
                        uiDispatcher.Invoke(() => items.Add("row"));
                        break;

                    case Fix.Synchronization:
                        lock (gate)
                        {
                            items.Add("row");
                        }

                        break;

                    default:
                        items.Add("row");
                        break;
                }

                return "no exception";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        });

        string counts = $"{items.Count}";
        if (host is not null)
        {
            Settle(host);
            counts = $"{items.Count} / {list!.Items.Count} / {viewEvents}";
            host.Content = null;
            host.Close();
            Settle(host);
        }

        if (fix == Fix.Synchronization)
        {
            BindingOperations.DisableCollectionSynchronization(items);
        }

        return (result, counts);
    }

    /// <summary>レイアウトとバインドの反映が終わるまでディスパッチャーを回す。</summary>
    private static void Settle(Window window)
    {
        window.UpdateLayout();
        for (int i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
