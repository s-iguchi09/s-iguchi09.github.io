| case | measured |
|---|---|
| TextWrapping=NoWrap, width 150, demo text | 1 lines, line 1 has 53 chars, extent 385.01 / viewport 144 |
| TextWrapping=Wrap, width 150, demo text | 4 lines, line 1 has 20 chars, extent 143.4 / viewport 144 |
| TextWrapping=WrapWithOverflow, width 150, demo text | 2 lines, line 1 has 26 chars, extent 190.86 / viewport 144 |
| MinLines=1, empty | height 17.96 |
| MinLines=2, empty | height 17.96 |
| MinLines=4, empty | height 17.96 |
| MinLines=6, empty | height 17.96 |
| MinLines=4, TextWrapping=Wrap, empty | height 17.96 |
| MinLines=4, one line of text | height 17.96 |
| MinLines=4, empty, in a 150 x 300 Grid, Top | height 17.96 |
| MinLines=\"4\" written in XAML (the demo app\'s markup), empty | height 17.96 |
| then Text changed / then FontSize changed | height 65.84 / 71.16 |
| MinLines=4 set after showing: height before / after / + MaxLines=10 | 17.96 / 65.84 / 65.84 |
| MinLines=4 set in a Loaded handler, not in XAML: height | 65.84 |
| MinLines=\"4\" in XAML, and 4 set again in a Loaded handler: height | 17.96 |
| MaxLines=2, 10 lines | height 33.92, vertical bar Visible |
| MaxLines=4, 10 lines | height 65.84, vertical bar Visible |
| MaxLines=6, 10 lines | height 97.77, vertical bar Visible |
| Height=60, 10 lines, VerticalScrollBarVisibility not set | bar Collapsed, extent 159.6 / viewport 58 |
| Height=60, 10 lines, VerticalScrollBarVisibility Auto | bar Visible, extent 159.6 / viewport 58 |
| AcceptsReturn, 10 lines, inside a StackPanel (no height limit) | height 161.6 |
| TextAlignment=Left, \"123\" in width 200 | first character at x 3 |
| TextAlignment=Center, \"123\" in width 200 | first character at x 90.3 |
| TextAlignment=Right, \"123\" in width 200 | first character at x 177.59 |
| TextDecorations=\"Underline, Strikethrough\" | 2 decorations: Underline, Strikethrough |
| ScrollToEnd() from a worker thread | InvalidOperationException |
| ScrollToEnd() on the UI thread: VerticalOffset / scrollable height | 101.6 / 101.6 |
| default template: IsReadOnly triggers / Background, BorderBrush | 0 / \#FFFFFFFF \#FFABADB3 (normal), \#FFFFFFFF \#FFABADB3 (read-only) |
