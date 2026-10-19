using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ch02.InputLimits;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes.BookA;

/// <summary>
/// 本の付録サンプル（<c>samples/book-a</c>）第 2 章の検証。
///
/// MVVM で書き直した再現版（<see cref="ReproView"/> と <see cref="ReproCustomerViewModel"/>）と
/// 修正版（<see cref="FixedView"/> と <see cref="CustomerViewModel"/>）を表示し、
/// 記事「WPF の MaxLength などの入力制限が、コードやバインドから入る値に効かない理由」の計測
/// （<see cref="InputLimitsFromCodeScene"/>）と同じ入力経路で値を入れ、記事の 2 つの表の値と一致するかを確かめる。
/// 文字とキーは記事の計測と同じく InputManager 経由で送る。
/// </summary>
internal sealed class BookACh02InputLimitsScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "付録サンプル第 2 章の再現版（制限は TextBox.MaxLength=5 と Slider の 0〜100・10 刻みだけ）で、利用者の入力は 5 文字で止まるが、ビューモデルの読み込みで入った 8 文字の顧客コードはそのまま表示され、音量 150 は Slider が 100、ビューモデルが 150 のまま、23.4 は 23.4 のままであること（記事の表 1 の 1・6・7 行目と同じ）",
        "付録サンプル第 2 章の修正版（ビューモデルの INotifyDataErrorInfo）で、上限を超えた顧客コードの読み込みは HasErrors と Validation.HasError が True になり、入力では 5 文字で止まってエラーにならず、読み込み後の末尾への入力は追加されず、Backspace 1 回ではエラーが残ること（記事の表 2 の 1・2 行目と同じ）",
        "修正版の保存ボタン（SaveCommand の CanExecute がエラーの無いこと）は、上限を超えた値で無効、5 文字に直すと有効になり、Backspace 3 回での編集にも追従すること（記事の表 2 の 3 行目と同じ。記事は IsEnabled を HasErrors の反転にバインドしていた）",
        "修正版の音量は、ビューモデルからの 150 と 23.4 が 100 と 20 に、End キーと 50 からの右矢印キーの値が 100 と 60 に丸められ、ビューモデルと Slider が一致すること（記事の表 2 の 4・5 行目と同じ。記事と同じく Slider を 0〜200・刻みなし・SmallChange 5 にして測る）",
    ];

    public string Slug => "book-a-ch02-input-limits";

    public string ImageDirectory => Path.Combine("images", "book-a", "ch02");

    public async Task CaptureAsync(SceneContext context)
    {
        var checks = new List<BookACheck>();

        // ---------------------------------------------------------------- 再現版（記事の表 1）
        checks.Add(new("再現版、顧客コードに abcdefgh を入力", "表 1・1 行目、利用者の操作", "\"abcde\"",
            await ReproAsync((view, vm) => TypeAsync(Box(view), "abcdefgh"), (view, vm) => Quote(Box(view).Text))));
        checks.Add(new("再現版、「上限を超えたデータを読み込む」の顧客コード", "表 1・1 行目、バインドした値", "\"abcdefgh\"",
            await ReproAsync((view, vm) => RunAsync(view, vm.LoadOverLimitCommand), (view, vm) => Quote(Box(view).Text))));
        checks.Add(new("再現版、「上限を超えたデータを読み込む」の音量", "表 1・7 行目、バインドした値", "150: Slider 100、ソース 150",
            await ReproAsync((view, vm) => RunAsync(view, vm.LoadOverLimitCommand), (view, vm) => $"150: Slider {D(Slider(view).Value)}、ソース {D(vm.Volume)}")));
        checks.Add(new("再現版、「刻みに合わないデータを読み込む」の音量", "表 1・6 行目、バインドした値", "23.4: 23.4",
            await ReproAsync((view, vm) => RunAsync(view, vm.LoadOffStepCommand), (view, vm) => $"23.4: {D(Slider(view).Value)}")));

        // ---------------------------------------------------------------- 修正版（記事の表 2）
        checks.Add(new("修正版、「上限を超えたデータを読み込む」", "表 2・1 行目、コードからの代入", "\"abcdefgh\": HasErrors True、Validation.HasError True",
            await FixedAsync((view, vm) => RunAsync(view, vm.LoadOverLimitCommand),
                (view, vm) => $"{Quote(vm.Code)}: HasErrors {B(vm.HasErrors)}、Validation.HasError {B(Validation.GetHasError(Box(view)))}")));
        checks.Add(new("修正版、顧客コードに abcdefgh を入力", "表 2・1 行目、利用者の操作", "\"abcdefgh\" を入力: \"abcde\"、HasErrors False",
            await FixedAsync((view, vm) => TypeAsync(Box(view), "abcdefgh"),
                (view, vm) => $"\"abcdefgh\" を入力: {Quote(vm.Code)}、HasErrors {B(vm.HasErrors)}")));
        checks.Add(new("修正版、読み込んだ後に末尾へ x を入力し、Backspace を 1 回", "表 2・2 行目", "読み込み: HasErrors True、末尾に x: \"abcdefgh\"、Backspace: HasErrors True",
            await FixedTextAsync(async (view, vm) =>
            {
                await RunAsync(view, vm.LoadOverLimitCommand);
                string loaded = B(vm.HasErrors);
                TextBox box = Box(view);
                await FocusAsync(box);
                box.CaretIndex = box.Text.Length;
                TypeLetters(box, "x");
                string afterX = Quote(box.Text);
                Press(Key.Back);
                await Settle(view);
                return $"読み込み: HasErrors {loaded}、末尾に x: {afterX}、Backspace: HasErrors {B(vm.HasErrors)}";
            })));
        checks.Add(new("修正版、保存ボタン（ビューモデルで abcdefgh、続けて abcde）", "表 2・3 行目、コードからの代入", "\"abcdefgh\": False、\"abcde\": True",
            await FixedTextAsync(async (view, vm) =>
            {
                vm.Code = "abcdefgh";
                await Settle(view);
                string tooLong = B(Save(view).IsEnabled);
                vm.Code = "abcde";
                await Settle(view);
                return $"\"abcdefgh\": {tooLong}、\"abcde\": {B(Save(view).IsEnabled)}";
            })));
        checks.Add(new("修正版、保存ボタン（読み込んだ後に Backspace を 3 回）", "表 2・3 行目、利用者の操作", "読み込み後: False、Backspace 3 回で \"abcde\": True",
            await FixedTextAsync(async (view, vm) =>
            {
                await RunAsync(view, vm.LoadOverLimitCommand);
                string before = B(Save(view).IsEnabled);
                TextBox box = Box(view);
                await FocusAsync(box);
                box.CaretIndex = box.Text.Length;
                Press(Key.Back);
                Press(Key.Back);
                Press(Key.Back);
                await Settle(view);
                return $"読み込み後: {before}、Backspace 3 回で {Quote(vm.Code)}: {B(Save(view).IsEnabled)}";
            })));
        checks.Add(new("修正版、ビューモデルで音量 150", "表 2・4 行目、コードからの代入", "150: ソース 100、Slider 100",
            await VolumeAsync(150, key: null)));
        checks.Add(new("修正版、ビューモデルで音量 23.4", "表 2・5 行目、コードからの代入", "23.4: ソース 20、Slider 20",
            await VolumeAsync(23.4, key: null)));
        checks.Add(new("修正版、音量 50 から End キー", "表 2・4 行目、利用者の操作（記事は送った値 200 も記録。サンプルのビューモデルには記録用のコードが無いため比べない）", "ソース 100、Slider 100",
            await VolumeAsync(50, Key.End)));
        checks.Add(new("修正版、音量 50 から右矢印キー", "表 2・5 行目、利用者の操作（送った値 55 は比べない）", "ソース 60、Slider 60",
            await VolumeAsync(50, Key.Right)));

        BookAReport.WriteAndAssert(
            "ch02.md", Slug, "第 2 章",
            "WPF の MaxLength などの入力制限が、コードやバインドから入る値に効かない理由",
            "wpf-input-limits-not-applied-to-code",
            "「表 1」「表 2」は記事の 1 つ目（経路ごとの制限）と 2 つ目（ビューモデルでの検証と丸め）の表。記事の「コードからの代入」は、サンプルではビューモデルのコマンドかプロパティで値を入れることに当たる。",
            checks);
    }

    // ---------------------------------------------------------------- 表示と操作

    private static async Task<string> ReproAsync(Func<ReproView, ReproCustomerViewModel, Task> act, Func<ReproView, ReproCustomerViewModel, string> read)
    {
        var vm = new ReproCustomerViewModel();
        var view = new ReproView { DataContext = vm, Width = 460 };
        string result = "";
        await ShowAsync(view, async () =>
        {
            await act(view, vm);
            await Settle(view);
            result = read(view, vm);
        }, activate: true);
        return result;
    }

    private static Task<string> FixedAsync(Func<FixedView, CustomerViewModel, Task> act, Func<FixedView, CustomerViewModel, string> read) =>
        FixedTextAsync(async (view, vm) =>
        {
            await act(view, vm);
            await Settle(view);
            return read(view, vm);
        });

    private static async Task<string> FixedTextAsync(Func<FixedView, CustomerViewModel, Task<string>> run)
    {
        var vm = new CustomerViewModel();
        var view = new FixedView { DataContext = vm, Width = 460 };
        string result = "";
        await ShowAsync(view, async () => result = await run(view, vm), activate: true);
        return result;
    }

    /// <summary>
    /// 修正版の音量。ビューモデルの規則だけを確かめるため、記事の計測と同じく Slider を 0〜200・刻みなし・SmallChange 5 にする
    /// （サンプルの Slider は 0〜100・10 刻みで、Slider 自身が値を丸めてしまうため）。
    /// </summary>
    private static Task<string> VolumeAsync(double value, Key? key) =>
        FixedTextAsync(async (view, vm) =>
        {
            Slider slider = Slider(view);
            slider.Maximum = 200;
            slider.IsSnapToTickEnabled = false;
            slider.SmallChange = 5;
            vm.Volume = value;
            await Settle(view);
            if (key is null)
            {
                return $"{D(value)}: ソース {D(vm.Volume)}、Slider {D(slider.Value)}";
            }

            await FocusAsync(slider);
            Press(key.Value);
            await Settle(view);
            return $"ソース {D(vm.Volume)}、Slider {D(slider.Value)}";
        });

    private static async Task TypeAsync(TextBox box, string letters)
    {
        await FocusAsync(box);
        TypeLetters(box, letters);
    }

    /// <summary>ボタンを押す代わりに、ビューにバインドされたコマンドを実行する（ビューモデルからの読み込みの経路）。</summary>
    private static Task RunAsync(FrameworkElement view, System.Windows.Input.ICommand command)
    {
        command.Execute(null);
        return Settle(view);
    }

    /// <summary>フォーカスのある要素へ、キーの押下と離しを InputManager を通して送る（記事の計測と同じ）。</summary>
    private static void Press(Key key)
    {
        SendKey(key);
        SendKey(key, down: false);
    }

    private static Task Settle(FrameworkElement view) => Capture.SettleAsync(Window.GetWindow(view)!);

    private static TextBox Box(FrameworkElement view) => (TextBox)view.FindName("codeBox");

    private static Slider Slider(FrameworkElement view) => (Slider)view.FindName("volumeSlider");

    private static Button Save(FrameworkElement view) => (Button)view.FindName("saveButton");

    private static string Quote(string value) => $"\"{value}\"";

    private static string B(bool value) => WpfProbe.Describe(value);
}
