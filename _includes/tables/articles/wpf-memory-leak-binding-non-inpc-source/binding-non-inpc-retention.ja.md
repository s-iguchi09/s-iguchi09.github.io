| ソースとバインド先の要素 | バインド | 開いている間に \_valueChangedHandlers のキーだった回数 | 前の BindingExpression が回収される前に開いた回数 | 10 回後に残るソース | 50 回後 | 100 回後 | 100 回後に残る BindingExpression | 100 回後に残るウィンドウ |
|---|---|---|---|---|---|---|---|---|
| 通知なし、set のあるプロパティ、TextBlock | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 8 | 3 | 20 | 0 | 0 |
| 通知なし、set のあるプロパティ、TextBox（既定で TwoWay） | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 10 | 1 | 5 | 0 | 0 |
| 通知なし、get だけのプロパティ | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | 10 | 1 | 1 | 0 | 0 |
| 通知なし、Mode=OneTime | &#123;Binding Title, Mode=OneTime&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC のビューモデル（PropertyChanged は一度も発行しない） | &#123;Binding Title&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC のビューモデル、List&lt;T&gt;.Count（List&lt;T&gt; を追跡） | &#123;Binding Rows.Count&#125; | 100 / 100 | 97 / 99 | 7 | 11 | 7 | 0 | 0 |
| INPC のビューモデル、List&lt;T&gt; の件数を返す RowCount（List&lt;T&gt; を追跡） | &#123;Binding RowCount&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| INPC のビューモデル、ObservableCollection&lt;T&gt;.Count（コレクションを追跡） | &#123;Binding Rows.Count&#125; | 0 / 100 | 97 / 99 | 0 | 0 | 0 | 0 | 0 |
| 通知なし、1 つの長寿命のソースを全ウィンドウで共有 | &#123;Binding Title&#125; | 100 / 100 | 97 / 99 | -（共有） | -（共有） | -（共有） | 0 | 0 |
