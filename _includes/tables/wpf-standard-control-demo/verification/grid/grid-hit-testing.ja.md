| case | element hit |
|---|---|
| front Grid Background=null, empty area | Behind |
| front Grid Background=Transparent, empty area | Front |
| same cell, no ZIndex (First added first) | Second |
| same cell, First ZIndex=1 | First |
| Inner (ZIndex=100) in nested Grid (ZIndex=0) vs later sibling Outer | Outer |
| Inner (ZIndex=100) in nested Grid (ZIndex=1) vs later sibling Outer | Inner |
