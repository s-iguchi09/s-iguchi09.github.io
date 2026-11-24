| display | Text before | Text after |
|---|---|---|
| converter: Text=\"&#123;Binding Status, Converter=&#123;StaticResource EnumDescription&#125;&#125;\" | \"Awaiting payment\" | \"Shipped\" |
| view model: StatusText, also notified in the Status setter | \"Awaiting payment\" | \"Shipped\" |
| view model: StatusText, not notified | \"Awaiting payment\" | \"Awaiting payment\" |
