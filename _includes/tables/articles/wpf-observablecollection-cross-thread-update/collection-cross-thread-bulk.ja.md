| 対策 | バックグラウンドのループ（ms） | await の後のビューへの通知の数 | すべて通知されるまで（ms） |
|---|---|---|---|
| 1 件ごとに Dispatcher.Invoke | 992 | 5,000 | 994 |
| EnableCollectionSynchronization + lock | 6 | 746 | 64 |
