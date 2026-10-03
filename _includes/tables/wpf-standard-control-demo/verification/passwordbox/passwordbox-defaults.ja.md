| 項目 | 値 |
|---|---|
| 基底クラス | Control |
| PasswordProperty / PasswordCharProperty があるか | False / True |
| PasswordChar（メタデータの既定値） | \'\*\' (U+002A) |
| MaxLength / SelectionOpacity / IsInactiveSelectionHighlightEnabled | 0 / 0.4 / False |
| 既定のスタイルでの PasswordChar（値の出どころ） | \'●\' (U+25CF, DefaultStyle) |
| CaretBrush / SelectionBrush（値の出どころ） | null (Default) / \#FF0078D7 (Default) |
| 内部のテキストの入れ物 / その文字列のフィールド | PasswordTextContainer / \_password: SecureString |
| Password = \"secret\": Password | String \"secret\" |
| SecurePassword | SecureString、Length 6、読み取り専用 False |
| SecurePassword を 2 回読む: 同じインスタンスか | False |
| XAML の Password=\"PASSWORD\"（デモアプリのマークアップ） | Password \"PASSWORD\" |
