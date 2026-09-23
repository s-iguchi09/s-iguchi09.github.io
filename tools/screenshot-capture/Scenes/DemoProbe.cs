using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// コントロール別デモページ（<c>apps/wpf-standard-control-demo/</c>）の記述を実測する共通部品。
///
/// デモページは 2026-03 に一括生成され、本文の主張が実測されていなかった。
/// ページごとにシーンを作り、本文に残す主張をすべてここで確かめる。
/// レイアウトの計測はウィンドウを作らずに Measure / Arrange で行う。
/// 描画結果ではなく配置と寸法を読むだけなので、ディスプレイの状態に左右されない。
/// </summary>
internal static class DemoProbe
{
    /// <summary>デモページの検証シーンが図を書き出す場所。</summary>
    public static string ImageDirectory(string control) =>
        Path.Combine("images", "wpf-standard-control-demo", "verification", control);

    /// <summary>要素を指定の大きさでレイアウトする。</summary>
    public static T Layout<T>(T element, double width, double height) where T : UIElement
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0,
            double.IsInfinity(width) ? element.DesiredSize.Width : width,
            double.IsInfinity(height) ? element.DesiredSize.Height : height));
        element.UpdateLayout();
        return element;
    }

    /// <summary><paramref name="ancestor"/> から見た要素の矩形。</summary>
    public static Rect Bounds(UIElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    public static string Format(Rect rect) =>
        $"x={D(rect.X)} y={D(rect.Y)} w={D(rect.Width)} h={D(rect.Height)}";

    public static string D(double value) => WpfProbe.Describe(value);

    /// <summary>
    /// 指定位置をヒットテストし、最初に当たった名前付き要素の名前を返す。
    /// 何にも当たらなければ <c>(nothing)</c>。
    /// </summary>
    public static string HitName(Visual root, Point point)
    {
        DependencyObject? hit = VisualTreeHelper.HitTest(root, point)?.VisualHit;
        while (hit is not null)
        {
            if (hit is FrameworkElement { Name.Length: > 0 } named)
            {
                return named.Name;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        return "(nothing)";
    }

    /// <summary>例外の型名。投げなければ <c>no exception</c>。</summary>
    public static string Throws(Action action)
    {
        try
        {
            action();
            return "no exception";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    /// <summary>
    /// 要素をウィンドウに表示し、描画が落ち着いてから <paramref name="act"/> を実行して閉じる。
    ///
    /// キー入力・ポップアップ・装飾層など、表示中のウィンドウが要る計測に使う。
    /// 値を読むだけなので、ディスプレイが消えていても結果は変わらない（撮影はしない）。
    ///
    /// <see cref="Capture.ShowAndSettleAsync"/> は表示の後に必ず Activate を呼ぶため、
    /// <paramref name="activate"/> が false でもウィンドウはアクティブになる（非アクティブな状態を前提にした計測には使えない）。
    /// true にすると、Activate と描画の待機をもう 1 回行い、それでもアクティブでなければ例外にする
    /// （Windows が前面化を制限すると Activate は失敗する。フォーカスが前提の計測を誤った状態で記録しないため）。
    /// フォーカスが要る計測では true にする。
    /// </summary>
    public static async Task ShowAsync(FrameworkElement content, Func<Task> act, bool activate = false)
    {
        var window = new Window
        {
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowActivated = activate,
        };

        try
        {
            await Capture.ShowAndSettleAsync(window);
            if (activate)
            {
                window.Activate();
                await Capture.SettleAsync(window);
                if (!window.IsActive)
                {
                    throw new InvalidOperationException("計測用のウィンドウをアクティブにできない（ほかのウィンドウが前面を保持している）。");
                }
            }

            await act();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>表示中の要素へキー押下のイベントを送る。</summary>
    public static void PressKey(UIElement target, System.Windows.Input.Key key)
    {
        PresentationSource source = PresentationSource.FromVisual(target)
            ?? throw new InvalidOperationException("ウィンドウに表示していない要素にはキー入力を送れない。");
        target.RaiseEvent(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent,
        });
    }

    /// <summary>
    /// キーボードフォーカスを移し、移ったことを確かめる。
    /// 移らなければ（ウィンドウが前面に出られないなど）例外にする。フォーカスが前提の計測を誤った値で残さないためである。
    /// </summary>
    public static async Task FocusAsync(UIElement element)
    {
        System.Windows.Input.Keyboard.Focus(element);
        await Capture.SettleAsync(Window.GetWindow(element)!, 50);
        if (!element.IsKeyboardFocusWithin)
        {
            throw new InvalidOperationException($"{element.GetType().Name} にキーボードフォーカスを移せない。");
        }
    }

    /// <summary>
    /// フォーカスのある要素へ、実際のキー入力と同じ経路（InputManager）でキーを送る。
    ///
    /// <see cref="PressKey"/> は要素のイベントを直接発生させるだけなので、
    /// InputManager の後処理で動くもの（アクセスキー、IsDefault / IsCancel のボタン）には届かない。
    /// こちらは PreviewKeyDown / PreviewKeyUp から入れるため、KeyDown / KeyUp への昇格と後処理まで通る。
    /// </summary>
    public static void SendKey(System.Windows.Input.Key key, bool down = true)
    {
        var target = System.Windows.Input.Keyboard.FocusedElement as DependencyObject
            ?? throw new InvalidOperationException("キーボードフォーカスのある要素が無い。");
        PresentationSource source = PresentationSource.FromDependencyObject(target)
            ?? throw new InvalidOperationException("フォーカスのある要素がウィンドウに表示されていない。");
        System.Windows.Input.InputManager.Current.ProcessInput(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = down
                ? System.Windows.Input.Keyboard.PreviewKeyDownEvent
                : System.Windows.Input.Keyboard.PreviewKeyUpEvent,
        });
    }

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 2 つの構築方法について、初回レイアウトにかかる時間の中央値を交互に測る。
    ///
    /// 暖機で JIT の影響を外し、条件を交互に試行して実行順の影響を避ける。
    /// 時間は環境に依存するため、本文には比率だけを書き、絶対値は図に持たせる。
    /// </summary>
    public static (double A, double B) CompareLayoutTime(
        Func<UIElement> buildA, Func<UIElement> buildB, double width, double height, int trials = 15)
    {
        for (int i = 0; i < 3; i++)
        {
            Layout(buildA(), width, height);
            Layout(buildB(), width, height);
        }

        var a = new List<double>();
        var b = new List<double>();
        for (int i = 0; i < trials; i++)
        {
            a.Add(Time(buildA));
            b.Add(Time(buildB));
        }

        return (Median(a), Median(b));

        double Time(Func<UIElement> build)
        {
            UIElement element = build();
            var watch = Stopwatch.StartNew();
            Layout(element, width, height);
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}

/// <summary>
/// <see cref="MeasureOverride"/> が呼ばれた回数を数える子要素。
/// パネルが子をいくつ測り直すかを読むために使う。
/// </summary>
internal sealed class MeasureCounter : FrameworkElement
{
    public int MeasureCount { get; private set; }

    public Size Content { get; init; } = new(40, 20);

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCount++;
        return Content;
    }
}
