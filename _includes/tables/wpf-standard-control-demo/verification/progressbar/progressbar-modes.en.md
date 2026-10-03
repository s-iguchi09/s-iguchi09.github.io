| case | measured |
|---|---|
| IsIndeterminate=True, Value 30: indicator width / visual state | 200 / CommonStates: Indeterminate |
| animation running (template changes in 300 ms): visible / Collapsed / determinate | True / False / False |
| Value set from a worker thread | InvalidOperationException |
| Progress&lt;double&gt; made on the UI thread, Report(40) from a worker: callback on the UI thread / Value | True / 40 |
| UI Automation RangeValue pattern: determinate / IsIndeterminate=True | value 30, IsReadOnly True / not supported |
