| case | measured |
|---|---|
| in a UserControl, focus in a TextBox: Esc -&gt; clicked / window still open | IsCancel / True |
| Enter -&gt; clicked (IsDefaulted before the key) | IsDefault (True) |
| focus in a TextBox with AcceptsReturn: Enter -&gt; clicked / line breaks in the text | (none) / 1 |
| focus on another Button: Enter -&gt; clicked (IsDefaulted of the IsDefault button) | Other (False) |
| two IsCancel buttons: Esc -&gt; clicked / keyboard focus | (none) / Cancel 1 |
| Esc again -&gt; clicked / keyboard focus | (none) / Cancel 2 |
| IsCancel in a window shown with ShowDialog: Esc -&gt; window closed / ShowDialog returned | True / False |
