| 条件 | 計測値 |
|---|---|
| 基底クラス / TabStripPlacement の既定値 / Dock の最初の値（デモの初期値） | Selector / Top / Left |
| 表示前の SelectedIndex / 表示後 | -1 / 0 |
| デモの \"Format: &#123;0&#125;.\": 表示される文字列 / SelectedContent / SelectedContentStringFormat | Format: Item1. / Item1 / Format: &#123;0&#125;. |
| Tab2（自身の \"Own &#123;0&#125;\"）に切り替え: 表示される文字列 / SelectedContentStringFormat | Format: Item2. / Own &#123;0&#125; |
| 表示前に Tab2 を選択: 表示される文字列 / SelectedContentStringFormat | Own Item2 / Own &#123;0&#125; |
| タブ 2 つ、SelectedIndex = 5: 例外 / SelectedIndex | 例外なし / 0 |
| SelectedIndex = -2: 例外 / SelectedIndex | ArgumentException / 0 |
| Tab2 を選択中、一覧に無い TabItem を SelectedItem に | 1 |
| SelectedIndex = -1: SelectedContent / 表示される文字列 | null / （なし） |
| タブ 3 つ、選択中の Tab2 を削除: SelectedIndex / 選ばれているタブ | 1 / Tab3 |
| Items を埋めてから ItemsSource / ItemsSource を設定してから Items.Add | InvalidOperationException / InvalidOperationException |
| Tab1 の見出しにフォーカス、右矢印キー: SelectedIndex / フォーカスのある見出し | 1 / Tab2 |
