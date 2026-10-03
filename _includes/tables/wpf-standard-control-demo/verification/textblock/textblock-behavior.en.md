| case | measured |
|---|---|
| base class / Focusable / Padding / TextWrapping, TextTrimming | FrameworkElement / False / 0,0,0,0 / NoWrap, None |
| Inlines \"Hello \" + Bold \"world\": Text before / after layout | \"\" / \"\" |
| then Text = \"Replaced\": exception / Inlines count | no exception / 1 |
| Run with bound Text after Bold \"Name: \": Run.Text / after change | \"bound\" / \"changed\" |
| \"Say Supercalifragilisticexpialidocious now\", width 100: lines | NoWrap 1, clipped / Wrap 4 / WrapWithOverflow 3, clipped |
| CharacterEllipsis: width in Grid 100 / horizontal StackPanel 100 | 100 / 220.43 |
| Padding=10: size without / with | 61.57 x 15.96 / 81.57 x 35.96 |
