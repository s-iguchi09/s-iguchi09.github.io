| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| Numbers.Append(6) | \[1, 2, 3, 4, 5, 6\] | \[1, 2, 3, 4, 5, 6\] | same |
| Numbers.Prepend(0) | \[0, 1, 2, 3, 4, 5\] | \[0, 1, 2, 3, 4, 5\] | same |
| Numbers.TakeLast(2) | \[4, 5\] | \[4, 5\] | same |
| Numbers.TakeLast(0) | \[\] | \[\] | same |
| Numbers.TakeLast(10) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | same |
| Numbers.TakeLast(-1) | \[\] | \[\] | same |
| Numbers.SkipLast(2) | \[1, 2, 3\] | \[1, 2, 3\] | same |
| Numbers.SkipLast(0) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | same |
| Numbers.SkipLast(10) | \[\] | \[\] | same |
| Numbers.SkipLast(-1) | \[1, 2, 3, 4, 5\] | \[1, 2, 3, 4, 5\] | same |
| empty.TakeLast(2) | \[\] | \[\] | same |
| SkipLast(0) is source | False | False | same |
