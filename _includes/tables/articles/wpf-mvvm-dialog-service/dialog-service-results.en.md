| operation | return value | state after the call |
|---|---|---|
| DeleteCommand → OK | Confirm: true | customers: 1 of 2 |
| DeleteCommand → Cancel | Confirm: false | customers: 2 of 2 |
| ImportCommand → Cancel | OpenFile: null | ImportPath: null |
| ImportCommand → type a file name, Open | OpenFile: the full path of the file | ImportPath: the same path |
| EditCommand → type \"x\", OK | ShowDialog: true | Customer.Name: \"Alicex\" |
| EditCommand → type \"x\", Cancel | ShowDialog: false | Customer.Name: \"Alice\" |
| EditCommand → type \"x\", × (close) | ShowDialog: false | Customer.Name: \"Alice\" |
| Window.ShowDialog() itself → × (close) | ShowDialog(): false | DialogResult was never set |
| Customer itself bound → type \"x\", Cancel | ShowDialog: false | Customer.Name: \"Alicex\" |
| DeleteCommand of a view model that is no window\'s DataContext | throws InvalidOperationException | shown windows: 1 → 1, Application.Windows: 1 → 1, customers: 1 → 1 |
| EditCommand of a view model that is no window\'s DataContext | throws InvalidOperationException | shown windows: 1 → 1, Application.Windows: 1 → 1, customers: 1 → 1 |
| Same EditCommand, service that creates the window before finding the owner | throws InvalidOperationException | shown windows: 1 → 1, Application.Windows: 1 → 2, customers: 1 → 1 |
