using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF DataGrid でセル編集中と表示時でコントロールを切り替える方法」の図。
/// 同じ DataGridTemplateColumn が、表示中は CellTemplate、編集中は CellEditingTemplate で
/// 描画されることを実際の画面で示す。
/// </summary>
internal sealed class DataGridEditingTemplateScene : IScene
{
    public IReadOnlyList<string> Verifies =>
    [
        "表示中は CellTemplate、編集中は CellEditingTemplate の要素がセルに置かれること",
        "BeginEdit を呼ぶと実際に要素の型が入れ替わること",
    ];

    public string Slug => "wpf-datagrid-cell-editing-template";

    public async Task CaptureAsync(SceneContext context)
    {
        ObservableCollection<Product> displayItems = SampleData.Products();
        ObservableCollection<Product> editingItems = SampleData.Products();

        DataGrid display = BuildGrid(displayItems);
        DataGrid editing = BuildGrid(editingItems);

        Window window = DemoLayout.BuildPanelWindow(
            "DataGridTemplateColumn",
            [
                new DemoLayout.Panel("CellTemplate", display),
                new DemoLayout.Panel("CellEditingTemplate", editing),
            ]);

        await context.ShootAsync(window, "datagrid-cell-template-vs-editing.png", async _ =>
        {
            // 2 行目の Category セルを編集状態にして、CellEditingTemplate を描画させる。
            editing.Focus();
            editing.CurrentCell = new DataGridCellInfo(editingItems[1], editing.Columns[1]);
            editing.BeginEdit();
            await Task.Delay(200);
        });

        await context.SaveTableAsync(
            "element placed in the cell",
            ["state", "element in the cell", "cell.IsEditing"],
            await DataGridMeasurements.EditingTemplateAsync(),
            "datagrid-editing-template.svg");
    }

    private static DataGrid BuildGrid(ObservableCollection<Product> items) => new()
    {
        ItemsSource = items,
        DataContext = new ProductListContext(),
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserResizeColumns = false,
        CanUserSortColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        HorizontalAlignment = HorizontalAlignment.Left,
        Columns =
        {
            new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(Product.Name)),
                Width = 110,
            },
            new DataGridTemplateColumn
            {
                Header = "Category",
                Width = 130,
                CellTemplate = SceneContext.LoadXaml<DataTemplate>(
                    """
                    <DataTemplate>
                      <TextBlock Text="{Binding Category}" VerticalAlignment="Center" Margin="4,0" />
                    </DataTemplate>
                    """),
                CellEditingTemplate = SceneContext.LoadXaml<DataTemplate>(
                    """
                    <DataTemplate>
                      <ComboBox ItemsSource="{Binding DataContext.Categories, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                SelectedItem="{Binding Category, Mode=TwoWay}" />
                    </DataTemplate>
                    """),
            },
        },
    };
}
