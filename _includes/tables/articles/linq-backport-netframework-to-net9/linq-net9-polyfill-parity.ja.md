| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| CountBy(w =&gt; w) | \[\[pear, 2\], \[fig, 1\], \[PEAR, 1\]\] | \[\[pear, 2\], \[fig, 1\], \[PEAR, 1\]\] | 一致 |
| CountBy(w =&gt; w.Length) | \[\[4, 3\], \[3, 1\]\] | \[\[4, 3\], \[3, 1\]\] | 一致 |
| CountBy(w =&gt; w)、大文字小文字を区別しない | \[\[pear, 3\], \[fig, 1\]\] | \[\[pear, 3\], \[fig, 1\]\] | 一致 |
| AggregateBy(len, 0, +1) | \[\[4, 3\], \[3, 1\]\] | \[\[4, 3\], \[3, 1\]\] | 一致 |
| AggregateBy(w, \"\", concat) | \[\[4, ppP\], \[3, f\]\] | \[\[4, ppP\], \[3, f\]\] | 一致 |
| Index() | \[(0, pear), (1, fig), (2, pear), (3, PEAR)\] | \[(0, pear), (1, fig), (2, pear), (3, PEAR)\] | 一致 |
| empty.Index() | \[\] | \[\] | 一致 |
| empty.CountBy() | \[\] | \[\] | 一致 |
| CountBy、null のキー | ArgumentNullException が発生 | ArgumentNullException が発生 | 一致 |
| AggregateBy、null のキー | ArgumentNullException が発生 | ArgumentNullException が発生 | 一致 |
