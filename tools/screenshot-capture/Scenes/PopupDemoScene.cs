using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Popup」（apps/wpf-standard-control-demo/popup.md と日本語版）の記述を実測する。
///
/// 位置は、ポップアップの子と配置の基準の要素の画面上の左上を読み、その差を DIP で表す。
/// StaysOpen の「外側のクリックで閉じる」は、実際のマウス（<see cref="RealMouse"/>）で計測用のウィンドウの空き領域をクリックして確かめる。
/// </summary>
internal sealed class PopupDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-popup";

    public string ImageDirectory => DemoProbe.ImageDirectory("popup");

    public IReadOnlyList<string> Verifies =>
    [
        "Popup の基底クラスと、IsOpen / StaysOpen / AllowsTransparency / Placement / PopupAnimation の既定値",
        "開いた Popup の子が、ウィンドウとは別の PresentationSource（別のウィンドウハンドル）と PopupRoot の下にあること",
        "Placement（Bottom / Top / Right / Left / Center）ごとの、基準の要素に対する子の位置と、HorizontalOffset 20 / VerticalOffset 10 を加えたときの位置",
        "画面の下端近くで Placement=Bottom にしたときの位置",
        "Placement=Absolute で PlacementRectangle を設定しないときの画面上の位置",
        "PlacementTarget を設定しないときの基準（親要素）と、デモアプリと同じ PlacementRectangle（0, 0, 100, 50）の位置",
        "StaysOpen が False と True のとき、実際のマウスでウィンドウの空き領域をクリックした後の IsOpen と、IsOpen にバインドした CheckBox",
        "開いたままウィンドウを動かしたとき、ポップアップが一緒に動くか",
        "ComboBox と最上位の MenuItem の既定のテンプレートの中の Popup（PART_Popup）",
        "AllowsTransparency が True と False のときのポップアップのウィンドウの WS_EX_LAYERED と、PopupAnimation=Fade で開いた直後の不透明度",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Popup: where the child appears (target 150 x 30, child 120 x 40, DIP)",
            ["case", "measured"],
            await MeasurePlacementAsync(),
            "popup-placement.svg");

        await context.SaveTableAsync(
            "Popup: defaults, separate window, StaysOpen and transparency",
            ["case", "measured"],
            await MeasureBehaviorAsync(),
            "popup-behavior.svg");
    }

    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x80000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    /// <summary>要素の左上の画面上の位置（DIP）。</summary>
    private static Point ScreenDip(Visual element)
    {
        Point device = element.PointToScreen(new Point(0, 0));
        return PresentationSource.FromVisual(element)!.CompositionTarget!.TransformFromDevice.Transform(device);
    }

    private static string Offset(Visual child, Visual target)
    {
        Point c = ScreenDip(child);
        Point t = ScreenDip(target);
        return $"({D(c.X - t.X)}, {D(c.Y - t.Y)})";
    }

    private static Border Child() => new() { Width = 120, Height = 40, Background = Brushes.White };

    private static async Task<List<IReadOnlyList<string>>> MeasurePlacementAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var target = new Button { Content = "Placement Target", Width = 150, Height = 30 };
        var host = new Grid { Width = 450, Height = 150 };
        host.Children.Add(target);
        var popup = new Popup { PlacementTarget = target, Child = Child() };
        host.Children.Add(popup);

        await ShowAsync(host, async () =>
        {
            Window window = Window.GetWindow(host)!;
            window.Left = 400;
            window.Top = 300;
            await Capture.SettleAsync(window);

            async Task<string> Open(PlacementMode mode, double h, double v)
            {
                popup.IsOpen = false;
                popup.Placement = mode;
                popup.HorizontalOffset = h;
                popup.VerticalOffset = v;
                popup.IsOpen = true;
                await Capture.SettleAsync(window, 100);
                return Offset(popup.Child, target);
            }

            foreach (PlacementMode mode in new[] { PlacementMode.Bottom, PlacementMode.Top, PlacementMode.Right, PlacementMode.Left, PlacementMode.Center })
            {
                string plain = await Open(mode, 0, 0);
                string shifted = await Open(mode, 20, 10);
                rows.Add([$"Placement={mode}: child from target / with offsets (20, 10)",
                    $"{plain} / {shifted}"]);
            }

            // 画面の下端近く。基準の要素の下端を、作業領域（タスクバーを除く）の下端と画面の下端の 10 上に置く。
            foreach ((string name, double bottom) in new[] { ("work area", SystemParameters.WorkArea.Bottom), ("screen", SystemParameters.PrimaryScreenHeight) })
            {
                window.Top += bottom - 10 - (ScreenDip(target).Y + target.ActualHeight);
                await Capture.SettleAsync(window);
                string position = await Open(PlacementMode.Bottom, 0, 0);
                double childBottom = ScreenDip(popup.Child).Y + 40;
                rows.Add([$"Bottom, target 10 above the {name} bottom: child / its bottom past it",
                    $"{position} / {D(childBottom - bottom)}"]);
            }

            window.Top = 300;
            await Capture.SettleAsync(window);

            popup.IsOpen = false;
            popup.Placement = PlacementMode.Absolute;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 0;
            popup.IsOpen = true;
            await Capture.SettleAsync(window, 100);
            rows.Add(["Placement=Absolute, no PlacementRectangle: child on the screen",
                $"({D(ScreenDip(popup.Child).X)}, {D(ScreenDip(popup.Child).Y)})"]);
            popup.IsOpen = false;
        });

        {
            // PlacementTarget なし。Popup を置いた Grid が基準になるかを見る。Grid は Border の中で (50, 20) にある。
            var parent = new Grid { Width = 200, Height = 60, Margin = new Thickness(50, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var noTarget = new Popup { Placement = PlacementMode.Bottom, Child = Child() };
            parent.Children.Add(noTarget);
            var rectPopup = new Popup { Placement = PlacementMode.Bottom, PlacementRectangle = new Rect(0, 0, 100, 50), Child = Child() };
            parent.Children.Add(rectPopup);
            var outer = new Grid { Width = 300, Height = 120, Children = { parent } };
            await ShowAsync(outer, async () =>
            {
                Window window = Window.GetWindow(outer)!;
                window.Left = 400;
                window.Top = 300;
                await Capture.SettleAsync(window);
                noTarget.IsOpen = true;
                await Capture.SettleAsync(window, 100);
                rows.Add(["no PlacementTarget, Bottom, Popup in a Grid 200 x 60: child from Grid", Offset(noTarget.Child, parent)]);
                noTarget.IsOpen = false;
                rectPopup.IsOpen = true;
                await Capture.SettleAsync(window, 100);
                rows.Add(["  with PlacementRectangle (0, 0, 100, 50): child from Grid", Offset(rectPopup.Child, parent)]);
                rectPopup.IsOpen = false;
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureBehaviorAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new Popup();
        rows.Add(["base class / IsOpen, StaysOpen, AllowsTransparency, Placement, PopupAnimation",
            $"{typeof(Popup).BaseType!.Name} / {defaults.IsOpen}, {defaults.StaysOpen}, {defaults.AllowsTransparency}, {defaults.Placement}, {defaults.PopupAnimation}"]);

        {
            var combo = new ComboBox { ItemsSource = new[] { "a" } };
            var menu = new Menu { Items = { new MenuItem { Header = "File", Items = { new MenuItem { Header = "Open" } } } } };
            var panel = new StackPanel { Width = 200, Children = { combo, menu } };
            await ShowAsync(panel, async () =>
            {
                string Name(Control control) => control.Template.FindName("PART_Popup", control) is Popup found ? $"Popup {found.Name}" : "none";
                rows.Add(["Popup in the default template: ComboBox / top-level MenuItem", $"{Name(combo)} / {Name((MenuItem)menu.Items[0])}"]);
                await Task.CompletedTask;
            });
        }

        foreach (bool staysOpen in new[] { false, true })
        {
            var check = new CheckBox { Content = "IsOpen" };
            var target = new Button { Content = "Placement Target", Width = 150, Height = 30 };
            var blank = new Border { Width = 150, Height = 60, Background = Brushes.White };
            var popup = new Popup { PlacementTarget = target, Placement = PlacementMode.Right, StaysOpen = staysOpen, Child = Child() };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = check });
            var panel = new StackPanel { Children = { check, target, blank, popup } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                window.Left = 400;
                window.Top = 300;
                await Capture.SettleAsync(window);

                check.IsChecked = true;
                await Capture.SettleAsync(window, 100);
                if (!staysOpen)
                {
                    var source = (HwndSource)PresentationSource.FromVisual(popup.Child)!;
                    string root = VisualTreeHelper.GetParent(popup.Child) is { } p ? p.GetType().Name : "none";
                    rows.Add(["open: child in a separate window handle / child's parent",
                        $"{source.Handle != new WindowInteropHelper(window).Handle} / {root}"]);
                }

                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(blank);
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([$"StaysOpen={staysOpen}, mouse click outside it: IsOpen / bound CheckBox",
                    $"{popup.IsOpen} / {WpfProbe.Describe(check.IsChecked)}"]);

                if (staysOpen)
                {
                    Point before = ScreenDip(popup.Child);
                    window.Left += 40;
                    window.Top += 40;
                    await Capture.SettleAsync(window, 100);
                    Point after = ScreenDip(popup.Child);
                    rows.Add(["open, window moved by (40, 40): child moved by",
                        $"({D(after.X - before.X)}, {D(after.Y - before.Y)})"]);
                }

                popup.IsOpen = false;
            });
        }

        foreach (bool transparent in new[] { false, true })
        {
            var target = new Button { Content = "Target", Width = 150, Height = 30 };
            var popup = new Popup { PlacementTarget = target, AllowsTransparency = transparent, PopupAnimation = PopupAnimation.Fade, Child = Child() };
            var panel = new StackPanel { Children = { target, popup } };
            await ShowAsync(panel, async () =>
            {
                // アニメーションの途中を読むので、読んだ時点は開いてからの実際の経過時間で示す（毎回わずかに変わる）。
                var clock = System.Diagnostics.Stopwatch.StartNew();
                popup.IsOpen = true;
                var source = (HwndSource)PresentationSource.FromVisual(popup.Child)!;
                var root = (UIElement)source.RootVisual;
                string atOpen = $"{D(root.Opacity)} at {clock.ElapsedMilliseconds} ms";
                await Task.Delay(50);
                string early = $"{D(root.Opacity)} at {clock.ElapsedMilliseconds} ms";
                await Capture.SettleAsync(Window.GetWindow(panel)!, 400);
                string later = $"{D(root.Opacity)} at {clock.ElapsedMilliseconds} ms";
                bool layered = (GetWindowLongPtr(source.Handle, GwlExStyle).ToInt64() & WsExLayered) != 0;
                rows.Add([$"AllowsTransparency={transparent}, Fade: layered window / opacity after opening",
                    $"{layered} / {atOpen}, {early}, {later}"]);
                popup.IsOpen = false;
            });
        }

        return rows;
    }
}
