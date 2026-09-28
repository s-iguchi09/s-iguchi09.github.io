| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| LeftJoin | \[pear:-, apple:ripe, fig:dry\] | \[pear:-, apple:ripe, fig:dry\] | same |
| RightJoin | \[apple:ripe, fig:dry, -:unknown\] | \[apple:ripe, fig:dry, -:unknown\] | same |
| LeftJoin, no match at all | \[pear:-, apple:-, fig:-\] | \[pear:-, apple:-, fig:-\] | same |
| RightJoin, empty right | \[\] | \[\] | same |
| Shuffle, sorted back | \[apple, fig, pear\] | \[apple, fig, pear\] | same |
| Shuffle, count | 3 | 3 | same |
| empty.Shuffle() | \[\] | \[\] | same |
| Shuffle, 8 threads started together: distinct orders | 8 | 8 | same |
