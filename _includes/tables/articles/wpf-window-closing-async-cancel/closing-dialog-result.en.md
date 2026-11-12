| condition | Closing raised | checks started / completed | ShowDialog() result |
|---|---|---|---|
| ShowDialog, DialogResult = true (no ClosingGuard) | 1 | 0 / 0 | True |
| ShowDialog, DialogResult = true, Cancel then Close() again | 2 | 1 / 1 | False |
| ShowDialog, DialogResult = true, ClosingGuard (true) | 2 | 1 / 1 | True |
