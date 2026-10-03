| binding | Switch.Level | reported as | message |
|---|---|---|---|
| path not found | Warning | Error 40 | property not found |
| path not found | Error | Error 40 | property not found |
| path not found | Critical | nothing | - |
| DataContext not set | Warning | nothing | - |
| DataContext not set | Information | Information 10 | DataItem=null |
| DataContext not set, TraceLevel=High | Warning | Warning 71: True, Information 10: False | DataContext is null |
| ConvertBack fails | Warning | Error 7 | ConvertBack cannot convert |
| empty (Validation.Errors)\[0\] | Warning | Error 17 | Cannot get \'Item\[\]\' value |
| validation error raised, then cleared | Warning | shown: nothing; raised: Error 7; cleared: Error 17 | Cannot get \'Item\[\]\' value |
| getter throws | Warning | Error 17 | Cannot get \'&lt;property&gt;\' value |
| binding that resolves | Warning | nothing | - |
