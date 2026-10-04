| case | measured |
|---|---|
| Maximum 100, Value = 150; then Maximum = 200 | Value 100; then 150 |
| Minimum = 50, Maximum = 10 | no exception; effective Maximum 50, Value 50 |
| then Maximum = 80 | Maximum 80 |
| 0\.\.10 to 100\.\.200, Minimum first | no exception; Minimum 100, Maximum 200, Value 100 |
| 0\.\.10 to 100\.\.200, Maximum first | no exception; Minimum 100, Maximum 200, Value 100 |
| source 150 bound TwoWay to Value (Maximum 100) | slider Value 100, source 150 |
