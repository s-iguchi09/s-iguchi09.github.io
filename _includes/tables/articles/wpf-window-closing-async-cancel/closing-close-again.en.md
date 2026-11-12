| condition | Closing raised | checks started / completed | Closed raised | unhandled exception | window after 1.5 s |
|---|---|---|---|---|---|
| Cancel first, then Close() (no flag) | 5 (keeps repeating) | 5 / 4 | not raised | none | open |
| + approved flag, Close() directly (300 ms) | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| + approved flag, Close() directly (synchronous) | 1 | 1 / 1 | not raised | InvalidOperationException | open |
| ClosingGuard: BeginInvoke(Close) (synchronous) | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| × twice, no in-progress check | 3 | 2 / 2 | after check 1 completes, before check 2 completes | none | closed |
| × twice, ClosingGuard | 3 | 1 / 1 | after the check completes | none | closed |
| ClosingGuard: check throws, × again | 3 | 2 / 2 | after the check completes | IOException | closed, process ended |
| ClosingGuard: DataContext replaced during the check (true) | 1 | 1 / 1 | not raised | none | open |
| DataContext replaced during the check, no identity check (true) | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| ClosingGuard: DataContext set to null during the check, × again (true) | 2 | 1 / 1 | not raised | none | open |
| ClosingGuard: DataContext replaced and Close() called before closing again (synchronous) | 2 | 1 / 1 | not raised | none | open |
| ClosingGuard: another handler cancels closing again, × again | 4 | 2 / 2 | after the check completes | none | closed, process ended |
| ClosingGuard: a later Closing handler runs a nested message loop, × again (synchronous) | 3 | 2 / 2 | after the check completes | none | closed, process ended |
| ClosingGuard: nested loop (500 ms) longer than the check (300 ms), × again | 3 | 2 / 2 | after the check completes | none | closed, process ended |
| ClosingGuard: an earlier Closing handler replaces DataContext while closing again (new check false) | 2 | 2 / 2 | not raised | none | open |
| ClosingGuard: a later Closing handler replaces DataContext while closing again (new check false) | 2 | 2 / 2 | not raised | none | open |
| same as above, without re-subscribing before closing again | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| ClosingGuard: Close() before Show, closing again cancelled, then shown and × (true) | 4 | 2 / 2 | after the check completes | none | closed, process ended |
