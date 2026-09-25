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
        "記事の単一テンプレートの例で、DataTemplate.Triggers を Grid の中に置いた形と DataTemplate の直下（ルート要素の後と前）に置いた形を、DataGrid のセルに使ったときの結果と、BeginEdit の前後の表示の切り替わり",
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

        await context.SaveTableAsync(
            "single template switched by DataGridCell.IsEditing",
            ["placement of DataTemplate.Triggers", "result"],
            await SingleTemplateAsync(),
            "datagrid-single-template.svg");
    }

    /// <summary>記事の単一テンプレートの例。{0}・{1}・{2} のどれかに Triggers を入れ、位置による違いを見る。</summary>
    private const string SingleTemplate = """
        <DataTemplate>
          {2}
          <Grid>
            <TextBlock x:Name="display" Text="{Binding Name}" />
            <TextBox x:Name="editor" Text="{Binding Name, Mode=TwoWay}" Visibility="Collapsed" />
            {0}
          </Grid>
          {1}
        </DataTemplate>
        """;

    private const string Triggers = """
        <DataTemplate.Triggers>
          <DataTrigger
            Binding="{Binding RelativeSource={RelativeSource AncestorType=DataGridCell}, Path=IsEditing}"
            Value="True">
            <Setter TargetName="display" Property="Visibility" Value="Collapsed" />
            <Setter TargetName="editor" Property="Visibility" Value="Visible" />
          </DataTrigger>
        </DataTemplate.Triggers>
        """;

    /// <summary>
    /// Triggers を Grid の中に置いた形（記事の以前の例）と、DataTemplate の直下（ルート要素の前と後）に置いた形を実際に使う。
    /// XAML の読み取りは読み込み時に行われるが、テンプレートの要素はセルに表示するときに生成される。
    /// Grid の中に置いた誤りは読み込みでは検出されず、要素を生成する時点で結果が分かれる。
    /// </summary>
    private static async Task<List<IReadOnlyList<string>>> SingleTemplateAsync()
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach ((string label, string inside, string after, string leading) in new[]
        {
            ("inside <Grid>", Triggers, "", ""),
            ("under <DataTemplate>, after <Grid>", "", Triggers, ""),
            ("under <DataTemplate>, before <Grid>", "", "", Triggers),
        })
        {
            DataTemplate template;
            try
            {
                template = SceneContext.LoadXaml<DataTemplate>(SingleTemplate.Replace("{0}", inside).Replace("{1}", after).Replace("{2}", leading));
            }
            catch (Exception e)
            {
                rows.Add([label, $"loading the template: {e.GetType().Name}"]);
                continue;
            }

            var items = new ObservableCollection<Product>(SampleData.Products().Take(1));
            var grid = new DataGrid { ItemsSource = items, AutoGenerateColumns = false, CanUserAddRows = false };
            grid.Columns.Add(new DataGridTemplateColumn { Header = "Name", CellTemplate = template });
            var window = new Window { Content = grid, Width = 320, Height = 160, ShowActivated = false };
            try
            {
                try
                {
                    await Capture.ShowAndSettleAsync(window);
                }
                catch (Exception e)
                {
                    rows.Add([label, $"loading succeeds; showing the grid throws {e.GetType().Name}"]);
                    continue;
                }

                string before = Visibilities(grid);
                grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0]);
                grid.BeginEdit();
                await Capture.SettleAsync(window);
                rows.Add([label, $"before BeginEdit: {before}; after: {Visibilities(grid)}"]);
            }
            finally
            {
                window.Close();
            }
        }

        rows.Add(["under <DataTemplate>, before <Grid> (dotnet build)", await BuildBeforeRootAsync()]);
        return rows;
    }

    /// <summary>
    /// 読者が書くのは、XamlReader ではなくビルドする .xaml ファイルである。
    /// ルート要素の前に Triggers を置いたテンプレートを一時プロジェクトでビルドし、マークアップ コンパイラーの結果を返す。
    /// </summary>
    private static async Task<string> BuildBeforeRootAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "datatemplate-triggers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var utf8 = new System.Text.UTF8Encoding(true);
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "probe.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Library</OutputType>
                    <TargetFramework>net10.0-windows</TargetFramework>
                    <UseWPF>true</UseWPF>
                  </PropertyGroup>
                </Project>
                """,
                utf8);
            string template = SingleTemplate.Replace("{0}", "").Replace("{1}", "").Replace("{2}", Triggers);
            await File.WriteAllTextAsync(
                Path.Combine(workspace, "Templates.xaml"),
                "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                    + template.Replace("<DataTemplate>", "<DataTemplate x:Key=\"Single\">")
                    + "</ResourceDictionary>",
                utf8);

            var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet", "build -c Release -v q --nologo -nodeReuse:false")
            {
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("dotnet を起動できない。");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("ビルドが 5 分以内に終わらなかったため止めた。");
            }

            string output = await stdout + await stderr;
            if (process.ExitCode == 0)
            {
                return "build succeeds";
            }

            // エラーコードだけを出す。本文は OS の言語で変わるため。
            string[] codes = System.Text.RegularExpressions.Regex.Matches(output, @"error (MC\d+|CS\d+)")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToArray();
            return codes.Length == 0
                ? throw new InvalidOperationException($"ビルドが失敗したが、エラーコードを読み取れない。{Environment.NewLine}{output}")
                : $"build fails: {string.Join(", ", codes)} (target 'display' must come before its Setter)";
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string Visibilities(DataGrid grid)
    {
        TextBlock? display = null;
        TextBox? editor = null;
        void Walk(DependencyObject node)
        {
            if (node is TextBlock { Name: "display" } block) display = block;
            if (node is TextBox { Name: "editor" } box) editor = box;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(grid);
        return $"TextBlock {display?.Visibility}, TextBox {editor?.Visibility}";
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
