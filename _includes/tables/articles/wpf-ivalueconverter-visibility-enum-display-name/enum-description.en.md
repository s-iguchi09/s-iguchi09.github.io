| source value | EnumDescriptionConverter | without null checks |
|---|---|---|
| OrderStatus.InProgress | \"In progress\" | \"In progress\" |
| OrderStatus.Shipped | \"Shipped\" | NullReferenceException thrown to the setter\'s caller (Text stays \"In progress\") |
| (OrderStatus)99 | \"99\" | ArgumentNullException thrown to the setter\'s caller (Text stays \"In progress\") |
| FileAttributes.ReadOnly \| FileAttributes.Hidden | \"ReadOnly, Hidden\" | ArgumentNullException thrown to the setter\'s caller (Text stays \"In progress\") |
| null | \"\" | NullReferenceException thrown to the setter\'s caller (Text stays \"In progress\") |
