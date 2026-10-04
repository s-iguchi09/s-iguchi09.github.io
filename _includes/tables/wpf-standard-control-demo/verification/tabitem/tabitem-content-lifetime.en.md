| how the content is given | Loaded count per tab | same instance after switching back |
|---|---|---|
| elements as TabItem.Content | at start 1 / 1 / 1; after 1 -&gt; 2 -&gt; 1: 2 / 2 / 1 | True |
| same, measure calls per tab at start | 1 / 0 / 0 | - |
| ItemsSource + ContentTemplate | at start 1 created; after 1 -&gt; 2 -&gt; 1: 1 instances created | True |
