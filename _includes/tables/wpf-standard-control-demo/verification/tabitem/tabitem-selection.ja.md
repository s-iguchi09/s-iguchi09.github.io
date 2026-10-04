| 条件 | 計測値 |
|---|---|
| 基底クラス | HeaderedContentControl |
| IsSelected: BindsTwoWayByDefault | True |
| TabItem の TabStripPlacement: 読み取り専用か | True |
| TabControl.TabStripPlacement=Left: 両方のタブの TabItem.TabStripPlacement | Left / Left |
| Tab3.IsSelected = true: SelectedIndex / Tab1\.\.3 の IsSelected | 2 / False, False, True |
| XAML: Tab2 と Tab3 に IsSelected=\"True\": SelectedIndex / Tab1\.\.3 の IsSelected | 1 / False, True, False |
| コード: Tab2、続けて Tab3 を IsSelected=true に: SelectedIndex | 2 |
| Mode なしで IsSelected をバインド、SelectedIndex = 1: Tab1 / Tab2 のソースの値 | False / True |
| 続けて Tab1 のソースを True に: SelectedIndex / Tab1 / Tab2 のソースの値 | 0 / True / False |
| 続けて Tab2 の見出しを実際にマウスでクリック: SelectedIndex / Tab1 / Tab2 のソースの値 | 1 / False / True |
| Tab2 が IsEnabled=False、コードから SelectedIndex = 1: SelectedIndex / 表示される内容 | 1 / Item2 |
| Tab2 が IsEnabled=False、UI オートメーションの ISelectionItemProvider.Select(): 結果 / SelectedIndex | ElementNotEnabledException / 0 |
| Tab2 が IsEnabled=False、見出しを実際にマウスでクリック: SelectedIndex | 0 |
