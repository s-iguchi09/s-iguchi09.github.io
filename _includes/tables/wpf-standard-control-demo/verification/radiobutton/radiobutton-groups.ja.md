| 条件 | 計測値 |
|---|---|
| 基底クラス / IsChecked の既定値 | ToggleButton / False |
| チェック済みの RadioButton をもう一度クリック: IsChecked | True |
| GroupName なし、1 つの StackPanel に A-C: 順にすべてクリック -&gt; チェックされたもの | C |
| 別の StackPanel の D-E を A-C の後にクリック -&gt; A-E でチェックされたもの | E; A-C: C |
| 1 つの StackPanel で F と G をそれぞれ Border で包む: 両方クリック -&gt; チェックされたもの | F, G |
| GroupName なし、ItemsControl の ItemTemplate の RadioButton: すべてクリック -&gt; チェックされたもの（Parent） | H, I, J (null) |
| 同じ親: GroupName なしの A、B と GroupName=\"1\" の X、X・A・B の順にクリック -&gt; チェックされたもの | B, X |
| 2 つの GroupBox の GroupName=\"shared\": 両方クリック -&gt; チェックされたもの | L |
| ウィンドウ・別のウィンドウ・Popup の GroupName=\"shared\": すべてクリック -&gt; チェックされたもの | M, N, P |
