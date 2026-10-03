| コレクション | 対策 | 結果 | Count / Items.Count / ビューへの通知の数 |
|---|---|---|---|
| ObservableCollection だけ | - | 例外なし | 1 |
| ItemsControl にバインド | - | NotSupportedException | 1 / 1 / 0 |
| ItemsControl にバインド | Dispatcher.Invoke | 例外なし | 1 / 1 / 1 |
| ItemsControl にバインド | EnableCollectionSynchronization | 例外なし | 1 / 1 / 1 |
