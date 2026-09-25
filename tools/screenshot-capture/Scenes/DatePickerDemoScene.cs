using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using static ScreenshotCapture.Scenes.DemoProbe;
using Calendar = System.Windows.Controls.Calendar;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「DatePicker」（apps/wpf-standard-control-demo/datepicker.md と日本語版）の記述を実測する。
///
/// 表示書式とカルチャの関係は記事 wpf-datepicker-custom-format（DatePickerFormatScene）で実測済みのため、
/// ここでは範囲・入力・バインド・カレンダーの表示など、デモページだけが述べている挙動を確かめる。
/// 日付の解析と表示はカルチャに左右されるため、DatePicker には xml:lang="en-US" を与えて固定する。
/// </summary>
internal sealed class DatePickerDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-datepicker";

    public string ImageDirectory => DemoProbe.ImageDirectory("datepicker");

    public IReadOnlyList<string> Verifies =>
    [
        "SelectedDate・Text・IsDropDownOpen の依存関係プロパティが既定で TwoWay か、DisplayDate・FirstDayOfWeek の既定値（スレッドのカルチャを変えて比べる）",
        "IsTodayHighlighted のメタデータの既定値と、既定のスタイルが適用された後の値とその出どころ（Calendar と比べる）",
        "SelectedDate を設定したときの DisplayDate と、DisplayDate を設定したときの SelectedDate",
        "DisplayDateStart / DisplayDateEnd の範囲外の日付を、コード・Text プロパティ・テキスト欄への入力と Enter で与えたときの結果と、カレンダーの日付ボタンの状態",
        "BlackoutDates に含まれる日付を SelectedDate に設定したときの結果と、カレンダーの日付ボタンの状態",
        "SelectedDate より後の日付を DisplayDateStart に設定したときの補正",
        "解析できない文字列を入力したときの SelectedDate・Text と DateValidationError イベント、テキスト欄を空にしたときの SelectedDate",
        "Text プロパティに文字列を設定したときに SelectedDate へ反映される書式",
        "null 非許容の DateTime プロパティへ SelectedDate をバインドし、SelectedDate を null にしたときのソースとバインドの状態",
        "表示前に IsDropDownOpen を True にした場合に、表示後にカレンダーが開いているか",
        "IsTodayHighlighted の指定なし・True・False での、ポップアップの Calendar への受け渡しと、今日の日付ボタンの IsToday・IsEnabled・表示状態（DayStates）・今日の印の不透明度",
        "カレンダーの FirstDayOfWeek が DatePicker の値に従うこと",
    ];

    private static readonly DateTime Day = new(2026, 4, 15);

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "DatePicker: metadata and defaults",
            ["item", "value"],
            Defaults(),
            "datepicker-defaults.svg");

        await context.SaveTableAsync(
            "DatePicker (xml:lang=\"en-US\"): range 2026-04-10 to 2026-04-20, blackout dates, input",
            ["case", "result"],
            await RangeAndInputAsync(),
            "datepicker-range-input.svg");

        await context.SaveTableAsync(
            "DatePicker: calendar popup and bindings",
            ["case", "measured"],
            await CalendarAndBindingAsync(),
            "datepicker-calendar-binding.svg");
    }

    private static DatePicker NewPicker()
    {
        var picker = new DatePicker { Width = 200, Language = XmlLanguage.GetLanguage("en-US") };
        return picker;
    }

    private static string Date(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "null";

    private static DatePickerTextBox TextPart(DatePicker picker) =>
        (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);

    private static Calendar CalendarOf(DatePicker picker)
    {
        var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
        return (Calendar)popup.Child;
    }

    private static CalendarDayButton? DayButton(Calendar calendar, DateTime date) =>
        Descendants(calendar).OfType<CalendarDayButton>()
            .FirstOrDefault(b => b.DataContext is DateTime d && d.Date == date.Date);

    /// <summary>
    /// 日付ボタンのテンプレートが今日の強調表示をどう切り替えているかを読む。
    /// 表示状態のグループの現在の状態と、今日の印の要素の不透明度を返す。
    /// </summary>
    private static string TodayMarker(CalendarDayButton button)
    {
        if (System.Windows.Media.VisualTreeHelper.GetChildrenCount(button) == 0)
        {
            return "no template";
        }

        var root = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetChild(button, 0);
        var groups = VisualStateManager.GetVisualStateGroups(root)?.Cast<VisualStateGroup>().ToList() ?? [];
        string states = string.Join(", ", groups.Where(g => g.Name == "DayStates")
            .Select(g => $"{g.Name}={g.CurrentState?.Name ?? "(none)"}"));
        var marker = button.Template.FindName("TodayBackground", button) as UIElement;
        return $"{states}, TodayBackground opacity {(marker is null ? "n/a" : D(marker.Opacity))}";
    }

    private static List<IReadOnlyList<string>> Defaults()
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach ((string name, DependencyProperty property) in new[]
        {
            ("SelectedDate", DatePicker.SelectedDateProperty),
            ("Text", DatePicker.TextProperty),
            ("IsDropDownOpen", DatePicker.IsDropDownOpenProperty),
        })
        {
            var metadata = (FrameworkPropertyMetadata)property.GetMetadata(typeof(DatePicker));
            rows.Add([$"{name}: BindsTwoWayByDefault", metadata.BindsTwoWayByDefault.ToString()]);
        }

        rows.Add(["base class of the text part (DatePickerTextBox)", typeof(DatePickerTextBox).BaseType!.Name]);

        var picker = new DatePicker();
        rows.Add(["DisplayDate of a new DatePicker (today is " + Date(DateTime.Today) + ")", Date(picker.DisplayDate)]);
        rows.Add(["IsTodayHighlighted of a new DatePicker / metadata default",
            $"{picker.IsTodayHighlighted} / {DatePicker.IsTodayHighlightedProperty.GetMetadata(typeof(DatePicker)).DefaultValue}"]);
        rows.Add(["IsTodayHighlighted of a new Calendar / metadata default",
            $"{new Calendar().IsTodayHighlighted} / {Calendar.IsTodayHighlightedProperty.GetMetadata(typeof(Calendar)).DefaultValue}"]);

        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "ja-JP", "de-DE", "fr-FR" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                var created = new DatePicker();
                rows.Add([$"FirstDayOfWeek of a new DatePicker, CurrentCulture {culture}",
                    $"{created.FirstDayOfWeek} (culture's own: {CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek})"]);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }

        return rows;
    }

    private static async Task<List<IReadOnlyList<string>>> RangeAndInputAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        var start = new DateTime(2026, 4, 10);
        var end = new DateTime(2026, 4, 20);

        DatePicker Ranged()
        {
            DatePicker picker = NewPicker();
            picker.DisplayDateStart = start;
            picker.DisplayDateEnd = end;
            picker.SelectedDate = Day;
            return picker;
        }

        {
            DatePicker picker = NewPicker();
            picker.SelectedDate = Day;
            string first = Date(picker.DisplayDate);
            picker.DisplayDate = new DateTime(2026, 7, 1);
            rows.Add(["SelectedDate = 04-15: DisplayDate / then DisplayDate = 07-01: SelectedDate",
                $"{first} / {Date(picker.SelectedDate)}"]);
        }

        {
            DatePicker picker = Ranged();
            string thrown = Throws(() => picker.SelectedDate = new DateTime(2026, 4, 5));
            rows.Add(["out of range from code: SelectedDate = 04-05", $"{thrown}; SelectedDate {Date(picker.SelectedDate)}"]);
        }

        {
            DatePicker picker = Ranged();
            await ShowAsync(picker, async () =>
            {
                string thrown = Throws(() => picker.Text = "4/5/2026");
                rows.Add(["out of range through Text: Text = \"4/5/2026\"",
                    $"{thrown}; SelectedDate {Date(picker.SelectedDate)}, Text \"{picker.Text}\""]);
                await Task.CompletedTask;
            });
        }

        foreach ((string label, string typed) in new[]
        {
            ("out of range typed: \"4/5/2026\" + Enter", "4/5/2026"),
            ("in range typed: \"4/18/2026\" + Enter", "4/18/2026"),
            ("unparsable typed: \"abc\" + Enter", "abc"),
            ("text cleared + Enter", ""),
        })
        {
            DatePicker picker = Ranged();
            int errors = 0;
            string? errorText = null;
            picker.DateValidationError += (_, e) =>
            {
                errors++;
                errorText = e.Text;
            };

            await ShowAsync(picker, async () =>
            {
                DatePickerTextBox box = TextPart(picker);
                box.Text = typed;
                PressKey(box, Key.Enter);
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                rows.Add([label,
                    $"SelectedDate {Date(picker.SelectedDate)}, Text \"{picker.Text}\", " +
                    $"DateValidationError {errors}" + (errorText is null ? "" : $" (\"{errorText}\")")]);
            });
        }

        {
            DatePicker picker = Ranged();
            await ShowAsync(picker, async () =>
            {
                picker.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                Calendar calendar = CalendarOf(picker);
                foreach (DateTime date in new[] { new DateTime(2026, 4, 5), new DateTime(2026, 4, 12) })
                {
                    CalendarDayButton? button = DayButton(calendar, date);
                    rows.Add([$"calendar popup, day button {Date(date)}",
                        button is null ? "not found" : $"IsEnabled {button.IsEnabled}, IsBlackedOut {button.IsBlackedOut}, IsInactive {button.IsInactive}"]);
                }

                picker.IsDropDownOpen = false;
            });
        }

        {
            DatePicker picker = Ranged();
            picker.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 4, 12), new DateTime(2026, 4, 13)));
            string thrown = Throws(() => picker.SelectedDate = new DateTime(2026, 4, 12));
            rows.Add(["BlackoutDates 04-12..04-13; SelectedDate = 04-12 from code", $"{thrown}; SelectedDate {Date(picker.SelectedDate)}"]);

            await ShowAsync(picker, async () =>
            {
                picker.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                CalendarDayButton? button = DayButton(CalendarOf(picker), new DateTime(2026, 4, 12));
                rows.Add(["  calendar popup, day button 2026-04-12",
                    button is null ? "not found" : $"IsEnabled {button.IsEnabled}, IsBlackedOut {button.IsBlackedOut}"]);
                picker.IsDropDownOpen = false;
            });
        }

        {
            // 期間の終了日側に、開始日より前の日付がすでに選ばれている場合。
            DatePicker picker = NewPicker();
            picker.SelectedDate = new DateTime(2026, 4, 10);
            string thrown = Throws(() => picker.DisplayDateStart = new DateTime(2026, 4, 15));
            rows.Add(["SelectedDate 04-10, then DisplayDateStart = 04-15",
                $"{thrown}; DisplayDateStart {Date(picker.DisplayDateStart)}, SelectedDate {Date(picker.SelectedDate)}"]);
        }

        foreach (string text in new[] { "4/15/2026", "2026-04-15", "April 15, 2026", "15/4/2026" })
        {
            DatePicker picker = NewPicker();
            await ShowAsync(picker, async () =>
            {
                string thrown = Throws(() => picker.Text = text);
                rows.Add([$"Text = \"{text}\"", $"{thrown}; SelectedDate {Date(picker.SelectedDate)}, Text \"{picker.Text}\""]);
                await Task.CompletedTask;
            });
        }

        return rows;
    }

    private sealed class Source : INotifyPropertyChanged
    {
        private DateTime _date = Day;

        public DateTime Date
        {
            get => _date;
            set
            {
                _date = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static async Task<List<IReadOnlyList<string>>> CalendarAndBindingAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        {
            var source = new Source();
            DatePicker picker = NewPicker();
            picker.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(Source.Date)) { Source = source });
            await ShowAsync(picker, async () =>
            {
                picker.SelectedDate = null;
                BindingExpression? expression = BindingOperations.GetBindingExpression(picker, DatePicker.SelectedDateProperty);
                rows.Add(["bound to a DateTime (not nullable); SelectedDate = null",
                    $"source {Date(source.Date)}, HasError {expression?.HasError}, Validation.HasError {Validation.GetHasError(picker)}"]);
                await Task.CompletedTask;
            });
        }

        {
            // 表示前（Loaded より前）に開く指定をする。
            var picker = new DatePicker { Width = 200, IsDropDownOpen = true, SelectedDate = Day };
            string thrown = "no exception";
            try
            {
                await ShowAsync(picker, async () =>
                {
                    await Capture.SettleAsync(Window.GetWindow(picker)!);
                    var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
                    rows.Add(["IsDropDownOpen = True before showing: IsDropDownOpen / Popup.IsOpen",
                        $"{picker.IsDropDownOpen} / {popup.IsOpen}"]);
                    picker.IsDropDownOpen = false;
                });
            }
            catch (Exception ex)
            {
                thrown = ex.GetType().Name;
            }

            rows.Add(["  exception while showing", thrown]);
        }

        foreach (bool? highlighted in new bool?[] { null, true, false })
        {
            var picker = new DatePicker { Width = 200, SelectedDate = DateTime.Today.AddDays(1) };
            if (highlighted is not null)
            {
                picker.IsTodayHighlighted = highlighted.Value;
            }

            await ShowAsync(picker, async () =>
            {
                picker.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                Calendar calendar = CalendarOf(picker);
                CalendarDayButton? today = DayButton(calendar, DateTime.Today);
                string label = highlighted is null ? "not set" : highlighted.Value.ToString();
                rows.Add([$"IsTodayHighlighted {label}: DatePicker / popup Calendar value (source)",
                    $"{WpfProbe.ValueAndSource(picker, DatePicker.IsTodayHighlightedProperty)} / " +
                    WpfProbe.ValueAndSource(calendar, Calendar.IsTodayHighlightedProperty)]);
                rows.Add(["  today's day button: IsToday / IsEnabled / today marker",
                    today is null ? "not found" : $"{today.IsToday} / {today.IsEnabled} / {TodayMarker(today)}"]);
                picker.IsDropDownOpen = false;
            });
        }

        foreach (DayOfWeek first in new[] { DayOfWeek.Sunday, DayOfWeek.Monday })
        {
            var picker = new DatePicker { Width = 200, FirstDayOfWeek = first, SelectedDate = Day };
            await ShowAsync(picker, async () =>
            {
                picker.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(picker)!);
                Calendar calendar = CalendarOf(picker);
                rows.Add([$"FirstDayOfWeek={first}: the popup Calendar's FirstDayOfWeek", calendar.FirstDayOfWeek.ToString()]);
                picker.IsDropDownOpen = false;
            });
        }

        return rows;
    }
}
