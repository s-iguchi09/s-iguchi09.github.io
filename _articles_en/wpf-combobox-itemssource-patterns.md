---
layout: article-en
title: "WPF ComboBox ItemsSource Binding Patterns and Selected Value Retrieval"
date: 2026-04-26
category: WPF
excerpt: "The correct combination of DisplayMemberPath, SelectedItem, SelectedValue, and SelectedValuePath depends on the element type bound to ItemsSource. This article covers the five main patterns."
---

## Overview

In WPF's `ComboBox`, the configuration of `DisplayMemberPath`, `ItemTemplate`, `SelectedItem`, `SelectedValue`, and `SelectedValuePath` varies depending on the element type of the collection bound to `ItemsSource`. This article organizes the representative binding patterns and explains how to select the appropriate properties for each case.

---

## Prerequisites / Environment

- Framework / Language: .NET 8 / C# 12
- Target control: WPF ComboBox
- Architecture: MVVM (binding through DataContext)
- Prior knowledge: WPF binding basics, `INotifyPropertyChanged`

---

## Problem

When the element type of the collection bound to `ItemsSource` changes, the method for retrieving the selected value also changes. For example, binding a list of strings differs from binding a list of objects that each have an ID and a display name: the ViewModel property type and the required XAML properties are different in each case. Without understanding this distinction, the selected value may not be reflected, or the initial selection may fail to appear.

---

## Cause / Background

The `ComboBox` exposes three selection-related properties:

| Property | Returns | Primary use |
|---|---|---|
| `SelectedItem` | The element itself from `ItemsSource` | Pass the whole object to the ViewModel |
| `SelectedValue` | The value of the property named by `SelectedValuePath` | Retrieve only a specific field such as an ID |
| `SelectedIndex` | The zero-based index of the selected row | Manage position only |

When `ItemsSource` holds strings, `SelectedItem` returns a `string`. When `ItemsSource` holds objects and `SelectedValuePath` is set, `SelectedValue` returns the value of the specified property. The appropriate binding target depends on the data structure, so the configuration must be aligned accordingly.

---

## Solution

Before looking at implementations, choose the selection properties based on the element type of `ItemsSource`:

- If the element is a simple value such as a `string` or an `enum`, use `SelectedItem` to receive the element directly.
- If the element is an object and the entire selected object is needed, use `SelectedItem`.
- If the element is an object and only a specific value such as an ID needs to be held in the ViewModel, use `SelectedValue` together with `SelectedValuePath`.
- To separate the display name from the value stored in the ViewModel, combine `DisplayMemberPath` with `SelectedValuePath`.
- Reserve `SelectedIndex` for cases where the position in the list itself carries meaning; otherwise prefer working with values or objects directly.

This approach keeps the ViewModel property type aligned with the `ComboBox` configuration and reduces missed initial selections or update failures.

---

## Implementation

### Pattern A: String List

When `ItemsSource` is `ObservableCollection<string>`, `DisplayMemberPath` is unnecessary. Binding `SelectedItem` to a `string` property in the ViewModel is sufficient.

```xml
<ComboBox ItemsSource="{Binding Regions}"
          SelectedItem="{Binding SelectedRegion}" />
```

The corresponding ViewModel implementation is as follows.

```csharp
public ObservableCollection<string> Regions { get; } = new()
{
    "Northeast", "Midwest", "South", "West"
};

private string? _selectedRegion;
public string? SelectedRegion
{
    get => _selectedRegion;
    set { _selectedRegion = value; OnPropertyChanged(); }
}
```

Binding `SelectedItem` to a type other than `string` (for example, `int`) results in a type mismatch and the binding will not reflect the selection.

---

### Pattern B: Object List + DisplayMemberPath + SelectedItem

When `ItemsSource` is `ObservableCollection<T>` and the entire selected object is needed in the ViewModel, specify the display property with `DisplayMemberPath` and bind `SelectedItem` to a property of type `T`.

```xml
<ComboBox ItemsSource="{Binding Departments}"
          DisplayMemberPath="Name"
          SelectedItem="{Binding SelectedDepartment}" />
```

The above XAML displays the `Name` property of each `Department` object while binding the entire selected `Department` to `SelectedDepartment`. The model and ViewModel definitions are as follows.

```csharp
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public ObservableCollection<Department> Departments { get; } = new()
{
    new Department { Id = 1, Name = "Sales" },
    new Department { Id = 2, Name = "Engineering" },
    new Department { Id = 3, Name = "Administration" },
};

private Department? _selectedDepartment;
public Department? SelectedDepartment
{
    get => _selectedDepartment;
    set { _selectedDepartment = value; OnPropertyChanged(); }
}
```

After selection, any field such as `SelectedDepartment.Id` or `SelectedDepartment.Name` is accessible. Holding the whole object in the ViewModel makes it straightforward to reference multiple fields later.

---

### Pattern C: Object List + DisplayMemberPath + SelectedValuePath

This pattern applies when only a specific field — such as an ID — needs to be stored in the ViewModel. Specify the desired property name in `SelectedValuePath` and bind `SelectedValue` to a property of the matching type.

```xml
<ComboBox ItemsSource="{Binding Departments}"
          DisplayMemberPath="Name"
          SelectedValuePath="Id"
          SelectedValue="{Binding SelectedDepartmentId}" />
```

The corresponding ViewModel property that holds the selected `Id` is defined as follows.

```csharp
private int _selectedDepartmentId;
public int SelectedDepartmentId
{
    get => _selectedDepartmentId;
    set { _selectedDepartmentId = value; OnPropertyChanged(); }
}
```

If the type of `SelectedValue` does not match the type of the property named by `SelectedValuePath`, the selection will not be reflected. `SelectedItem` and `SelectedValue` can coexist; updating one automatically updates the other.

---

### Pattern D: Custom Display with ItemTemplate

When multiple fields need to appear in a single row, or when an icon is included alongside text, use `ItemTemplate`. Both `DisplayMemberPath` and `ItemTemplate` can be set at the same time, but `ItemTemplate` takes precedence and `DisplayMemberPath` is ignored. For custom display, use `ItemTemplate` only and avoid combining it with `DisplayMemberPath`.

```xml
<ComboBox ItemsSource="{Binding Employees}"
          SelectedItem="{Binding SelectedEmployee}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Id}" Width="40" Foreground="Gray"/>
                <TextBlock Text="{Binding Name}" />
            </StackPanel>
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

The `Employee` model referenced by this XAML is defined as follows.

```csharp
public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

When different layouts are needed for the collapsed selection display versus the expanded dropdown list, use `ContentTemplate` alongside `ItemContainerStyle` instead of relying on `ItemTemplate` alone.

---

### Pattern E: Enum List

For a fixed set of choices defined by an enumeration, generating the collection in the ViewModel with `Enum.GetValues` is more maintainable than using `ObjectDataProvider` in XAML.

The ViewModel generates the enum values as follows.

```csharp
public enum Priority { Low, Medium, High }

public IEnumerable<Priority> Priorities { get; }
    = (Priority[])Enum.GetValues(typeof(Priority));

private Priority _selectedPriority = Priority.Medium;
public Priority SelectedPriority
{
    get => _selectedPriority;
    set { _selectedPriority = value; OnPropertyChanged(); }
}
```

The XAML binds `ItemsSource` and `SelectedItem` to these ViewModel properties as follows.

```xml
<ComboBox ItemsSource="{Binding Priorities}"
          SelectedItem="{Binding SelectedPriority}" />
```

`SelectedItem` is of type `Priority`. Retrieving the underlying integer value via `SelectedValue` and `SelectedValuePath` is possible, but an explicit cast such as `(int)SelectedPriority` expresses the intent more clearly.

---

## Notes

- **Behavior when both `DisplayMemberPath` and `ItemTemplate` are set**  
  When both are set, `ItemTemplate` takes precedence and `DisplayMemberPath` is ignored. To prevent unintended behavior, use `ItemTemplate` when custom display is needed and do not combine it with `DisplayMemberPath`.

- **Setting the initial value for `SelectedValue` correctly**  
  When using `SelectedValuePath`, if the ViewModel's initial value does not exist in `ItemsSource`, the selection state will be empty. Setting `SelectedValue` before `ItemsSource` is assigned can also cause the binding to have no effect. Always assign `ItemsSource` before setting the selected value.

- **`SelectedItem` matching considers `Equals`**  
  `SelectedItem` does not strictly perform reference comparison; it involves `Equals`-based matching. With the default implementation, two separate instances with identical contents will not be considered equal, so setting a different instance as an initial value will not result in a visible selection. To achieve value-based equality, override `Equals` and `GetHashCode` on the target type and implement `IEquatable<T>` if necessary. When selection should be managed by an identifier or code value, use `SelectedValuePath` with `SelectedValue`.

- **Handling `null`**  
  To include a "no selection" option in the list, bind a `null` entry in `ItemsSource`; the `ComboBox` displays it as a blank entry. Use a nullable type (for example, `string?` or `int?`) for the ViewModel property.

---

## Alternatives / Comparison

| Pattern | ItemsSource type | Selected value retrieval | Best suited for |
|---|---|---|---|
| A: String list | `ObservableCollection<string>` | `SelectedItem` (`string`) | Choices are plain labels only |
| B: Object + SelectedItem | `ObservableCollection<T>` | `SelectedItem` (`T`) | Multiple fields needed after selection |
| C: Object + SelectedValuePath | `ObservableCollection<T>` | `SelectedValue` (specified property type) | Only a specific field such as an ID is needed |
| D: ItemTemplate | `ObservableCollection<T>` | `SelectedItem` (`T`) | Multiple fields displayed in a single row |
| E: Enum | `IEnumerable<TEnum>` | `SelectedItem` (`TEnum`) | Selecting from a fixed enumeration |

---

## Summary

The appropriate `ComboBox` implementation pattern is determined by the type bound to `ItemsSource`.

- A string-only list requires nothing more than binding `SelectedItem` to a `string` property.
- An object list where the whole object is needed in the ViewModel uses `DisplayMemberPath` together with `SelectedItem`.
- An object list where only a specific field such as an ID is needed uses `SelectedValuePath` with `SelectedValue`.
- Custom row layouts require `ItemTemplate`; combining it with `DisplayMemberPath` should be avoided.
- Enum values are exposed as a collection in the ViewModel and bound via `SelectedItem` typed to the enum.

Most initial-value failures trace back to either `Equals`-based mismatch or incorrect assignment order relative to `ItemsSource`. Using `SelectedValuePath`, or ensuring the exact same instance is referenced, resolves the majority of these cases.

---

<!-- Related articles -->
<!-- - [WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法](/ja/articles/wpf-combobox-itemssource-patterns) -->
