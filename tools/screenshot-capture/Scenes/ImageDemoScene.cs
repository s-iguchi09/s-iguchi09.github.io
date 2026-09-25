using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「Image」（apps/wpf-standard-control-demo/image.md と日本語版）の記述を実測する。
///
/// 画像は一時フォルダーに PNG を書き出して使う（大きさと DPI を決めるため）。
/// Source はデモアプリと同じく、TextBox の文字（ファイルのパス）をバインドで渡す。
/// ファイルがロックされるかは、表示した後に File.Delete が通るかで読む。
/// </summary>
internal sealed class ImageDemoScene : IScene
{
    /// <summary>デモアプリの TextBox の初期値。</summary>
    private const string DemoPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";

    public string Slug => "wpf-standard-control-demo-image";

    public string ImageDirectory => DemoProbe.ImageDirectory("image");

    public IReadOnlyList<string> Verifies =>
    [
        "Image の基底クラス・Focusable と、Stretch / StretchDirection の既定値",
        "Stretch 4 種類と StretchDirection 3 種類の組み合わせごとの、300 x 200 の領域での小さい画像（100 x 50）と大きい画像（600 x 300）の表示の大きさ",
        "大きい画像を UniformToFill と、デモアプリの初期値（None / UpOnly）で表示したときの位置と、はみ出した部分が切れるか（レイアウトのクリップ）",
        "72 DPI の画像を Stretch=None で表示したときの大きさ",
        "TextBox の文字（パス）を Source にバインドしたときの Source の型と、存在しないパス・SVG ファイルのとき",
        "パスのバインドで表示したファイル・BitmapCacheOption.OnLoad で読んだファイル・Source を null にした後のファイルを削除できるか",
        "BitmapImage の DecodePixelWidth を設定したときのピクセル数と表示の大きさ",
        "デモアプリの初期のパス（img0.jpg）が計測したマシンにあるか、その大きさ",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        string folder = Path.Combine(Path.GetTempPath(), "image-demo-scene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string small = WritePng(Path.Combine(folder, "small.png"), 100, 50, 96);
            string large = WritePng(Path.Combine(folder, "large.png"), 600, 300, 96);

            await context.SaveTableAsync(
                "Image: displayed size in a 300 x 200 area (small image 100 x 50, large 600 x 300)",
                ["Stretch", "image", "UpOnly", "DownOnly", "Both"],
                MatrixRows(small, large),
                "image-matrix.svg");

            await context.SaveTableAsync(
                "Image: type, clipping, DPI, sources from a path, file locks and decoding",
                ["case", "measured"],
                await OtherRowsAsync(folder, large),
                "image-behavior.svg");
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 一時フォルダーなので、ロックが残って消せなくても計測は続ける。
            }
        }
    }

    private static string WritePng(string path, int width, int height, double dpi)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xC0;
            pixels[i + 3] = 0xFF;
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static BitmapImage Load(string path, int decodePixelWidth = 0)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        return bitmap;
    }

    private static Image Laid(ImageSource source, Stretch stretch, StretchDirection direction)
    {
        var image = new Image { Source = source, Stretch = stretch, StretchDirection = direction };
        Layout(new Grid { Children = { image } }, 300, 200);
        return image;
    }

    private static string Size(UIElement element) => $"{D(element.RenderSize.Width)} x {D(element.RenderSize.Height)}";

    private static List<IReadOnlyList<string>> MatrixRows(string small, string large)
    {
        var rows = new List<IReadOnlyList<string>>();
        BitmapImage smallImage = Load(small);
        BitmapImage largeImage = Load(large);
        foreach (Stretch stretch in new[] { Stretch.None, Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill })
        {
            foreach ((string name, BitmapImage source) in new[] { ("100 x 50", smallImage), ("600 x 300", largeImage) })
            {
                var cells = new List<string> { stretch.ToString(), name };
                foreach (StretchDirection direction in new[] { StretchDirection.UpOnly, StretchDirection.DownOnly, StretchDirection.Both })
                {
                    cells.Add(Size(Laid(source, stretch, direction)));
                }

                rows.Add(cells);
            }
        }

        return rows;
    }

    /// <summary>ファイルを削除できるか。消せたら同じ内容で書き戻す（後の計測で使うため）。</summary>
    private static string TryDelete(string path)
    {
        byte[] content = File.ReadAllBytes(path);
        try
        {
            File.Delete(path);
            File.WriteAllBytes(path, content);
            return "deleted";
        }
        catch (IOException ex)
        {
            return $"locked ({ex.GetType().Name})";
        }
    }

    private static async Task<List<IReadOnlyList<string>>> OtherRowsAsync(string folder, string large)
    {
        var rows = new List<IReadOnlyList<string>>();

        var plain = new Image();
        rows.Add(["base class, Focusable; defaults Stretch, StretchDirection",
            $"{typeof(Image).BaseType!.Name}, {plain.Focusable}; {plain.Stretch}, {plain.StretchDirection}"]);

        foreach ((Stretch stretch, StretchDirection direction, bool center) in new[]
                 {
                     (Stretch.UniformToFill, StretchDirection.Both, false),
                     (Stretch.UniformToFill, StretchDirection.Both, true),
                     (Stretch.None, StretchDirection.UpOnly, false),
                 })
        {
            var image = new Image { Source = Load(large), Stretch = stretch, StretchDirection = direction };
            if (center)
            {
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
            }

            Layout(new Grid { Children = { image } }, 300, 200);
            var host = (Grid)VisualTreeHelper.GetParent(image);
            Rect bounds = Bounds(image, host);
            Geometry? clip = LayoutInformation.GetLayoutClip(image);
            rows.Add([$"large image, {stretch} / {direction}{(center ? ", aligned Center" : "")} in 300 x 200: size; top-left; clip",
                $"{Size(image)}; {D(bounds.X)}, {D(bounds.Y)}; {(clip is null ? "none" : $"{D(clip.Bounds.Width)} x {D(clip.Bounds.Height)}")}"]);
        }

        {
            string dpi72 = WritePng(Path.Combine(folder, "dpi72.png"), 100, 50, 72);
            Image image = Laid(Load(dpi72), Stretch.None, StretchDirection.Both);
            rows.Add(["100 x 50 pixels at 72 DPI, Stretch None: size", Size(image)]);
        }

        foreach ((string name, string path) in new[]
                 {
                     ("PNG file", WritePng(Path.Combine(folder, "bound.png"), 100, 50, 96)),
                     ("missing file", Path.Combine(folder, "missing.png")),
                     ("SVG file", WriteSvg(Path.Combine(folder, "icon.svg"))),
                 })
        {
            var box = new TextBox { Text = path };
            var image = new Image { Stretch = Stretch.None };
            image.SetBinding(Image.SourceProperty, new Binding(nameof(TextBox.Text)) { Source = box });
            var grid = new Grid { Width = 300, Height = 200, Children = { image } };
            await ShowAsync(grid, async () =>
            {
                await Capture.SettleAsync(Window.GetWindow(grid)!, 100);
                rows.Add([$"path bound to Source, {name}: Source type, size",
                    $"{image.Source?.GetType().Name ?? "null"}, {Size(image)}"]);
                if (name == "PNG file")
                {
                    string shown = TryDelete(path);
                    BindingOperations.ClearBinding(image, Image.SourceProperty);
                    image.Source = null;
                    await Capture.SettleAsync(Window.GetWindow(grid)!, 50);
                    string cleared = TryDelete(path);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    rows.Add(["  delete the file: while shown; Source = null; then GC",
                        $"{shown}; {cleared}; {TryDelete(path)}"]);
                }
            });
        }

        {
            string loaded = WritePng(Path.Combine(folder, "onload.png"), 100, 50, 96);
            Image image = Laid(Load(loaded), Stretch.None, StretchDirection.Both);
            rows.Add(["BitmapImage with CacheOption OnLoad, shown: delete the file", TryDelete(loaded)]);
            GC.KeepAlive(image);
        }

        {
            BitmapImage decoded = Load(large, 100);
            Image image = Laid(decoded, Stretch.None, StretchDirection.Both);
            rows.Add(["600 x 300 with DecodePixelWidth 100: pixels; size in DIPs; Stretch None size",
                $"{decoded.PixelWidth} x {decoded.PixelHeight}; {D(decoded.Width)} x {D(decoded.Height)}; {Size(image)}"]);
        }

        if (File.Exists(DemoPath))
        {
            BitmapFrame frame = BitmapFrame.Create(new Uri(DemoPath), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            rows.Add(["demo's start path img0.jpg: exists; pixels; DPI",
                $"True; {frame.PixelWidth} x {frame.PixelHeight}; {D(frame.DpiX)}"]);
        }
        else
        {
            rows.Add(["demo's start path img0.jpg: exists", "False"]);
        }

        return rows;
    }

    private static string WriteSvg(string path)
    {
        File.WriteAllText(path,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"50\"><rect width=\"100\" height=\"50\" fill=\"blue\"/></svg>");
        return path;
    }
}
