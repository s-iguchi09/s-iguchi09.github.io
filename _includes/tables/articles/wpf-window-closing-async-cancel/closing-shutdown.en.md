| condition | Closing raised | checks started / completed | Closed raised | unhandled exception | window after 1.5 s |
|---|---|---|---|---|---|
| Application.Shutdown(), check returns false | 1 | 1 / 0 | before the check completes | none | closed, process ended |
| OnMainWindowClose, another main window closed, false | 1 | 1 / 0 | before the check completes | none | closed, process ended |
| Owner window closed, false | 0 | 0 / 0 | raised (no check started) | none | closed, process ended |
| exit button (ApplicationCommands.Close), false | 1 | 1 / 1 | not raised | none | open |
| exit button (ApplicationCommands.Close), true | 2 | 1 / 1 | after the check completes | none | closed, process ended |
