| 条件 | 計測値 |
|---|---|
| Maximum 100、Value = 150、続けて Maximum = 200 | Value 100、続けて 150 |
| Minimum = 50、Maximum = 10 | 例外なし、実効の Maximum 50、Value 50 |
| 続けて Maximum = 80 | Maximum 80 |
| 0\.\.10 から 100\.\.200 へ、Minimum が先 | 例外なし; Minimum 100, Maximum 200, Value 100 |
| 0\.\.10 から 100\.\.200 へ、Maximum が先 | 例外なし; Minimum 100, Maximum 200, Value 100 |
| ソースの 150 を Value に TwoWay でバインド（Maximum 100） | Slider の Value 100、ソース 150 |
