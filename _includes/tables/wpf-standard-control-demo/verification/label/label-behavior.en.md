| case | measured |
|---|---|
| base class / Focusable, IsTabStop / Padding / content alignment | ContentControl / False, False / 5,5,5,5 / Left, Top |
| TextBox, Label, TextBox: Tab from the first TextBox goes to | the second TextBox |
| demo Target labels: focus after access key N / A | Name TextBox / Age TextBox |
| Name TextBox, read by a UI Automation client: name / LabeledBy | (empty) / none |
| Age TextBox, AutomationProperties.LabeledBy set by hand: name / LabeledBy | Age(Press Alt+A) / Age(Press Alt+A) |
| Label \"\_Plain\" without Target: focus after access key P | Other (unchanged) |
| Target is a TextBox inside a ToolBar (own focus scope): focus after access key S | the TextBox in the ToolBar |
| string Content on one line: height | 25.96 |
| string Content with a line break: height | 41.92 |
| access key, Target ComboBox / editable ComboBox / DatePicker: focus goes to | ComboBox / TextBox / DatePickerTextBox |
