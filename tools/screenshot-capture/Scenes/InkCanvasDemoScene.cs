using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「InkCanvas」（apps/wpf-standard-control-demo/inkcanvas.html と日本語版）の記述を実測する。
///
/// 描く・消す・選ぶ操作は実際のマウス（<see cref="RealMouse"/>）でドラッグして行う。ペン（スタイラス）は計測しない。
/// Copy / Cut / Paste はクリップボードを書き換えるため実行せず、CanExecute だけを読む。
/// デモアプリの初期値は、デモアプリのマークアップ拡張と同じ方法（列挙値の順、Colors / Brushes の静的プロパティの順）で求める。
/// </summary>
internal sealed class InkCanvasDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-inkcanvas";

    public string ImageDirectory => DemoProbe.ImageDirectory("inkcanvas");

    public IReadOnlyList<string> Verifies =>
    [
        "InkCanvas の EditingMode / EditingModeInverted / ActiveEditingMode / Background / DefaultDrawingAttributes の既定値（ペンの幅は丸めない値、Background は既定のスタイルの適用後の値と出どころ）と、ジェスチャー認識が使えるか",
        "デモアプリのコンボボックスの初期値（EditingMode は 1 番目、EditingModeInverted は 3 番目、ペンの色は Colors の 1 番目、背景は Brushes の 0 番目）と、ペンの色と背景のコントラスト比",
        "実際のマウスでドラッグしたときのストロークの数、ドラッグ中の ActiveEditingMode、ストロークの色と太さ",
        "DefaultDrawingAttributes をその場で書き換えたとき（デモアプリの方法）に、描いた後のストロークの色が変わるか",
        "Background が null のときにドラッグで描けるか",
        "EditingMode が None / GestureOnly / InkAndGesture / EraseByStroke / EraseByPoint / Select のときにドラッグ・クリックした結果",
        "SelectAll / Copy / Undo コマンドを InkCanvas に対して実行できるか（Ink / Select モード、ストロークの有無）と、SelectAll で選ばれたストローク",
        "GestureOnly / InkAndGesture で左から右へドラッグしたときに Gesture イベントで認識されたジェスチャーと、Cancel を設定したとき・SetEnabledGestures で円だけに限ったときにストロークが残るか",
        "StrokeCollection を ISF で保存して読み込んだときのストロークの数",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "InkCanvas: defaults, the demo's start, drawing with the real mouse, modes and commands",
            ["case", "measured"],
            await MeasureAsync(),
            "inkcanvas-behavior.svg");
    }

    private static InkCanvas Canvas(InkCanvasEditingMode mode = InkCanvasEditingMode.Ink) => new()
    {
        Width = 300,
        Height = 200,
        Background = Brushes.White,
        EditingMode = mode,
    };

    /// <summary>
    /// InkCanvas の上の点を順にたどって、実際のマウスでドラッグする。
    /// <paramref name="whileDown"/> を渡すと、ボタンを押したまま最初に動かした後に呼び、その結果を返す。
    /// </summary>
    private static async Task<string> DragAsync(Window window, InkCanvas canvas, IReadOnlyList<Point> points, Func<string>? whileDown = null)
    {
        string during = "";
        using (RealMouse.Preserve())
        {
            await RealMouse.MoveToAsync(canvas, points[0]);
            await RealMouse.LeftDownAsync(window);
            foreach (Point point in points.Skip(1))
            {
                await RealMouse.MoveToAsync(canvas, point);
                if (whileDown is not null && during.Length == 0)
                {
                    during = whileDown();
                }
            }

            await RealMouse.LeftUpAsync(window);
        }

        return during;
    }

    private static Point[] Horizontal(double y) =>
        [new(50, y), new(90, y), new(130, y), new(170, y), new(210, y), new(250, y)];

    private static Point[] Vertical(double x) =>
        [new(x, 40), new(x, 70), new(x, 100), new(x, 130), new(x, 160)];

    /// <summary>(50, 100) から (250, 100) までの横線のストローク。</summary>
    private static Stroke Line() =>
        new(new StylusPointCollection(Enumerable.Range(0, 11).Select(i => new StylusPoint(50 + i * 20, 100))));

    private static string Name(Color color) =>
        typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(p => (Color)p.GetValue(null)! == color)?.Name ?? color.ToString();

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var plain = new InkCanvas();
            DrawingAttributes a = plain.DefaultDrawingAttributes;
            rows.Add(["defaults: EditingMode, EditingModeInverted, ActiveEditingMode, Background",
                $"{plain.EditingMode}, {plain.EditingModeInverted}, {plain.ActiveEditingMode}, {WpfProbe.Describe(plain.Background)}"]);
            rows.Add(["  pen: Color; Width, Height; StylusTip, IsHighlighter; recognizer",
                $"{Name(a.Color)}; {a.Width:R}, {(a.Height == a.Width ? "same" : a.Height.ToString("R"))}; {a.StylusTip}, {a.IsHighlighter}; {plain.IsGestureRecognizerAvailable}"]);

            // 既定のスタイルが適用された後の Background と、その出どころ。システムのウィンドウ色と同じか。
            var styled = new InkCanvas();
            Layout(new Grid { Children = { styled } }, 100, 100);
            rows.Add(["  Background after style (source) / SystemColors.WindowBrush; metadata",
                $"{WpfProbe.ValueAndSource(styled, InkCanvas.BackgroundProperty)} / {SystemColors.WindowBrush}; {WpfProbe.Describe(InkCanvas.BackgroundProperty.GetMetadata(typeof(InkCanvas)).DefaultValue)}"]);
        }

        {
            InkCanvasEditingMode[] modes = Enum.GetValues<InkCanvasEditingMode>();
            PropertyInfo[] colors = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static);
            PropertyInfo[] brushes = typeof(Brushes).GetProperties(BindingFlags.Public | BindingFlags.Static);
            var pen = (Color)colors[1].GetValue(null)!;
            var background = ((SolidColorBrush)brushes[0].GetValue(null)!).Color;
            double l1 = Luminance(pen);
            double l2 = Luminance(background);
            double contrast = (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
            rows.Add(["demo's start: EditingMode, EditingModeInverted",
                $"{modes[1]}, {modes[3]}"]);
            rows.Add(["  pen color, background; contrast ratio",
                $"{colors[1].Name} ({pen}), {brushes[0].Name} ({background}); {D(contrast)} : 1"]);
        }

        {
            InkCanvas canvas = Canvas();
            await ShowAsync(canvas, async () =>
            {
                Window window = await FrontAsync(canvas);
                string during = await DragAsync(window, canvas, Horizontal(60), () => canvas.ActiveEditingMode.ToString());
                DrawingAttributes first = canvas.Strokes[0].DrawingAttributes;
                rows.Add(["Ink, real drag: strokes; ActiveEditingMode while dragging; stroke color, width",
                    $"{canvas.Strokes.Count}; {during}; {Name(first.Color)}, {D(first.Width)}"]);

                canvas.DefaultDrawingAttributes.Color = Colors.Red;
                canvas.DefaultDrawingAttributes.Width = 10;
                await DragAsync(window, canvas, Horizontal(140));
                DrawingAttributes second = canvas.Strokes[1].DrawingAttributes;
                rows.Add(["  DefaultDrawingAttributes changed in place (as the demo does): 1st stroke; 2nd stroke",
                    $"{Name(canvas.Strokes[0].DrawingAttributes.Color)}, {D(canvas.Strokes[0].DrawingAttributes.Width)}; {Name(second.Color)}, {D(second.Width)}"]);
                rows.Add(["  same DrawingAttributes object: default and 1st stroke; 1st and 2nd",
                    $"{ReferenceEquals(canvas.DefaultDrawingAttributes, first)}; {ReferenceEquals(first, second)}"]);
            });
        }

        foreach ((string name, InkCanvasEditingMode mode, Brush? background, bool cancel, bool onlyCircle) in new[]
                 {
                     ("Background null", InkCanvasEditingMode.Ink, (Brush?)null, false, false),
                     ("None", InkCanvasEditingMode.None, Brushes.White, false, false),
                     ("GestureOnly", InkCanvasEditingMode.GestureOnly, Brushes.White, false, false),
                     ("InkAndGesture", InkCanvasEditingMode.InkAndGesture, Brushes.White, false, false),
                     ("InkAndGesture, Gesture handler sets Cancel", InkCanvasEditingMode.InkAndGesture, Brushes.White, true, false),
                     ("InkAndGesture, SetEnabledGestures(Circle)", InkCanvasEditingMode.InkAndGesture, Brushes.White, false, true),
                 })
        {
            InkCanvas canvas = Canvas(mode);
            canvas.Background = background;
            if (onlyCircle)
            {
                // 認識するジェスチャーを円だけに限る。左から右の直線（Right）は認識の対象から外れる。
                canvas.SetEnabledGestures([ApplicationGesture.Circle]);
            }

            string gesture = "none";
            canvas.Gesture += (_, e) =>
            {
                gesture = e.GetGestureRecognitionResults()[0].ApplicationGesture.ToString();
                if (cancel)
                {
                    // ジェスチャーとして扱わず、インクとして残す。
                    e.Cancel = true;
                }
            };
            var host = new Grid { Background = Brushes.White, Children = { canvas } };
            await ShowAsync(host, async () =>
            {
                Window window = await FrontAsync(host);
                await DragAsync(window, canvas, Horizontal(100));
                rows.Add([$"{name}, real drag left to right: strokes; Gesture event", $"{canvas.Strokes.Count}; {gesture}"]);
            });
        }

        foreach (InkCanvasEditingMode mode in new[] { InkCanvasEditingMode.EraseByStroke, InkCanvasEditingMode.EraseByPoint })
        {
            InkCanvas canvas = Canvas(mode);
            canvas.Strokes.Add(Line());
            await ShowAsync(canvas, async () =>
            {
                Window window = await FrontAsync(canvas);
                await DragAsync(window, canvas, Vertical(150));
                string pieces = string.Join(" and ", canvas.Strokes.Select(s =>
                {
                    Rect bounds = s.GetBounds();
                    return $"x {D(Math.Round(bounds.Left))} to {D(Math.Round(bounds.Right))}";
                }));
                rows.Add([$"{mode}, one line from x 50 to 250, real drag across x 150: strokes",
                    $"{canvas.Strokes.Count}{(pieces.Length > 0 ? $" ({pieces})" : "")}"]);
            });
        }

        {
            InkCanvas canvas = Canvas(InkCanvasEditingMode.Select);
            canvas.Strokes.Add(Line());
            await ShowAsync(canvas, async () =>
            {
                Window window = await FrontAsync(canvas);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(canvas, new Point(150, 100));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add(["Select, real click on the line: selected strokes; Copy can execute",
                    $"{canvas.GetSelectedStrokes().Count}; {ApplicationCommands.Copy.CanExecute(null, canvas)}"]);
            });
        }

        foreach (InkCanvasEditingMode mode in new[] { InkCanvasEditingMode.Ink, InkCanvasEditingMode.Select })
        {
            InkCanvas canvas = Canvas(mode);
            await ShowAsync(canvas, async () =>
            {
                string empty = ApplicationCommands.SelectAll.CanExecute(null, canvas).ToString();
                canvas.Strokes.Add(Line());
                string oneStroke = ApplicationCommands.SelectAll.CanExecute(null, canvas).ToString();
                ApplicationCommands.SelectAll.Execute(null, canvas);
                await Capture.SettleAsync(Window.GetWindow(canvas)!, 50);
                rows.Add([$"{mode} mode, SelectAll can execute: no strokes; one stroke; executed: selected",
                    $"{empty}; {oneStroke}; {canvas.GetSelectedStrokes().Count}"]);
                if (mode == InkCanvasEditingMode.Select)
                {
                    rows.Add(["  after SelectAll: Copy can execute; Undo can execute",
                        $"{ApplicationCommands.Copy.CanExecute(null, canvas)}; {ApplicationCommands.Undo.CanExecute(null, canvas)}"]);
                }
            });
        }

        {
            var strokes = new StrokeCollection { Line(), Line() };
            using var stream = new MemoryStream();
            strokes.Save(stream);
            long length = stream.Length;
            stream.Position = 0;
            var loaded = new StrokeCollection(stream);
            rows.Add(["2 strokes saved as ISF and loaded: bytes; strokes", $"{length}; {loaded.Count}"]);
        }

        return rows;
    }
}
