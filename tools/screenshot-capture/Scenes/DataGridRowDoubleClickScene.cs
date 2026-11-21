using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ScreenshotCapture.Scenes.DataGridRowDoubleClick;
using static ScreenshotCapture.Scenes.DemoProbe;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF DataGrid の行のダブルクリックをコマンドにつなぐ」の計測。
///
/// 記事と同じ XAML の DataGrid を表示し、2 行目（beta）を実際のマウス（<see cref="RealMouse"/>）でクリックして選んでから、
/// 3 行目のセル・列ヘッダー・スクロールバーのつまみ・行の無い空白などを実際のマウスでダブルクリックする。
/// ダブルクリックを拾う 4 つの書き方ごとに、OpenCommand が実行されたか、どの顧客が渡されたかを記録する。
/// </summary>
internal sealed class DataGridRowDoubleClickScene : IScene
{
    private const string LocalNamespace = "clr-namespace:ScreenshotCapture.Scenes.DataGridRowDoubleClick;assembly=ScreenshotCapture";

    // 記事の XAML は xmlns:b="http://schemas.microsoft.com/xaml/behaviors" と書く。XamlReader.Parse ではこの URI から
    // Interaction.Triggers を解決できない（「不明なメンバー」になる）ため、計測では同じ型を clr-namespace で指定する。
    private const string BehaviorsNamespace = "clr-namespace:Microsoft.Xaml.Behaviors;assembly=Microsoft.Xaml.Behaviors";

    private const ushort VirtualKeyControl = 0x11;

    public IReadOnlyList<string> Verifies =>
    [
        "ダブルクリックは 1 回目と 2 回目のクリックの間を 100 ms 空けて送り、4 つの書き方の計測・2 回目のクリックの観測・図の撮影のいずれでも、2 つのマウスダウンが DataGrid に ClickCount 1、2 として届いたこと（そうでなければ計測を止める）。2 回目のクリックの観測では、ウィンドウにもマウスダウンが 2 つ届いていること",
        "IsReadOnly=True の DataGrid で 2 行目（beta）をクリックで選んだ後、DataGrid 自身の MouseDoubleClick を EventTrigger（Microsoft.Xaml.Behaviors.Wpf）の InvokeCommandAction で拾い、CommandParameter に SelectedItem を渡すと、列ヘッダー・縦のスクロールバーのつまみ・つまみより下のトラック・最後の行より下の空白のダブルクリックでも OpenCommand が実行されて beta が渡され、Ctrl を押しながら未選択の gamma をダブルクリックしても beta が渡され、右ボタンのダブルクリックでも実行されること。何も選ばずに空白をダブルクリックしたときは実行されないこと",
        "列ヘッダーのダブルクリックで、並び替えていない Name の列が降順になること。トラックのダブルクリックで VerticalOffset が 0 より大きくなること",
        "DataGrid の InputBindings に MouseBinding（LeftDoubleClick）を置き、CommandParameter に SelectedItem を渡すと、IsReadOnly=True の DataGrid の行（Ctrl なし）と最後の行より下の空白のダブルクリックでは OpenCommand が実行され（空白では beta が渡される）、列ヘッダー・スクロールバーのつまみ・トラック・Ctrl を押しながらのダブルクリック（選択中の行・未選択の行）・編集できるセル（DataGrid も列も IsReadOnly=False。未選択の行・選択中の行）・右ボタンのダブルクリックでは実行されないこと",
        "2 回目の MouseLeftButtonDown（ClickCount=2）が DataGrid のハンドラーに届いた時点で、列ヘッダー・スクロールバーのつまみ・トラック・Ctrl を押しながらのセル・編集できるセル（未選択の gamma の Name・選択中の beta の Name・Name 列だけ IsReadOnly=True の DataGrid の City）では Handled が True、IsReadOnly=True の DataGrid の未選択の gamma のセル（Ctrl なし）・空白・IsReadOnly=True の Name 列のセルでは False であること。これらのいずれでも DataGrid の MouseDoubleClick は発生し、行をダブルクリックした場合は行の MouseDoubleClick も発生すること",
        "DataGrid も列も IsReadOnly=False のとき、未選択の gamma の Name のセルをダブルクリックすると、DataGrid の MouseDoubleClick のハンドラーが動いた時点で、そのセルの IsEditing が True で、OriginalSource（TextBlock）の visual の親が null であること。選択中の beta の Name のセルをダブルクリックすると、その時点の IsEditing が True で、OriginalSource が TextBoxView（visual の親は ScrollContentPresenter）であること",
        "DataGrid を IsReadOnly=False にして Name 列だけ IsReadOnly=True にすると、gamma の Name のセルのダブルクリックで編集状態にならず（MouseDoubleClick のハンドラーが動いた時点の IsEditing も False）、2 回目の MouseLeftButtonDown の Handled が False で、OriginalSource（TextBlock）の visual の親が ContentPresenter のままで、4 つの書き方すべてで gamma が渡されること。同じ DataGrid の gamma の City のセルでは、編集状態になり、DataGrid も列も IsReadOnly=False の Name のセルと同じ結果（EventTrigger ＋ SelectedItem と RowStyle ＋ 添付プロパティでは gamma、MouseBinding と EventArgsConverter では実行されない）になること",
        "InvokeCommandAction の EventArgsConverter で OriginalSource から DataGridRow をさかのぼって行のアイテムを渡すと（左ボタン以外は null）、列ヘッダー・スクロールバーのつまみ・トラック・空白・右ボタンのダブルクリックと、編集できるセルを未選択の行でダブルクリックしたときは OpenCommand が実行されず、IsReadOnly=True の DataGrid の行のダブルクリック（Ctrl なしで未選択の gamma・選択中の beta、Ctrl ありで選択中の beta・未選択の gamma）と、編集できるセルを選択中の行（beta）でダブルクリックしたときはその行が渡されること",
        "RowStyle の Setter で DataGridRow に添付プロパティを付け、行の MouseDoubleClick で左ボタンのときだけ DataGridRow.Item を渡すと、列ヘッダー・スクロールバーのつまみ・トラック・空白・右ボタンのダブルクリックでは実行されず、IsReadOnly=True の DataGrid の行（Ctrl なしで未選択の gamma・選択中の beta、Ctrl ありで選択中の beta・未選択の gamma）と、編集できるセル（Ctrl なしで未選択の gamma・選択中の beta、Name 列だけ IsReadOnly=True の DataGrid の City）のダブルクリックではその行が渡されること",
        "Style の Setter で DataGridRow の InputBindings を設定する XAML は、読み込み時に XamlParseException（内側は ParamName が property の ArgumentNullException）になること",
    ];

    public string Slug => "wpf-datagrid-row-doubleclick-command-mvvm";

    public async Task CaptureAsync(SceneContext context)
    {
        await ShootHeaderDoubleClickAsync(context);

        var rows = new List<IReadOnlyList<Loc>>();
        foreach (Target target in Enum.GetValues<Target>())
        {
            rows.Add(await MeasureTargetAsync(target));
        }

        await context.SaveTableAsync(
            "Double-clicking each place: which customer OpenCommand received",
            [
                T("double-clicked", "ダブルクリックした場所"),
                T("SelectedItem after", "ダブルクリック後の SelectedItem"),
                T("other effects", "そのほかの変化"),
                .. Enum.GetValues<Hook>().Select(Describe),
            ],
            rows,
            "row-doubleclick-results.svg");

        var observed = new List<IReadOnlyList<Loc>>();
        foreach (Target target in new[]
        {
            Target.Row, Target.ColumnHeader, Target.ScrollBarThumb, Target.ScrollBarTrack, Target.BlankArea,
            Target.CtrlOnSelectedRow, Target.CtrlOnUnselectedRow, Target.EditableRow, Target.EditableSelectedRow,
            Target.EditableGridReadOnlyColumn, Target.EditableGridReadOnlyColumnCityCell,
        })
        {
            observed.Add(await ObserveAsync(target));
        }

        await context.SaveTableAsync(
            "What the DataGrid receives on the second click",
            [
                T("double-clicked", "ダブルクリックした場所"),
                T("ClickCount of the two mouse-downs", "2 つのマウスダウンの ClickCount"),
                T("MouseLeftButtonDown (ClickCount=2) handled at the DataGrid", "DataGrid での MouseLeftButtonDown（ClickCount=2）の Handled"),
                T("OriginalSource of DataGrid.MouseDoubleClick", "DataGrid.MouseDoubleClick の OriginalSource"),
                T("its visual parent at that time", "その時点の OriginalSource の visual の親"),
                T("IsEditing of the double-clicked cell at that time", "その時点のダブルクリックしたセルの IsEditing"),
                T("mouse-downs reaching the window", "ウィンドウに届いたマウスダウンの数"),
                T("MouseDoubleClick of the double-clicked row", "ダブルクリックした行の MouseDoubleClick"),
            ],
            observed,
            "datagrid-second-click.svg");

        await context.SaveTableAsync(
            "Setting InputBindings of DataGridRow in a Style",
            [T("XAML", "XAML"), T("result", "結果")],
            [["<Setter Property=\"InputBindings\" ...>", StyleInputBindingsResult()]],
            "row-style-inputbindings.svg");
    }

    private static Loc T(string en, string ja) => Loc.Of(en, ja);

    /// <summary>OpenCommand が 1 回も実行されなかったことを表す、計測の内部の印。表では言語ごとの文言に置き換える。</summary>
    private const string NotExecuted = "(not executed)";

    // ---------------------------------------------------------------- 記事の XAML

    /// <summary>記事の DataGrid。{ReadOnly} は IsReadOnly の値、{Hook} はダブルクリックを拾う書き方。</summary>
    private const string GridXaml = """
        <DataGrid x:Name="grid" Width="240" Height="150" ItemsSource="{Binding Customers}"
                  AutoGenerateColumns="False" IsReadOnly="{ReadOnly}">
          {Hook}
          <DataGrid.Columns>
            <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="90"{NameColumnReadOnly} />
            <DataGridTextColumn Header="City" Binding="{Binding City}" Width="90" />
          </DataGrid.Columns>
        </DataGrid>
        """;

    /// <summary>よく紹介される書き方 1: DataGrid 自身の MouseDoubleClick を InvokeCommandAction で拾い、SelectedItem を渡す。</summary>
    private const string GridEventTriggerXaml = """
        <b:Interaction.Triggers>
            <b:EventTrigger EventName="MouseDoubleClick">
              <b:InvokeCommandAction Command="{Binding OpenCommand}"
                                     CommandParameter="{Binding SelectedItem, ElementName=grid}" />
            </b:EventTrigger>
          </b:Interaction.Triggers>
        """;

    /// <summary>よく紹介される書き方 2: DataGrid の InputBindings に MouseBinding を置き、SelectedItem を渡す。</summary>
    private const string GridMouseBindingXaml = """
        <DataGrid.InputBindings>
            <MouseBinding Gesture="LeftDoubleClick" Command="{Binding OpenCommand}"
                          CommandParameter="{Binding SelectedItem, ElementName=grid}" />
          </DataGrid.InputBindings>
        """;

    /// <summary>行だけを拾う書き方 1: InvokeCommandAction の EventArgsConverter で、ダブルクリックされた行のアイテムを渡す。</summary>
    private const string EventArgsConverterXaml = """
        <DataGrid.Resources>
            <local:RowItemFromEventArgsConverter x:Key="RowItemFromEventArgs" />
          </DataGrid.Resources>
          <b:Interaction.Triggers>
            <b:EventTrigger EventName="MouseDoubleClick">
              <b:InvokeCommandAction Command="{Binding OpenCommand}"
                                     EventArgsConverter="{StaticResource RowItemFromEventArgs}" />
            </b:EventTrigger>
          </b:Interaction.Triggers>
        """;

    /// <summary>行だけを拾う書き方 2: RowStyle で DataGridRow に添付プロパティを付ける。</summary>
    private const string RowStyleXaml = """
        <DataGrid.RowStyle>
            <Style TargetType="DataGridRow">
              <Setter Property="local:RowDoubleClick.Command"
                      Value="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" />
            </Style>
          </DataGrid.RowStyle>
        """;

    /// <summary>Style の Setter で InputBindings を設定しようとする XAML（読み込めないことを確かめる）。</summary>
    private const string StyleInputBindingsXaml = """
        <Style TargetType="DataGridRow">
          <Setter Property="InputBindings">
            <Setter.Value>
              <InputBindingCollection>
                <MouseBinding Gesture="LeftDoubleClick" Command="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" />
              </InputBindingCollection>
            </Setter.Value>
          </Setter>
        </Style>
        """;

    private enum Hook
    {
        GridEventTrigger,
        GridMouseBinding,
        EventArgsConverter,
        RowStyle,
    }

    private enum Target
    {
        /// <summary>3 行目（gamma）の Name のセル。</summary>
        Row,

        /// <summary>Name の列ヘッダー。</summary>
        ColumnHeader,

        /// <summary>縦のスクロールバーのつまみ。</summary>
        ScrollBarThumb,

        /// <summary>縦のスクロールバーの、つまみより下のトラック（ページ送りの RepeatButton）。</summary>
        ScrollBarTrack,

        /// <summary>行が 3 行しか無い DataGrid の、最後の行より下の空白。</summary>
        BlankArea,

        /// <summary>行が 3 行しか無い DataGrid で、どの行も選ばずに、最後の行より下の空白。</summary>
        BlankAreaNothingSelected,

        /// <summary>修飾キーを押さずに、選んでいる 2 行目（beta）の Name のセル。</summary>
        SelectedRow,

        /// <summary>Ctrl を押しながら、選んでいる 2 行目（beta）の Name のセル。</summary>
        CtrlOnSelectedRow,

        /// <summary>Ctrl を押しながら、選んでいない 3 行目（gamma）の Name のセル。</summary>
        CtrlOnUnselectedRow,

        /// <summary>IsReadOnly=False の DataGrid の、3 行目（gamma）の Name のセル。</summary>
        EditableRow,

        /// <summary>DataGrid も列も IsReadOnly=False のときの、選んでいる 2 行目（beta）の Name のセル（1 回目のクリックで編集が始まる）。</summary>
        EditableSelectedRow,

        /// <summary>DataGrid は IsReadOnly=False で、Name 列だけ IsReadOnly=True にしたときの、3 行目（gamma）の Name のセル。</summary>
        EditableGridReadOnlyColumn,

        /// <summary>DataGrid は IsReadOnly=False で、Name 列だけ IsReadOnly=True にしたときの、3 行目（gamma）の City のセル（編集できる列）。</summary>
        EditableGridReadOnlyColumnCityCell,

        /// <summary>右ボタンでの、3 行目（gamma）の Name のセル。</summary>
        RightButtonOnRow,
    }

    private static bool IsEditableGrid(Target target) =>
        target is Target.EditableRow or Target.EditableSelectedRow or Target.EditableGridReadOnlyColumn or Target.EditableGridReadOnlyColumnCityCell;

    /// <summary>
    /// ダブルクリックの 1 回目と 2 回目のクリックの間隔。人が操作するときのように間を空ける（ダブルクリックの時間の既定 500 ms より短い）。
    /// 4 つの入力を間を空けずに 1 回の SendInput で送ると、編集できるセルを選択中の行でダブルクリックしたときに、
    /// 2 つ目のマウスダウンが DataGrid に届かなかった（1 回目のクリックで始まる編集の処理と重なるため）。人の操作では起きない結果を測らないよう、すべて間を空ける。
    /// </summary>
    private static readonly TimeSpan ClickInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>ダブルクリックする行の番号。行の外をダブルクリックするターゲットでは null。</summary>
    private static int? TargetRowIndex(Target target) => target switch
    {
        Target.ColumnHeader or Target.ScrollBarThumb or Target.ScrollBarTrack or Target.BlankArea or Target.BlankAreaNothingSelected => null,
        Target.SelectedRow or Target.CtrlOnSelectedRow or Target.EditableSelectedRow => 1,
        _ => 2,
    };

    private static bool IsReadOnlyNameColumn(Target target) =>
        target is Target.EditableGridReadOnlyColumn or Target.EditableGridReadOnlyColumnCityCell;

    private static bool IsShort(Target target) => target is Target.BlankArea or Target.BlankAreaNothingSelected;

    private static Loc Describe(Hook hook) => hook switch
    {
        Hook.GridEventTrigger => T("EventTrigger on DataGrid + SelectedItem", "DataGrid の EventTrigger ＋ SelectedItem"),
        Hook.GridMouseBinding => T("MouseBinding on DataGrid + SelectedItem", "DataGrid の MouseBinding ＋ SelectedItem"),
        Hook.EventArgsConverter => T("EventTrigger + EventArgsConverter (row item)", "EventTrigger ＋ EventArgsConverter（行のアイテム）"),
        Hook.RowStyle => T("RowStyle + attached property (row item)", "RowStyle ＋ 添付プロパティ（行のアイテム）"),
        _ => throw new ArgumentOutOfRangeException(nameof(hook)),
    };

    private static Loc Describe(Target target) => target switch
    {
        Target.Row => T("cell of gamma (row 3)", "gamma（3 行目）のセル"),
        Target.ColumnHeader => T("Name column header", "Name の列ヘッダー"),
        Target.ScrollBarThumb => T("vertical scroll bar thumb", "縦のスクロールバーのつまみ"),
        Target.ScrollBarTrack => T("vertical scroll bar track below the thumb", "縦のスクロールバーのつまみより下のトラック"),
        Target.BlankArea => T("blank area below the last row (3 rows)", "最後の行より下の空白（3 行）"),
        Target.BlankAreaNothingSelected => T("blank area below the last row (3 rows), nothing selected beforehand", "最後の行より下の空白（3 行、事前に何も選ばない）"),
        Target.SelectedRow => T("cell of beta (selected)", "beta（選択中）のセル"),
        Target.CtrlOnSelectedRow => T("cell of beta (selected) with Ctrl held", "Ctrl を押しながら beta（選択中）のセル"),
        Target.CtrlOnUnselectedRow => T("cell of gamma (not selected) with Ctrl held", "Ctrl を押しながら gamma（未選択）のセル"),
        Target.EditableRow => T("Name cell of gamma, DataGrid and columns IsReadOnly=False", "gamma の Name のセル（DataGrid も列も IsReadOnly=False）"),
        Target.EditableSelectedRow => T("Name cell of beta (selected), DataGrid and columns IsReadOnly=False", "beta（選択中）の Name のセル（DataGrid も列も IsReadOnly=False）"),
        Target.EditableGridReadOnlyColumn => T("Name cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True", "gamma の Name のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True）"),
        Target.EditableGridReadOnlyColumnCityCell => T("City cell of gamma, DataGrid IsReadOnly=False, only the Name column IsReadOnly=True", "gamma の City のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True）"),
        Target.RightButtonOnRow => T("cell of gamma with the right button", "右ボタンで gamma のセル"),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static readonly string[] Names =
    [
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
        "lambda", "mu", "nu", "xi", "omicron", "pi", "rho", "sigma", "tau", "upsilon",
    ];

    private sealed record Page(DataGrid Grid, CustomersViewModel ViewModel);

    /// <summary>ターゲットに合わせて DataGrid を作る（行数、DataGrid の IsReadOnly、Name 列の IsReadOnly）。</summary>
    private static Page Build(Hook hook, Target target) =>
        Build(hook, IsShort(target) ? 3 : 20, readOnly: !IsEditableGrid(target), readOnlyNameColumn: IsReadOnlyNameColumn(target));

    private static Page Build(Hook hook, int rowCount, bool readOnly, bool readOnlyNameColumn = false)
    {
        // clr-namespace の assembly= で Microsoft.Xaml.Behaviors を読めるよう、アセンブリを先に読み込んでおく。
        _ = typeof(Microsoft.Xaml.Behaviors.Interaction);
        string hookXaml = hook switch
        {
            Hook.GridEventTrigger => GridEventTriggerXaml,
            Hook.GridMouseBinding => GridMouseBindingXaml,
            Hook.EventArgsConverter => EventArgsConverterXaml,
            Hook.RowStyle => RowStyleXaml,
            _ => throw new ArgumentOutOfRangeException(nameof(hook)),
        };
        string xaml = GridXaml
            .Replace("{Hook}", hookXaml)
            .Replace("{ReadOnly}", readOnly ? "True" : "False")
            .Replace("{NameColumnReadOnly}", readOnlyNameColumn ? " IsReadOnly=\"True\"" : "");
        var grid = SceneContext.LoadXaml<DataGrid>(xaml, ("b", BehaviorsNamespace), ("local", LocalNamespace));
        var vm = new CustomersViewModel(Names.Take(rowCount).Select((name, i) => new Customer(name, $"City {i + 1}")));
        grid.DataContext = vm;
        return new Page(grid, vm);
    }

    // ---------------------------------------------------------------- 読み取りと操作

    private static string Quote(string? text) => text is null ? "null" : $"\"{text}\"";

    private static string NameOf(object? item) => item is Customer c ? Quote(c.Name) : "null";

    private static DataGridRow RowAt(DataGrid grid, int index) =>
        (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);

    private static DataGridCell NameCell(DataGrid grid, int index) =>
        Descendants(RowAt(grid, index)).OfType<DataGridCell>().First(c => c.Column == grid.Columns[0]);

    private static DataGridCell CityCell(DataGrid grid, int index) =>
        Descendants(RowAt(grid, index)).OfType<DataGridCell>().First(c => c.Column == grid.Columns[1]);

    /// <summary>編集できる DataGrid の計測で、ダブルクリックするセル（IsEditing を読むセル）。</summary>
    private static DataGridCell EditableTargetCell(DataGrid grid, Target target) => target switch
    {
        Target.EditableSelectedRow => NameCell(grid, 1),
        Target.EditableGridReadOnlyColumnCityCell => CityCell(grid, 2),
        _ => NameCell(grid, 2),
    };

    /// <summary>直前のクリックと次のダブルクリックが 1 続きのクリック（ClickCount 3）にならないよう、ダブルクリックの時間より長く待つ。</summary>
    private static Task WaitDoubleClickTimeAsync() =>
        Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 200);

    /// <summary>
    /// 2 行目（beta）を実際のマウスでクリックして選ぶ。<paramref name="target"/> が事前に何も選ばない場所なら、選ばずに同じ時間だけ待つ。
    /// </summary>
    private static async Task PrepareSelectionAsync(Window window, Page page, Target target = Target.Row)
    {
        if (target == Target.BlankAreaNothingSelected)
        {
            if (page.Grid.SelectedItem is not null)
            {
                throw new InvalidOperationException($"何も選んでいないはずが選ばれている（SelectedItem: {NameOf(page.Grid.SelectedItem)}）。");
            }

            await WaitDoubleClickTimeAsync();
            return;
        }

        using (RealMouse.Preserve())
        {
            await RealMouse.ClickAsync(window, NameCell(page.Grid, 1));
        }

        await Capture.SettleAsync(window, 200);
        if (page.Grid.SelectedItem is not Customer { Name: "beta" })
        {
            throw new InvalidOperationException($"beta を選べていない（SelectedItem: {NameOf(page.Grid.SelectedItem)}）。");
        }

        await WaitDoubleClickTimeAsync();
        page.ViewModel.Opened.Clear();
    }

    private static async Task DoubleClickAsync(Window window, Page page, Target target)
    {
        // 前の計測で Ctrl を離せていなければ、修飾キーが押されたまま測ることになるので止める。
        RealKeyboard.EnsureNoStuckKey();
        DataGrid grid = page.Grid;
        using (RealMouse.Preserve())
        {
            switch (target)
            {
                case Target.Row:
                case Target.EditableRow:
                case Target.EditableSelectedRow:
                case Target.EditableGridReadOnlyColumn:
                case Target.EditableGridReadOnlyColumnCityCell:
                    await RealMouse.DoubleClickAsync(window, EditableTargetCell(grid, target), interval: ClickInterval);
                    break;
                case Target.SelectedRow:
                    await RealMouse.DoubleClickAsync(window, NameCell(grid, 1), interval: ClickInterval);
                    break;
                case Target.RightButtonOnRow:
                    await RealMouse.DoubleClickAsync(window, NameCell(grid, 2), button: MouseButton.Right, interval: ClickInterval);
                    break;
                case Target.ColumnHeader:
                    DataGridColumnHeader header = Descendants(grid).OfType<DataGridColumnHeader>().First(h => h.Column == grid.Columns[0]);
                    await RealMouse.DoubleClickAsync(window, header, interval: ClickInterval);
                    break;
                case Target.ScrollBarThumb:
                    ScrollBar bar = Descendants(grid).OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical);
                    if (bar.Visibility != Visibility.Visible || bar.Track?.Thumb is not { ActualHeight: > 0 } thumb)
                    {
                        throw new InvalidOperationException("縦のスクロールバーのつまみが表示されていない。");
                    }

                    await RealMouse.DoubleClickAsync(window, thumb, interval: ClickInterval);
                    break;
                case Target.ScrollBarTrack:
                    Track track = Descendants(grid).OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical).Track;
                    if (track.IncreaseRepeatButton is not { ActualHeight: > 10 } pageDown)
                    {
                        throw new InvalidOperationException("つまみより下のトラック（IncreaseRepeatButton）が表示されていない。");
                    }

                    // 下端の近くを押す。1 回目のクリックでつまみが下へ動いても、2 回目がつまみに当たらないようにする。
                    // 行数や高さを変えて 2 回目がつまみに当たると、トラックの行につまみの結果が残るため、つまみへの押下を数えて止める。
                    var trackPoint = new Point(pageDown.ActualWidth / 2, pageDown.ActualHeight - 4);
                    Thumb thumbOnTrack = track.Thumb;
                    int thumbPresses = 0;
                    var countThumb = new MouseButtonEventHandler((_, _) => thumbPresses++);
                    thumbOnTrack.AddHandler(UIElement.PreviewMouseDownEvent, countThumb, handledEventsToo: true);
                    try
                    {
                        await RealMouse.DoubleClickAsync(window, pageDown, trackPoint, interval: ClickInterval);
                    }
                    finally
                    {
                        thumbOnTrack.RemoveHandler(UIElement.PreviewMouseDownEvent, countThumb);
                    }

                    if (thumbPresses != 0)
                    {
                        throw new InvalidOperationException($"トラックのダブルクリックのうち {thumbPresses} 回がつまみに当たった。");
                    }

                    break;
                case Target.BlankArea:
                case Target.BlankAreaNothingSelected:
                    var point = new Point(60, grid.ActualHeight - 12);
                    EnsureOutsideRows(grid, point);
                    await RealMouse.DoubleClickAsync(window, grid, point, interval: ClickInterval);
                    break;
                case Target.CtrlOnSelectedRow:
                    using (RealKeyboard.Hold(window, VirtualKeyControl))
                    {
                        await RealMouse.DoubleClickAsync(window, NameCell(grid, 1), interval: ClickInterval);
                    }

                    break;
                case Target.CtrlOnUnselectedRow:
                    using (RealKeyboard.Hold(window, VirtualKeyControl))
                    {
                        await RealMouse.DoubleClickAsync(window, NameCell(grid, 2), interval: ClickInterval);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }
        }

        await Capture.SettleAsync(window, 200);
    }

    /// <summary>
    /// DataGrid に届いたマウスダウンの ClickCount を順に記録するハンドラーを足す（観測のためだけで、記事のコードではない）。
    /// 届かなかった回を「実行されない」と記録しないため、計測の後に <see cref="EnsureDoubleClickArrived"/> で確かめる。
    /// </summary>
    private static List<int> RecordClickCounts(DataGrid grid)
    {
        var counts = new List<int>();
        grid.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, e) => counts.Add(e.ClickCount)), handledEventsToo: true);
        return counts;
    }

    /// <summary>
    /// ダブルクリックの 2 つのマウスダウンが DataGrid に届き、WPF に 1 回目・2 回目のクリック（ClickCount が 1、2）として認識されたことを確かめる。
    /// </summary>
    private static string EnsureDoubleClickArrived(List<int> counts, int before, Hook hook, Target target)
    {
        List<int> arrived = counts.Skip(before).ToList();
        string text = string.Join(", ", arrived);
        if (arrived is not [1, 2])
        {
            throw new InvalidOperationException($"{hook} / {target}: ダブルクリックとして認識されていない（ClickCount: {text}）。");
        }

        return text;
    }

    /// <summary>空白をダブルクリックする位置が、行（DataGridRow）の中でも列ヘッダーの中でもないことを確かめる。</summary>
    private static void EnsureOutsideRows(DataGrid grid, Point point)
    {
        DependencyObject? hit = grid.InputHitTest(point) as DependencyObject;
        if (hit is null)
        {
            throw new InvalidOperationException("空白の位置に DataGrid の要素が無い。");
        }

        for (DependencyObject? current = hit; current is not null && current != grid; current = VisualTreeHelper.GetParent(current))
        {
            if (current is DataGridRow or DataGridColumnHeader)
            {
                throw new InvalidOperationException($"空白のつもりの位置が {current.GetType().Name} の中にある。");
            }
        }
    }

    private static string OtherEffects(Page page, Target target) => target switch
    {
        Target.ColumnHeader => $"SortDirection: {page.Grid.Columns[0].SortDirection?.ToString() ?? "null"}",
        _ when IsEditableGrid(target) => $"IsEditing: {EditableTargetCell(page.Grid, target).IsEditing}",
        Target.ScrollBarTrack => $"VerticalOffset > 0: {Descendants(page.Grid).OfType<ScrollViewer>().First().VerticalOffset > 0}",
        _ => "-",
    };

    // ---------------------------------------------------------------- 図

    /// <summary>DataGrid 自身の MouseDoubleClick を拾う書き方で、列ヘッダーをダブルクリックした後の画面。</summary>
    private static async Task ShootHeaderDoubleClickAsync(SceneContext context)
    {
        Page page = Build(Hook.GridEventTrigger, 20, readOnly: true);
        var window = new Window { Title = "Customers", Content = page.Grid, SizeToContent = SizeToContent.WidthAndHeight };
        List<int> clickCounts = RecordClickCounts(page.Grid);
        await context.ShootAsync(window, "header-doubleclick.png", async w =>
        {
            await FrontCenteredAsync(page.Grid);
            await PrepareSelectionAsync(w, page);
            int before = clickCounts.Count;
            await DoubleClickAsync(w, page, Target.ColumnHeader);
            EnsureDoubleClickArrived(clickCounts, before, Hook.GridEventTrigger, Target.ColumnHeader);
            if (page.ViewModel.Opened is not ["beta"])
            {
                throw new InvalidOperationException($"列ヘッダーのダブルクリックで beta が開かれていない（{string.Join(", ", page.ViewModel.Opened)}）。図の前提が崩れている。");
            }
        });
    }

    // ---------------------------------------------------------------- 計測

    private static async Task<IReadOnlyList<Loc>> MeasureTargetAsync(Target target)
    {
        var selected = new List<string>();
        var effects = new List<string>();
        var opened = new List<Loc>();
        foreach (Hook hook in Enum.GetValues<Hook>())
        {
            (string selectedAfter, string effect, string names) = await MeasureAsync(hook, target);
            selected.Add(selectedAfter);
            effects.Add(effect);
            opened.Add(names == NotExecuted ? T("not executed", "実行されない") : names);
        }

        return [Describe(target), Merge(selected), Merge(effects), .. opened];
    }

    /// <summary>書き方によらず同じ値ならその値を、食い違えば書き方ごとの値を並べる。</summary>
    private static Loc Merge(List<string> values) =>
        values.Distinct().Count() == 1
            ? values[0]
            : T(
                string.Join(" / ", values.Select((v, i) => $"{Describe((Hook)i).En}: {v}")),
                string.Join(" / ", values.Select((v, i) => $"{Describe((Hook)i).Ja}: {v}")));

    private static async Task<(string SelectedAfter, string Effect, string Opened)> MeasureAsync(Hook hook, Target target)
    {
        Page page = Build(hook, target);
        List<int> clickCounts = RecordClickCounts(page.Grid);
        (string, string, string)? result = null;
        await ShowAsync(page.Grid, async () =>
        {
            Window window = await FrontCenteredAsync(page.Grid);
            await PrepareSelectionAsync(window, page, target);
            int before = clickCounts.Count;
            await DoubleClickAsync(window, page, target);
            string counts = EnsureDoubleClickArrived(clickCounts, before, hook, target);
            List<string> opened = page.ViewModel.Opened;
            string effects = OtherEffects(page, target);
            result = (
                NameOf(page.Grid.SelectedItem),
                counts == "1, 2" ? effects : $"{effects}, ClickCount: {counts}",
                opened.Count == 0 ? NotExecuted : string.Join(", ", opened.Select(Quote)));
        }, activate: true);

        return result ?? throw new InvalidOperationException("計測できなかった。");
    }

    /// <summary>
    /// 2 回目のクリックで DataGrid に届くものを読む。ハンドラーは観測のためだけに handledEventsToo で足す（記事のコードではない）。
    /// MouseDoubleClick は Control ごとに発生する直接イベントなので、DataGrid に足したハンドラーには DataGrid 自身の分だけが届く。
    /// </summary>
    private static async Task<IReadOnlyList<Loc>> ObserveAsync(Target target)
    {
        Page page = Build(Hook.GridEventTrigger, target);
        bool? downHandled = null;
        string? originalSource = null;
        string parent = "-";
        string editing = "-";
        List<int> clickCounts = RecordClickCounts(page.Grid);
        string counts = "";
        int windowDowns = 0;
        bool rowDoubleClicked = false;
        page.Grid.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ClickCount == 2)
            {
                downHandled = e.Handled;
            }
        }), handledEventsToo: true);
        page.Grid.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler((_, e) =>
        {
            originalSource = e.OriginalSource.GetType().Name;
            parent = e.OriginalSource is Visual visual ? VisualTreeHelper.GetParent(visual)?.GetType().Name ?? "null" : "(not a Visual)";
            if (IsEditableGrid(target))
            {
                editing = EditableTargetCell(page.Grid, target).IsEditing.ToString();
            }
        }), handledEventsToo: true);

        await ShowAsync(page.Grid, async () =>
        {
            Window window = await FrontCenteredAsync(page.Grid);
            await PrepareSelectionAsync(window, page, target);

            // ウィンドウに届いたマウスダウン（DataGrid より外側）と、ダブルクリックした行自身の MouseDoubleClick も読む。
            window.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => windowDowns++), handledEventsToo: true);
            if (TargetRowIndex(target) is int rowIndex)
            {
                RowAt(page.Grid, rowIndex).AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler((_, _) => rowDoubleClicked = true), handledEventsToo: true);
            }

            int before = clickCounts.Count;
            await DoubleClickAsync(window, page, target);
            counts = EnsureDoubleClickArrived(clickCounts, before, Hook.GridEventTrigger, target);
        }, activate: true);

        // ClickCount=2 のマウスダウンが届いた回では、MouseDoubleClick も届いていなければならない。
        if (downHandled is not null && originalSource is null)
        {
            throw new InvalidOperationException($"{target}: ClickCount=2 のマウスダウンは届いたが、MouseDoubleClick が DataGrid に届いていない。");
        }

        return
        [
            Describe(target),
            counts,
            downHandled?.ToString() ?? T("(no ClickCount=2)", "（ClickCount=2 が無い）"),
            originalSource ?? T("(not raised)", "（発生しない）"),
            parent,
            editing,
            windowDowns.ToString(),
            TargetRowIndex(target) is null ? "-" : rowDoubleClicked ? T("raised", "発生する") : T("not raised", "発生しない"),
        ];
    }

    private static string StyleInputBindingsResult()
    {
        try
        {
            SceneContext.LoadXaml<Style>(StyleInputBindingsXaml);
            return "no exception";
        }
        catch (Exception ex)
        {
            Exception inner = ex;
            while (inner.InnerException is not null)
            {
                inner = inner.InnerException;
            }

            // Message は OS の表示言語で変わるため、型と ParamName だけを出す（どの環境で再実行しても同じ表になる）。
            string param = inner is ArgumentException { ParamName: string name } ? $", ParamName: {name}" : "";
            return inner == ex ? ex.GetType().Name : $"{ex.GetType().Name} ({inner.GetType().Name}{param})";
        }
    }
}
