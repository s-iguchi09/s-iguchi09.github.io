| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| Words.Order() | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| Words.OrderDescending() | \[pear, kiwi, fig, date, apple\] | \[pear, kiwi, fig, date, apple\] | 一致 |
| Order(OrdinalIgnoreCase) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| OrderDescending(ByLength) | \[apple, pear, kiwi, date, fig\] | \[apple, pear, kiwi, date, fig\] | 一致 |
| Order(ByLength), stability | \[fig, pear, kiwi, date, apple\] | \[fig, pear, kiwi, date, apple\] | 一致 |
| Order().ThenByDescending(len) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| empty.Order() | \[\] | \[\] | 一致 |
| comparison engine (NLS or ICU) | ICU | NLS | 不一致 |
| Order(ja-JP comparer), hyphen and underscore | \[co\_op, co-op, Co-op, coop\] | \[co\_op, coop, co-op, Co-op\] | 不一致 |
| non-comparable items: Order().ToList() | throws InvalidOperationException | throws ArgumentException | 不一致 |
| non-comparable items: Order().First() | throws ArgumentException | throws ArgumentException | 一致 |
