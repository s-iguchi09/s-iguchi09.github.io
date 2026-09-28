| expression | net10.0 (built-in) | net48 (polyfill) |  |
|---|---|---|---|
| Words.Order() | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | same |
| Words.OrderDescending() | \[pear, kiwi, fig, date, apple\] | \[pear, kiwi, fig, date, apple\] | same |
| Order(OrdinalIgnoreCase) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | same |
| OrderDescending(ByLength) | \[apple, pear, kiwi, date, fig\] | \[apple, pear, kiwi, date, fig\] | same |
| Order(ByLength), stability | \[fig, pear, kiwi, date, apple\] | \[fig, pear, kiwi, date, apple\] | same |
| Order().ThenByDescending(len) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | same |
| empty.Order() | \[\] | \[\] | same |
| comparison engine (NLS or ICU) | ICU | NLS | DIFFERS |
| Order(ja-JP comparer), hyphen and underscore | \[co\_op, co-op, Co-op, coop\] | \[co\_op, coop, co-op, Co-op\] | DIFFERS |
| non-comparable items: Order().ToList() | throws InvalidOperationException | throws ArgumentException | DIFFERS |
| non-comparable items: Order().First() | throws ArgumentException | throws ArgumentException | same |
