using System.Windows;
using System.Windows.Controls;
using Ch07.DataTemplateParentBinding;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 7 章の検証。
///
/// 再現版（テンプレートの中で素の {Binding DeleteCommand}）と修正版（RelativeSource で ItemsControl の DataContext を経由）を表示し、
/// 1 行目の削除ボタンの Command と IsEnabled、単位の表示を読んでから、実際のマウスでクリックして行が消えるかを確かめる。
/// 値は記事「WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け」の表と本文と比べる。
/// </summary>
internal sealed class BookACh07DataTemplateParentScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 7 章の再現版（テンプレートの中で素の {Binding DeleteCommand}）で、削除ボタンの Command が null で IsEnabled が True のままであり（記事の表の 1 行目と同じ）、単位は空で、実際のマウスでクリックしても行が消えないこと（記事の「問題」の節と同じ）",
        "付録サンプル第 7 章の修正版（RelativeSource AncestorType=ItemsControl で DataContext を経由）で、削除ボタンの Command が解決されて IsEnabled が True であり（記事の表の 2 行目と同じ）、単位が kg と表示され、クリックするとその行が消えること（記事の実装例と図と同じ）",
    ];

    public string Slug => "book-a-ch07-datatemplate-parent-binding";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch07");

    public async Task CaptureAsync(SceneContext context)
    {
        (string reproBinding, string reproUnit, string reproClick) = await RunAsync(new ReproView());
        (string fixedBinding, string fixedUnit, string fixedClick) = await RunAsync(new FixedView());

        var checks = new List<BookACheck>
        {
            new("再現版、1 行目の削除ボタン", "表 1 行目（{Binding DeleteCommand}）", "Command null、IsEnabled True", reproBinding),
            new("再現版、1 行目の単位", "実装例の図（素のバインドは空のまま）", "（空）", reproUnit),
            new("再現版、1 行目の削除ボタンをクリック", "「問題」の節（押しても何も起きない）", "行は 3 件のまま", reproClick),
            new("修正版、1 行目の削除ボタン", "表 2 行目（RelativeSource AncestorType=ItemsControl）", "Command 解決できた、IsEnabled True", fixedBinding),
            new("修正版、1 行目の単位", "実装例の図（RelativeSource は kg を表示）", "kg", fixedUnit),
            new("修正版、1 行目の削除ボタンをクリック", "実装例（押した行を削除する。先頭の B はサンプルのデータ）", "行は 2 件、先頭は B", fixedClick),
        };

        BookAReport.WriteAndAssert(
            "ch07.md", Slug, "第 7 章",
            "WPF の DataTemplate 内から親の DataContext にバインドできない原因と RelativeSource の使い分け",
            "wpf-datatemplate-parent-datacontext-binding",
            "「表」は記事のテンプレートに書いたバインドごとの表（Command と IsEnabled）。クリックは実際のマウスで行った。",
            checks);
    }

    private static async Task<(string Binding, string Unit, string Click)> RunAsync(UserControl view)
    {
        var vm = new MeasurementListViewModel();
        view.DataContext = vm;
        view.Width = 260;
        (string, string, string) result = default;
        await ShowAsync(view, async () =>
        {
            Window window = await FrontCenteredAsync(view);
            var list = (ItemsControl)view.FindName("list");
            var row = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            Button delete = Descendants(row).OfType<Button>().First(b => b.Name == "deleteButton");
            TextBlock unit = Descendants(row).OfType<TextBlock>().First(t => t.Name == "unitText");

            string binding = $"Command {(delete.Command is null ? "null" : "解決できた")}、IsEnabled {delete.IsEnabled}";
            string unitText = unit.Text.Length == 0 ? "（空）" : unit.Text;

            using (RealMouse.Preserve())
            {
                await RealMouse.ClickAsync(window, delete);
            }

            await Capture.SettleAsync(window, 100);
            string click = vm.Items.Count == 3 ? "行は 3 件のまま" : $"行は {vm.Items.Count} 件、先頭は {vm.Items[0].Name}";
            result = (binding, unitText, click);
        }, activate: true);
        return result;
    }
}
