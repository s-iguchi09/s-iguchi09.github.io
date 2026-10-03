| reference | result |
|---|---|
| inner TextBlock, ElementName=Root | from InfoCard.Title |
| inner root DataContext = the control, then &#123;Binding Title&#125; | from InfoCard.Title |
| ContextMenu MenuItem.Header, AncestorType=UserControl | null |
| ContextMenu MenuItem.Header, ElementName=Root | null |
| ContextMenu MenuItem.Header, &#123;Binding Title&#125; with DataContext delegated | from InfoCard.Title |
| inline Popup, AncestorType=UserControl | from InfoCard.Title |
| inline Popup, ElementName=Root | from InfoCard.Title |
| inline DataTemplate, AncestorType=UserControl | from InfoCard.Title |
| inline DataTemplate, ElementName=Root | from InfoCard.Title |
| DataTemplate in UserControl.Resources, AncestorType=UserControl | from InfoCard.Title |
| DataTemplate in UserControl.Resources, ElementName=Root | from InfoCard.Title |
| UserControl nested inside the card, AncestorType=UserControl (Path=Tag) | inner UserControl |
