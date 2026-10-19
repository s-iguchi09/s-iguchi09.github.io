using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Ch05.ValidationNotDisplayed;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 5 章の検証。
///
/// 再現版（IDataErrorInfo を実装し、バインドに ValidatesOnDataErrors を書かない）と、修正版（INotifyDataErrorInfo と、
/// メッセージを出す ErrorTemplate）を表示し、記事「WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け」の
/// 表（検証の段階）の値と、実装例の節で述べた表示の変化と一致するかを確かめる。
/// </summary>
internal sealed class BookACh05ValidationScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 5 章の再現版（IDataErrorInfo を実装し、ValidatesOnDataErrors を書かない）で、名前が空でも Validation.HasError が False、Validation.Errors が 0 件、アドーナーが 0 個であること（記事の表の「IDataErrorInfo だけ」の行と同じ）",
        "付録サンプル第 5 章の修正版（INotifyDataErrorInfo）で、起動直後に Validation.HasError が True、Validation.Errors が 1 件、アドーナーが 1 個で、ErrorTemplate のメッセージにビューモデルのエラー（名前は必須です。）が出ること（記事の表の「INotifyDataErrorInfo だけ」の行と、実装例の節と同じ）",
        "修正版で 21 文字を入力するとメッセージが長さのエラーに替わり、正しい名前を入力するとアドーナーが 0 個になって消えること（記事の実装例の節と同じ）",
    ];

    public string Slug => "book-a-ch05-validation-not-displayed";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch05");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>();

        await ShowViewAsync(new ReproView { DataContext = new ReproAccountViewModel() }, view =>
        {
            checks.Add(new("再現版、起動直後（名前は空）", "表「IDataErrorInfo だけ」", "HasError False、Errors 0、装飾 0", Stages(Box(view))));
            return Task.CompletedTask;
        });

        await ShowViewAsync(new FixedView { DataContext = new AccountViewModel() }, async view =>
        {
            TextBox box = Box(view);
            checks.Add(new("修正版、起動直後（名前は空）", "表「INotifyDataErrorInfo だけ」", "HasError True、Errors 1、装飾 1", Stages(box)));
            checks.Add(new("修正版、起動直後のメッセージ", "実装例の節（起動直後に必須のメッセージが出る。記事の文言は「名前は必須である。」）", "名前は必須です。", Message(box)));

            await FocusAsync(box);
            TypeLetters(box, "abcdefghijklmnopqrstu");
            await Settle(view);
            checks.Add(new("修正版、21 文字を入力", "実装例の節（21 文字で長さのメッセージに替わる。記事の文言は「名前は 20 文字以内である。」）", "名前は 20 文字以内にしてください。", Message(box)));

            box.SelectAll();
            TypeLetters(box, "taro");
            await Settle(view);
            checks.Add(new("修正版、正しい名前（taro）を入力", "実装例の節（正しい値でアドーナーが消える）", "HasError False、Errors 0、装飾 0", Stages(box)));
        });

        BookAReport.WriteAndAssert(
            "ch05.md", Slug, "第 5 章",
            "WPF で入力検証のエラーが表示されない原因と IDataErrorInfo / INotifyDataErrorInfo の使い分け",
            "wpf-validation-error-not-displayed",
            "「表」は記事の検証の段階の表（HasError・Errors の数・装飾（adorner）の数）。記事の実装例のメッセージは「名前は必須である。」「名前は 20 文字以内である。」で、サンプルでは同じ内容を「です・ください」の言い回しにしている。",
            checks);
    }

    internal static async Task ShowViewAsync(UserControl view, Func<UserControl, Task> act)
    {
        view.Width = 320;
        await ShowAsync(view, async () =>
        {
            await Settle(view);
            await act(view);
        }, activate: true);
    }

    internal static string Stages(TextBox box)
    {
        int adorners = AdornerLayer.GetAdornerLayer(box)?.GetAdorners(box)?.Length ?? 0;
        return $"HasError {Validation.GetHasError(box)}、Errors {Validation.GetErrors(box).Count}、装飾 {adorners}";
    }

    /// <summary>ErrorTemplate の中のメッセージ（errorMessage）の文字列。アドーナーが無ければ「（なし）」。</summary>
    internal static string Message(TextBox box)
    {
        Adorner[]? adorners = AdornerLayer.GetAdornerLayer(box)?.GetAdorners(box);
        TextBlock? message = adorners?.SelectMany(a => Descendants(a)).OfType<TextBlock>().FirstOrDefault(t => t.Name == "errorMessage");
        return message?.Text ?? "（なし）";
    }

    internal static TextBox Box(FrameworkElement view) => (TextBox)view.FindName("nameBox");

    internal static Task Settle(FrameworkElement view) => Capture.SettleAsync(Window.GetWindow(view)!, 100);
}
