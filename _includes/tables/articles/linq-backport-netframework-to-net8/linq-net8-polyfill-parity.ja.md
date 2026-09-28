| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| Pairs.ToDictionary() | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | 一致 |
| Tuples.ToDictionary() | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | 一致 |
| Pairs.ToDictionary(cmp) | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | 一致 |
| Tuples.ToDictionary(cmp) | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | 一致 |
| キーの重複 | ArgumentException が発生 | ArgumentException が発生 | 一致 |
| 大文字小文字だけ違うキー、序数比較 | \[\[pear, 1\], \[PEAR, 2\]\] | \[\[pear, 1\], \[PEAR, 2\]\] | 一致 |
| 大文字小文字だけ違うキー、大文字小文字を区別しない | ArgumentException が発生 | ArgumentException が発生 | 一致 |
| empty.ToDictionary() | \[\] | \[\] | 一致 |
