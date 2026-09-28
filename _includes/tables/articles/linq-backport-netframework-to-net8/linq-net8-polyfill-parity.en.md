| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| Pairs.ToDictionary() | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | same |
| Tuples.ToDictionary() | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | same |
| Pairs.ToDictionary(cmp) | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | same |
| Tuples.ToDictionary(cmp) | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | \[\[apple, 2\], \[fig, 3\], \[pear, 1\]\] | same |
| duplicate key | throws ArgumentException | throws ArgumentException | same |
| case variants, ordinal | \[\[pear, 1\], \[PEAR, 2\]\] | \[\[pear, 1\], \[PEAR, 2\]\] | same |
| case variants, ignore case | throws ArgumentException | throws ArgumentException | same |
| empty.ToDictionary() | \[\] | \[\] | same |
