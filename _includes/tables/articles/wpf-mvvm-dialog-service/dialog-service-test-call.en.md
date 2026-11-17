| view model | windows the thread had open at 1 s | Execute returned within 1 s | customers left (of 2) |
|---|---|---|---|
| calls MessageBox.Show directly | message box (\#32770) | no (returned after OK (IDOK) was sent from outside) | 1 |
| IDialogService (test double returns true) | (already returned) | yes | 1 |
| IDialogService (test double returns false) | (already returned) | yes | 2 |
