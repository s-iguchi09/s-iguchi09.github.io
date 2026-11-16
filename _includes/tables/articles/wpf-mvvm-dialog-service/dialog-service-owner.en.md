| how the dialog is opened | Win32 owner | after main is moved to the top of the z-order | rectangle vs. main | centered on main | main enabled |
|---|---|---|---|---|---|
| Window.ShowDialog(), Owner not set | (none) | dialog below main | entirely inside main | no | disabled |
| DialogService.ShowDialog (Owner = main) | main window | dialog above main | entirely inside main | yes | disabled |
| MessageBox.Show(message, …), no owner argument | main window | dialog above main | does not overlap main | no | disabled |
| DialogService.Confirm (MessageBox.Show(owner, …)) | main window | dialog above main | does not overlap main | no | disabled |
| OpenFileDialog.ShowDialog(), no owner argument | main window | dialog above main | partly overlaps main | no | disabled |
| DialogService.OpenFile (ShowDialog(owner)) | main window | dialog above main | partly overlaps main | no | disabled |
| DialogService.ShowDialog, Search window active | main window | dialog above main | entirely inside main | yes | disabled |
| MessageBox.Show(message, …), no owner argument, Search window active | the other window (Search) | dialog below main | does not overlap main | no | enabled |
| DialogService.Confirm, Search window active | main window | dialog above main | does not overlap main | no | disabled |
| OpenFileDialog.ShowDialog(), no owner argument, Search window active | the other window (Search) | dialog below main | does not overlap main | no | enabled |
| DialogService.OpenFile, Search window active | main window | dialog above main | partly overlaps main | no | disabled |
