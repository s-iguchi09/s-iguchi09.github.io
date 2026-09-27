using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の MaxLength などの入力制限が、コードやバインドから入る値に効かない理由」の検証。
///
/// 入力を制限するプロパティ（TextBox の MaxLength・CharacterCasing、PasswordBox の MaxLength、
/// DatePicker の DisplayDateStart / DisplayDateEnd、Slider の IsSnapToTickEnabled、TabItem の IsEnabled）を、
/// 利用者の操作・コードからの代入・バインドした値の 3 つの経路で試し、どの経路で効くかを表にする。
/// 利用者の操作は、WPF の入力管理（InputManager / TextCompositionManager）かキーのイベント、
/// または支援技術が使う UI オートメーションで送る。OS の前面になくても送れる経路なので、画面の状態に左右されない。
///
/// あわせて、記事の対処（ビューモデル側で検証する・値を丸める）が、コードからの値とバインドの両方に効くことを確かめる。
/// </summary>
internal sealed class InputLimitsFromCodeScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "TextBox の MaxLength は利用者の入力だけを切り詰め、コードからの Text とバインドした値は切り詰めないこと",
        "TextBox の CharacterCasing は利用者の入力だけを変換し、コードからの Text とバインドした値は変換しないこと",
        "PasswordBox の MaxLength は利用者の入力だけを切り詰め、コードからの Password は切り詰めないこと。PasswordBox には PasswordProperty が無く、Password をバインドできないこと",
        "DatePicker の DisplayDateStart / DisplayDateEnd はカレンダーの範囲外の日付ボタンを無効にするが、テキスト欄への入力・コード・バインドからの範囲外の日付は受け付けること。受け付けた後は 3 つの経路とも DisplayDateStart がその日付まで下がり、コードから設定した後はカレンダーの範囲外の日付ボタンが有効になること",
        "DisplayDateStart を既定（TwoWay）でバインドしても、範囲外の日付で下がった値はソースへ書き戻されず、バインドは残ってソースの変更に追従すること。OneWay でも同じであること。DisplayDateStart と DisplayDateEnd を読み取り専用のプロパティへ既定でバインドすると、Source を指定した場合は SetBinding で、DataContext 経由では表示中の DatePicker に DataContext を設定した時点で、表示前に DataContext を設定した場合は（DatePicker 自身に設定しても、親の要素に設定して継承させても）ウィンドウを表示する処理の中で、読み取り専用のプロパティを理由とする InvalidOperationException になること",
        "Slider の IsSnapToTickEnabled は矢印キーの操作だけを目盛りに合わせ、コードとバインドからの値は合わせないこと",
        "Slider の Maximum は Value を補正し（Maximum を広げると設定した値に戻る）、コードとバインドからの値を画面上で範囲に収めるが、補正した値はバインドしたソースへ書き戻されないこと。End キーでは上限に止まること",
        "IsEnabled=False の TabItem は UI オートメーションの Select では選べないが、コードの SelectedIndex とバインドした SelectedIndex では選ばれ、その内容が SelectedContent になること",
        "INotifyDataErrorInfo で長さを検証するビューモデルは、コードからの長すぎる値に検証エラーを出し、TextBox にも Validation.HasError が立つこと。入力では MaxLength が先に切り詰めるため、エラーにならないこと。上限を超えた値を読み込んだ後は、末尾に文字を打っても入らず、1 文字消してもエラーが残ること",
        "保存ボタンの IsEnabled を HasErrors の反転にバインドすると、コードからの値でも Backspace での編集でも HasErrors の変化に追従すること",
        "setter で範囲と刻みに丸めるビューモデルは、コードからの値も、目盛り合わせの無い Slider がキー操作で送った値も丸め、Slider とソースの値が一致すること",
    ];

    public string Slug => "wpf-input-limits-not-applied-to-code";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "input restrictions: which path they apply to",
            ["restriction", "user input", "value from code", "value from a binding"],
            await PathsAsync(),
            "input-limits-by-path.svg");

        await context.SaveTableAsync(
            "checking in the view model: both paths",
            ["view model", "value from code", "user input"],
            await ViewModelAsync(),
            "input-limits-viewmodel.svg");

        await context.SaveTableAsync(
            "DisplayDateStart / DisplayDateEnd bound to the view model",
            ["binding", "result"],
            await DisplayDateBindingAsync(),
            "input-limits-displaydate-binding.svg");
    }

    private static async Task<List<IReadOnlyList<string>>> PathsAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // TextBox.MaxLength
        {
            string typed = await TypedTextAsync(new TextBox { MaxLength = 5 }, "abcdefgh");
            string code = await ShownAsync(new TextBox { MaxLength = 5 }, b => b.Text = "abcdefgh", b => Quote(b.Text));
            var source = new Holder<string> { Value = "" };
            var bound = new TextBox { MaxLength = 5 };
            bound.SetBinding(TextBox.TextProperty, new Binding(nameof(Holder<string>.Value)) { Source = source });
            string viaBinding = await ShownAsync(bound, _ => source.Value = "abcdefgh", b => Quote(b.Text));
            rows.Add(["TextBox MaxLength=5, \"abcdefgh\"", Quote(typed), code, viaBinding]);
        }

        // TextBox.CharacterCasing
        {
            string typed = await TypedTextAsync(new TextBox { CharacterCasing = CharacterCasing.Upper }, "hello");
            string code = await ShownAsync(new TextBox { CharacterCasing = CharacterCasing.Upper }, b => b.Text = "hello", b => Quote(b.Text));
            var source = new Holder<string> { Value = "" };
            var bound = new TextBox { CharacterCasing = CharacterCasing.Upper };
            bound.SetBinding(TextBox.TextProperty, new Binding(nameof(Holder<string>.Value)) { Source = source });
            string viaBinding = await ShownAsync(bound, _ => source.Value = "hello", b => Quote(b.Text));
            rows.Add(["TextBox CharacterCasing=Upper, \"hello\"", Quote(typed), code, viaBinding]);
        }

        // PasswordBox.MaxLength。Password には依存関係プロパティの識別子が無いため、バインドの対象にできない。
        {
            var typedBox = new PasswordBox { MaxLength = 8 };
            string typed = "";
            await ShowAsync(typedBox, async () =>
            {
                await FocusAsync(typedBox);
                TypeLetters(typedBox, "abcdefghij");
                typed = $"{typedBox.Password.Length} characters";
            });
            string code = await ShownAsync(new PasswordBox { MaxLength = 8 }, b => b.Password = "abcdefghij", b => $"{b.Password.Length} characters");
            bool hasProperty = typeof(PasswordBox).GetField("PasswordProperty", BindingFlags.Public | BindingFlags.Static) is not null;
            string viaBinding = hasProperty ? "PasswordProperty exists" : "no PasswordProperty to bind";
            rows.Add(["PasswordBox MaxLength=8, 10 letters", typed, code, viaBinding]);
        }

        // DatePicker.DisplayDateStart / DisplayDateEnd（2026-04-10 〜 04-20）に対して 2026-04-05
        {
            var outside = new DateTime(2026, 4, 5);

            DatePicker calendarPicker = RangedPicker();
            string calendar = "";
            await ShowAsync(calendarPicker, async () =>
            {
                calendarPicker.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(calendarPicker)!);
                var popup = (Popup)calendarPicker.Template.FindName("PART_Popup", calendarPicker);
                var button = Descendants((System.Windows.Controls.Calendar)popup.Child).OfType<CalendarDayButton>()
                    .First(b => b.DataContext is DateTime d && d.Date == outside);
                calendar = button.IsEnabled ? "calendar: day enabled" : "calendar: day disabled";
                calendarPicker.IsDropDownOpen = false;
            });

            DatePicker typedPicker = RangedPicker();
            string typed = "";
            await ShowAsync(typedPicker, async () =>
            {
                var box = (DatePickerTextBox)typedPicker.Template.FindName("PART_TextBox", typedPicker);
                await FocusAsync(box);
                TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, "4/5/2026"));
                if (box.Text != "4/5/2026")
                {
                    throw new InvalidOperationException($"入力が DatePicker のテキスト欄に届いていない（Text = \"{box.Text}\"）。");
                }

                Press(Key.Enter);
                await Capture.SettleAsync(Window.GetWindow(typedPicker)!);
                typed = $"typed: {Date(typedPicker.SelectedDate)}";
            });

            DatePicker fromCode = RangedPicker();
            string code = "";
            string loadedCalendar = "";
            await ShowAsync(fromCode, async () =>
            {
                fromCode.SelectedDate = outside;
                await Capture.SettleAsync(Window.GetWindow(fromCode)!);
                code = Date(fromCode.SelectedDate);
                fromCode.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(fromCode)!);
                var popup = (Popup)fromCode.Template.FindName("PART_Popup", fromCode);
                var button = Descendants((System.Windows.Controls.Calendar)popup.Child).OfType<CalendarDayButton>()
                    .First(b => b.DataContext is DateTime d && d.Date == new DateTime(2026, 4, 7));
                loadedCalendar = button.IsEnabled ? "04-07 day enabled" : "04-07 day disabled";
                fromCode.IsDropDownOpen = false;
            });
            var source = new Holder<DateTime?> { Value = null };
            DatePicker bound = RangedPicker();
            bound.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(Holder<DateTime?>.Value)) { Source = source });
            string viaBinding = await ShownAsync(bound, _ => source.Value = outside, p => Date(p.SelectedDate));
            rows.Add(["DatePicker 04-10 to 04-20, 04-05", $"{calendar}, {typed}", code, viaBinding]);

            // 範囲外の日付が入った後の DisplayDateStart（設定したのは 04-10）
            rows.Add(["DatePicker, DisplayDateStart afterwards",
                $"after typing: {Date(typedPicker.DisplayDateStart)}",
                $"{Date(fromCode.DisplayDateStart)}, {loadedCalendar}",
                Date(bound.DisplayDateStart)]);
        }

        // Slider.IsSnapToTickEnabled（TickFrequency=10）
        {
            Slider keyed = SnappingSlider();
            keyed.Value = 50;
            string typed = "";
            await ShowAsync(keyed, async () =>
            {
                await FocusAsync(keyed);
                Press(Key.Right);
                typed = $"50, Right arrow: {D(keyed.Value)}";
            });
            string code = await ShownAsync(SnappingSlider(), s => s.Value = 23.4, s => $"23.4: {D(s.Value)}");
            var source = new Holder<double> { Value = 0 };
            Slider bound = SnappingSlider();
            bound.SetBinding(RangeBase.ValueProperty, new Binding(nameof(Holder<double>.Value)) { Source = source });
            string viaBinding = await ShownAsync(bound, _ => source.Value = 23.4, s => $"23.4: {D(s.Value)}");
            rows.Add(["Slider snap to ticks of 10", typed, code, viaBinding]);
        }

        // Slider.Maximum。値そのものを範囲に収める補正（coerce）なので、Maximum を広げると設定した値に戻る。
        // 補正はコードとバインドにも効くが、補正した値はソースへ書き戻されない。
        {
            Slider keyed = new() { Minimum = 0, Maximum = 100, Value = 50 };
            string typed = "";
            await ShowAsync(keyed, async () =>
            {
                await FocusAsync(keyed);
                Press(Key.End);
                typed = $"End key: {D(keyed.Value)}";
            });
            var fromCode = new Slider { Minimum = 0, Maximum = 100 };
            string code = "";
            await ShowAsync(fromCode, async () =>
            {
                fromCode.Value = 150;
                await Capture.SettleAsync(Window.GetWindow(fromCode)!);
                string clamped = D(fromCode.Value);
                fromCode.Maximum = 200;
                code = $"150: {clamped}, Maximum 200: {D(fromCode.Value)}";
            });
            var source = new Holder<double> { Value = 0 };
            var bound = new Slider { Minimum = 0, Maximum = 100 };
            bound.SetBinding(RangeBase.ValueProperty, new Binding(nameof(Holder<double>.Value)) { Source = source, Mode = BindingMode.TwoWay });
            string viaBinding = await ShownAsync(bound, _ => source.Value = 150, s => $"150: Slider {D(s.Value)}, source {D(source.Value)}");
            rows.Add(["Slider Maximum=100", typed, code, viaBinding]);
        }

        // TabItem.IsEnabled=False
        {
            TabControl uiaTabs = Tabs();
            string uia = "";
            await ShowAsync(uiaTabs, async () =>
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(uiaTabs)
                    .GetChildren().OfType<TabItemAutomationPeer>().ElementAt(1);
                string thrown = Throws(() => ((ISelectionItemProvider)peer).Select());
                await Capture.SettleAsync(Window.GetWindow(uiaTabs)!);
                uia = $"UIA Select(): {thrown}, index {uiaTabs.SelectedIndex}";
            });

            string code = await ShownAsync(Tabs(), t => t.SelectedIndex = 1, TabState);
            TabControl boundTabs = Tabs();
            var source = new Holder<int> { Value = 0 };
            boundTabs.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(Holder<int>.Value)) { Source = source });
            string viaBinding = await ShownAsync(boundTabs, _ => source.Value = 1, TabState);
            rows.Add(["2nd TabItem IsEnabled=False", uia, code, viaBinding]);
        }

        return rows;
    }

    /// <summary>選ばれているタブの番号と、表示している内容（SelectedContent）。</summary>
    private static string TabState(TabControl tabs) => $"index {tabs.SelectedIndex}, \"{tabs.SelectedContent}\"";

    /// <summary>コントロールを表示してから値を設定し、レイアウトが落ち着いた後に読んだ結果を返す。</summary>
    private static async Task<string> ShownAsync<T>(T control, Action<T> set, Func<T, string> read) where T : FrameworkElement
    {
        string result = "";
        await ShowAsync(control, async () =>
        {
            set(control);
            await Capture.SettleAsync(Window.GetWindow(control)!);
            result = read(control);
        });
        return result;
    }

    private static async Task<List<IReadOnlyList<string>>> ViewModelAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        // 長さを INotifyDataErrorInfo で検証するビューモデル。TextBox の MaxLength は入力の補助として残す。
        {
            var fromCode = new NameViewModel();
            TextBox codeBox = BoundNameBox(fromCode);
            string code = "";
            await ShowAsync(codeBox, async () =>
            {
                fromCode.Name = "abcdefgh";
                await Capture.SettleAsync(Window.GetWindow(codeBox)!);
                code = $"\"abcdefgh\": HasErrors {WpfProbe.Describe(fromCode.HasErrors)}, Validation.HasError {WpfProbe.Describe(Validation.GetHasError(codeBox))}";
            });

            var typedVm = new NameViewModel();
            await TypedTextAsync(BoundNameBox(typedVm), "abcdefgh");
            rows.Add(["INotifyDataErrorInfo, at most 5", code,
                $"typed \"abcdefgh\": {Quote(typedVm.Name)}, HasErrors {WpfProbe.Describe(typedVm.HasErrors)}"]);
        }

        // 上限を超えた値を読み込んだ後に、利用者が編集する。キャレットは末尾で、選択範囲は無い。
        {
            var loaded = new NameViewModel();
            TextBox box = BoundNameBox(loaded);
            string code = "";
            string edited = "";
            await ShowAsync(box, async () =>
            {
                loaded.Name = "abcdefgh";
                await Capture.SettleAsync(Window.GetWindow(box)!);
                code = $"\"abcdefgh\" loaded: HasErrors {WpfProbe.Describe(loaded.HasErrors)}";
                await FocusAsync(box);
                box.CaretIndex = box.Text.Length;
                TypeLetters(box, "x");
                string afterX = box.Text;
                Press(Key.Back);
                await Capture.SettleAsync(Window.GetWindow(box)!);
                edited = $"x at the end: {Quote(afterX)}; Backspace: HasErrors {WpfProbe.Describe(loaded.HasErrors)}";
            });
            rows.Add(["INotifyDataErrorInfo, loaded over the limit", code, edited]);
        }

        // 保存ボタンの IsEnabled を HasErrors の反転にバインドし、PropertyChanged(HasErrors) で追従するかを見る。
        {
            var codeVm = new NameViewModel();
            Button codeSave = SaveButton(codeVm);
            string code = "";
            await ShowAsync(codeSave, async () =>
            {
                codeVm.Name = "abcdefgh";
                await Capture.SettleAsync(Window.GetWindow(codeSave)!);
                string tooLong = WpfProbe.Describe(codeSave.IsEnabled);
                codeVm.Name = "abcde";
                await Capture.SettleAsync(Window.GetWindow(codeSave)!);
                code = $"\"abcdefgh\": {tooLong}; \"abcde\": {WpfProbe.Describe(codeSave.IsEnabled)}";
            });

            var editedVm = new NameViewModel();
            TextBox box = BoundNameBox(editedVm);
            Button editedSave = SaveButton(editedVm);
            var panel = new StackPanel { Children = { box, editedSave } };
            string edited = "";
            await ShowAsync(panel, async () =>
            {
                editedVm.Name = "abcdefgh";
                await Capture.SettleAsync(Window.GetWindow(panel)!);
                string before = WpfProbe.Describe(editedSave.IsEnabled);
                await FocusAsync(box);
                box.CaretIndex = box.Text.Length;
                Press(Key.Back);
                Press(Key.Back);
                Press(Key.Back);
                await Capture.SettleAsync(Window.GetWindow(panel)!);
                edited = $"loaded: {before}; 3 Backspaces to {Quote(editedVm.Name)}: {WpfProbe.Describe(editedSave.IsEnabled)}";
            });
            rows.Add(["Save button, IsEnabled bound to !HasErrors", code, edited]);
        }

        // 範囲と刻みを setter で丸めるビューモデル。Slider には TwoWay でバインドする。
        // ビューモデルの規則だけを確かめるため、Slider は 0〜200・目盛り合わせ無し・SmallChange 5 にし、
        // Slider 自身の制限では値が丸まらないようにする。Slider が送った値は LastRequested に記録する。
        rows.Add(["setter clamps to 0-100",
            await VolumeFromCodeAsync(150),
            await VolumeFromKeyAsync(50, Key.End, "End key")]);
        rows.Add(["setter rounds to 10",
            await VolumeFromCodeAsync(23.4),
            await VolumeFromKeyAsync(50, Key.Right, "50, Right arrow")]);

        return rows;
    }

    /// <summary>
    /// DisplayDateStart をビューモデルにバインドした場合。範囲外の SelectedDate をコードから入れた後の、
    /// DisplayDateStart・ソース・バインドの状態を読む。既定（TwoWay）・OneWay・読み取り専用プロパティへの既定のバインドを比べる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> DisplayDateBindingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        var outside = new DateTime(2026, 4, 5);

        foreach (BindingMode? mode in new BindingMode?[] { null, BindingMode.OneWay })
        {
            var period = new Holder<DateTime> { Value = new DateTime(2026, 4, 10) };
            var picker = new DatePicker { Width = 200, Language = XmlLanguage.GetLanguage("en-US"), DisplayDate = new DateTime(2026, 4, 15) };
            var binding = new Binding(nameof(Holder<DateTime>.Value)) { Source = period };
            if (mode is BindingMode m)
            {
                binding.Mode = m;
            }

            picker.SetBinding(DatePicker.DisplayDateStartProperty, binding);
            string result = "";
            await ShowAsync(picker, async () =>
            {
                picker.SelectedDate = outside;
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                bool attached = BindingOperations.GetBindingExpression(picker, DatePicker.DisplayDateStartProperty) is not null;
                result = $"DisplayDateStart {Date(picker.DisplayDateStart)}, source {Date(period.Value)}, binding {(attached ? "kept" : "removed")}";
                picker.SelectedDate = null;
                period.Value = new DateTime(2026, 4, 12);
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                result += $"; source set to 04-12: {Date(picker.DisplayDateStart)}";
            });
            rows.Add([mode is null ? "DisplayDateStart, default (TwoWay)" : "DisplayDateStart, Mode=OneWay", result]);
        }

        // 読み取り専用のプロパティへの既定のバインド。Source を指定する場合と、記事の XAML と同じ DataContext 経由の場合。
        foreach ((string name, DependencyProperty property, string path) in new[]
        {
            ("DisplayDateStart", DatePicker.DisplayDateStartProperty, nameof(ReadOnlyPeriod.First)),
            ("DisplayDateEnd", DatePicker.DisplayDateEndProperty, nameof(ReadOnlyPeriod.Last)),
        })
        {
            var withSource = new DatePicker();
            string bySource = ThrowsReadOnly(() => withSource.SetBinding(property, new Binding(path) { Source = new ReadOnlyPeriod() }));

            // 記事の XAML と同じく DataContext 経由でバインドし、ウィンドウに表示した状態で DataContext を設定する。
            var viaContext = new DatePicker { Width = 200 };
            viaContext.SetBinding(property, new Binding(path));
            string byContext = "";
            await ShowAsync(viaContext, async () =>
            {
                byContext = ThrowsReadOnly(() => viaContext.DataContext = new ReadOnlyPeriod());
                await Capture.SettleAsync(Window.GetWindow(viaContext)!);
            });
            // 表示する前に DataContext を設定し、その後に表示する（InitializeComponent の後に DataContext を設定してから Show する形）。
            var beforeShow = new DatePicker { Width = 200 };
            beforeShow.SetBinding(property, new Binding(path));
            string atContext = ThrowsReadOnly(() => beforeShow.DataContext = new ReadOnlyPeriod());
            string atShow = await ShowThrowsReadOnlyAsync(beforeShow);

            rows.Add([$"{name}, get-only, Source set", $"SetBinding: {bySource}"]);
            rows.Add([$"{name}, get-only, DataContext set while shown", byContext]);
            rows.Add([$"{name}, get-only, DataContext set before showing", $"at DataContext: {atContext}; at Show: {atShow}"]);

            // 実務で多い形。DatePicker を置いた親の要素に表示前に DataContext を設定し、継承させてから表示する。
            var inherited = new DatePicker { Width = 200 };
            inherited.SetBinding(property, new Binding(path));
            var host = new StackPanel { Children = { inherited } };
            string atParent = ThrowsReadOnly(() => host.DataContext = new ReadOnlyPeriod());
            string atParentShow = await ShowThrowsReadOnlyAsync(host);

            rows.Add([$"{name}, get-only, parent's DataContext before showing", $"at DataContext: {atParent}; at Show: {atParentShow}"]);
        }

        return rows;
    }

    /// <summary>例外の型と、メッセージが読み取り専用のプロパティを理由に挙げているか（CannotWriteToReadOnly）。</summary>
    private static string ThrowsReadOnly(Action action)
    {
        try
        {
            action();
            return "no exception";
        }
        catch (Exception ex)
        {
            return DescribeReadOnly(ex);
        }
    }

    /// <summary>表示し、表示の処理の中で出た例外を ThrowsReadOnly と同じ形で返す。</summary>
    private static async Task<string> ShowThrowsReadOnlyAsync(FrameworkElement content)
    {
        try
        {
            await ShowAsync(content, async () => await Capture.SettleAsync(Window.GetWindow(content)!));
            return "no exception";
        }
        catch (Exception ex)
        {
            return DescribeReadOnly(ex);
        }
    }

    /// <summary>
    /// 例外の型と、メッセージが読み取り専用のプロパティを理由に挙げているか（CannotWriteToReadOnly）。
    /// メッセージは OS の表示言語でローカライズされるため、英語と日本語の両方の文言で判定する。
    /// どちらにも当たらなければ、判別できるようメッセージをそのまま出す。
    /// </summary>
    private static string DescribeReadOnly(Exception ex) =>
        ex.Message.Contains("read-only property", StringComparison.Ordinal) || ex.Message.Contains("読み取り専用プロパティ", StringComparison.Ordinal)
            ? $"{ex.GetType().Name} (read-only property)"
            : $"{ex.GetType().Name}: {ex.Message}";

    private static Button SaveButton(NameViewModel vm)
    {
        var button = new Button { Content = "Save" };
        button.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(NameViewModel.HasErrors)) { Source = vm, Converter = new InvertBooleanConverter() });
        return button;
    }

    private static TextBox BoundNameBox(NameViewModel vm)
    {
        var box = new TextBox { MaxLength = 5 };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(NameViewModel.Name)) { Source = vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return box;
    }

    private static async Task<string> VolumeFromCodeAsync(double value)
    {
        var vm = new VolumeViewModel();
        Slider slider = BoundVolumeSlider(vm);
        string result = "";
        await ShowAsync(slider, async () =>
        {
            vm.Volume = value;
            await Capture.SettleAsync(Window.GetWindow(slider)!);
            result = $"{D(value)}: source {D(vm.Volume)}, Slider {D(slider.Value)}";
        });
        return result;
    }

    private static async Task<string> VolumeFromKeyAsync(double start, Key key, string label)
    {
        var vm = new VolumeViewModel { Volume = start };
        Slider slider = BoundVolumeSlider(vm);
        string result = "";
        await ShowAsync(slider, async () =>
        {
            await FocusAsync(slider);
            Press(key);
            await Capture.SettleAsync(Window.GetWindow(slider)!);
            result = $"{label}: sent {D(vm.LastRequested)}, source {D(vm.Volume)}, Slider {D(slider.Value)}";
        });
        return result;
    }

    /// <summary>ビューモデルの規則だけが値を丸めるよう、広い範囲で目盛り合わせをしない Slider。</summary>
    private static Slider BoundVolumeSlider(VolumeViewModel vm)
    {
        var slider = new Slider { Width = 200, Minimum = 0, Maximum = 200, SmallChange = 5 };
        slider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(VolumeViewModel.Volume)) { Source = vm, Mode = BindingMode.TwoWay });
        return slider;
    }

    /// <summary>TextBox を表示してフォーカスを移し、英小文字を 1 文字ずつ入力した後の Text を返す。</summary>
    private static async Task<string> TypedTextAsync(TextBox box, string letters)
    {
        string text = "";
        await ShowAsync(box, async () =>
        {
            await FocusAsync(box);
            TypeLetters(box, letters);
            await Capture.SettleAsync(Window.GetWindow(box)!);
            text = box.Text;
        });
        return text;
    }

    /// <summary>フォーカスのある要素へ、キーの押下と離しを InputManager を通して送る。</summary>
    private static void Press(Key key)
    {
        SendKey(key);
        SendKey(key, down: false);
    }

    private static DatePicker RangedPicker() => new()
    {
        Width = 200,
        Language = XmlLanguage.GetLanguage("en-US"),
        DisplayDateStart = new DateTime(2026, 4, 10),
        DisplayDateEnd = new DateTime(2026, 4, 20),
        DisplayDate = new DateTime(2026, 4, 15),
    };

    private static Slider SnappingSlider() => new()
    {
        Width = 200,
        Minimum = 0,
        Maximum = 100,
        SmallChange = 1,
        IsSnapToTickEnabled = true,
        TickFrequency = 10,
    };

    private static TabControl Tabs()
    {
        var tabs = new TabControl { Width = 240, Height = 100 };
        for (int i = 0; i < 3; i++)
        {
            tabs.Items.Add(new TabItem { Header = $"Tab{i + 1}", Content = $"Page {i + 1}", IsEnabled = i != 1 });
        }

        return tabs;
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string Date(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "null";

    /// <summary>バインドのソースにする、値を 1 つだけ持つ通知付きのクラス。</summary>
    private sealed class Holder<T> : INotifyPropertyChanged
    {
        private T _value = default!;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>記事の実装例と同じ、名前の長さを INotifyDataErrorInfo で検証するビューモデル。</summary>
    private sealed class NameViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private const int MaxNameLength = 5;
        private string _name = "";
        private readonly List<string> _nameErrors = [];

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
                _nameErrors.Clear();
                if (value.Length > MaxNameLength)
                {
                    _nameErrors.Add($"Name must be at most {MaxNameLength} characters.");
                }

                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name)));
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        public bool HasErrors => _nameErrors.Count > 0;

        public IEnumerable GetErrors(string? propertyName) =>
            propertyName == nameof(Name) ? _nameErrors : Array.Empty<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>記事の実装例と同じ、setter で 0〜100 に収めて 10 刻みに丸めるビューモデル。</summary>
    private sealed class VolumeViewModel : INotifyPropertyChanged
    {
        private double _volume;

        /// <summary>計測用。setter が丸める前に受け取った値（記事の実装例には無い）。</summary>
        public double LastRequested { get; private set; } = double.NaN;

        public double Volume
        {
            get => _volume;
            set
            {
                LastRequested = value;
                if (double.IsNaN(value))
                {
                    return;
                }

                double clamped = Math.Clamp(value, 0, 100);
                _volume = Math.Round(clamped / 10, MidpointRounding.AwayFromZero) * 10;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>受付期間を読み取り専用で持つビューモデル。</summary>
    private sealed class ReadOnlyPeriod
    {
        public DateTime First { get; } = new(2026, 4, 10);

        public DateTime Last { get; } = new(2026, 4, 20);
    }

    /// <summary>bool を反転する。保存ボタンの IsEnabled を HasErrors から決めるために使う。</summary>
    private sealed class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    }
}
