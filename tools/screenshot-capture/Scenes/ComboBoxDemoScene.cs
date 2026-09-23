using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// デモページ「ComboBox」（apps/wpf-standard-control-demo/combobox.html と日本語版）の記述を実測する。
///
/// 項目はデモアプリの EnumBindingSource と同じく、Name と Value を持つ DayOfWeek の 7 項目にする（ToString は上書きしない）。
/// 文字の入力は編集用の TextBox へ TextComposition で 1 文字ずつ送り、ドロップダウンの操作は SendKey（InputManager 経由）、
/// 編集欄のクリックは実際のマウス（<see cref="RealMouse"/>）で行う。
/// </summary>
internal sealed class ComboBoxDemoScene : IScene
{
    public string Slug => "wpf-standard-control-demo-combobox";

    public string ImageDirectory => DemoProbe.ImageDirectory("combobox");

    public IReadOnlyList<string> Verifies =>
    [
        "ComboBox の IsEditable / IsReadOnly / StaysOpenOnEdit / ShouldPreserveUserEnteredPrefix / IsTextSearchEnabled / MaxDropDownHeight の既定値と、IsDropDownOpen が既定で TwoWay か",
        "Mode 指定なしで IsDropDownOpen に結んだ CheckBox で開き、下矢印と Enter で選んだときの選択・IsDropDownOpen・CheckBox",
        "DisplayMemberPath=Name のときの Text と、ToString を上書きしない項目の SelectedItem の文字列",
        "編集可能なときに \"tue\" を入力したときの Text と SelectedValue（ShouldPreserveUserEnteredPrefix が False / True）と、どの項目にも合わない文字",
        "IsEditable と IsReadOnly が True のときに入力した文字と、下矢印キーでの選択",
        "MaxDropDownHeight 100（デモアプリの初期値）と既定値のときのドロップダウンの高さ",
        "ドロップダウンを開いたまま実際のマウスで編集欄をクリックしたときの IsDropDownOpen（StaysOpenOnEdit が False / True）",
        "同じリストを ItemsSource にした 2 つの ComboBox の、IsSynchronizedWithCurrentItem が True / 未設定のときの初期選択と連動",
    ];

    public async Task CaptureAsync(SceneContext context)
    {
        await context.SaveTableAsync(
            "ComboBox: selection, editing, drop-down and synchronization (the demo's DayOfWeek items)",
            ["case", "measured"],
            await MeasureAsync(),
            "combobox-behavior.svg");
    }

    /// <summary>デモアプリの EnumBindingSource の項目と同じ形。</summary>
    public sealed class EnumItem(string name, object? value)
    {
        public string Name { get; } = name;

        public object? Value { get; } = value;
    }

    private static List<EnumItem> Days() =>
        Enum.GetValues<DayOfWeek>().Select(d => new EnumItem(d.ToString(), d)).ToList();

    private static ComboBox DemoCombo(IEnumerable<EnumItem>? items = null) => new()
    {
        Width = 160,
        DisplayMemberPath = "Name",
        SelectedValuePath = "Value",
        ItemsSource = items ?? Days(),
    };

    private static TextBox EditBox(ComboBox combo) => (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);

    private static async Task TypeAsync(TextBox box, string text)
    {
        foreach (char c in text)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, c.ToString()));
            await Capture.SettleAsync(Window.GetWindow(box)!, 20);
        }
    }

    private static async Task<List<IReadOnlyList<string>>> MeasureAsync()
    {
        var rows = new List<IReadOnlyList<string>>();

        var defaults = new ComboBox();
        var metadata = (FrameworkPropertyMetadata)ComboBox.IsDropDownOpenProperty.GetMetadata(typeof(ComboBox));
        rows.Add(["defaults: IsEditable, IsReadOnly, StaysOpenOnEdit", $"{defaults.IsEditable}, {defaults.IsReadOnly}, {defaults.StaysOpenOnEdit}"]);
        rows.Add(["  ShouldPreserveUserEnteredPrefix, IsTextSearchEnabled", $"{defaults.ShouldPreserveUserEnteredPrefix}, {defaults.IsTextSearchEnabled}"]);
        rows.Add(["MaxDropDownHeight (screen height / 3) / IsDropDownOpen two-way",
            $"{D(defaults.MaxDropDownHeight)} ({D(SystemParameters.PrimaryScreenHeight / 3)}) / {metadata.BindsTwoWayByDefault}"]);

        {
            var check = new CheckBox { Content = "IsDropDownOpen" };
            ComboBox combo = DemoCombo();
            combo.SetBinding(ComboBox.IsDropDownOpenProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = check });
            var panel = new StackPanel { Children = { check, combo } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                await FocusAsync(combo);
                check.IsChecked = true;
                await Capture.SettleAsync(window, 100);
                bool opened = combo.IsDropDownOpen;
                SendKey(Key.Down);
                SendKey(Key.Down);
                SendKey(Key.Enter);
                await Capture.SettleAsync(window, 100);
                rows.Add(["bound CheckBox opens it; Down, Down, Enter: selected / open / CheckBox",
                    $"{opened}; {((EnumItem?)combo.SelectedItem)?.Name ?? "none"} / {combo.IsDropDownOpen} / {WpfProbe.Describe(check.IsChecked)}"]);
                rows.Add(["  Text / SelectedValue / SelectedItem.ToString()",
                    $"{WpfProbe.Describe(combo.Text)} / {WpfProbe.Describe(combo.SelectedValue)} / {combo.SelectedItem?.ToString()?.Split('.').Last()}"]);
            }, activate: true);
        }

        foreach (bool preserve in new[] { false, true })
        {
            ComboBox combo = DemoCombo();
            combo.IsEditable = true;
            combo.ShouldPreserveUserEnteredPrefix = preserve;
            await ShowAsync(combo, async () =>
            {
                TextBox box = EditBox(combo);
                await FocusAsync(box);
                await TypeAsync(box, "tue");
                rows.Add([$"editable, \"tue\" typed, preserve prefix {preserve}: Text / SelectedValue",
                    $"{WpfProbe.Describe(combo.Text)} / {WpfProbe.Describe(combo.SelectedValue)}"]);
                if (!preserve)
                {
                    box.SelectAll();
                    await TypeAsync(box, "xyz");
                    rows.Add(["  then \"xyz\" typed over it: Text / index / SelectedValue",
                        $"{WpfProbe.Describe(combo.Text)} / {combo.SelectedIndex} / {WpfProbe.Describe(combo.SelectedValue)}"]);
                }
            }, activate: true);
        }

        {
            ComboBox combo = DemoCombo();
            combo.IsEditable = true;
            combo.IsReadOnly = true;
            await ShowAsync(combo, async () =>
            {
                TextBox box = EditBox(combo);
                await FocusAsync(box);
                await TypeAsync(box, "tue");
                string typed = $"{WpfProbe.Describe(combo.Text)} / {combo.SelectedIndex}";
                SendKey(Key.Down);
                await Capture.SettleAsync(Window.GetWindow(combo)!, 50);
                rows.Add(["editable + IsReadOnly: \"tue\" typed: Text / index; then Down",
                    $"{typed}; {WpfProbe.Describe(combo.Text)} / {combo.SelectedIndex}"]);
            }, activate: true);
        }

        foreach (double max in new[] { double.NaN, 100 })
        {
            ComboBox combo = DemoCombo();
            if (!double.IsNaN(max))
            {
                combo.MaxDropDownHeight = max;
            }

            await ShowAsync(combo, async () =>
            {
                combo.IsDropDownOpen = true;
                await Capture.SettleAsync(Window.GetWindow(combo)!, 150);
                var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                var child = (FrameworkElement)popup.Child;
                rows.Add([$"MaxDropDownHeight {(double.IsNaN(max) ? "default" : D(max) + " (demo start)")}: drop-down height",
                    D(child.ActualHeight)]);
                combo.IsDropDownOpen = false;
            });
        }

        foreach (bool staysOpen in new[] { false, true })
        {
            ComboBox combo = DemoCombo();
            combo.IsEditable = true;
            combo.StaysOpenOnEdit = staysOpen;
            var panel = new StackPanel { Width = 200, Height = 260, Children = { combo } };
            await ShowAsync(panel, async () =>
            {
                Window window = Window.GetWindow(panel)!;
                window.Topmost = true;
                await Capture.SettleAsync(window);
                combo.IsDropDownOpen = true;
                await Capture.SettleAsync(window, 150);
                TextBox box = EditBox(combo);
                using (RealMouse.Preserve())
                {
                    await RealMouse.MoveToAsync(box, new Point(10, box.ActualHeight / 2));
                    await RealMouse.LeftDownAsync(window);
                    await RealMouse.LeftUpAsync(window);
                }

                rows.Add([$"open, real click in edit box, StaysOpenOnEdit={staysOpen}: still open",
                    combo.IsDropDownOpen.ToString()]);
                combo.IsDropDownOpen = false;
            });
        }

        foreach (bool? sync in new bool?[] { true, null })
        {
            List<EnumItem> shared = Days();
            ComboBox first = DemoCombo(shared);
            ComboBox second = DemoCombo(shared);
            if (sync is { } value)
            {
                first.IsSynchronizedWithCurrentItem = value;
                second.IsSynchronizedWithCurrentItem = value;
            }

            var panel = new StackPanel { Children = { first, second } };
            await ShowAsync(panel, async () =>
            {
                string initial = $"{first.SelectedIndex}, {second.SelectedIndex}";
                first.SelectedIndex = 3;
                await Capture.SettleAsync(Window.GetWindow(panel)!, 20);
                rows.Add([$"shared list, IsSynchronizedWithCurrentItem={WpfProbe.Describe(sync)}: start; first set to 3",
                    $"{initial}; {first.SelectedIndex}, {second.SelectedIndex}"]);
            });
        }

        return rows;
    }
}
