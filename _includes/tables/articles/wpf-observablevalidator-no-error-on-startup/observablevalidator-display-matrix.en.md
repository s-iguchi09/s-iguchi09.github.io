| how the view model validates | on startup (name is empty) | after typing 21 characters | after clearing the input |
|---|---|---|---|
| Toolkit, setter calls SetProperty(ref \_name, value, true) | False | True | True |
| Toolkit \[ObservableProperty\] + \[Required\] / \[MaxLength\] only | False | False | False |
| Toolkit \[ObservableProperty\] + \[NotifyDataErrorInfo\] | False | True | True |
| same, plus ValidateAllProperties() in the constructor | True | True | True |
| Prism BindableBase + ErrorsContainer, setter validates | False | True | True |
| same, plus validation in the constructor | True | True | True |
