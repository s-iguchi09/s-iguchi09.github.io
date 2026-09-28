| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| LeftJoin | \[pear:-, apple:ripe, fig:dry\] | \[pear:-, apple:ripe, fig:dry\] | 一致 |
| RightJoin | \[apple:ripe, fig:dry, -:unknown\] | \[apple:ripe, fig:dry, -:unknown\] | 一致 |
| LeftJoin、一致が 1 件も無い | \[pear:-, apple:-, fig:-\] | \[pear:-, apple:-, fig:-\] | 一致 |
| RightJoin、右側が空 | \[\] | \[\] | 一致 |
| Shuffle、並べ直した結果 | \[apple, fig, pear\] | \[apple, fig, pear\] | 一致 |
| Shuffle、要素数 | 3 | 3 | 一致 |
| empty.Shuffle() | \[\] | \[\] | 一致 |
| Shuffle、8 スレッドを同時に開始: 異なる順序の数 | 8 | 8 | 一致 |
