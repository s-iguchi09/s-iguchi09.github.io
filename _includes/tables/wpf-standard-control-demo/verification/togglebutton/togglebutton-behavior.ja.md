| 条件 | 計測値 |
|---|---|
| 基底クラス / 派生クラス: CheckBox, RadioButton | ButtonBase / ToggleButton, ToggleButton |
| IsChecked: BindsTwoWayByDefault / ToggleButton に GroupName プロパティがあるか | True / False |
| IsThreeState=False、最初は null: 3 回クリック | null -&gt; false -&gt; true -&gt; false |
| IsThreeState=False、最初は false: 3 回クリック | false -&gt; true -&gt; false -&gt; true |
| IsThreeState=False、最初は true: 3 回クリック | true -&gt; false -&gt; true -&gt; false |
| IsThreeState=True、最初は false: 3 回クリック | false -&gt; true -&gt; null -&gt; false |
| 既定のテンプレート: VisualStateGroups / IsChecked のトリガー（値） | 0 / true |
| UI オートメーションの ToggleState | false -&gt; Off, true -&gt; On, null -&gt; Indeterminate |
| ClickMode=Press: ボタンを押した後の IsChecked / 離した後（前は false） | true / true |
| Command、CommandParameter を自身の IsChecked にバインド、false からクリック（OnClick）: Execute の中で | IsChecked true、パラメーター True |
| 同じボタンを UI オートメーションの Toggle で切り替え: Command | 実行されない |
| Popup.IsOpen を IsChecked にバインド（Mode なし）: クリックの後 / ポップアップが閉じた後 | IsChecked true, IsOpen True / IsChecked false |
| Popup.IsOpen: BindsTwoWayByDefault | True |
| StaysOpen=False、実際のクリック（IsOpen, IsChecked）: ボタン / 何も無い所 | True, true / False, false |
| もう一度ボタン / 開いている間にボタン | True, true / True, true |
