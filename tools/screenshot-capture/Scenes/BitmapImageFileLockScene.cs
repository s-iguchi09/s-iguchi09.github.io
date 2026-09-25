using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の BitmapImage で表示した画像ファイルが削除・上書きできなくなる問題の解決方法」の図。
///
/// 読み込み方を変えて実際に画像を読み、その直後に <see cref="File.Delete"/> を試みる。
/// ファイルが掴まれたままかどうかは実行しないと分からないため、撮影のたびに測り直す。
/// </summary>
internal sealed class BitmapImageFileLockScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "読み込み方ごとに、直後の File.Delete が成功するかを確かめる",
        "BitmapImage(Uri) の後に CacheOption を設定しても効かないこと",
        "IgnoreImageCache ではファイルのロックを回避できないこと",
        "既定の読み込み方でも参照を手放して GC が走れば解放されること",
        "読み込み方ごとに、直後の上書き（File.Copy で上書き）とリネーム（File.Move）が成功するか",
        "UriSource をバインドした BitmapImage の XAML を読み込んだとき（DataContext なし、DataContext に有効なパスがあるテンプレートの中）",
    ];

    public string Slug => "wpf-bitmapimage-file-lock-cacheoption";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "BitmapImage: delete, overwrite and rename right after loading",
            ["how the image is loaded", "size", "File.Delete", "overwrite (File.Copy)", "rename (File.Move)"],
            MeasureLoadStyles(),
            "bitmapimage-file-lock-matrix.svg");

        await context.SaveTableAsync(
            "BitmapImage with UriSource=\"{Binding ImagePath}\"",
            ["where the XAML is loaded", "result"],
            await MeasureUriSourceBindingAsync(),
            "bitmapimage-urisource-binding.svg");
    }

    private const string BoundImage = """
        <Image>
          <Image.Source>
            <BitmapImage UriSource="{Binding ImagePath}" CacheOption="OnLoad" />
          </Image.Source>
        </Image>
        """;

    public sealed class PathSource : System.ComponentModel.INotifyPropertyChanged
    {
        private string _imagePath = "";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ImagePath)));
            }
        }
    }

    /// <summary>
    /// UriSource をバインドした BitmapImage。BitmapImage は読み込みの最後（EndInit）に UriSource か StreamSource を
    /// 必要とするため、バインドがまだ解決していない時点で失敗するかを確かめる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> MeasureUriSourceBindingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        try
        {
            SceneContext.LoadXaml<System.Windows.Controls.Image>(BoundImage);
            rows.Add(["XamlReader, no DataContext", "loaded"]);
        }
        catch (Exception e)
        {
            rows.Add(["XamlReader, no DataContext", Describe(e)]);
        }

        string path = CreateImage();
        string second = CreateImage(64, 96);
        var source = new PathSource { ImagePath = path };
        var template = SceneContext.LoadXaml<DataTemplate>($"<DataTemplate>{BoundImage}</DataTemplate>");
        var presenter = new System.Windows.Controls.ContentControl { Content = source, ContentTemplate = template };
        var window = new Window { Content = presenter, Width = 200, Height = 150, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            await Capture.ShowAndSettleAsync(window);
            rows.Add(["DataTemplate, DataContext holds a valid path", $"shown: {Shown(presenter)}"]);

            // 表示した後でパスを変える（通知あり）。初期化後の変更は反映されないとされる。
            source.ImagePath = second;
            await Capture.SettleAsync(window);
            rows.Add(["then ImagePath changed to a 64x96 image", $"shown: {Shown(presenter)}"]);
        }
        catch (Exception e)
        {
            rows.Add(["DataTemplate, DataContext holds a valid path", Describe(e)]);
        }
        finally
        {
            window.Close();
            TryCleanup(path);
            TryCleanup(second);
        }

        return rows;
    }

    /// <summary>表示中の Image の BitmapImage の UriSource のファイル名と、画素の大きさ。</summary>
    private static string Shown(DependencyObject root)
    {
        System.Windows.Controls.Image? image = null;
        void Walk(DependencyObject node)
        {
            if (node is System.Windows.Controls.Image found) image = found;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
        }

        Walk(root);
        return image?.Source is BitmapImage bitmap
            ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}, UriSource {(bitmap.UriSource is null ? "null" : Path.GetFileName(bitmap.UriSource.LocalPath) == Path.GetFileName(((PathSource)((System.Windows.Controls.ContentControl)root).Content).ImagePath) ? "= current ImagePath" : "= earlier path")}"
            : "no image";
    }

    /// <summary>文言は OS の言語で変わるため、例外の型だけを出す。</summary>
    private static string Describe(Exception e)
        => e.InnerException is null ? e.GetType().Name : $"{e.GetType().Name} (inner {e.InnerException.GetType().Name})";

    /// <summary>読み込み方と、その直後にファイルを削除できるかの対応。</summary>
    private static List<IReadOnlyList<string>> MeasureLoadStyles()
    {
        (string Label, Func<string, BitmapSource> Load)[] cases =
        [
            ("new BitmapImage(uri)", LoadWithConstructor),
            ("+ CacheOption = OnLoad", LoadWithConstructorThenOption),
            ("BeginInit / EndInit", LoadWithInit),
            ("+ CacheOption = OnLoad", LoadWithInitAndOnLoad),
            ("+ CreateOptions = IgnoreImageCache", LoadWithInitAndIgnoreCache),
            ("StreamSource + OnLoad", LoadFromStream),
            ("ImageSourceConverter", LoadWithConverter),
        ];

        var rows = new List<IReadOnlyList<string>>();
        foreach ((string label, Func<string, BitmapSource> load) in cases)
        {
            // 削除・上書き・リネームは互いに影響するため、それぞれ別のファイルを読み込んで試す。
            string path = CreateImage();
            BitmapSource image = load(path);
            string delete = DeleteResult(path);
            GC.KeepAlive(image);
            TryCleanup(path);
            rows.Add([label, $"{image.PixelWidth}x{image.PixelHeight}", delete, Attempt(load, OverwriteResult), Attempt(load, RenameResult)]);
        }

        // 参照が確実に消える形で読み込み、GC 後に解放されるかを測る。
        string collected = CreateImage();
        LoadAndDrop(collected);
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }

        rows.Add(["(default) after GC", "-", DeleteResult(collected), "-", "-"]);
        TryCleanup(collected);

        return rows;
    }

    private static BitmapSource LoadWithConstructor(string path)
        => new BitmapImage(new Uri(path, UriKind.Absolute));

    /// <summary>コンストラクタで初期化済みのため、あとから CacheOption を設定しても効かない。</summary>
    private static BitmapSource LoadWithConstructorThenOption(string path)
    {
        var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        return bitmap;
    }

    private static BitmapSource LoadWithInit(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        return bitmap;
    }

    private static BitmapSource LoadWithInitAndOnLoad(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource LoadWithInitAndIgnoreCache(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        return bitmap;
    }

    private static BitmapSource LoadFromStream(string path)
    {
        var bitmap = new BitmapImage();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }

        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>XAML の <c>Source="パス"</c> と同じ経路をたどる。</summary>
    private static BitmapSource LoadWithConverter(string path)
        => (BitmapSource)new ImageSourceConverter().ConvertFromString(path)!;

    /// <summary>
    /// 読み込んだ <see cref="BitmapImage"/> をメソッドの外へ出さない。
    /// 呼び出し元のローカル変数に残ると、GC の対象にならず測定にならない。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadAndDrop(string path)
    {
        var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
        _ = bitmap.PixelWidth;
    }

    /// <summary>検証用の PNG を 1 枚作る。</summary>
    private static string CreateImage(int width = 64, int height = 48)
    {
        string directory = Path.Combine(Path.GetTempPath(), "bitmapimage-file-lock-scene");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"probe-{Guid.NewGuid():N}.png");

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(0, 0, width, height));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
        return path;
    }

    /// <summary>新しいファイルを読み込み、そのファイルに操作を試して結果を返す。</summary>
    private static string Attempt(Func<string, BitmapSource> load, Func<string, string> action)
    {
        string path = CreateImage();
        BitmapSource image = load(path);
        string result = action(path);
        GC.KeepAlive(image);
        TryCleanup(path);
        TryCleanup(path + ".renamed");
        return result;
    }

    private static string OverwriteResult(string path)
    {
        string other = CreateImage();
        try
        {
            File.Copy(other, path, overwrite: true);
            return "OK";
        }
        catch (IOException)
        {
            return "IOException";
        }
        finally
        {
            TryCleanup(other);
        }
    }

    private static string RenameResult(string path)
    {
        try
        {
            File.Move(path, path + ".renamed");
            return "OK";
        }
        catch (IOException)
        {
            return "IOException";
        }
    }

    private static string DeleteResult(string path)
    {
        try
        {
            File.Delete(path);
            return "OK";
        }
        catch (IOException)
        {
            return "IOException";
        }
    }

    /// <summary>削除できなかったファイルを残さないようにする。失敗しても無視する。</summary>
    private static void TryCleanup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // ロックされたままのファイルは、プロセス終了時に解放される。
        }
    }
}
