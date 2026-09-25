using System.Collections.ObjectModel;
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
        "Add のあと、コレクションの Count と ItemsControl の Items.Count（CollectionView に反映された件数）が一致するか",
        "バックグラウンドスレッドから 5,000 件を Add したときの所要時間を、Dispatcher.Invoke と EnableCollectionSynchronization で比べる",
    ];

    public string Slug => "wpf-observablecollection-cross-thread-update";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<string>>();

        async Task AddRowAsync(string collection, string countermeasure, Bound bound, Fix fix)
        {
            (string result, string counts) = await RunDetailedAsync(bound, fix);
            rows.Add([collection, countermeasure, result, counts]);
        }

        await AddRowAsync("ObservableCollection alone", "-", Bound.No, Fix.None);
        await AddRowAsync("bound to ItemsControl", "-", Bound.Yes, Fix.None);
        await AddRowAsync("bound to ItemsControl", "Dispatcher.Invoke", Bound.Yes, Fix.Dispatcher);
        await AddRowAsync("bound to ItemsControl", "EnableCollectionSynchronization", Bound.Yes, Fix.Synchronization);

        await context.SaveTableAsync(
            "Add() from a background thread",
            ["collection", "countermeasure", "result", "Count / Items.Count"],
            rows,
            "collection-cross-thread-matrix.svg");

        await context.SaveTableAsync(
            $"{BulkCount:N0} Add() calls from a background thread",
            ["countermeasure", "background loop ms", "Items.Count at loop end", "until all shown ms"],
            [
                await MeasureBulkAsync(Fix.Dispatcher),
                await MeasureBulkAsync(Fix.Synchronization),
            ],
            "collection-cross-thread-bulk.svg");
    }

    private const int BulkCount = 5_000;

    /// <summary>
    /// バックグラウンドスレッドから <see cref="BulkCount"/> 件を 1 件ずつ Add し、
    /// ループにかかった時間、ループを抜けた時点で UI に反映されていた件数、
    /// 全件が UI に反映されるまでの時間を返す。
    /// Dispatcher.Invoke は 1 件ごとに UI スレッドでの実行を待つ。
    /// </summary>
    private static async Task<IReadOnlyList<string>> MeasureBulkAsync(Fix fix)
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

        int shownAtLoopEnd = list.Items.Count;

        // 反映は UI スレッドで非同期に進む。全件が Items に載るまで回す。
        while (list.Items.Count < BulkCount && total.Elapsed < TimeSpan.FromSeconds(60))
        {
            Settle(host);
        }

        total.Stop();
        string until = list.Items.Count == BulkCount ? total.Elapsed.TotalMilliseconds.ToString("N0") : $"not reached ({list.Items.Count:N0})";
        host.Content = null;
        host.Close();

        if (fix == Fix.Synchronization)
        {
            BindingOperations.DisableCollectionSynchronization(items);
        }

        return [fix == Fix.Dispatcher ? "Dispatcher.Invoke per item" : "EnableCollectionSynchronization + lock", elapsed.ToString("N0"), shownAtLoopEnd.ToString("N0"), until];
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
    /// <c>Add</c> の結果に加えて、コレクションの件数と、ItemsControl に反映された件数を返す。
    /// 例外になった構成で、両者が食い違うかを見る。
    /// </summary>
    private static async Task<(string Result, string Counts)> RunDetailedAsync(Bound bound, Fix fix)
    {
        var items = new ObservableCollection<string>();
        var gate = new object();
        Window? host = null;
        ItemsControl? list = null;

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
            counts = $"{items.Count} / {list!.Items.Count}";
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
