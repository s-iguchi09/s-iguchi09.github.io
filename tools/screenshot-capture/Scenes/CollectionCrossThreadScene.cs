using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        "この構成では変更と CollectionChanged 通知が同じロックの中で起きるため、UI スレッド側の列挙と競合しないこと",
    ];

    public string Slug => "wpf-observablecollection-cross-thread-update";

    public async Task CaptureAsync(SceneContext context)
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add(["ObservableCollection alone", "-", await RunAsync(Bound.No, Fix.None)]);
        rows.Add(["bound to ItemsControl", "-", await RunAsync(Bound.Yes, Fix.None)]);
        rows.Add(["bound to ItemsControl", "Dispatcher.Invoke", await RunAsync(Bound.Yes, Fix.Dispatcher)]);
        rows.Add(["bound to ItemsControl", "EnableCollectionSynchronization", await RunAsync(Bound.Yes, Fix.Synchronization)]);

        await context.SaveTableAsync(
            "Add() from a background thread",
            ["collection", "countermeasure", "result"],
            rows,
            "collection-cross-thread-matrix.svg");
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
    /// 指定の構成でバックグラウンドスレッドから <c>Add</c> を呼び、結果を返す。
    /// </summary>
    private static async Task<string> RunAsync(Bound bound, Fix fix)
    {
        var items = new ObservableCollection<string>();
        var gate = new object();
        Window? host = null;

        if (fix == Fix.Synchronization)
        {
            BindingOperations.EnableCollectionSynchronization(items, gate);
        }

        if (bound == Bound.Yes)
        {
            var list = new ItemsControl { ItemsSource = items };
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

        if (host is not null)
        {
            Settle(host);
            host.Content = null;
            host.Close();
            Settle(host);
        }

        if (fix == Fix.Synchronization)
        {
            BindingOperations.DisableCollectionSynchronization(items);
        }

        return result;
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
