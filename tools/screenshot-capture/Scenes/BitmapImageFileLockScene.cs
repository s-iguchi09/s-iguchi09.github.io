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
    ];

    public string Slug => "wpf-bitmapimage-file-lock-cacheoption";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildLoadStyleWindow(), "bitmapimage-file-lock-matrix.png");
    }

    /// <summary>読み込み方と、その直後にファイルを削除できるかの対応。</summary>
    private static Window BuildLoadStyleWindow()
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
            string path = CreateImage();
            BitmapSource image = load(path);
            rows.Add([label, $"{image.PixelWidth}x{image.PixelHeight}", DeleteResult(path)]);
            GC.KeepAlive(image);
            TryCleanup(path);
        }

        // 参照が確実に消える形で読み込み、GC 後に解放されるかを測る。
        string collected = CreateImage();
        LoadAndDrop(collected);
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }

        rows.Add(["(default) after GC", "-", DeleteResult(collected)]);
        TryCleanup(collected);

        return DemoLayout.BuildTableWindow(
            "BitmapImage: File.Delete right after loading",
            ["how the image is loaded", "size", "File.Delete"],
            rows);
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
    private static string CreateImage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "bitmapimage-file-lock-scene");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"probe-{Guid.NewGuid():N}.png");

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(0, 0, 64, 48));
        }

        var target = new RenderTargetBitmap(64, 48, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
        return path;
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
