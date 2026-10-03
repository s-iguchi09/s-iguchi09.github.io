| 条件 | 計測値 |
|---|---|
| 基底クラス / 既定値: Minimum、Maximum、Value、IsIndeterminate、Orientation | RangeBase / 0, 100, 0, False, Horizontal |
| 幅 200、Minimum 0、Maximum 100、Value 30: 進捗の部分の幅 | 60 |
| 幅 200、Minimum 20、Maximum 120、Value 70: 進捗の部分の幅 | 100 |
| 幅 200、Minimum 0、Maximum 100、Value 0: 進捗の部分の幅 | 0 |
| Minimum = Maximum = 50: 進捗の部分の幅（例外） | 200 （なし） |
| Minimum 50、続けて Maximum = 10: Maximum / Value / 進捗の部分の幅 | 50 / 50 / 200 |
| Maximum 100 で Value = 150: Value / 進捗の部分の幅、続けて Maximum = 200: Value | 100 / 200; 150 |
| Orientation=Vertical、20 x 200、Value 75: 進捗の部分の位置と大きさ | x=0 y=50 w=20 h=150 |
