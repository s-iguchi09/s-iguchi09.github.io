| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| CountBy(w =&gt; w) | \[\[pear, 2\], \[fig, 1\], \[PEAR, 1\]\] | \[\[pear, 2\], \[fig, 1\], \[PEAR, 1\]\] | same |
| CountBy(w =&gt; w.Length) | \[\[4, 3\], \[3, 1\]\] | \[\[4, 3\], \[3, 1\]\] | same |
| CountBy(w =&gt; w, ignore case) | \[\[pear, 3\], \[fig, 1\]\] | \[\[pear, 3\], \[fig, 1\]\] | same |
| AggregateBy(len, 0, +1) | \[\[4, 3\], \[3, 1\]\] | \[\[4, 3\], \[3, 1\]\] | same |
| AggregateBy(w, \"\", concat) | \[\[4, ppP\], \[3, f\]\] | \[\[4, ppP\], \[3, f\]\] | same |
| Index() | \[(0, pear), (1, fig), (2, pear), (3, PEAR)\] | \[(0, pear), (1, fig), (2, pear), (3, PEAR)\] | same |
| empty.Index() | \[\] | \[\] | same |
| empty.CountBy() | \[\] | \[\] | same |
| CountBy, a null key | throws ArgumentNullException | throws ArgumentNullException | same |
| AggregateBy, a null key | throws ArgumentNullException | throws ArgumentNullException | same |
