| 条件 | 計測値 |
|---|---|
| CheckBox / RadioButton の基底クラス | ToggleButton / ToggleButton |
| IsChecked: BindsTwoWayByDefault | True |
| Checked / Unchecked / Indeterminate のルーティング | Bubble / Bubble / Bubble |
| VerticalContentAlignment（値の出どころ） | Top (Default) |
| IsThreeState=True、false から開始: 3 回クリックした後の IsChecked | false -&gt; true -&gt; null -&gt; false |
| IsThreeState=False、false から開始: 3 回クリックした後の IsChecked | false -&gt; true -&gt; false -&gt; true |
| IsThreeState=False、null から開始: 3 回クリックした後の IsChecked | null -&gt; false -&gt; true -&gt; false |
| IsThreeState=True、null から開始: 3 回クリックした後の IsChecked | null -&gt; false -&gt; true -&gt; null |
| IsThreeState、bool?（true）にバインド、クリックで null に: IsChecked / ソース | null / null |
| IsThreeState、bool（true）にバインド、クリックで null に: IsChecked / ソース / バインドのエラー | null / true / HasError True |
| フォーカスのある CheckBox で Space を押して離す（前は false） | true |
| ラベルの中央のヒットテスト: 当たった要素 / CheckBox の中か | TextBlock / True |
| 3 行のラベル、VerticalContentAlignment=Top、CheckBox の高さ 200: 記号の y / ラベルの y | 1（高さ 13.1） / -1（高さ 47.88） |
| 3 行のラベル、VerticalContentAlignment=Center、CheckBox の高さ 200: 記号の y / ラベルの y | 93.45（高さ 13.1） / 75.56（高さ 47.88） |
| 3 行のラベル、VerticalContentAlignment=Center、CheckBox の高さ 46.88: 記号の y / ラベルの y | 16.89（高さ 13.1） / -1（高さ 47.88） |
| 親の StackPanel の Checked ハンドラー、子をクリック: ハンドラーの呼び出し回数 | 1 |
| CheckBox の IsChecked にバインドした Button の IsEnabled: CheckBox を実際にクリックする前 / 後 | False / True |
