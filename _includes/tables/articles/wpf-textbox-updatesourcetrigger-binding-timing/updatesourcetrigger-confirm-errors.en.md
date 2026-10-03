| case | measured |
|---|---|
| default binding, type \"sato\", then Enter on an IsDefault button | in Click: UserName = suzuki, TextBox focused True |
| ErrorsChanged raised on the UI thread | Validation.HasError True (raised on thread UI) |
| ErrorsChanged raised on a background thread | Validation.HasError True (raised on thread other than UI) |
