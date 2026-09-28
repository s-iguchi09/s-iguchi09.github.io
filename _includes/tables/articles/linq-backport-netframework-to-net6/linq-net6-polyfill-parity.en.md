| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| Numbers.Chunk(2) | \[\[1, 2\], \[3, 4\], \[5\]\] | \[\[1, 2\], \[3, 4\], \[5\]\] | same |
| Numbers.Chunk(5) | \[\[1, 2, 3, 4, 5\]\] | \[\[1, 2, 3, 4, 5\]\] | same |
| Numbers.Chunk(10) | \[\[1, 2, 3, 4, 5\]\] | \[\[1, 2, 3, 4, 5\]\] | same |
| Chunk(int.MaxValue) | \[\[1, 2, 3\]\] | \[\[1, 2, 3\]\] | same |
| Numbers.Chunk(0) | throws ArgumentOutOfRangeException | throws ArgumentOutOfRangeException | same |
| Items.MaxBy(Price) | mug | mug | same |
| Items.MinBy(Price) | pen | pen | same |
| empty int\[\].MaxBy | throws InvalidOperationException | throws InvalidOperationException | same |
| empty Item\[\].MaxBy | null | null | same |
| Items.DistinctBy(Category) | \[pen, mug\] | \[pen, mug\] | same |
| Words.DistinctBy(Length) | \[pear, apple, fig\] | \[pear, apple, fig\] | same |
