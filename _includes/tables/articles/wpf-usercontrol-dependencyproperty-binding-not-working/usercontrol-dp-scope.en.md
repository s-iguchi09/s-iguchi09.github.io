| binding inside the control | resulting Text | its DataContext |
|---|---|---|
| &#123;Binding Title&#125; | (empty) | PageViewModel |
| RelativeSource Self (on the inner element) | (empty) | PageViewModel |
| RelativeSource AncestorType=UserControl | from InfoCard.Title | PageViewModel |
