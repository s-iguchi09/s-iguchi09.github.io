| condition | Closing raised | checks started / completed | Closed raised | unhandled exception | window after 1.5 s |
|---|---|---|---|---|---|
| Cancel first, then Close() (no flag) | 5 (keeps repeating) | 5 / 4 | not raised | none | open |
| + approved flag, Close() directly (300 ms) | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| + approved flag, Close() directly (synchronous) | 1 | 1 / 1 | not raised | InvalidOperationException | open |
| ClosingGuard: InvokeAsync(Close) (synchronous) | 2 | 1 / 1 | after the check completes | none | closed, process ended |
| × twice, no in-progress check | 3 | 2 / 2 | after check 1 completes, before check 2 completes | none | closed |
| × twice, ClosingGuard | 3 | 1 / 1 | after the check completes | none | closed |
| ClosingGuard: check throws, × again | 3 | 2 / 2 | after the check completes | IOException | closed, process ended |
