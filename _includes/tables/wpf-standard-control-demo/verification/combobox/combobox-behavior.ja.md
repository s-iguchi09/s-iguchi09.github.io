| 条件 | 計測値 |
|---|---|
| 既定値: IsEditable、IsReadOnly、StaysOpenOnEdit | False, False, False |
| ShouldPreserveUserEnteredPrefix、IsTextSearchEnabled | False, True |
| MaxDropDownHeight（画面の高さ / 3） / IsDropDownOpen が既定で双方向か | 480 (480) / True |
| バインドした CheckBox で開く、Down、Down、Enter: 選ばれた項目 / 開いているか / CheckBox | True、Monday / False / False |
| Text / SelectedValue / SelectedItem.ToString() | Monday / Monday / ScreenshotCapture.Scenes.ComboBoxDemoScene+EnumItem |
| 編集可能、\"tue\" を入力、入力した前方一致を保つ False: Text / SelectedValue | Tuesday / Tuesday |
| 続けて \"xyz\" で上書き: Text / インデックス / SelectedValue | xyz / -1 / null |
| 編集可能、\"tue\" を入力、入力した前方一致を保つ True: Text / SelectedValue | tuesday / Tuesday |
| 編集可能 + IsReadOnly: \"tue\" を入力: Text / インデックス、続けて Down | （空） / -1、Sunday / 0 |
| MaxDropDownHeight 既定値: ドロップダウンの高さ | 141.72 |
| MaxDropDownHeight 100（デモの初期値）: ドロップダウンの高さ | 100 |
| 開いた状態で編集欄を実際にクリック、StaysOpenOnEdit=False: 開いたままか | False |
| 開いた状態で編集欄を実際にクリック、StaysOpenOnEdit=True: 開いたままか | True |
| 一覧を共有、IsSynchronizedWithCurrentItem=True: 最初、1 つ目を 3 にした後 | 0, 0、3, 3 |
| 一覧を共有、IsSynchronizedWithCurrentItem=null: 最初、1 つ目を 3 にした後 | -1, -1、3, -1 |
