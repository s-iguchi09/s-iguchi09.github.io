---
layout: article-en
title: "Why a WPF DataGrid Edit Does Not Reach the ViewModel When You Click Save, and How to Use CommitEdit"
seo_title: "WPF DataGrid Edits Lost on Save and How CommitEdit Fixes It"
date: 2026-10-10
category: WPF
excerpt: "Saving from a ToolBar or Menu while a DataGrid cell is in edit mode stores the old value, and CommitEdit() alone leaves the row open. Measured cause and fix."
image: /images/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-toolbar-save-while-editing.png
---

## Overview

Type a value into a `DataGrid` cell, click Save in the toolbar without leaving the cell, and the value that gets saved is the one from before the edit.
The cell on screen still shows the new value, so users report that "it went back after saving".
No exception is thrown, and no binding error appears in the Output window.

The cause is that the `DataGrid` has not committed the edit yet when the save command runs.
Calling a commit in the save handler looks like an easy fix, but the parameterless `CommitEdit()` does not push the value to the source.
On top of that, the commit itself fails for a row that contains invalid input.

This article measures the source value and the editing state when Save is triggered with real mouse and keyboard input while a cell is still being edited.
It then works through these three obstacles in order and shows a save handler that commits the row before saving.

---

## Prerequisites and Environment

- Framework: WPF (.NET Framework 4.0 or later / .NET Core 3.0 or later, where `DataGrid` is part of WPF)
- Scope: `DataGridTextColumn` in a `DataGrid`, a save command placed in a `ToolBar` and a `Menu`, and `DataGrid.CommitEdit`
- Architecture: MVVM (the view model implements `INotifyPropertyChanged` and exposes saving as an `ICommand`)
- Verified on: .NET 10 / Windows 11 (default theme; the Fluent theme was not measured)
- Method: each save input was performed once with OS mouse and keyboard input (`SendInput`).
  When the save command ran, the scene read the keyboard focus, the return value of `CommitEdit`, the source value, the number of `IEditableObject.EndEdit` calls, the result of `ICollectionView.Refresh`, and `IsEditing` of the cell and row.
  Text was typed into the cell through WPF's `InputManager`.
  The measurement is implemented as a scene in `tools/screenshot-capture`.

---

## Problem

The same save command is placed in three places: a menu, a toolbar, and a button outside the toolbar.
The column bindings of the `DataGrid` have no special settings.

```xml
<DockPanel>
  <Menu DockPanel.Dock="Top">
    <MenuItem x:Name="menuSave" Header="Save" Command="{Binding SaveCommand}" />
  </Menu>
  <ToolBar DockPanel.Dock="Top">
    <Button x:Name="toolBarSave" Content="Save" Command="{Binding SaveCommand}" />
  </ToolBar>
  <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="4">
    <Button x:Name="plainSave" Content="Save" Command="{Binding SaveCommand}" Padding="12,2" />
    <TextBlock Text="{Binding LastSaved}" Margin="8,0" VerticalAlignment="Center" />
  </StackPanel>
  <DataGrid x:Name="grid" ItemsSource="{Binding Items}"
            AutoGenerateColumns="False" CanUserAddRows="False">
    <DataGrid.Columns>
      <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
      <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" Width="90" />
    </DataGrid.Columns>
  </DataGrid>
</DockPanel>
```

`SaveCommand` writes the `Name` of the first row to `LastSaved`.
The next screenshot shows the result of changing `Name` in the first row from `alpha` to `edited` and clicking Save in the toolbar while the cell is still being edited.

<figure class="article-figure">
  <img src="/images/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-toolbar-save-while-editing.png" alt="A window after clicking Save in the toolbar while the Name cell in the first row of a DataGrid is still being edited with the text edited. The status at the bottom reads saved: Name = alpha, so the value saved is the one from before the edit." width="402" height="207" loading="lazy">
  <figcaption>The window right after clicking Save in the toolbar with the real mouse on .NET 10 / Windows 11. The cell is still in edit mode and shows <code>edited</code>, but the command read <code>alpha</code>.</figcaption>
</figure>

The next table shows the results for different combinations of save input and save handler.
In rows without a note, the item implements `IEditableObject`.

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-save-while-editing.en.md %}

Measured on .NET 10 / Windows 11. Each row used a new window and one input. "source value" and "EndEdit" are read after the commit called in the save handler, and "Refresh" is the result of calling <code>ICollectionView.Refresh</code> after that.
{: .table-caption}

The button outside the toolbar delivers the typed value, while the toolbar, the menu, and `Ctrl+S` do not.
The following sections fix the failing cases one obstacle at a time.

---

## First Obstacle: Toolbar and Menu Clicks Do Not End the Cell Edit

A `DataGrid` has its own triggers for ending an edit.
According to the official documentation, a cell edit is committed when you move to another cell in the same row or press Enter, and a row edit is committed when you move to another row or press Enter ([DataGrid](https://learn.microsoft.com/dotnet/desktop/wpf/controls/datagrid#editing)).
In the table row for the button outside the toolbar, both the cell edit and the row edit ended when focus moved to that button, and `edited` reached the source.

With the toolbar and the menu, focus also moves to the clicked control for a moment (the "where focus went when it left the DataGrid" column).
Even so, by the time the command ran, focus was back in the cell's `TextBox`, and both the cell and the row were still being edited.
The difference among the three controls is not `Focusable` but the focus scope each one belongs to.

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-save-controls-focus.en.md %}

Read on .NET 10 / Windows 11 by showing the XAML above and calling <code>Focusable</code> and <code>FocusManager.GetFocusScope</code>.
{: .table-caption}

`ToolBar` and `Menu` are focus scopes by default ([FocusManager](https://learn.microsoft.com/dotnet/api/system.windows.input.focusmanager)).
When keyboard focus leaves a focus scope, the focused element in that scope keeps logical focus, and it regains keyboard focus when focus returns to the scope ([Focus Overview](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/focus-overview#logical-focus)).
In this measurement, the `DataGrid` committed the edit when focus moved to another element in the same `Window` scope, and did not commit it when focus only visited the `ToolBar` or `Menu` scope.

When `Ctrl+S` is handled by a `KeyBinding` on the `Window`, focus never leaves the `DataGrid` at all.
As long as committing is left to focus movement, the result depends on how the user triggers Save.
The save handler therefore has to call the commit explicitly.

---

## Second Obstacle: CommitEdit() Does Not Commit the Row

Calling `DataGrid.CommitEdit()` at the start of the save handler returns `True` and ends the cell edit.
However, in the `CommitEdit()` row of the table, the source value stayed `alpha`, `EndEdit` was not called, and the row remained in edit mode.
The result is the same for an item that does not implement `IEditableObject`.

This matches the official documentation.
When a cell is being edited, the parameterless `CommitEdit()` only propagates the cell's change to the pending row and does not commit the row ([DataGrid.CommitEdit](https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.commitedit)).
The source is written when the row edit is committed.

A row left in edit mode causes more than a missing value.
Calling `ICollectionView.Refresh` after saving, for example to re-sort or re-filter the list, throws `InvalidOperationException`.

To commit up to the row, specify `DataGridEditingUnit.Row` as the unit.

```csharp
bool committed = grid.CommitEdit(DataGridEditingUnit.Row, true);
```

The second argument `true` means "exit edit mode after committing".
In the `CommitEdit(DataGridEditingUnit.Row, true)` row of the table, `edited` reached the source, `EndEdit` was called, both the cell and row edits ended, and `Refresh` succeeded.

---

## Third Obstacle: A Row With Invalid Input Cannot Be Committed

With `abc` typed into the `int` column `Quantity`, `CommitEdit(DataGridEditingUnit.Row, true)` returned `False`.
The source `Quantity` stayed at its original `1`, and the cell and row remained in edit mode.

If the save continues without checking the return value, the old value is saved while the cell still shows `abc`.
The same mismatch as in the first obstacle occurs even though a commit was called.
When the return value is `False`, the save must stop and the user must be told about the invalid input.

---

## Complete Implementation

The view model does not know about the `DataGrid`, so the view hands the commit logic to it.
The view model exposes a property that receives the logic to run before saving.

```csharp
public sealed class ItemsViewModel : INotifyPropertyChanged
{
    public ItemsViewModel()
    {
        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<Item> Items { get; } = [];

    public ICommand SaveCommand { get; }

    /// <summary>Commits pending edits in the view before saving. Returns false if they cannot be committed.</summary>
    public Func<bool>? CommitPendingEdits { get; set; }

    private void Save()
    {
        if (CommitPendingEdits?.Invoke() == false)
        {
            StatusMessage = "Rows with invalid input cannot be saved.";
            return;
        }

        // Save Items here.
    }

    // StatusMessage and the INotifyPropertyChanged implementation are omitted.
}
```

`RelayCommand` stands for a typical `ICommand` implementation that runs an `Action`.
When `CommitPendingEdits` is `null` (the view has not set it), the save proceeds without committing.

In the view's code-behind, pass the logic that commits the row.

```csharp
public partial class ItemsWindow : Window
{
    public ItemsWindow(ItemsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CommitPendingEdits = () => grid.CommitEdit(DataGridEditingUnit.Row, true);
    }
}
```

Calling this logic when no cell has been edited also returned `True` (the last row of the table).
It can therefore be called unconditionally on every save.
Because the commit happens inside the command, saving from the toolbar, the menu, `Ctrl+S`, or a regular button gives the same result.

---

## Caveats

- **`UpdateSourceTrigger=PropertyChanged` is not a substitute.**
  Adding it to the column binding updates the source on every keystroke without a commit (the corresponding table row).
  However, the row stays in edit mode, `EndEdit` is not called, and `Refresh` throws `InvalidOperationException`.
  In addition, whether pressing Esc restores the original value depends on how the item is implemented.

{% include tables/articles/wpf-datagrid-edit-not-committed-on-save/datagrid-escape-after-typing.en.md %}

Measured on .NET 10 / Windows 11 by typing <code>edited</code> into Name in the first row and then pressing the real Esc key twice. The original value is <code>alpha</code>.
{: .table-caption}

- **`IEditableObject.EndEdit` is not always called once.**
  In this measurement, committing a row once called `EndEdit` twice.
  If `EndEdit` saves data or raises notifications, make it safe to call more than once.
- **Workarounds that rely on focus movement depend on the save input.**
  Placing the Save button outside the toolbar triggers the commit, but `Ctrl+S` and saving from a menu do not.
- **Commit each `DataGrid` separately.**
  `CommitEdit` is an instance method of `DataGrid`, and it targets the cell and row being edited in that instance.
  `CommitPendingEdits` should combine the results of all `DataGrid` controls on the screen.

---

## Summary

When a `DataGrid` cell is saved while still being edited, the toolbar, the menu, and `Ctrl+S` do not commit the edit, and the value from before the edit is saved.
A button outside the toolbar does commit it, but an implementation whose result depends on the save input should be avoided.

The recommended approach is to call `CommitEdit(DataGridEditingUnit.Row, true)` at the start of the save handler and stop saving when it returns `False`.
The parameterless `CommitEdit()` commits only the cell and does not deliver the value to the source, so it is not suitable for this purpose.
`UpdateSourceTrigger=PropertyChanged` only delivers the source value early and does not end the row edit, so it should not replace the commit.

---

## Related Articles

- [Calling TextBox UpdateSource from the View in WPF: Implementation and Pitfalls](/articles/wpf-textbox-updatesource-from-view-pitfalls/)
- [Controlling When TextBox Input Reaches the Source with UpdateSourceTrigger in WPF](/articles/wpf-textbox-updatesourcetrigger-binding-timing/)
- [Causes of a Stale ICollectionView Filter in WPF and Choosing Between Refresh and Live Filtering](/articles/wpf-collectionviewsource-filter-not-refreshing/)
- [Switching Controls Between Display and Edit Modes in WPF DataGrid Cells](/articles/wpf-datagrid-cell-editing-template/)
- [Why WPF Validation Errors Are Not Displayed, and Choosing Between IDataErrorInfo and INotifyDataErrorInfo](/articles/wpf-validation-error-not-displayed/)
- [DataGrid (WPF Standard Control Demo App)](/apps/wpf-standard-control-demo/datagrid.html): a page that measures editing with F2, Esc, and Enter
