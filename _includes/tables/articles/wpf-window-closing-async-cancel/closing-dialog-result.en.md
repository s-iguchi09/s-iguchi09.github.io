| condition | Closing raised | checks started / completed | ShowDialog() returned |
|---|---|---|---|
| ShowDialog, DialogResult = true (no ClosingGuard) | 1 | 0 / 0 | True |
| ShowDialog, DialogResult = true, Cancel then Close() again | 2 | 1 / 1 | False |
| ShowDialog, DialogResult = true, ClosingGuard (true) | 2 | 1 / 1 | True |
| ShowDialog, DialogResult = true, ClosingGuard, Owner closed during the check | 1 | 1 / 1 | not returned within 1.5 s |
