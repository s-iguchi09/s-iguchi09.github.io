| binding and input | after the input: displayed / source | binding after the input | value source after the input | displayed after the source is assigned | binding errors |
|---|---|---|---|---|---|
| ItemContainerStyle &#123;Binding IsExpanded&#125;, click the expander button | True / False | removed | Local | True | 0 |
| ItemContainerStyle &#123;Binding IsExpanded&#125;, select the node and press the right arrow key | True / False | kept | Style, expression, current | False | 0 |
| ItemContainerStyle &#123;Binding IsExpanded&#125;, double-click the header | True / False | kept | Style, expression, current | False | 0 |
| ItemContainerStyle &#123;Binding IsExpanded&#125;, set IsChecked of the expander button to True from code | True / False | removed | Local | True | 0 |
| ItemContainerStyle &#123;Binding IsExpanded&#125;, click the expander button, then ClearValue(IsExpanded) | False / False | kept | Style, expression | True | 0 |
| ItemContainerStyle &#123;Binding IsExpanded, Mode=TwoWay&#125;, click the expander button | True / True | kept | Style, expression | False | 0 |
| ItemContainerStyle &#123;Binding IsExpanded, Mode=TwoWay&#125;, select the node and press the right arrow key | True / True | kept | Style, expression | False | 0 |
| ItemContainerStyle &#123;Binding IsExpanded, Mode=TwoWay&#125;, double-click the header | True / True | kept | Style, expression | False | 0 |
| TreeViewItem IsExpanded=\"&#123;Binding Value&#125;\" written on the item, click the expander button | True / False | removed | Local | True | 0 |
| TreeViewItem IsExpanded=\"&#123;Binding Value&#125;\" written on the item, click, then ClearValue(IsExpanded) | False / False | removed | Default | False | 0 |
| Expander IsExpanded=\"&#123;Binding Value&#125;\", click the header | True / True | kept | Local, expression | False | 0 |
| Expander IsExpanded=\"&#123;Binding Value, Mode=OneWay&#125;\", click the header | True / False | removed | Local | True | 0 |
| MenuItem IsCheckable IsChecked=\"&#123;Binding Value&#125;\", click it | True / True | kept | Local, expression | False | 0 |
| CheckBox IsChecked=\"&#123;Binding Value&#125;\", click it | True / True | kept | Local, expression | False | 0 |
| ColumnDefinition Width=\"&#123;Binding Value&#125;\", drag the GridSplitter 60 DIP to the right | 160 / 100 | removed | Local | 160 | 0 |
| ColumnDefinition Width=\"&#123;Binding Value, Mode=TwoWay&#125;\", drag the GridSplitter 60 DIP to the right | 160 / 160 | kept | Local, expression | 100 | 0 |
| check of the error count: TreeViewItem IsExpanded=\"&#123;Binding Missing&#125;\" (no such property), no input | False / False | kept | Local, expression | False | 1 |
