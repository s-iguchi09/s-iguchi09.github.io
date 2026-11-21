| ダブルクリックした場所 | ダブルクリック後の SelectedItem | そのほかの変化 | DataGrid の EventTrigger ＋ SelectedItem | DataGrid の MouseBinding ＋ SelectedItem | EventTrigger ＋ EventArgsConverter（行のアイテム） | RowStyle ＋ 添付プロパティ（行のアイテム） |
|---|---|---|---|---|---|---|
| gamma（3 行目）のセル | \"gamma\" | - | \"gamma\" | \"gamma\" | \"gamma\" | \"gamma\" |
| Name の列ヘッダー | \"beta\" | SortDirection: Descending | \"beta\" | 実行されない | 実行されない | 実行されない |
| 縦のスクロールバーのつまみ | \"beta\" | - | \"beta\" | 実行されない | 実行されない | 実行されない |
| 縦のスクロールバーのつまみより下のトラック | \"beta\" | VerticalOffset &gt; 0: True | \"beta\" | 実行されない | 実行されない | 実行されない |
| 最後の行より下の空白（3 行） | \"beta\" | - | \"beta\" | \"beta\" | 実行されない | 実行されない |
| 最後の行より下の空白（3 行、事前に何も選ばない） | null | - | 実行されない | 実行されない | 実行されない | 実行されない |
| beta（選択中）のセル | \"beta\" | - | \"beta\" | \"beta\" | \"beta\" | \"beta\" |
| Ctrl を押しながら beta（選択中）のセル | \"beta\" | - | \"beta\" | 実行されない | \"beta\" | \"beta\" |
| Ctrl を押しながら gamma（未選択）のセル | \"beta\" | - | \"beta\" | 実行されない | \"gamma\" | \"gamma\" |
| gamma の Name のセル（DataGrid も列も IsReadOnly=False） | \"gamma\" | IsEditing: True | \"gamma\" | 実行されない | 実行されない | \"gamma\" |
| beta（選択中）の Name のセル（DataGrid も列も IsReadOnly=False） | \"beta\" | IsEditing: True | \"beta\" | 実行されない | \"beta\" | \"beta\" |
| gamma の Name のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | \"gamma\" | IsEditing: False | \"gamma\" | \"gamma\" | \"gamma\" | \"gamma\" |
| gamma の City のセル（DataGrid は IsReadOnly=False、Name 列だけ IsReadOnly=True） | \"gamma\" | IsEditing: True | \"gamma\" | 実行されない | 実行されない | \"gamma\" |
| 右ボタンで gamma のセル | \"gamma\" | - | \"gamma\" | 実行されない | 実行されない | 実行されない |
