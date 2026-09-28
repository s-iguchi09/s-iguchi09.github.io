| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| Numbers.Append(6) | \[1, 2, 3, 4, 5, 6\] | \[1, 2, 3, 4, 5, 6\] | 一致 |
| Numbers.Prepend(0) | \[0, 1, 2, 3, 4, 5\] | \[0, 1, 2, 3, 4, 5\] | 一致 |
| Numbers.TakeLast(2) | \[4, 5\] | \[4, 5\] | 一致 |
| Numbers.TakeLast(0) | \[\] | \[\] | 一致 |
| Numbers.TakeLast(10) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | 一致 |
| Numbers.TakeLast(-1) | \[\] | \[\] | 一致 |
| Numbers.SkipLast(2) | \[1, 2, 3\] | \[1, 2, 3\] | 一致 |
| Numbers.SkipLast(0) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | 一致 |
| Numbers.SkipLast(10) | \[\] | \[\] | 一致 |
| Numbers.SkipLast(-1) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | 一致 |
| empty.TakeLast(2) | \[\] | \[\] | 一致 |
| SkipLast(0) が元のシーケンスそのものか | False | False | 一致 |
