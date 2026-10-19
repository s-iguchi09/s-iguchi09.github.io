using System.Windows;
using System.Windows.Controls;
using Ch08.UserControlDependencyProperty;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 8 章の検証。
///
/// 再現版のカード（中で素の {Binding Title}）と修正版のカード（直下の Grid の DataContext をカード自身に向け、
/// Title を既定で双方向に登録）を、利用側から Title="{Binding HeaderText}" で置き、
/// 記事「WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計」の表と一致するかを確かめる。
/// バインドのエラーは記事の計測と同じ数え方（<see cref="BindingErrorCounter"/>）で、表示から読み取りまでの間に数える。
/// </summary>
internal sealed class BookACh08UserControlScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 8 章の再現版（カードの中で素の {Binding Title}）で、利用側から Title に HeaderText をバインドすると、カードの Title は from PageViewModel になるが中の見出しは空で、バインドのエラーが 1 件出ること（記事の 3 つ目の表の 1 行目と同じ）",
        "付録サンプル第 8 章の修正版（カードの直下の Grid の DataContext をカード自身に向ける）で、中の DataContext はカード、カード要素の DataContext は PageViewModel のままで、見出しに from PageViewModel が出て、エラーは 0 件であること（記事の 3 つ目の表の 4 行目と同じ）",
        "修正版で Title を既定で双方向に登録したカードの中の TextBox に xyz と入力すると、ビューモデルの HeaderText が xyz になり、エラーは 0 件であること（記事の 3 つ目の表の 6 行目と同じ）",
    ];

    public string Slug => "book-a-ch08-usercontrol-dependency-property";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch08");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>();

        using (BindingErrorCounter errors = BindingErrorCounter.Start())
        {
            var vm = new PageViewModel();
            var view = new ReproView { DataContext = vm };
            int before = errors.Count;
            await ShowAsync(view, async () =>
            {
                await Settle(view);
                var card = (ReproInfoCard)view.FindName("card");
                var title = (TextBlock)card.FindName("titleText");
                checks.Add(new("再現版、利用側から Title に HeaderText をバインド", "3 つ目の表 1 行目（記事の値は「Title from PageViewModel、内側 （空）、Error 40 が 1 件」）",
                    "Title from PageViewModel、内側 （空）、エラー 1 件",
                    $"Title {card.Title}、内側 {Text(title)}、エラー {errors.Count - before} 件"));
            });
        }

        using (BindingErrorCounter errors = BindingErrorCounter.Start())
        {
            var vm = new PageViewModel();
            var view = new FixedView { DataContext = vm };
            int before = errors.Count;
            await ShowAsync(view, async () =>
            {
                await Settle(view);
                var card = (FixedInfoCard)view.FindName("card");
                var root = (Grid)card.FindName("cardRoot");
                var title = (TextBlock)card.FindName("titleText");
                checks.Add(new("修正版、利用側から Title に HeaderText をバインド", "3 つ目の表 4 行目（DataContext を内側の Grid に引き継ぐ。記事の値は「内側の DataContext は InfoCard、カードの DataContext は PageViewModel、エラー 0 件」。見出しの値はサンプルで足した確認）",
                    "内側の DataContext はカード、カードの DataContext は PageViewModel、見出し from PageViewModel、エラー 0 件",
                    $"内側の DataContext は{(root.DataContext == card ? "カード" : root.DataContext?.GetType().Name)}、カードの DataContext は {card.DataContext?.GetType().Name}、見出し {Text(title)}、エラー {errors.Count - before} 件"));

                var box = (TextBox)card.FindName("titleBox");
                await FocusAsync(box);
                box.SelectAll();
                TypeLetters(box, "xyz");
                await Settle(view);
                checks.Add(new("修正版、カードの中の TextBox に xyz と入力", "3 つ目の表 6 行目（BindsTwoWayByDefault）",
                    "HeaderText xyz、エラー 0 件", $"HeaderText {vm.HeaderText}、エラー {errors.Count - before} 件"));
            }, activate: true);
        }

        BookAReport.WriteAndAssert(
            "ch08.md", Slug, "第 8 章",
            "WPF の UserControl に定義した DependencyProperty へ内部からバインドできない原因と DataContext の設計",
            "wpf-usercontrol-dependencyproperty-binding-not-working",
            "「3 つ目の表」は記事の注意点の表（条件ごとの計測値）。記事のカードの型名は InfoCard で、サンプルでは ReproInfoCard と FixedInfoCard に分けている。記事の「Error 40 が 1 件」は、ここではバインドのエラーと警告の件数として数える。",
            checks);
    }

    private static string Text(TextBlock block) => block.Text.Length == 0 ? "（空）" : block.Text;

    private static Task Settle(FrameworkElement view) => Capture.SettleAsync(Window.GetWindow(view)!, 100);
}
