| 内容の渡し方 | タブごとの Loaded の回数 | 戻したときに同じインスタンスか |
|---|---|---|
| TabItem.Content に要素 | 最初 1 / 1 / 1、1 -&gt; 2 -&gt; 1 の後: 2 / 2 / 1 | True |
| 同じ条件、最初のタブごとの測定の回数 | 1 / 0 / 0 | - |
| ItemsSource + ContentTemplate | 最初 1 個作成、1 -&gt; 2 -&gt; 1 の後: 1 個作成 | True |
