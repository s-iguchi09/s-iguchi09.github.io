| 条件 | 計測値 |
|---|---|
| コードから Delay = -1 / Delay = 0 | ArgumentException / 例外なし |
| コードから Interval = 0 / Interval = 1 | ArgumentException / 例外なし |
| デモのバインド、TextBox.Text から Delay へ（入力した順） | \"200\" -&gt; 200, \"-1\" -&gt; 500, \"\" -&gt; 500, \"abc\" -&gt; 500 |
| デモのバインド、TextBox.Text から Interval へ（入力した順） | \"50\" -&gt; 50, \"0\" -&gt; 33 |
| 縦の ScrollBar のテンプレートの中の RepeatButton（そのコマンド） | LineUp, PageUp, PageDown, LineDown |
| 横の ScrollBar のテンプレートの中の RepeatButton（そのコマンド） | LineLeft, PageLeft, PageRight, LineRight |
| Slider のテンプレートの中の RepeatButton（そのコマンド） | DecreaseLarge, IncreaseLarge |
