| how the items are added | VirtualizationMode | SelectedIndex | SelectedItem | vertical offset (items) | indices realized before: same instance / other instance / unrealized | containers realized after: created before / created after the add | Tag set on the selected container | view notifications |
|---|---|---|---|---|---|---|---|---|
| Add per item | Standard | 500 → 500 | item 500 → item 500 | 492 → 492 | 23 / 0 / 0 | 23 / 0 | kept | 1,000 |
| AddRange (one Reset) | Standard | 500 → 500 | item 500 → item 500 | 492 → 492 | 0 / 12 / 11 | 0 / 12 | lost | 1 |
| Add per item | Recycling | 500 → 500 | item 500 → item 500 | 492 → 492 | 12 / 0 / 0 | 12 / 0 | kept | 1,000 |
| AddRange (one Reset) | Recycling | 500 → 500 | item 500 → item 500 | 492 → 492 | 0 / 12 / 0 | 0 / 12 | lost | 1 |
