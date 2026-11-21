| ダブルクリックした場所 | 2 つのマウスダウンの ClickCount | DataGrid での MouseLeftButtonDown（ClickCount=2）の Handled | DataGrid.MouseDoubleClick の OriginalSource | その時点の OriginalSource の visual の親 | その時点のダブルクリックしたセルの IsEditing |
|---|---|---|---|---|---|
| gamma（3 行目）のセル | 1, 2 | False | TextBlock | ContentPresenter | - |
| Name の列ヘッダー | 1, 2 | True | DataGridHeaderBorder | Grid | - |
| 縦のスクロールバーのつまみ | 1, 2 | True | Rectangle | Thumb | - |
| 縦のスクロールバーのつまみより下のトラック | 1, 2 | True | Rectangle | RepeatButton | - |
| 最後の行より下の空白（3 行） | 1, 2 | False | ScrollViewer | Border | - |
| Ctrl を押しながら beta（選択中）のセル | 1, 2 | True | TextBlock | ContentPresenter | - |
| Ctrl を押しながら gamma（未選択）のセル | 1, 2 | True | TextBlock | ContentPresenter | - |
| gamma の Name のセル（DataGrid も列も IsReadOnly=False） | 1, 2 | True | TextBlock | null | True |
| beta（選択中）の Name のセル（DataGrid も列も IsReadOnly=False） | 1 | （ClickCount=2 が無い） | （発生しない） | - | - |
| gamma の Name のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | 1, 2 | False | TextBlock | ContentPresenter | False |
| gamma の City のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | 1, 2 | True | TextBlock | null | True |
