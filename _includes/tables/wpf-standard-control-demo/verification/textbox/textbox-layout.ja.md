| 条件 | 計測値 |
|---|---|
| TextWrapping=NoWrap、幅 150、デモの文字列 | 1 行、1 行目は 53 文字、内容の幅 385.01 / 表示幅 144 |
| TextWrapping=Wrap、幅 150、デモの文字列 | 4 行、1 行目は 20 文字、内容の幅 143.4 / 表示幅 144 |
| TextWrapping=WrapWithOverflow、幅 150、デモの文字列 | 2 行、1 行目は 26 文字、内容の幅 190.86 / 表示幅 144 |
| MinLines=1、空 | 高さ 17.96 |
| MinLines=2、空 | 高さ 17.96 |
| MinLines=4、空 | 高さ 17.96 |
| MinLines=6、空 | 高さ 17.96 |
| MinLines=4、TextWrapping=Wrap、空 | 高さ 17.96 |
| MinLines=4、1 行の文字列 | 高さ 17.96 |
| MinLines=4、空、150 x 300 の Grid の中で Top | 高さ 17.96 |
| XAML に MinLines=\"4\"（デモアプリのマークアップ）、空 | 高さ 17.96 |
| 続けて Text を変更 / FontSize を変更 | 高さ 65.84 / 71.16 |
| 表示後に MinLines=4 を設定: 設定前 / 設定後 / MaxLines=10 を足した後の高さ | 17.96 / 65.84 / 65.84 |
| XAML ではなく Loaded ハンドラーで MinLines=4: 高さ | 65.84 |
| XAML に MinLines=\"4\"、Loaded ハンドラーでも 4 を設定: 高さ | 17.96 |
| MaxLines=2、10 行 | 高さ 33.92、縦のバー Visible |
| MaxLines=4、10 行 | 高さ 65.84、縦のバー Visible |
| MaxLines=6、10 行 | 高さ 97.77、縦のバー Visible |
| Height=60、10 行、VerticalScrollBarVisibility 指定なし | バー Collapsed、内容の高さ 159.6 / 表示の高さ 58 |
| Height=60、10 行、VerticalScrollBarVisibility Auto | バー Visible、内容の高さ 159.6 / 表示の高さ 58 |
| AcceptsReturn、10 行、StackPanel の中（高さの制限なし） | 高さ 161.6 |
| TextAlignment=Left、幅 200 に \"123\" | 1 文字目の x 座標 3 |
| TextAlignment=Center、幅 200 に \"123\" | 1 文字目の x 座標 90.3 |
| TextAlignment=Right、幅 200 に \"123\" | 1 文字目の x 座標 177.59 |
| TextDecorations=\"Underline, Strikethrough\" | 装飾 2 個: Underline, Strikethrough |
| ワーカースレッドから ScrollToEnd() | InvalidOperationException |
| UI スレッドで ScrollToEnd(): VerticalOffset / スクロールできる高さ | 101.6 / 101.6 |
| 既定のテンプレート: IsReadOnly のトリガー数 / Background、BorderBrush | 0 / \#FFFFFFFF \#FFABADB3（通常）、\#FFFFFFFF \#FFABADB3（読み取り専用） |
