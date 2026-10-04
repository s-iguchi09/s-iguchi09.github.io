| case | measured |
|---|---|
| defaults: IsEditable, IsReadOnly, StaysOpenOnEdit | False, False, False |
| ShouldPreserveUserEnteredPrefix, IsTextSearchEnabled | False, True |
| MaxDropDownHeight (screen height / 3) / IsDropDownOpen two-way | 480 (480) / True |
| bound CheckBox opens it; Down, Down, Enter: selected / open / CheckBox | True; Monday / False / False |
| Text / SelectedValue / SelectedItem.ToString() | Monday / Monday / ScreenshotCapture.Scenes.ComboBoxDemoScene+EnumItem |
| editable, \"tue\" typed, preserve prefix False: Text / SelectedValue | Tuesday / Tuesday |
| then \"xyz\" typed over it: Text / index / SelectedValue | xyz / -1 / null |
| editable, \"tue\" typed, preserve prefix True: Text / SelectedValue | tuesday / Tuesday |
| editable + IsReadOnly: \"tue\" typed: Text / index; then Down | (empty) / -1; Sunday / 0 |
| MaxDropDownHeight default: drop-down height | 141.72 |
| MaxDropDownHeight 100 (demo start): drop-down height | 100 |
| open, real click in edit box, StaysOpenOnEdit=False: still open | False |
| open, real click in edit box, StaysOpenOnEdit=True: still open | True |
| shared list, IsSynchronizedWithCurrentItem=True: start; first set to 3 | 0, 0; 3, 3 |
| shared list, IsSynchronizedWithCurrentItem=null: start; first set to 3 | -1, -1; 3, -1 |
