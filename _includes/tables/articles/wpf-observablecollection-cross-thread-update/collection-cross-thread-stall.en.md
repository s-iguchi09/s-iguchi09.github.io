| how the items are applied | longest frame interval ms (median) | longest frame interval min–max of runs ms | until all applied ms (median) | most items applied in one timer tick |
|---|---|---|---|---|
| no updates (baseline) | 18 | 17–23 | - | - |
| Dispatcher.Invoke per item | 23 | 20–25 | 4,507 | - |
| EnableCollectionSynchronization + lock | 33 | 29–39 | 226 | - |
| queue, drain every 100 ms, Add per item | 104 | 99–256 | 200 | 50,000 |
| queue, drain every 100 ms, one Reset | 31 | 22–34 | 111 | 50,000 |
