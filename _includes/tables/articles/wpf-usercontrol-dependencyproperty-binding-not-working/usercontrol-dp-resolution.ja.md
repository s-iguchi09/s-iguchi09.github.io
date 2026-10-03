| 参照 | 結果 |
|---|---|
| 内側の TextBlock、ElementName=Root | from InfoCard.Title |
| 内側のルートの DataContext = コントロール、続けて &#123;Binding Title&#125; | from InfoCard.Title |
| ContextMenu の MenuItem.Header、AncestorType=UserControl | null |
| ContextMenu の MenuItem.Header、ElementName=Root | null |
| ContextMenu の MenuItem.Header、DataContext を引き継いで &#123;Binding Title&#125; | from InfoCard.Title |
| その場に書いた Popup、AncestorType=UserControl | from InfoCard.Title |
| その場に書いた Popup、ElementName=Root | from InfoCard.Title |
| その場に書いた DataTemplate、AncestorType=UserControl | from InfoCard.Title |
| その場に書いた DataTemplate、ElementName=Root | from InfoCard.Title |
| UserControl.Resources の DataTemplate、AncestorType=UserControl | from InfoCard.Title |
| UserControl.Resources の DataTemplate、ElementName=Root | from InfoCard.Title |
| カードの中に入れ子にした UserControl、AncestorType=UserControl（Path=Tag） | inner UserControl |
