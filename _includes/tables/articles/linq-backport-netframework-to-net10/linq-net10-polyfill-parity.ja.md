| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| LeftJoin | \[pear:-, apple:ripe, fig:dry\] | \[pear:-, apple:ripe, fig:dry\] | 一致 |
| RightJoin | \[apple:ripe, fig:dry, -:unknown\] | \[apple:ripe, fig:dry, -:unknown\] | 一致 |
| LeftJoin, no match at all | \[pear:-, apple:-, fig:-\] | \[pear:-, apple:-, fig:-\] | 一致 |
| RightJoin, empty right | \[\] | \[\] | 一致 |
| Shuffle, sorted back | \[apple, fig, pear\] | \[apple, fig, pear\] | 一致 |
| Shuffle, count | 3 | 3 | 一致 |
| empty.Shuffle() | \[\] | \[\] | 一致 |
| Shuffle, 8 threads started together: distinct orders | 8 | 8 | 一致 |
