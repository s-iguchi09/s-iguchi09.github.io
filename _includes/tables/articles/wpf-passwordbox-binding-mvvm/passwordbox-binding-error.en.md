| case | measured |
|---|---|
| XAML: Password=\"&#123;Binding Password&#125;\" | XamlParseException: A \'Binding\' cannot be set on the \'Password\' property of type \'PasswordBox\'. A \'Binding\' can only be set on a DependencyProperty of a DependencyObject. |
| PasswordBox is sealed | True |
| has PasswordProperty / SecurePasswordProperty / implements INotifyPropertyChanged | False / False / False |
