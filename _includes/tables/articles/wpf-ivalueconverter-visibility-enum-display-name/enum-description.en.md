| source value | EnumDescriptionConverter | without null checks |
|---|---|---|
| OrderStatus.InProgress | \"In progress\" | \"In progress\" |
| OrderStatus.Shipped | \"Shipped\" | NullReferenceException thrown to the setter\'s caller (Text stays \"Awaiting payment\") |
| (OrderStatus)99 | \"99\" | ArgumentNullException thrown to the setter\'s caller (Text stays \"Awaiting payment\") |
| FileAttributes.ReadOnly \| FileAttributes.Hidden | \"ReadOnly, Hidden\" | ArgumentNullException thrown to the setter\'s caller (Text stays \"Awaiting payment\") |
| null | \"\" | NullReferenceException thrown to the setter\'s caller (Text stays \"Awaiting payment\") |
