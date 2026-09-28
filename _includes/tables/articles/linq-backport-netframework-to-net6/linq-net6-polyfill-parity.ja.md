| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| Numbers.Chunk(2) | \[\[1, 2\], \[3, 4\], \[5\]\] | \[\[1, 2\], \[3, 4\], \[5\]\] | 一致 |
| Numbers.Chunk(5) | \[\[1, 2, 3, 4, 5\]\] | \[\[1, 2, 3, 4, 5\]\] | 一致 |
| Numbers.Chunk(10) | \[\[1, 2, 3, 4, 5\]\] | \[\[1, 2, 3, 4, 5\]\] | 一致 |
| Chunk(int.MaxValue) | \[\[1, 2, 3\]\] | \[\[1, 2, 3\]\] | 一致 |
| Numbers.Chunk(0) | ArgumentOutOfRangeException が発生 | ArgumentOutOfRangeException が発生 | 一致 |
| Items.MaxBy(Price) | mug | mug | 一致 |
| Items.MinBy(Price) | pen | pen | 一致 |
| 空の int\[\] の MaxBy | InvalidOperationException が発生 | InvalidOperationException が発生 | 一致 |
| 空の Item\[\] の MaxBy | null | null | 一致 |
| Items.DistinctBy(Category) | \[pen, mug\] | \[pen, mug\] | 一致 |
| Words.DistinctBy(Length) | \[pear, apple, fig\] | \[pear, apple, fig\] | 一致 |
