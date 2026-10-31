| Source and target element | Binding | Rounds in which it was a key in \_valueChangedHandlers while open | Windows opened before the previous BindingExpression was collected | Sources alive after 10 | after 50 | after 100 | BindingExpressions alive after 100 | Windows alive after 100 |
|---|---|---|---|---|---|---|---|---|
| No INPC, settable property, TextBlock | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 8 | 3 | 20 | 0 | 0 |
| No INPC, settable property, TextBox (TwoWay by default) | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 10 | 1 | 5 | 0 | 0 |
| No INPC, get-only property | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 10 | 1 | 1 | 0 | 0 |
| No INPC, Mode=OneTime | &#123;Binding Title, Mode=OneTime&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC view model (PropertyChanged never raised) | &#123;Binding Title&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC view model, List&lt;T&gt;.Count (tracking the List&lt;T&gt;) | &#123;Binding Rows.Count&#125; | 100 / 100 | 97 / 99 | 7 | 11 | 7 | 0 | 0 |
| INPC view model, RowCount property over a List&lt;T&gt; (tracking the List&lt;T&gt;) | &#123;Binding RowCount&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC view model, ObservableCollection&lt;T&gt;.Count (tracking the collection) | &#123;Binding Rows.Count&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| No INPC, one long-lived source shared by all windows | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | - (shared) | - (shared) | - (shared) | 0 | 0 |
