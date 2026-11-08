| how the items are applied | longest frame interval ms (median) | longest frame interval min–max of runs ms | until all applied ms (median) | most items applied in one timer tick |
|---|---|---|---|---|
| no updates (baseline) | 18 | 18–26 | - | - |
| Dispatcher.Invoke per item | 24 | 18–26 | 4,476 | - |
| EnableCollectionSynchronization + lock | 29 | 28–33 | 202 | - |
| queue, drain every 100 ms, Add per item | 123 | 105–129 | 213 | 50,000 |
| queue, drain every 100 ms, one Reset | 33 | 20–46 | 110 | 50,000 |
