| case | measured |
|---|---|
| local Red: right after SetCurrentValue(White) | \#FFFFFFFF (Local) |
| local Red: SetCurrentValue(White), then Tag = on | \#FFFFFFFF (Local) |
| no local value: right after SetCurrentValue(White) | \#FFFFFFFF (Default) |
| no local value: SetCurrentValue(White), then Tag = on | \#FF008000 (StyleTrigger) |
| TextBox.Text bound OneWay, then Text = \"typed\" in code | binding removed, source.Value = from source |
| TextBox.Text bound TwoWay, then Text = \"typed\" in code | binding kept, source.Value = from source |
| Button with an explicit Style (Foreground only): Template | set (DefaultStyle) |
