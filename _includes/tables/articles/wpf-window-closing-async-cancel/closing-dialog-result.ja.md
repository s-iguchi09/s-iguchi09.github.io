| 条件 | Closing の回数 | 判定の開始 / 完了 | ShowDialog() の戻り値 |
|---|---|---|---|
| ShowDialog・DialogResult = true（ClosingGuard なし） | 1 | 0 / 0 | True |
| ShowDialog・DialogResult = true・Cancel して Close() で閉じ直す | 2 | 1 / 1 | False |
| ShowDialog・DialogResult = true・ClosingGuard（判定が true） | 2 | 1 / 1 | True |
| ShowDialog・DialogResult = true・ClosingGuard・判定中に Owner を閉じる | 1 | 1 / 1 | 1.5 秒以内に戻らない |
