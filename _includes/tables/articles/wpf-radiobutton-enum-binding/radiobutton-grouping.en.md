| case | measured |
|---|---|
| runtime | net48: .NET Framework 4.8.9345.0 / net10.0-windows: .NET 10.0.10 |
| GroupName default | (empty) |
| no GroupName: checked | Single |
| no GroupName: ConvertBack(false) | 1 (Standard) |
| no GroupName: ViewModel | Standard / Single |
| GroupName set: checked | Standard + Single |
| GroupName set: ConvertBack(false) | 0 |
| GroupName set: ViewModel | Standard / Single |
| GroupName set: select Fine | ConvertBack(true) 1, (false) 0; checked Fine + Single; ViewModel Fine / Single |
| GroupName set: select Draft | checked Draft + Single; ViewModel Draft / Single |
| no GroupName, one StackPanel per enum | checked Standard + Single; ConvertBack(false) 0 |
| GroupName=\'quality\' in two Borders | checked B:Standard |
| ConverterParameter=Draft (string): checked | Single |
| ConverterParameter=Draft (string): select Fine | checked Single; ViewModel Fine / Single |
| same GroupName, two ViewModels: ConvertBack(false) | 1 (Standard) |
| unbound RadioButton in the group: ConvertBack(false) | 1 (Standard) |
| ConvertBack throws NotImplementedException | ConvertBack(false) 1; NotImplementedException thrown to the caller |
| ConvertBack returns UnsetValue (FallbackValue=True) | Standard unchecked; error \"Value \'False\' could not be converted.\"; source Standard / Single |
| ConvertBack returns parameter for false (no GroupName) | checked Standard + Single; ConvertBack(false) 1 |
| bool wrapper properties (no GroupName) | checked Standard + Single; setter(false) 1 |
| Grid, different cells | checked B (both set True) |
| GroupBox Header and Content | checked B (both set True) |
| ItemsControl, RadioButtons in Items | checked B (both set True) |
