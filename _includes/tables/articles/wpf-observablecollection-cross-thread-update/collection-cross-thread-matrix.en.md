| collection | countermeasure | result | Count / Items.Count / view notifications |
|---|---|---|---|
| ObservableCollection alone | - | no exception | 1 |
| bound to ItemsControl | - | NotSupportedException | 1 / 1 / 0 |
| bound to ItemsControl | Dispatcher.Invoke | no exception | 1 / 1 / 1 |
| bound to ItemsControl | EnableCollectionSynchronization | no exception | 1 / 1 / 1 |
