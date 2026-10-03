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
    }

    private const int BulkCount = 5_000;

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
