using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ch04.CanExecuteNotUpdating;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 4 章の検証。
///
/// 再現版（CanExecuteChanged を発行しない <see cref="NeverRaisingCommand"/>）と、修正版（名前が変わったら
/// <c>RaiseCanExecuteChanged</c> を呼ぶ <see cref="SaveViewModel"/>）を表示し、名前の TextBox へ文字を InputManager 経由で入力して、
/// 保存ボタンの IsEnabled が記事「WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法」の表と一致するかを確かめる。
/// </summary>
internal sealed class BookACh04CanExecuteScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 4 章の再現版（CanExecuteChanged を宣言しただけで発行しないコマンド）で、名前の TextBox にキーで入力して CanExecute が true になっても、保存ボタンの IsEnabled は False のままで、CommandManager.InvalidateRequerySuggested を呼んでも False のままであること（記事の表の own event / key typed と own event / InvalidateRequerySuggested の行と同じ）",
        "付録サンプル第 4 章の修正版（名前の setter で RaiseCanExecuteChanged を呼ぶ）で、同じ入力をすると保存ボタンの IsEnabled が False から True になること（記事の表の own event / RaiseCanExecuteChanged の行と同じ）",
    ];

    public string Slug => "book-a-ch04-canexecute-not-updating";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch04");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>
        {
            new("再現版、名前の TextBox に taro を入力", "own event / key typed in a TextBox", "False → False",
                await TypeAsync(new ReproView { DataContext = new ReproSaveViewModel() }, requery: false)),
            new("再現版、入力の後に CommandManager.InvalidateRequerySuggested()", "own event / InvalidateRequerySuggested", "False → False",
                await TypeAsync(new ReproView { DataContext = new ReproSaveViewModel() }, requery: true)),
            new("修正版、名前の TextBox に taro を入力", "own event / RaiseCanExecuteChanged", "False → True",
                await TypeAsync(new FixedView { DataContext = new SaveViewModel() }, requery: false)),
        };

        BookAReport.WriteAndAssert(
            "ch04.md", Slug, "第 4 章",
            "WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法",
            "wpf-relaycommand-canexecute-not-updating",
            "「記事の該当箇所」は記事の表の行（実装 / 呼んだもの）。値は保存ボタンの IsEnabled の「入力前 → 入力後」。記事は条件をコードで変えたうえで隣の TextBox に 1 キー入力したが、サンプルでは条件（名前）そのものをキーで入力する。",
            checks);
    }

    internal static async Task<string> TypeAsync(UserControl view, bool requery)
    {
        view.Width = 320;
        string result = "";
        await ShowAsync(view, async () =>
        {
            var box = (TextBox)view.FindName("nameBox");
            var save = (Button)view.FindName("saveButton");
            string before = save.IsEnabled.ToString();
            await FocusAsync(box);
            TypeLetters(box, "taro");
            await Capture.SettleAsync(Window.GetWindow(view)!, 100);
            if (requery)
            {
                CommandManager.InvalidateRequerySuggested();
                await Capture.SettleAsync(Window.GetWindow(view)!, 100);
            }

            result = $"{before} → {save.IsEnabled}";
        }, activate: true);
        return result;
    }
}
