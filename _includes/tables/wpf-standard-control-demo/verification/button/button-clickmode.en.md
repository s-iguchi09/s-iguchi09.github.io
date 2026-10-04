| case | measured |
|---|---|
| default ClickMode / IsPressed is read-only | Release / True |
| Hover (mouse): pointer enters / pointer leaves | Hover, IsPressed True / (none), IsPressed False |
| Press (mouse): button down / button up | Press, IsPressed True / (none), IsPressed False |
| Release (mouse): button down / button up | (none), IsPressed True / Release, IsPressed False |
| Release (mouse): down on the button, moved off, released outside | IsPressed False after moving off / (none) |
| Release (keyboard): Space down / Space up / Enter down | (none), IsPressed True / Release, IsPressed False / Release |
| Press (keyboard): Space down / Space up / Enter down | Press, IsPressed True / (none), IsPressed False / Press |
| Hover (keyboard): Space down / Space up / Enter down | (none), IsPressed False / (none), IsPressed False / (none) |
