| 条件 | 計測値 |
|---|---|
| XAML: Password=\"&#123;Binding Password&#125;\" | XamlParseException: 型 \'PasswordBox\' の \'Password\' プロパティで \'Binding\' を設定することはできません。\'Binding\' は、DependencyObject の DependencyProperty でのみ設定できます。 |
| PasswordBox が sealed か | True |
| PasswordProperty / SecurePasswordProperty があるか / INotifyPropertyChanged を実装しているか | False / False / False |
