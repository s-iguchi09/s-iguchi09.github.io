| ダブルクリックした場所 | 2 つのマウスダウンの ClickCount | DataGrid での MouseLeftButtonDown（ClickCount=2）の Handled | DataGrid.MouseDoubleClick の OriginalSource | その時点の OriginalSource の visual の親 | その時点のダブルクリックしたセルの IsEditing | ウィンドウに届いたマウスダウンの数 | ダブルクリックした行の MouseDoubleClick |
|---|---|---|---|---|---|---|---|
| gamma（3 行目）のセル | 1, 2 | False | TextBlock | ContentPresenter | - | 2 | 発生する |
| Name の列ヘッダー | 1, 2 | True | DataGridHeaderBorder | Grid | - | 2 | - |
| 縦のスクロールバーのつまみ | 1, 2 | True | Rectangle | Thumb | - | 2 | - |
| 縦のスクロールバーのつまみより下のトラック | 1, 2 | True | Rectangle | RepeatButton | - | 2 | - |
| 最後の行より下の空白（3 行） | 1, 2 | False | ScrollViewer | Border | - | 2 | - |
| Ctrl を押しながら beta（選択中）のセル | 1, 2 | True | TextBlock | ContentPresenter | - | 2 | 発生する |
| Ctrl を押しながら gamma（未選択）のセル | 1, 2 | True | TextBlock | ContentPresenter | - | 2 | 発生する |
| gamma の Name のセル（DataGrid も列も IsReadOnly=False） | 1, 2 | True | TextBlock | null | True | 2 | 発生する |
| beta（選択中）の Name のセル（DataGrid も列も IsReadOnly=False） | 1, 2 | True | TextBoxView | ScrollContentPresenter | True | 2 | 発生する |
| gamma の Name のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | 1, 2 | False | TextBlock | ContentPresenter | False | 2 | 発生する |
| gamma の City のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | 1, 2 | True | TextBlock | null | True | 2 | 発生する |
