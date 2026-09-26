using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF ComboBox の ItemsSource 設計パターン」の図。
/// 同じ選択肢に対し、文字列リスト・DisplayMemberPath・ItemTemplate の
/// 3 パターンで選択済み表示がどう変わるかを実際の描画で示す。
/// </summary>
internal sealed class ComboBoxItemsSourceScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "同じ選択に対して SelectedItem / SelectedValue / SelectedIndex が返す値",
        "SelectedValuePath の有無で SelectedValue の中身が変わること",
        "DisplayMemberPath の有無で表示される文字列が変わること（計測用の型は ToString をオーバーライドしない）",
        "DisplayMemberPath と ItemTemplate をコードと XAML で両方設定したときの例外",
        "SelectedValuePath のプロパティが int で、SelectedValue にバインドしたソースが文字列のときの選択と、選択を変えたときに書き戻される値の型",
        "列挙型を ItemsSource にして SelectedValuePath=\"value__\" としたときの SelectedValue",
        "SelectedValue を ItemsSource より先に設定したとき（コードで設定、バインドで表示後に ItemsSource を入れる）の選択",
    ];

    public string Slug => "wpf-combobox-itemssource-patterns";

    public async Task CaptureAsync(SceneContext context)
    {
        ComboBox strings = Configure(
            new ComboBox { ItemsSource = new[] { "Aoki", "Baker", "Chen" } });

        ComboBox displayMember = Configure(
            new ComboBox
            {
                ItemsSource = SampleData.Employees(),
                DisplayMemberPath = nameof(Employee.Name),
            });

        // 記事のパターン D と同じテンプレート。
        ComboBox itemTemplate = Configure(
            new ComboBox
            {
                ItemsSource = SampleData.Employees(),
                ItemTemplate = SceneContext.LoadXaml<DataTemplate>(
                    """
                    <DataTemplate>
                      <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding Id}" Width="40" Foreground="Gray" />
                        <TextBlock Text="{Binding Name}" />
                      </StackPanel>
                    </DataTemplate>
                    """),
            });

        var rows = new[]
        {
            new DemoLayout.Row("ItemsSource = string[]", strings),
            new DemoLayout.Row("DisplayMemberPath = \"Name\"", displayMember),
            new DemoLayout.Row("ItemTemplate = Id + Name", itemTemplate),
        };

        await context.ShootAsync(
            DemoLayout.BuildComparisonWindow("ComboBox ItemsSource", rows),
            "combobox-itemssource-patterns.png");

        await context.SaveTableAsync(
            "what each selection property returns for the same selection",
            ["configuration", "SelectedItem", "SelectedValue", "SelectedIndex", "displayed"],
            await ComboBoxAndDatePickerMeasurements.SelectionPropertiesAsync(),
            "combobox-selection-properties.svg");

        await context.SaveTableAsync(
            "ComboBox pitfalls: settings that are said to fail",
            ["case", "measured"],
            await ComboBoxAndDatePickerMeasurements.PitfallsAsync(),
            "combobox-pitfalls.svg");
    }

    /// <summary>選択済みの表示を比べるため、いずれも 2 件目を選択しておく。</summary>
    private static ComboBox Configure(ComboBox comboBox)
    {
        comboBox.Width = 180;
        comboBox.SelectedIndex = 1;
        comboBox.HorizontalAlignment = HorizontalAlignment.Left;
        return comboBox;
    }
}
