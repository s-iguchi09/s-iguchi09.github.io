| 条件 | 計測値 |
|---|---|
| ContextMenu.StaysOpen のメタデータの既定値 | True |
| new ContextMenu().StaysOpen | True |
| Popup.StaysOpen のメタデータの既定値 | True |
| 自分のウィンドウが既に前面: SetForegroundWindow(自分) | 戻り値 True、前面のまま True |
| 別のプロセスが前面: SetForegroundWindow(自分) | 戻り値 False、前面が切り替わった False |
