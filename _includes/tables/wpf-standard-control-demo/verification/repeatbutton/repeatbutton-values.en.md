| case | measured |
|---|---|
| Delay = -1 / Delay = 0 set from code | ArgumentException / no exception |
| Interval = 0 / Interval = 1 set from code | ArgumentException / no exception |
| demo binding, Delay from TextBox.Text (in order) | \"200\" -&gt; 200, \"-1\" -&gt; 500, \"\" -&gt; 500, \"abc\" -&gt; 500 |
| demo binding, Interval from TextBox.Text (in order) | \"50\" -&gt; 50, \"0\" -&gt; 33 |
| RepeatButtons in a vertical ScrollBar template (their commands) | LineUp, PageUp, PageDown, LineDown |
| RepeatButtons in a horizontal ScrollBar template (their commands) | LineLeft, PageLeft, PageRight, LineRight |
| RepeatButtons in the Slider template (their commands) | DecreaseLarge, IncreaseLarge |
