| XAML を読み込む場所 | 結果 |
|---|---|
| XamlReader、DataContext なし | XamlParseException（内側の例外 InvalidOperationException） |
| DataTemplate、DataContext に有効なパス | 表示: 64x48、UriSource = 今の ImagePath |
| 続けて ImagePath を 64x96 の画像に変更 | 表示: 64x48、UriSource = 前のパス |
