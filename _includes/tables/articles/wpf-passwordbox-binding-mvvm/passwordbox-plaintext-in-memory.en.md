| what the code did | found in memory |
|---|---|
| typed into a PasswordBox only | no |
| read SecurePassword (and disposed it) | no |
| copied SecurePassword to unmanaged memory, then ZeroFreeGlobalAllocUnicode | no |
| read Password once (string, reference dropped) | yes |
| synced to a ViewModel string with the attached property | yes |
| same, then ViewModel.Password = \"\" and GC.Collect | yes |
