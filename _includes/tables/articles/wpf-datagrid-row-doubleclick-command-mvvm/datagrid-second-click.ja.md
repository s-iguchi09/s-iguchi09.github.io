| ダブルクリックした場所 | DataGrid での MouseLeftButtonDown（ClickCount=2）の Handled | DataGrid.MouseDoubleClick の OriginalSource | その時点の OriginalSource の visual の親 | その時点のダブルクリックしたセルの IsEditing |
|---|---|---|---|---|
| gamma（3 行目）のセル | False | TextBlock | ContentPresenter | - |
| Name の列ヘッダー | True | DataGridHeaderBorder | Grid | - |
| 縦のスクロールバーのつまみ | True | Rectangle | Thumb | - |
| 縦のスクロールバーのつまみより下のトラック | True | Rectangle | RepeatButton | - |
| 最後の行より下の空白（3 行） | False | ScrollViewer | Border | - |
| Ctrl を押しながら beta（選択中）のセル | True | TextBlock | ContentPresenter | - |
| Ctrl を押しながら gamma（未選択）のセル | True | TextBlock | ContentPresenter | - |
| gamma のセル（IsReadOnly=False） | True | TextBlock | null | True |
| gamma のセル（DataGrid は IsReadOnly=False、Name 列は IsReadOnly=True） | False | TextBlock | ContentPresenter | False |
