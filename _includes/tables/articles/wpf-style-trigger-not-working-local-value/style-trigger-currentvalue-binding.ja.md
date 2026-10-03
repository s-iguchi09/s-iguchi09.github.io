| 条件 | 計測値 |
|---|---|
| ローカル値 Red: SetCurrentValue(White) の直後 | \#FFFFFFFF (Local) |
| ローカル値 Red: SetCurrentValue(White)、続けて Tag = on | \#FFFFFFFF (Local) |
| ローカル値なし: SetCurrentValue(White) の直後 | \#FFFFFFFF (Default) |
| ローカル値なし: SetCurrentValue(White)、続けて Tag = on | \#FF008000 (StyleTrigger) |
| TextBox.Text を OneWay でバインド、続けてコードで Text = \"typed\" | バインドは外れる、source.Value = from source |
| TextBox.Text を TwoWay でバインド、続けてコードで Text = \"typed\" | バインドは残る、source.Value = from source |
| Style を明示した Button（Foreground だけ）: Template | 設定あり（DefaultStyle） |
