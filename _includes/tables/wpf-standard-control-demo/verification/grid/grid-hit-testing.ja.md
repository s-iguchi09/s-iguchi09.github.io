| 条件 | 当たった要素 |
|---|---|
| 手前の Grid の Background=null、何も無い所 | Behind |
| 手前の Grid の Background=Transparent、何も無い所 | Front |
| 同じセル、ZIndex なし（First を先に追加） | Second |
| 同じセル、First の ZIndex=1 | First |
| 入れ子の Grid（ZIndex=0）の中の Inner（ZIndex=100）と、後から足した兄弟の Outer | Outer |
| 入れ子の Grid（ZIndex=1）の中の Inner（ZIndex=100）と、後から足した兄弟の Outer | Inner |
