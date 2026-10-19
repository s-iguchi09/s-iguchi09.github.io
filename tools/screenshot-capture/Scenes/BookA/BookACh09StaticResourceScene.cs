using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ch09.StaticResourceNotUpdating;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 9 章の検証。
///
/// 再現版（見本の枠を StaticResource で参照）と修正版（DynamicResource で参照）を表示し、ビューモデルの
/// 「テーマを切り替える」コマンドでリソースのエントリを差し替えて、枠の背景の色が
/// 記事「WPF で StaticResource を変更しても画面が更新されない原因と解決方法」の表と同じ振る舞いになるかを確かめる。
/// 記事の計測は White から Red への差し替え、サンプルは SkyBlue から DarkSlateGray への差し替えで、色の値は違う。
/// 比べるのは「差し替えの後も元の色のままか、差し替えた色になるか」である。
/// </summary>
internal sealed class BookACh09StaticResourceScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 9 章の再現版（StaticResource で参照）で、ビューモデルのコマンドでテーマを切り替えてリソースのエントリを差し替えても、枠の背景は元の SkyBlue のままであること（記事の表の StaticResource の差し替え前と差し替え後の行と同じ振る舞い）",
        "付録サンプル第 9 章の修正版（DynamicResource で参照）で、同じ操作で枠の背景が差し替えた DarkSlateGray になること（記事の表の DynamicResource の差し替え前と差し替え後の行と同じ振る舞い）",
    ];

    public string Slug => "book-a-ch09-staticresource-not-updating";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch09");

    public async Task CaptureAsync(SceneContext context)
    {
        string light = ThemeResources.LightColor.ToString();
        string dark = ThemeResources.DarkColor.ToString();
        var checks = new List<BookACheck>
        {
            new("再現版、「テーマを切り替える」の前と後", "記事の表の StaticResource、差し替え前と差し替え後（記事の値は #FFFFFFFF → #FFFFFFFF。同じ値のまま）",
                $"前 {light}、後 {light}", await ToggleAsync(new ReproView())),
            new("修正版、「テーマを切り替える」の前と後", "記事の表の DynamicResource、差し替え前と差し替え後（記事の値は #FFFFFFFF → #FFFF0000。差し替えた値になる）",
                $"前 {light}、後 {dark}", await ToggleAsync(new FixedView())),
        };

        BookAReport.WriteAndAssert(
            "ch09.md", Slug, "第 9 章",
            "WPF で StaticResource を変更しても画面が更新されない原因と解決方法",
            "wpf-staticresource-vs-dynamicresource",
            "記事の表は White（#FFFFFFFF）から Red（#FFFF0000）への差し替え、サンプルは SkyBlue（#FF87CEEB）から DarkSlateGray（#FF2F4F4F）への差し替えで、色の値は違う。比べているのは、差し替えの後も元の色のままか、差し替えた色になるか。",
            checks);
    }

    private static async Task<string> ToggleAsync(UserControl view)
    {
        var vm = new ThemeViewModel();
        view.DataContext = vm;
        view.Width = 260;
        string result = "";
        await ShowAsync(view, async () =>
        {
            Window window = Window.GetWindow(view)!;
            var sample = (Border)view.FindName("sample");
            string before = Color(sample);
            vm.ToggleThemeCommand.Execute(null);
            await Capture.SettleAsync(window, 100);
            result = $"前 {before}、後 {Color(sample)}";
        });
        return result;
    }

    private static string Color(Border border) =>
        border.Background is SolidColorBrush brush ? brush.Color.ToString() : border.Background?.ToString() ?? "null";
}
