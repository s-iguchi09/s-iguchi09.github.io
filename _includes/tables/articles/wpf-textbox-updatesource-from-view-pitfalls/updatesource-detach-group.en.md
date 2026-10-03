| case | result |
|---|---|
| TwoWay, TextBox removed from the tree, then UpdateSource() | no exception; Status PathError; source = before |
| OneTime: source changed, then UpdateTarget() | Text before -&gt; before (no call) -&gt; changed (after UpdateTarget) |
| BindingGroup.UpdateSources(), no rule | returns True; source = after |
| BindingGroup.UpdateSources(), rule at RawProposedValue fails | returns False; source = before |
| BindingGroup.UpdateSources(), rule at UpdatedValue fails | returns False; source = after |
