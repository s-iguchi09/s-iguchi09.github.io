using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「GridSplitter」（apps/wpf-standard-control-demo/gridsplitter.md と日本語版）の記述を実測する。
///
/// ドラッグは、Thumb がマウス操作で発生させるのと同じ DragStarted / DragDelta / DragCompleted
/// イベントを GridSplitter に発生させて再現する。GridSplitter はこれらのイベントを受けて列幅を変える。
/// IsDragging とキーボード操作は、表示したウィンドウでマウス・キーの入力イベントを送って確かめる。
/// </summary>
internal sealed class GridSplitterDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-gridsplitter";

    public string ImageDirectory => DemoProbe.ImageDirectory("gridsplitter");

    public IReadOnlyList<string> Verifies =>
    [
        "GridSplitter の継承関係（Thumb・Control）と、各プロパティの既定値（既定のスタイルが適用された後の PreviewStyle・HorizontalAlignment とその出どころを含む）",
        "ResizeBehavior（BasedOnAlignment / CurrentAndNext / PreviousAndCurrent / PreviousAndNext）ごとに、両隣の列が *・Auto・固定値の 9 通りでドラッグしたときに変わる列と幅（デモアプリの ResizeBehavior 欄と同じ構成）",
        "ResizeDirection（Auto / Columns / Rows）ごとの、デモアプリの ResizeDirection 欄と同じ縦横 2 本のスプリッターの動き",
        "スプリッターの HorizontalAlignment（Stretch / Center / Left / Right）と置き方（専用の列か、内容と同じ列か）による、変わる列の違い",
        "ShowsPreview が True と False のときの、ドラッグ中の列幅と、ドラッグ中に隣の列の内容が測り直される回数",
        "ShowsPreview が True のときに装飾層へ置かれるプレビュー要素の型と、PreviewStyle を設定しない場合の中身（塗りの色）",
        "DragIncrement によるドラッグ量の丸めと、KeyboardIncrement による矢印キー 1 回の移動量",
        "Focusable を False にするとキーボードフォーカスを受け取らなくなること",
        "MinWidth による最小幅の制限、Esc キーとドラッグの中止で元の幅に戻ること、Grid の外や前の列が無い位置では幅が変わらないこと",
        "ドラッグ後の ColumnDefinition.Width の単位と、TwoWay バインドしたソースへの反映",
        "マウスの左ボタンを押すと IsDragging が True になり、ドラッグを中止すると False に戻ること",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "GridSplitter: type and defaults",
            ["item", "value"],
            Defaults(),
            "gridsplitter-defaults.svg");

        await context.SaveTableAsync(
            "GridSplitter in a 400-wide Auto column, dragged 30 to the right (widths: left / splitter / right)",
            ["left | right", "before", "BasedOnAlignment", "CurrentAndNext", "PreviousAndCurrent", "PreviousAndNext"],
            BehaviorMatrix(),
            "gridsplitter-resize-behavior.svg");

        await context.SaveTableAsync(
            "GridSplitter: which columns move (Grid 400 wide, dragged 30 to the right)",
            ["case", "widths before", "widths after"],
            Placement(),
            "gridsplitter-placement.svg");

        await context.SaveTableAsync(
            "GridSplitter: ResizeDirection, the demo app's cross layout (300 x 100), each splitter dragged by (30, 20)",
            ["ResizeDirection", "vertical splitter dragged", "horizontal splitter dragged"],
            await DirectionAsync(),
            "gridsplitter-resize-direction.svg");

        await context.SaveTableAsync(
            "GridSplitter: preview, increments, keyboard, limits and binding",
            ["case", "measured"],
            await BehaviorsAsync(),
            "gridsplitter-behaviors.svg");
    }

    // ------------------------------------------------------------------
    // 組み立てとドラッグ
    // ------------------------------------------------------------------

    /// <summary>左の列・スプリッターの Auto 列・右の列の 3 列の Grid。デモアプリと同じ形。</summary>
    private static (Grid Grid, GridSplitter Splitter) ThreeColumns(
        GridLength left, GridLength right, GridResizeBehavior behavior, double width = 400)
    {
        var grid = new Grid { Width = width, Height = 40 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = left });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = right, MinWidth = 0 });

        // Auto 列の幅が決まるよう、内容の幅は 60 に固定する。
        var leftContent = new Border { Width = 60, Background = Brushes.LightBlue };
        var rightContent = new Border { Width = 60, Background = Brushes.SkyBlue };
        Grid.SetColumn(rightContent, 2);
        var splitter = new GridSplitter { Width = 5, Background = Brushes.Gray, ResizeBehavior = behavior };
        Grid.SetColumn(splitter, 1);

        grid.Children.Add(leftContent);
        grid.Children.Add(splitter);
        grid.Children.Add(rightContent);
        return (grid, splitter);
    }

    private static void Drag(GridSplitter splitter, double horizontal, double vertical, bool complete = true)
    {
        splitter.RaiseEvent(new DragStartedEventArgs(0, 0));
        splitter.RaiseEvent(new DragDeltaEventArgs(horizontal, vertical));
        if (complete)
        {
            splitter.RaiseEvent(new DragCompletedEventArgs(horizontal, vertical, false));
        }
    }

    private static string Widths(Grid grid) =>
        string.Join(" / ", grid.ColumnDefinitions.Select(c => D(c.ActualWidth)));

    private static string Heights(Grid grid) =>
        string.Join(" / ", grid.RowDefinitions.Select(r => D(r.ActualHeight)));

    private static Grid Relayout(Grid grid)
    {
        Layout(grid, grid.Width, grid.Height);
        return grid;
    }

    // ------------------------------------------------------------------
    // 計測
    // ------------------------------------------------------------------

    private static List<IReadOnlyList<string>> Defaults()
    {
        var splitter = new GridSplitter();
        var chain = new List<string>();
        for (Type? type = typeof(GridSplitter).BaseType; type is not null && type != typeof(FrameworkElement); type = type.BaseType)
        {
            chain.Add(type.Name);
        }

        return
        [
            ["base types", string.Join(" > ", chain)],
            ["drag events (declared on Thumb)", string.Join(", ",
                new[] { Thumb.DragStartedEvent, Thumb.DragDeltaEvent, Thumb.DragCompletedEvent }
                    .Select(e => $"{e.Name} ({e.OwnerType.Name})"))],
            ["IsDragging read-only", Thumb.IsDraggingProperty.ReadOnly.ToString()],
            ["ResizeDirection", splitter.ResizeDirection.ToString()],
            ["ResizeBehavior", splitter.ResizeBehavior.ToString()],
            ["ShowsPreview", splitter.ShowsPreview.ToString()],
            ["DragIncrement", D(splitter.DragIncrement)],
            ["KeyboardIncrement", D(splitter.KeyboardIncrement)],
            ["PreviewStyle", WpfProbe.Describe(splitter.PreviewStyle)],
            // 既定のスタイルが適用された後の値と、その出どころ。
            ["PreviewStyle after the default style is applied (value source)", StyledValue(GridSplitter.PreviewStyleProperty)],
            ["HorizontalAlignment (value source)", StyledValue(FrameworkElement.HorizontalAlignmentProperty)],
            ["VerticalAlignment (value source)", StyledValue(FrameworkElement.VerticalAlignmentProperty)],
            ["Focusable", splitter.Focusable.ToString()],
            ["DragIncrement = 0", Throws(() => new GridSplitter().DragIncrement = 0)],
            ["KeyboardIncrement = 0", Throws(() => new GridSplitter().KeyboardIncrement = 0)],
        ];
    }

    /// <summary>Grid に置いてレイアウトし、既定のスタイルが適用された状態の値を読む。</summary>
    private static string StyledValue(DependencyProperty property)
    {
        var splitter = new GridSplitter();
        var grid = new Grid();
        grid.Children.Add(splitter);
        Layout(grid, 100, 100);
        return WpfProbe.ValueAndSource(splitter, property);
    }

    private static List<IReadOnlyList<string>> BehaviorMatrix()
    {
        var sizes = new (string Label, GridLength Length)[]
        {
            ("*", new GridLength(1, GridUnitType.Star)),
            ("Auto", GridLength.Auto),
            ("200", new GridLength(200)),
        };
        var behaviors = new[]
        {
            GridResizeBehavior.BasedOnAlignment, GridResizeBehavior.CurrentAndNext,
            GridResizeBehavior.PreviousAndCurrent, GridResizeBehavior.PreviousAndNext,
        };

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string leftLabel, GridLength left) in sizes)
        {
            foreach ((string rightLabel, GridLength right) in sizes)
            {
                var row = new List<string> { $"{leftLabel} | {rightLabel}" };

                (Grid before, _) = ThreeColumns(left, right, GridResizeBehavior.PreviousAndNext);
                row.Add(Widths(Relayout(before)));

                foreach (GridResizeBehavior behavior in behaviors)
                {
                    (Grid grid, GridSplitter splitter) = ThreeColumns(left, right, behavior);
                    Relayout(grid);
                    string initial = Widths(grid);
                    Drag(splitter, 30, 0);
                    string after = Widths(Relayout(grid));
                    row.Add(after == initial ? "no change" : after);
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> Placement()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 専用の Auto 列に置き、配置を変える。
        foreach (HorizontalAlignment alignment in new[] { HorizontalAlignment.Stretch, HorizontalAlignment.Center })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(
                new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star),
                GridResizeBehavior.BasedOnAlignment);
            splitter.HorizontalAlignment = alignment;
            Relayout(grid);
            string before = Widths(grid);
            Drag(splitter, 30, 0);
            rows.Add([$"own Auto column, HorizontalAlignment={alignment}", before, Widths(Relayout(grid))]);
        }

        // 内容と同じ列（0 列目）に置く。Microsoft Learn の例に多い置き方。
        foreach (HorizontalAlignment alignment in new[]
                 { HorizontalAlignment.Right, HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Stretch })
        {
            var grid = new Grid { Width = 400, Height = 40 };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var splitter = new GridSplitter { Width = 5, HorizontalAlignment = alignment };
            grid.Children.Add(splitter);
            Relayout(grid);
            string before = Widths(grid);
            Drag(splitter, 30, 0);
            rows.Add([$"in column 0 with content, HorizontalAlignment={alignment}", before, Widths(Relayout(grid))]);
        }

        // 3 列の真ん中（1 列目）に内容と一緒に置く。Left と Right で変わる列の組が分かる。
        foreach (HorizontalAlignment alignment in new[] { HorizontalAlignment.Left, HorizontalAlignment.Right })
        {
            var grid = new Grid { Width = 400, Height = 40 };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var splitter = new GridSplitter { Width = 5, HorizontalAlignment = alignment };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);
            Relayout(grid);
            string before = Widths(grid);
            Drag(splitter, 30, 0);
            rows.Add([$"in column 1 with content, HorizontalAlignment={alignment}", before, Widths(Relayout(grid))]);
        }

        // 前の列が無い位置に PreviousAndNext で置く。
        {
            var grid = new Grid { Width = 400, Height = 40 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var splitter = new GridSplitter { Width = 5, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            grid.Children.Add(splitter);
            Relayout(grid);
            string before = Widths(grid);
            string thrown = Throws(() => Drag(splitter, 30, 0));
            rows.Add([$"column 0 of 2, PreviousAndNext (no previous column): {thrown}", before, Widths(Relayout(grid))]);
        }

        // Grid の外に置く。
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var splitter = new GridSplitter { Width = 5, Height = 40 };
            panel.Children.Add(new Border { Width = 60 });
            panel.Children.Add(splitter);
            Layout(panel, 400, 40);
            string thrown = Throws(() => Drag(splitter, 30, 0));
            rows.Add([$"in a StackPanel instead of a Grid: {thrown}",
                $"first child {D(((FrameworkElement)panel.Children[0]).ActualWidth)}",
                $"first child {D(((FrameworkElement)Layout(panel, 400, 40).Children[0]).ActualWidth)}"]);
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> DirectionAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (GridResizeDirection direction in new[]
                 { GridResizeDirection.Auto, GridResizeDirection.Columns, GridResizeDirection.Rows })
        {
            var cells = new List<string> { direction.ToString() };
            foreach (bool vertical in new[] { true, false })
            {
                // デモアプリの ResizeDirection 欄と同じ構成。
                var grid = new Grid { Width = 300, Height = 100 };
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var verticalSplitter = new GridSplitter
                {
                    Width = 5, VerticalAlignment = VerticalAlignment.Stretch,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext, ResizeDirection = direction,
                };
                Grid.SetRowSpan(verticalSplitter, 3);
                Grid.SetColumn(verticalSplitter, 1);

                var horizontalSplitter = new GridSplitter
                {
                    Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext, ResizeDirection = direction,
                };
                Grid.SetRow(horizontalSplitter, 1);
                Grid.SetColumnSpan(horizontalSplitter, 3);

                grid.Children.Add(verticalSplitter);
                grid.Children.Add(horizontalSplitter);
                Relayout(grid);

                string thrown = Throws(() => Drag(vertical ? verticalSplitter : horizontalSplitter, 30, 20));
                Relayout(grid);
                cells.Add($"columns {Widths(grid)}, rows {Heights(grid)}" + (thrown == "no exception" ? "" : $" ({thrown})"));
            }

            rows.Add(cells);
        }

        await Task.CompletedTask;
        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> BehaviorsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        GridLength star = new(1, GridUnitType.Star);

        // ShowsPreview: ドラッグ中（DragCompleted の前）と完了後の幅。
        // プレビューは装飾層に描くため、実際にウィンドウへ表示して測る。
        foreach (bool preview in new[] { false, true })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            splitter.ShowsPreview = preview;
            var counter = new MeasureCounter();
            grid.Children.Add(counter);

            await ShowAsync(grid, async () =>
            {
                string before = Widths(grid);
                int measuresBefore = counter.MeasureCount;
                splitter.RaiseEvent(new DragStartedEventArgs(0, 0));
                for (int i = 1; i <= 10; i++)
                {
                    // Thumb の DragDelta は、つまみ自身から見たマウスの移動量である。
                    // プレビューなしではスプリッター自体が動くので 1 回ごとの移動量（4）になり、
                    // プレビューありではスプリッターが動かないので開始位置からの累計（4, 8, ... 40）になる。
                    splitter.RaiseEvent(new DragDeltaEventArgs(preview ? i * 4 : 4, 0));
                    grid.UpdateLayout();
                }

                string during = Widths(grid);
                int measuresDuring = counter.MeasureCount - measuresBefore;

                if (preview)
                {
                    // プレビューとして装飾層に置かれた要素と、その中身を読む。
                    AdornerLayer? layer = AdornerLayer.GetAdornerLayer(grid);
                    List<Control> controls = layer is null ? [] : Descendants(layer).OfType<Control>().ToList();
                    rows.Add(["ShowsPreview=True: preview element in the adorner layer",
                        string.Join(", ", controls.Select(c =>
                            $"{c.GetType().Name} (Style is the splitter's PreviewStyle: {ReferenceEquals(c.Style, splitter.PreviewStyle)})"))]);
                    rows.Add(["ShowsPreview=True: visuals inside the preview element",
                        string.Join(", ", controls.SelectMany(c => Descendants(c)).Select(DescribeVisual))]);
                }

                splitter.RaiseEvent(new DragCompletedEventArgs(40, 0, false));
                grid.UpdateLayout();
                rows.Add([$"ShowsPreview={preview}: before / during drag / after release",
                    $"{before}  |  {during}  |  {Widths(grid)}"]);
                rows.Add([$"ShowsPreview={preview}: re-measures of left content over 10 drag steps",
                    measuresDuring.ToString()]);
                await Task.CompletedTask;
            });
        }

        // DragIncrement: ドラッグ量の丸め。
        foreach ((double increment, double drag) in new[] { (1.0, 27.0), (20.0, 9.0), (20.0, 11.0), (20.0, 27.0), (20.0, 31.0) })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            splitter.DragIncrement = increment;
            Relayout(grid);
            double left = grid.ColumnDefinitions[0].ActualWidth;
            Drag(splitter, drag, 0);
            Relayout(grid);
            rows.Add([$"DragIncrement={D(increment)}, dragged {D(drag)}",
                $"left column moved by {D(grid.ColumnDefinitions[0].ActualWidth - left)}"]);
        }

        // KeyboardIncrement: 右矢印キー 1 回。Focusable の既定値と False の場合のフォーカス。
        foreach (double? increment in new double?[] { null, 25 })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            if (increment is not null)
            {
                splitter.KeyboardIncrement = increment.Value;
            }

            await ShowAsync(grid, async () =>
            {
                double left = grid.ColumnDefinitions[0].ActualWidth;
                PressKey(splitter, Key.Right);
                grid.UpdateLayout();
                rows.Add([$"KeyboardIncrement={(increment is null ? "default" : D(increment.Value))}, Right arrow once",
                    $"left column moved by {D(grid.ColumnDefinitions[0].ActualWidth - left)}"]);
                await Task.CompletedTask;
            });
        }

        foreach (bool focusable in new[] { true, false })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            splitter.Focusable = focusable;
            await ShowAsync(grid, async () =>
            {
                bool focused = splitter.Focus();
                rows.Add([$"Focusable={focusable}: Focus() / IsKeyboardFocused",
                    $"{focused} / {splitter.IsKeyboardFocused}"]);
                await Task.CompletedTask;
            }, activate: true);
        }

        // MinWidth と、ドラッグの取り消し。
        foreach (double? minWidth in new double?[] { null, 50 })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            if (minWidth is not null)
            {
                grid.ColumnDefinitions[0].MinWidth = minWidth.Value;
            }

            Relayout(grid);
            Drag(splitter, -1000, 0);
            rows.Add([$"left MinWidth={(minWidth is null ? "none" : D(minWidth.Value))}, dragged -1000",
                Widths(Relayout(grid))]);
        }

        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            await ShowAsync(grid, async () =>
            {
                string before = Widths(grid);
                Drag(splitter, 40, 0, complete: false);
                grid.UpdateLayout();
                string during = Widths(grid);
                PressKey(splitter, Key.Escape);
                grid.UpdateLayout();
                rows.Add(["dragged 40, then Esc before release: before / during / after",
                    $"{before}  |  {during}  |  {Widths(grid)}"]);
                await Task.CompletedTask;
            });
        }

        // ドラッグ後の Width の単位と、TwoWay バインド。
        foreach ((string label, GridLength left, GridLength right) in new[]
        {
            ("* | *", star, star),
            ("200 | *", new GridLength(200), star),
            ("Auto | *", GridLength.Auto, star),
        })
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(left, right, GridResizeBehavior.PreviousAndNext);
            Relayout(grid);
            Drag(splitter, 30, 0);
            Relayout(grid);
            rows.Add([$"{label}: ColumnDefinition.Width after drag",
                $"{grid.ColumnDefinitions[0].Width} | {grid.ColumnDefinitions[2].Width}"]);
        }

        foreach (BindingMode? mode in new BindingMode?[] { null, BindingMode.TwoWay })
        {
            var source = new WidthSource { Left = new GridLength(1, GridUnitType.Star) };
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            var binding = new Binding(nameof(WidthSource.Left)) { Source = source };
            if (mode is not null)
            {
                binding.Mode = mode.Value;
            }

            BindingOperations.SetBinding(grid.ColumnDefinitions[0], ColumnDefinition.WidthProperty, binding);
            Relayout(grid);
            Drag(splitter, 30, 0);
            Relayout(grid);
            bool kept = BindingOperations.GetBindingExpression(grid.ColumnDefinitions[0], ColumnDefinition.WidthProperty) is not null;
            rows.Add([$"Width bound to a source property, Mode={(mode is null ? "not set" : mode.ToString())}: source after drag / binding",
                $"{source.Left} / {(kept ? "kept" : "removed")}"]);
        }

        // IsDragging: 実際にマウスの左ボタンを押したときの Thumb の経路を通す。
        {
            (Grid grid, GridSplitter splitter) = ThreeColumns(star, star, GridResizeBehavior.PreviousAndNext);
            await ShowAsync(grid, async () =>
            {
                bool before = splitter.IsDragging;
                splitter.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                });
                bool pressed = splitter.IsDragging;
                splitter.CancelDrag();
                rows.Add(["IsDragging: before / after left button down / after CancelDrag()",
                    $"{before} / {pressed} / {splitter.IsDragging}"]);
                await Task.CompletedTask;
            }, activate: true);
        }

        return rows;
    }

    private static string DescribeVisual(DependencyObject visual) => visual switch
    {
        System.Windows.Shapes.Shape shape => $"{shape.GetType().Name} Fill={shape.Fill} Opacity={D(shape.Opacity)}",
        Border border => $"Border Background={border.Background} Opacity={D(border.Opacity)}",
        _ => visual.GetType().Name,
    };

    private sealed class WidthSource : System.ComponentModel.INotifyPropertyChanged
    {
        private GridLength _left;

        public GridLength Left
        {
            get => _left;
            set
            {
                _left = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Left)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

}
