| ソースの値 | EnumDescriptionConverter | null を確かめない実装 |
|---|---|---|
| OrderStatus.InProgress | \"In progress\" | \"In progress\" |
| OrderStatus.Shipped | \"Shipped\" | NullReferenceException がセッターの呼び出し元へ上がる（Text は \"In progress\" のまま） |
| (OrderStatus)99 | \"99\" | ArgumentNullException がセッターの呼び出し元へ上がる（Text は \"In progress\" のまま） |
| FileAttributes.ReadOnly \| FileAttributes.Hidden | \"ReadOnly, Hidden\" | ArgumentNullException がセッターの呼び出し元へ上がる（Text は \"In progress\" のまま） |
| null | \"\" | NullReferenceException がセッターの呼び出し元へ上がる（Text は \"In progress\" のまま） |
