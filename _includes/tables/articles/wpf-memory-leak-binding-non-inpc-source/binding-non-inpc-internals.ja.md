| 項目 | 値 |
|---|---|
| PropertyDescriptor.\_valueChangedHandlers の宣言の型 | System.Collections.Concurrent.ConcurrentDictionary&lt;System.Object, System.EventHandler&gt; |
| 登録がある間の \_valueChangedHandlers の実際の型 | System.Collections.Concurrent.ConcurrentDictionary&lt;System.Object, System.EventHandler&gt; |
| ValueChangedRecord.\_source の宣言の型 | System.Object |
| TimelineViewModel.Title の PropertyDescriptor の具象型 | System.ComponentModel.ReflectPropertyDescriptor |
| 弱参照だけを残して GC した後も、同じ PropertyDescriptor が返る（登録が残っている状態） | True |
| 同上。一度もバインドしていない UnboundViewModel.Title | True |
| ValueChangedRecord のインスタンスフィールド | &lt;HasIgnorableListeners&gt;k\_\_BackingField: System.Boolean, \_eventArgs: MS.Internal.Data.ValueChangedEventArgs, \_listeners: System.Windows.WeakEventManager+ListenerList&lt;MS.Internal.Data.ValueChangedEventArgs&gt;, \_manager: MS.Internal.Data.ValueChangedEventManager, \_pd: System.ComponentModel.PropertyDescriptor, \_source: System.Object |
| OtherViewModel.Name と TimelineViewModel.Title の PropertyDescriptor が同一 | False |
| TextBox.Text の BindsTwoWayByDefault | True |
