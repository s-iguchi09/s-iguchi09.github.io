using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Slider」（apps/wpf-standard-control-demo/slider.html と日本語版）の記述を実測する。
///
/// つまみのドラッグは、Thumb がマウス操作で発生させる DragStarted / DragDelta イベントを
/// つまみに発生させて再現する。Slider はこれらのイベントを受けて Value を変える。
/// キー操作は表示したウィンドウでキー入力イベントを送り、トラックのクリックは
/// 繰り返しボタンの UI オートメーションの Invoke で行う。
/// </summary>
internal sealed class SliderDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-slider";

    public string ImageDirectory => DemoProbe.ImageDirectory("slider");

    public IReadOnlyList<string> Verifies =>
    [
        "Slider・ProgressBar・ScrollBar の基底クラスと、Slider の各プロパティの既定値",
        "Value の依存関係プロパティのメタデータ（既定で TwoWay か、既定の UpdateSourceTrigger）と TextBox.Text の既定の UpdateSourceTrigger、バインドしたソースがドラッグ中に更新されるか",
        "範囲外の Value と、Minimum より小さい Maximum を設定したときの補正と例外の有無、Minimum と Maximum を設定する順序による最終値の違い",
        "範囲外の値を TwoWay でバインドしたときにソースへ書き戻されるか",
        "Orientation と IsDirectionReversed の組み合わせごとの、Maximum のときのつまみの位置",
        "矢印キー・PageUp / PageDown・Home / End とトラックのクリックで変わる量（SmallChange / LargeChange）と、IsDirectionReversed・IsSnapToTickEnabled との組み合わせ",
        "つまみをドラッグしたときの IsSnapToTickEnabled・TickFrequency・Ticks による値の丸めと、プログラムから設定した Value が丸められるか",
        "TickFrequency が範囲を割り切れない場合に描かれる目盛りの数と位置、Ticks の目盛りが IsDirectionReversed で反転するか",
        "TickPlacement による高さの変化と、IsSelectionRangeEnabled による選択範囲の表示、選択範囲が Value を制限しないこと",
        "AutoToolTipPlacement を設定したときにドラッグ中に表示される文字列と AutoToolTipPrecision による丸め、書式を指定するプロパティの有無",
        "Delay / Interval がトラックの繰り返しボタンに渡されることと、トラックのクリックで LargeChange だけ動くこと",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "Slider: type, defaults and Value metadata",
            ["item", "value"],
            Defaults(),
            "slider-defaults.svg");

        await context.SaveTableAsync(
            "Slider: range coercion",
            ["case", "measured"],
            Coercion(),
            "slider-range.svg");

        await context.SaveTableAsync(
            "Slider (0 to 100): where the thumb is at Value = Maximum",
            ["Orientation", "IsDirectionReversed", "thumb center (fraction of the track, from left / top)"],
            Direction(),
            "slider-direction.svg");

        await context.SaveTableAsync(
            "Slider (0 to 100, SmallChange 1, LargeChange 10, Value 50): keys and track",
            ["case", "Value after"],
            await KeysAsync(),
            "slider-keys.svg");

        await context.SaveTableAsync(
            "Slider: Value after dragging the thumb",
            ["case", "Value"],
            await SnappingAsync(),
            "slider-snap.svg");

        await context.SaveTableAsync(
            "Slider: tick marks, selection range and auto tooltip",
            ["case", "measured"],
            await TicksAndToolTipAsync(),
            "slider-ticks-tooltip.svg");
    }

    // ------------------------------------------------------------------
    // 共通
    // ------------------------------------------------------------------

    private static Slider NewSlider(double maximum = 100, Orientation orientation = Orientation.Horizontal)
    {
        var slider = new Slider { Minimum = 0, Maximum = maximum, Orientation = orientation };
        if (orientation == Orientation.Horizontal)
        {
            slider.Width = 220;
        }
        else
        {
            slider.Height = 220;
        }

        return slider;
    }

    private static Track TrackOf(Slider slider) =>
        (Track)slider.Template.FindName("PART_Track", slider);

    /// <summary>
    /// つまみを、値が <paramref name="rawDelta"/> だけ変わる距離だけドラッグする。
    /// 距離は Track の換算（1 ピクセルあたりの値）から求める。
    /// </summary>
    private static void DragThumb(Slider slider, double rawDelta, bool start = true)
    {
        Track track = TrackOf(slider);
        Thumb thumb = track.Thumb;
        double perPixel = slider.Orientation == Orientation.Horizontal
            ? track.ValueFromDistance(1, 0)
            : track.ValueFromDistance(0, 1);
        double distance = rawDelta / perPixel;

        if (start)
        {
            thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
        }

        thumb.RaiseEvent(slider.Orientation == Orientation.Horizontal
            ? new DragDeltaEventArgs(distance, 0)
            : new DragDeltaEventArgs(0, distance));
    }

    private sealed class Source : INotifyPropertyChanged
    {
        private double _value;

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ------------------------------------------------------------------
    // 計測
    // ------------------------------------------------------------------

    private static List<IReadOnlyList<string>> Defaults()
    {
        var slider = new Slider();
        var metadata = (FrameworkPropertyMetadata)RangeBase.ValueProperty.GetMetadata(typeof(Slider));

        return
        [
            ["base class of Slider / ProgressBar / ScrollBar",
                $"{typeof(Slider).BaseType!.Name} / {typeof(ProgressBar).BaseType!.Name} / {typeof(ScrollBar).BaseType!.Name}"],
            ["Minimum / Maximum / Value", $"{D(slider.Minimum)} / {D(slider.Maximum)} / {D(slider.Value)}"],
            ["SmallChange / LargeChange", $"{D(slider.SmallChange)} / {D(slider.LargeChange)}"],
            ["Orientation / IsDirectionReversed", $"{slider.Orientation} / {slider.IsDirectionReversed}"],
            ["TickPlacement / TickFrequency / Ticks", $"{slider.TickPlacement} / {D(slider.TickFrequency)} / {WpfProbe.Describe(slider.Ticks)}"],
            ["IsSnapToTickEnabled / IsMoveToPointEnabled", $"{slider.IsSnapToTickEnabled} / {slider.IsMoveToPointEnabled}"],
            ["IsSelectionRangeEnabled / SelectionStart / SelectionEnd",
                $"{slider.IsSelectionRangeEnabled} / {D(slider.SelectionStart)} / {D(slider.SelectionEnd)}"],
            ["AutoToolTipPlacement / AutoToolTipPrecision", $"{slider.AutoToolTipPlacement} / {slider.AutoToolTipPrecision}"],
            ["Delay / Interval (ms)", $"{slider.Delay} / {slider.Interval}"],
            ["Value: BindsTwoWayByDefault / DefaultUpdateSourceTrigger",
                $"{metadata.BindsTwoWayByDefault} / {metadata.DefaultUpdateSourceTrigger}"],
            // デモアプリの Value 欄は、TextBox.Text 側のバインドに UpdateSourceTrigger を指定している。比較のため並べる。
            ["TextBox.Text: DefaultUpdateSourceTrigger",
                ((FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(TextBox))).DefaultUpdateSourceTrigger.ToString()],
            ["public Slider properties named AutoToolTip*", string.Join(", ",
                typeof(Slider).GetProperties().Where(p => p.Name.StartsWith("AutoToolTip")).Select(p => p.Name))],
        ];
    }

    private static List<IReadOnlyList<string>> Coercion()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            Slider slider = NewSlider();
            slider.Value = 150;
            string first = D(slider.Value);
            slider.Maximum = 200;
            rows.Add(["Maximum 100, Value = 150; then Maximum = 200", $"Value {first}; then {D(slider.Value)}"]);
        }

        {
            Slider slider = NewSlider();
            string thrown = Throws(() => { slider.Minimum = 50; slider.Maximum = 10; });
            rows.Add(["Minimum = 50, Maximum = 10", $"{thrown}; effective Maximum {D(slider.Maximum)}, Value {D(slider.Value)}"]);
            slider.Maximum = 80;
            rows.Add(["  then Maximum = 80", $"Maximum {D(slider.Maximum)}"]);
        }

        foreach (bool minimumFirst in new[] { true, false })
        {
            // 0〜10 から 100〜200 へ。一時的に Minimum > Maximum になる順序と、ならない順序。
            var slider = new Slider { Minimum = 0, Maximum = 10, Value = 5 };
            string thrown = Throws(() =>
            {
                if (minimumFirst)
                {
                    slider.Minimum = 100;
                    slider.Maximum = 200;
                }
                else
                {
                    slider.Maximum = 200;
                    slider.Minimum = 100;
                }
            });
            rows.Add([$"0..10 to 100..200, {(minimumFirst ? "Minimum first" : "Maximum first")}",
                $"{thrown}; Minimum {D(slider.Minimum)}, Maximum {D(slider.Maximum)}, Value {D(slider.Value)}"]);
        }

        {
            // 範囲外の値を持つソースを TwoWay で結ぶ。
            var source = new Source { Value = 150 };
            Slider slider = NewSlider();
            slider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(Source.Value)) { Source = source, Mode = BindingMode.TwoWay });
            rows.Add(["source 150 bound TwoWay to Value (Maximum 100)",
                $"slider Value {D(slider.Value)}, source {D(source.Value)}"]);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> Direction()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (Orientation orientation in new[] { Orientation.Horizontal, Orientation.Vertical })
        {
            foreach (bool reversed in new[] { false, true })
            {
                Slider slider = NewSlider(100, orientation);
                slider.IsDirectionReversed = reversed;
                slider.Value = 100;
                var host = new Grid();
                host.Children.Add(slider);
                Layout(host, 300, 300);

                Track track = TrackOf(slider);
                Rect thumb = Bounds(track.Thumb, track);
                double fraction = orientation == Orientation.Horizontal
                    ? (thumb.X + thumb.Width / 2) / track.ActualWidth
                    : (thumb.Y + thumb.Height / 2) / track.ActualHeight;
                string side = (orientation, fraction > 0.5) switch
                {
                    (Orientation.Horizontal, true) => "right end",
                    (Orientation.Horizontal, false) => "left end",
                    (Orientation.Vertical, true) => "bottom end",
                    _ => "top end",
                };
                rows.Add([orientation.ToString(), reversed.ToString(), $"{fraction:0.00} ({side})"]);
            }
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> KeysAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach ((string label, Key key, bool reversed, bool snap) in new[]
        {
            ("Right arrow", Key.Right, false, false),
            ("Left arrow", Key.Left, false, false),
            ("Up arrow", Key.Up, false, false),
            ("PageUp", Key.PageUp, false, false),
            ("PageDown", Key.PageDown, false, false),
            ("Home", Key.Home, false, false),
            ("End", Key.End, false, false),
            ("Right arrow, IsDirectionReversed=True", Key.Right, true, false),
            ("Right arrow, IsSnapToTickEnabled=True, TickFrequency=10", Key.Right, false, true),
        })
        {
            Slider slider = NewSlider();
            slider.SmallChange = 1;
            slider.LargeChange = 10;
            slider.Value = 50;
            slider.IsDirectionReversed = reversed;
            if (snap)
            {
                slider.IsSnapToTickEnabled = true;
                slider.TickFrequency = 10;
            }

            await ShowAsync(slider, async () =>
            {
                PressKey(slider, key);
                await Task.CompletedTask;
                rows.Add([label, D(slider.Value)]);
            });
        }

        // トラック（つまみ以外の部分）のクリック。繰り返しボタンを UI オートメーションで押す。
        {
            Slider slider = NewSlider();
            slider.SmallChange = 1;
            slider.LargeChange = 10;
            slider.Value = 50;
            await ShowAsync(slider, async () =>
            {
                Track track = TrackOf(slider);
                var peer = new RepeatButtonAutomationPeer(track.IncreaseRepeatButton);
                ((IInvokeProvider)peer).Invoke();
                await Capture.SettleAsync(Window.GetWindow(slider)!);
                rows.Add(["click on the track to the right of the thumb", D(slider.Value)]);
            });
        }

        // 目盛りへの吸着が有効なときのトラックのクリック。LargeChange を目盛りの間隔とずらしておく。
        {
            Slider slider = NewSlider();
            slider.LargeChange = 7;
            slider.Value = 50;
            slider.IsSnapToTickEnabled = true;
            slider.TickFrequency = 10;
            await ShowAsync(slider, async () =>
            {
                Track track = TrackOf(slider);
                ((IInvokeProvider)new RepeatButtonAutomationPeer(track.IncreaseRepeatButton)).Invoke();
                await Capture.SettleAsync(Window.GetWindow(slider)!);
                rows.Add(["click on the track, LargeChange 7, IsSnapToTickEnabled, TickFrequency=10", D(slider.Value)]);
            });
        }

        // Delay / Interval がトラックの繰り返しボタンへ渡されるか。デモアプリの初期値 1000 / 50 を使う。
        {
            Slider slider = NewSlider();
            slider.Delay = 1000;
            slider.Interval = 50;
            var host = new Grid();
            host.Children.Add(slider);
            Layout(host, 300, 100);
            Track track = TrackOf(slider);
            rows.Add(["Delay 1000 / Interval 50: values on the track's two RepeatButtons",
                $"{track.DecreaseRepeatButton.Delay} / {track.DecreaseRepeatButton.Interval}, " +
                $"{track.IncreaseRepeatButton.Delay} / {track.IncreaseRepeatButton.Interval}"]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> SnappingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        async Task Case(string label, double maximum, double start, double rawDelta, Action<Slider> configure)
        {
            Slider slider = NewSlider(maximum);
            slider.Value = start;
            configure(slider);
            await ShowAsync(slider, async () =>
            {
                DragThumb(slider, rawDelta);
                await Task.CompletedTask;
                rows.Add([label, D(slider.Value)]);
            });
        }

        await Case("0..100, dragged to 23.4, no snapping", 100, 0, 23.4, _ => { });
        await Case("  TickPlacement=BottomRight only", 100, 0, 23.4, s => s.TickPlacement = TickPlacement.BottomRight);
        await Case("  IsSnapToTickEnabled, TickFrequency=10", 100, 0, 23.4, s => { s.IsSnapToTickEnabled = true; s.TickFrequency = 10; });
        await Case("  IsSnapToTickEnabled, TickFrequency=1", 100, 0, 23.4, s => { s.IsSnapToTickEnabled = true; s.TickFrequency = 1; });
        await Case("0..100, TickFrequency=30, snapping, dragged to 94", 100, 0, 94, s => { s.IsSnapToTickEnabled = true; s.TickFrequency = 30; });
        await Case("0..100, TickFrequency=30, snapping, dragged to 97", 100, 0, 97, s => { s.IsSnapToTickEnabled = true; s.TickFrequency = 30; });
        await Case("0..10, Ticks=1,3,5,7,9, snapping, dragged to 5.8", 10, 0, 5.8,
            s => { s.IsSnapToTickEnabled = true; s.Ticks = new DoubleCollection([1, 3, 5, 7, 9]); });
        await Case("0..10, Ticks=1,3,5,7,9, snapping, dragged to 0.2", 10, 0, 0.2,
            s => { s.IsSnapToTickEnabled = true; s.Ticks = new DoubleCollection([1, 3, 5, 7, 9]); });

        {
            Slider slider = NewSlider();
            slider.IsSnapToTickEnabled = true;
            slider.TickFrequency = 10;
            slider.Value = 23.4;
            rows.Add(["Value = 23.4 set from code, IsSnapToTickEnabled, TickFrequency=10", D(slider.Value)]);
        }

        // バインドしたソースが、ドラッグ中（DragCompleted の前）に更新されるか。
        {
            var source = new Source();
            Slider slider = NewSlider();
            slider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(Source.Value)) { Source = source });
            await ShowAsync(slider, async () =>
            {
                DragThumb(slider, 40);
                await Task.CompletedTask;
                rows.Add(["Value bound with default settings: source during the drag (before release)", D(source.Value)]);
            });
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> TicksAndToolTipAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 目盛りは TickBar の OnRender が描く。描画内容から線の位置を読む。
        foreach ((string label, double maximum, double frequency, DoubleCollection? ticks, bool reversed) in new (string, double, double, DoubleCollection?, bool)[]
        {
            ("0..100, TickFrequency=30", 100, 30, null, false),
            ("0..10, Ticks=1,3,5,7,9", 10, 1, new DoubleCollection([1, 3, 5, 7, 9]), false),
            // 反転を見分けるため、左右非対称な目盛りで比べる。
            ("0..10, Ticks=1,2", 10, 1, new DoubleCollection([1, 2]), false),
            ("0..10, Ticks=1,2, IsDirectionReversed", 10, 1, new DoubleCollection([1, 2]), true),
        })
        {
            Slider slider = NewSlider(maximum);
            slider.TickPlacement = TickPlacement.BottomRight;
            slider.TickFrequency = frequency;
            slider.IsDirectionReversed = reversed;
            if (ticks is not null)
            {
                slider.Ticks = ticks;
            }

            await ShowAsync(slider, async () =>
            {
                var tickBar = (TickBar)slider.Template.FindName("BottomTick", slider);
                List<double> xs = TickPositions(tickBar);
                // 目盛りの位置を、つまみの移動範囲に対する割合で示す。
                Track track = TrackOf(slider);
                double thumbWidth = track.Thumb.ActualWidth;
                double start = thumbWidth / 2;
                double length = track.ActualWidth - thumbWidth;
                IEnumerable<string> values = xs.Select(x => D(Math.Round((x - start) / length * maximum, 1)));
                rows.Add([$"{label}: tick marks, left to right (as values of a non-reversed track)",
                    $"{xs.Count}: {string.Join(", ", values)}"]);
                await Task.CompletedTask;
            });
        }

        foreach (TickPlacement placement in new[] { TickPlacement.None, TickPlacement.BottomRight, TickPlacement.Both })
        {
            Slider slider = NewSlider();
            slider.TickPlacement = placement;
            var panel = new StackPanel();
            panel.Children.Add(slider);
            Layout(panel, 300, 200);
            rows.Add([$"TickPlacement={placement}: height", D(slider.ActualHeight)]);
        }

        foreach (bool enabled in new[] { false, true })
        {
            // デモアプリと同じ 0〜10 で 2〜8。
            Slider slider = NewSlider(10);
            slider.SelectionStart = 2;
            slider.SelectionEnd = 8;
            slider.IsSelectionRangeEnabled = enabled;
            var host = new Grid();
            host.Children.Add(slider);
            Layout(host, 300, 100);
            // 選択範囲の要素は Track の兄弟なので、どちらも Slider からの位置で比べる。
            var range = (FrameworkElement)slider.Template.FindName("PART_SelectionRange", slider);
            Track track = TrackOf(slider);
            Rect trackBounds = Bounds(track, slider);
            double thumbWidth = track.Thumb.ActualWidth;
            double start = trackBounds.Left + thumbWidth / 2;
            double length = track.ActualWidth - thumbWidth;
            string span = "";
            if (range.Visibility == Visibility.Visible)
            {
                Rect bounds = Bounds(range, slider);
                span = $", spans {D(Math.Round((bounds.Left - start) / length * 10, 1))} to {D(Math.Round((bounds.Right - start) / length * 10, 1))}";
            }

            rows.Add([$"0..10, Selection 2..8, IsSelectionRangeEnabled={enabled}", range.Visibility + span]);

            if (enabled)
            {
                // 選択範囲が Value を制限するか。
                slider.Value = 9.5;
                rows.Add(["  Value = 9.5 outside the selection", D(slider.Value)]);
            }
        }

        FieldInfo toolTipField = typeof(Slider).GetField("_autoToolTip", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Slider._autoToolTip が見つからない。");

        foreach ((AutoToolTipPlacement placement, int precision, double target) in new[]
        {
            (AutoToolTipPlacement.None, 0, 33.6),
            (AutoToolTipPlacement.TopLeft, 0, 33.6),
            (AutoToolTipPlacement.TopLeft, 0, 33.4),
            (AutoToolTipPlacement.TopLeft, 2, 33.456),
            (AutoToolTipPlacement.BottomRight, 1, 1234.56),
        })
        {
            Slider slider = NewSlider(target > 100 ? 2000 : 100);
            slider.AutoToolTipPlacement = placement;
            slider.AutoToolTipPrecision = precision;
            await ShowAsync(slider, async () =>
            {
                DragThumb(slider, target);
                var toolTip = (ToolTip?)toolTipField.GetValue(slider);
                string exact = slider.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                rows.Add([$"AutoToolTipPlacement={placement}, Precision={precision}, Value {exact}",
                    toolTip is null ? "no tooltip" : $"\"{toolTip.Content}\", IsOpen={toolTip.IsOpen}"]);
                TrackOf(slider).Thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
                await Task.CompletedTask;
            });
        }

        return rows;
    }

    /// <summary>TickBar が描いた線の x 座標（左から順）。</summary>
    private static List<double> TickPositions(TickBar tickBar)
    {
        var xs = new List<double>();
        Collect(VisualTreeHelper.GetDrawing(tickBar));
        return xs.Distinct().OrderBy(x => x).ToList();

        void Collect(Drawing? drawing)
        {
            switch (drawing)
            {
                case DrawingGroup group:
                    foreach (Drawing child in group.Children)
                    {
                        Collect(child);
                    }

                    break;
                case GeometryDrawing geometry when geometry.Geometry is not null:
                    CollectGeometry(geometry.Geometry);
                    break;
            }
        }

        void CollectGeometry(Geometry geometry)
        {
            switch (geometry)
            {
                case GeometryGroup group:
                    foreach (Geometry child in group.Children)
                    {
                        CollectGeometry(child);
                    }

                    break;
                case LineGeometry line:
                    xs.Add(Math.Round(line.StartPoint.X, 2));
                    break;
                default:
                    Rect bounds = geometry.Bounds;
                    xs.Add(Math.Round((bounds.Left + bounds.Right) / 2, 2));
                    break;
            }
        }
    }
}
