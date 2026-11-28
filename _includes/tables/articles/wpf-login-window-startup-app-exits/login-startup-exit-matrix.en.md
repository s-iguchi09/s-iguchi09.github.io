|  | MainWindow during login | ShowDialog() result | MainWindow after login closes | Shutdown right after ShowDialog() | IsVisible after Show() | MainWindow after Show() | Main window | Application.Exit triggered by | Process | Unhandled exception |
|---|---|---|---|---|---|---|---|---|---|---|
| OnLastWindowClose (default), Show after login | LoginWindow | True | null | started | false | MainWindow | never shown | login window closed | ends | — |
| OnMainWindowClose from startup | LoginWindow | True | null | started | false | MainWindow | never shown | login window closed | ends | — |
| StartupUri kept, login in OnStartup | LoginWindow | True | null | started | — | — | never shown | login window closed | ends | — |
| main window created before login | MainWindow | True | MainWindow | not started | true | MainWindow | open after 1.5 s | main window closed | ends | — |
| main window created before login, canceled, no Shutdown() | MainWindow | False | MainWindow | not started | — | — | never shown | not raised | keeps running | — |
| main window created before login, canceled → Shutdown() | MainWindow | False | MainWindow | not started | — | — | never shown | Shutdown() | ends | — |
| OnExplicitShutdown → OnMainWindowClose (recommended) | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | main window closed | ends | — |
| recommended, canceled → Shutdown() | LoginWindow | False | null | not started | — | — | never created | Shutdown() | ends | — |
| recommended, login closed with × (WM\_CLOSE) → Shutdown() | LoginWindow | False | null | not started | — | — | never created | Shutdown() | ends | — |
| recommended without assigning MainWindow | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | main window closed | ends | — |
| no MainWindow assignment, another Window created before main | LoginWindow | True | null | not started | true | Window | open after 1.5 s | not raised | keeps running | — |
| MainWindow assigned, another Window created before main | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | main window closed | ends | — |
| OnExplicitShutdown → OnLastWindowClose | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | main window closed | ends | — |
| OnExplicitShutdown → OnLastWindowClose, another Window created | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | not raised | keeps running | — |
| OnExplicitShutdown kept after login | LoginWindow | True | null | not started | true | MainWindow | open after 1.5 s | not raised | keeps running | — |
| OnExplicitShutdown set after ShowDialog returns | LoginWindow | True | null | started | — | — | never created | login window closed | ends | InvalidOperationException |
