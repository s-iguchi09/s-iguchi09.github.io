| 式 | net10.0（組み込み） | net48（ポリフィル） |  |
|---|---|---|---|
| Words.Order() | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| Words.OrderDescending() | \[pear, kiwi, fig, date, apple\] | \[pear, kiwi, fig, date, apple\] | 一致 |
| Order(OrdinalIgnoreCase) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| OrderDescending(ByLength) | \[apple, pear, kiwi, date, fig\] | \[apple, pear, kiwi, date, fig\] | 一致 |
| Order(ByLength)、安定性 | \[fig, pear, kiwi, date, apple\] | \[fig, pear, kiwi, date, apple\] | 一致 |
| Order().ThenByDescending(len) | \[apple, date, fig, kiwi, pear\] | \[apple, date, fig, kiwi, pear\] | 一致 |
| empty.Order() | \[\] | \[\] | 一致 |
| 比較エンジン（NLS か ICU か） | ICU | NLS | 不一致 |
| Order(ja-JP の比較子)、ハイフンとアンダースコア | \[co\_op, co-op, Co-op, coop\] | \[co\_op, coop, co-op, Co-op\] | 不一致 |
| 比較できない要素: Order().ToList() | InvalidOperationException が発生 | ArgumentException が発生 | 不一致 |
| 比較できない要素: Order().First() | ArgumentException が発生 | ArgumentException が発生 | 一致 |
