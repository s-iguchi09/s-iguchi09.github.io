| case | measured |
|---|---|
| ContextMenu.StaysOpen metadata default | True |
| new ContextMenu().StaysOpen | True |
| Popup.StaysOpen metadata default | True |
| own window already in front: SetForegroundWindow(own) | returns True, still in front True |
| another process in front: SetForegroundWindow(own) | returns False, foreground switched False |
