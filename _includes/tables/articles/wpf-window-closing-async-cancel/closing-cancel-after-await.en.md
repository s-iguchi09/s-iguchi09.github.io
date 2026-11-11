| condition | Closing raised | checks started / completed | Closed raised | unhandled exception | window after 1.5 s |
|---|---|---|---|---|---|
| Cancel after await (check takes 300 ms) | 1 | 1 / 0 | before the check completes | none | closed, process ended |
| Cancel after await (check completes synchronously) | 1 | 1 / 1 | not raised | none | open |
| ClosingGuard, check returns false | 1 | 1 / 1 | not raised | none | open |
| ClosingGuard, check returns true | 2 | 1 / 1 | after the check completes | none | closed, process ended |
