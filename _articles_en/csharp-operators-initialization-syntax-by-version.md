---
layout: article-en
title: "C# Operators and Initialization Syntax by Version"
date: 2026-06-22
category: C#
image: /images/articles/csharp-operators-initialization-syntax-by-version/csharp-net-framework-matrix.svg
excerpt: "A version-by-version guide to C# operators and initialization syntax, built from actual compilation against net48. Shows which constructs need only LangVersion, which need BCL types, and how to supply the missing ones."
---

## Overview

This article covers the C# operators and initialization syntax sugar introduced across versions from C# 1.0 through C# 12.
It explains which features are available in .NET Framework, which ones depend only on `LangVersion`, and which ones require additional BCL types or attributes.
The goal is to help with implementation decisions when newer syntax cannot be used in a legacy build environment.

---

## Prerequisites / Environment

- Language: C# 1.0 to C# 12
- Frameworks: .NET Framework 2.0 to 4.8 / .NET Core / .NET 5 and later
- Target features: null-safe operators, index and range operators, type operators, and initialization syntax sugar
- Verification environment: .NET SDK 10.0.302 / .NET Framework 4.8.9337.0 / Windows 11

The tables in this article record **what actually compiled**.
Each construct was compiled against `net10.0` with `LangVersion` lowered step by step to find the minimum the compiler accepts, and against `net48` to determine whether the BCL supplies the types it needs.
For `^` and `..`, the compiled output was also executed on .NET Framework to confirm the returned values.

That process surfaced **three discrepancies that documentation alone does not reveal**. Each is called out where it applies.

---

## Problem

In C# development targeting .NET Framework, a project may be compiled with a language version earlier than C# 8.0.
In that case, syntax such as `??=` cannot be used even if the target framework itself is otherwise capable of running the code.

```csharp
private List<int> _numbers;

public void AddNumber(int val)
{
    _numbers ??= new List<int>(); // Compilation error in environments that compile below C# 8.0
    _numbers.Add(val);
}
```

This becomes an issue when newer syntax is used in source code but the compiler is configured with an older `LangVersion`.

---

## Cause / Background

C# language version support is independent of the target framework.
Feature availability depends mainly on two factors:

1. The compiler and `LangVersion`.
2. Runtime-side requirements such as BCL types or attributes.

As a result, even when targeting .NET Framework, language features such as `??=` and `!` can be used if the build environment supports C# 8.0.

The following table summarizes the operators and syntax covered in this article and the C# version in which they were introduced.
Plotted over time, they line up as follows.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/csharp-operators-initialization-syntax-by-version/csharp-operator-timeline.svg" alt="A timeline from C# 1.0 to 12.0 with the operators and syntax added in each version. The chips for ^, .., init, with, and required are shaded to mark that they need a BCL type or attribute." width="900" height="300" loading="lazy">
  <figcaption>The C# version that introduced each construct. The shaded chips need more than a higher <code>LangVersion</code>: they depend on a BCL type or attribute. <code>??=</code> sits at C# 8.0. The shading matches the compilation results in the next section.</figcaption>
</figure>

| Operator / Syntax | C# Version | .NET Version | .NET Framework Support |
| --- | --- | --- | --- |
| `??` (null-coalescing) | C# 2.0 | .NET Framework 2.0 | ✅ Supported from 2.0 onward |
| `as` (type cast) | C# 1.0 | .NET Framework 1.0 | ✅ All versions |
| `is` (type check) | C# 1.0 | .NET Framework 1.0 | ✅ All versions |
| `=>` (lambda) | C# 3.0 | .NET Framework 3.5 | ✅ Language feature only (†1) |
| `=>` (expression-bodied members) | C# 6.0 | .NET Framework 4.6 | ✅ Language feature only (†1) |
| `?.` `?[]` (null-conditional) | C# 6.0 | .NET Framework 4.6 | ✅ Language feature only (†1) |
| `nameof` | C# 6.0 | .NET Framework 4.6 | ✅ Language feature only (†1) |
| `is` pattern matching | C# 7.0 | .NET Framework 4.7 | ✅ Language feature only (†1) |
| `??=` (null-coalescing assignment) | C# 8.0 | .NET Core 3.0 / .NET 5 | ✅ Language feature only (†1) |
| `!` (null-forgiving) | C# 8.0 | .NET Core 3.0 / .NET 5 | ✅ Language feature only (†1) |
| `^` (index from end) | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ Requires BCL type (†2) |
| `..` (range) | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ Requires BCL type (†2) |
| `init` accessor | C# 9.0 | .NET 5 | ⚠️ Requires BCL type (†3) |
| `with` (record class) | C# 9.0 | .NET 5 | ⚠️ Requires a BCL type (†3) |
| `with` (struct / record struct) | C# 10.0 | .NET 6 | ✅ Language feature only (†1, †5) |
| Target-typed `new` | C# 9.0 | .NET 5 | ✅ Language feature only (†1) |
| `required` property | C# 11.0 | .NET 7 | ⚠️ Requires BCL attributes (†4) |
| Collection expressions | C# 12.0 | .NET 8 | ✅ Language feature only (†1) |
| Primary constructors | C# 12.0 | .NET 8 | ✅ Language feature only (†1) |

- **†1**: Pure language features. These can be used on .NET Framework once `LangVersion` is set to the corresponding C# version. **Updating the SDK or Visual Studio is not enough on its own:** a project targeting .NET Framework defaults to C# 7.3 and does not move off it when the tooling is updated, so `LangVersion` has to be set explicitly in the `.csproj`.
- **†2**: Requires `System.Index` / `System.Range`, added in .NET Core 3.0+. Array slicing with `a[1..3]` additionally requires `System.Runtime.CompilerServices.RuntimeHelpers.GetSubArray`.
- **†3**: Requires `System.Runtime.CompilerServices.IsExternalInit`, added in .NET 5+. Whether a `with` expression needs it depends on **whether the target type has `init` accessors**. A `record` class and a `readonly record struct` generate them and therefore need it; a mutable `struct` and a positional `record struct` do not.
- **†4**: Paired with `init`, it needs `CompilerFeatureRequiredAttribute` and `IsExternalInit` on top of `RequiredMemberAttribute` (all in `System.Runtime.CompilerServices`). Paired with `set`, `IsExternalInit` is not involved and the first two suffice.
- **†5**: A mutable `struct` and a positional `record struct` do not generate `init`, so `IsExternalInit` is not required. The `with` expression itself is C# 10.0 or later, however: leaving `LangVersion` at 9.0 fails with `CS8773`.

**Classifying `init` as †1 (language feature only) is incorrect.**
A `with` expression whose target has `init` accessors carries the same constraint: it does not compile on .NET Framework, where `IsExternalInit` does not exist, no matter how high `LangVersion` is set.
The next section shows this classification as actual compiler output.

### Compilation Results Against .NET Framework 4.8

The table below records the result of compiling each construct against `net48` with `LangVersion=latest`.
Whether defining the missing type makes it compile was checked the same way.

<figure class="article-figure">
  <img src="/images/articles/csharp-operators-initialization-syntax-by-version/csharp-net-framework-matrix.svg" alt="A table of compilation results against net48. ??=, !, new(), collection expressions, primary constructors, with on a mutable struct, and with on a record struct are OK. a[^1], a[1..3], init, with on a record, required with init, and required with set are NG with the missing types named, and all become OK once a polyfill is added. required with init needs three types while required with set needs two." width="646" height="470" loading="lazy">
  <figcaption>Compiled with .NET SDK 10.0.302 against <code>net48</code> at <code>LangVersion=latest</code>. <code>missing type</code> is the type the compiler reported as absent; when several are missing, the first is named along with the count of the rest. <code>+ polyfill</code> is the result of recompiling after defining those types locally.</figcaption>
</figure>

Four things follow from the table.

**1. `??=` and `!` work on .NET Framework by raising `LangVersion` alone.**
The same holds for target-typed `new`, collection expressions, and primary constructors.
For the problem this article opens with — `??=` being unavailable — raising `LangVersion` is sufficient.

**2. `init`, and `with` on a type that has `init`, do not compile even at the highest `LangVersion`.**
`System.Runtime.CompilerServices.IsExternalInit` does not exist on .NET Framework.
What decides this is not the `with` syntax but **whether the target type has `init` accessors**.
As the table shows, `with` on a mutable `struct` and on a positional `record struct` compiles on `net48` as-is, because those generate ordinary setters.
A `record` class and a `readonly record struct` generate `init` and therefore need `IsExternalInit`.

**3. What `required` needs depends on whether it is paired with `init`.**
`required` can be declared alongside either `init` or `set`.
Paired with `init` it needs all three of `RequiredMemberAttribute`, `CompilerFeatureRequiredAttribute`, and `IsExternalInit`.
Paired with `set` it needs only the first two — `IsExternalInit` is not involved, as the differing count of missing types on the two rows shows.
Either way, defining `RequiredMemberAttribute` alone does not resolve it.

**4. The C# version that allows `with` depends on the shape of the target type.**
A `record` class works from C# 9.0, but a `struct` and a `record struct` require C# 10.0.
Writing `with` against a `struct` at `LangVersion` 9.0 fails with `CS8773`.

The figure below records the lowest `LangVersion` that compiles for each construct.

<figure class="article-figure">
  <img src="/images/articles/csharp-operators-initialization-syntax-by-version/csharp-langversion-matrix.svg" alt="A table of the lowest LangVersion that compiles on net48 per construct. With no LangVersion set, every row fails. The minimum is 8.0 for ??=, 9.0 for new() and with on a record class, 10.0 for with on a struct and a record struct, and 12.0 for collection expressions." width="603" height="260" loading="lazy">
  <figcaption>Measured with .NET SDK 10.0.302 against <code>net48</code>, raising <code>LangVersion</code> from 7.3 upward and recording the first value that compiles. Constructs needing BCL types were measured with their polyfill applied.</figcaption>
</figure>

**Note that every row fails in the column with no `LangVersion` set.** A project targeting .NET Framework stays on C# 7.3 by default and does not move off it when the SDK or Visual Studio is updated.
`LangVersion` has to be stated explicitly in the `.csproj`.

In every case, defining the missing types locally makes the code compile. The definitions appear under "Option 3: Supply the missing types yourself" below.

---

## Three Ways to Deal with It

When a compile error occurs because of new C# syntax, there are three main approaches.

### Option 1: Raise `LangVersion` or update the build environment

The C# version used by a project can be controlled by specifying `<LangVersion>` in the project file (`.csproj`).

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net481</TargetFramework>
    <LangVersion>8.0</LangVersion>  <!-- Enables ??= and ! -->
  </PropertyGroup>
</Project>
```

Updating Visual Studio or the .NET SDK can also make a newer compiler available.

### Option 2: Rewrite to older syntax

When the build environment cannot be changed, or when required BCL types are missing, equivalent older syntax can be used instead.

```csharp
// When ??= cannot be used
_numbers = _numbers ?? new List<int>();

// When ^ cannot be used because System.Index is unavailable
int last = array[array.Length - 1];

// When .. cannot be used because System.Range is unavailable
int[] sliced = array.Skip(1).Take(3).ToArray();
```

These examples preserve the same meaning while using older syntax. The LINQ example requires `using System.Linq;`.

### Option 3: Supply the Missing Types Yourself

When only a BCL type is missing, defining that type in your own project makes the code compile.
The compiler looks for a type with a matching namespace and shape; which assembly provides it is irrelevant.

The definitions below were compiled against `net48` and confirmed to work.

For `init` and `with`, define `IsExternalInit`.

```csharp
namespace System.Runtime.CompilerServices
{
    // Marker type required by init accessors and records. No members are needed.
    internal static class IsExternalInit { }
}
```

For `required`, two more attributes are needed.

```csharp
using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct
        | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        public string FeatureName { get; }
    }
}
```

For `^` and `..`, define `System.Index` and `System.Range`, plus the `RuntimeHelpers.GetSubArray` method that array slicing compiles down to.

```csharp
using System;

namespace System
{
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => new Index(value);
    }

    internal readonly struct Range
    {
        public Range(Index start, Index end) { Start = start; End = end; }

        public Index Start { get; }

        public Index End { get; }

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);

            // Without these checks a[3..1] yields a negative length. The real System.Range
            // throws ArgumentOutOfRangeException, so match that.
            if ((uint)end > (uint)length || (uint)start > (uint)end)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return (start, end - start);
        }
    }
}

namespace System.Runtime.CompilerServices
{
    // The compiler rewrites a[1..3] on an array into a call to this method.
    internal static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            (int offset, int length) = range.GetOffsetAndLength(array.Length);
            var result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}
```

Running a `net48` console application that includes these definitions returns the same values as .NET 5 and later.
For `int[] a = { 10, 20, 30, 40, 50 }`, `a[^1]` returned `50` and `a[1..3]` returned `[20, 30]` on actual .NET Framework.

#### A Caveat About NuGet Packages

`IndexRange` is a package that supplies `Index` and `Range`.
**There is no NuGet package named `System.Index`** — nuget.org returns 404 for it.

Referencing `IndexRange` 1.1.1 from `net48` makes `a[^1]` and the `Index` / `Range` types available, but **array slicing with `a[1..3]` still fails to compile**.

```text
error CS0656: Missing compiler required member
'System.Runtime.CompilerServices.RuntimeHelpers.GetSubArray'
```

The package does not include `GetSubArray`.
Array slicing therefore still requires defining `RuntimeHelpers` as shown above.

#### Notes on Defining These Yourself

- `RuntimeHelpers` also exists in `mscorlib`. Defining it locally makes the local type win inside that project. If other `RuntimeHelpers` members (such as `InitializeArray`) are in use, implement them on the local type as well. **Writing the fully qualified name does not reach the BCL type** — `System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray` still resolves to the local type and fails with `CS0117` (verified on `net48`). If adding those members is undesirable, avoid the array-slicing polyfill and use `Skip` / `Take` instead.
- Declare all of these as `internal`. Making them `public` can collide with the types of assemblies that reference yours.
- Remove the definitions after retargeting to .NET 5 or later. Duplicating a BCL type means the local definition wins, which can produce unintended behavior.

---

## Syntax by Version

### 1. Null-safe operators

#### `?.` and `?[]` (null-conditional operators) — C# 6.0 and later

These operators access members or elements only when the object or collection is not `null`.
If the target is `null`, evaluation stops and `null` is returned, which removes the need for a separate null check.

```csharp
string title = GetTitle();
int? length = title?.Length; // length becomes null if title is null

List<string> items = GetItems();
string firstItem = items?[0]; // firstItem becomes null if items is null
```

`?.` and `?[]` can be combined in a chain.
If any step evaluates to `null`, the entire chain returns `null`.

#### `??` (null-coalescing operator) — C# 2.0 and later

This operator returns the left-hand value when it is not `null`.
If the left-hand value is `null`, the right-hand value is returned instead.
It is used to provide a default value for nullable expressions.

```csharp
string typedName = GetName();
string displayName = typedName ?? "Anonymous"; // "Anonymous" when typedName is null
```

`??` has been supported since .NET Framework 2.0 and can be used regardless of version within that runtime range.

#### `??=` (null-coalescing assignment operator) — C# 8.0 and later

This operator assigns the right-hand value only when the left-hand variable is `null`.
It is often used for lazy initialization.

```csharp
private List<int> _numbers;

public void AddNumber(int val)
{
    _numbers ??= new List<int>(); // Create the instance only when _numbers is null
    _numbers.Add(val);
}
```

When the build environment compiles below C# 8.0, this syntax cannot be used.
An equivalent rewrite is shown below.

```csharp
_numbers = _numbers ?? new List<int>();
```

This form expresses the same intent, although `??=` is more concise in environments that support it.

#### `!` (null-forgiving operator) — C# 8.0 and later

This operator tells the C# static analyzer that a value is definitely not `null` at that point in the code.
It suppresses nullable warnings at compile time only and does not change runtime behavior.

```csharp
string? rawInput = GetValidatedInput();
// Assume validation guarantees that the value is not null here.
string solidInput = rawInput!;
```

Using `!` removes a null check from the compiler’s analysis.
If `null` is actually passed, a runtime exception can still occur.
For that reason, usage should be kept to a minimum.

---

### 2. Index and range

#### `^` (index from end operator) — C# 8.0 and later

This operator creates a `System.Index` value that represents a position counted from the end of a collection.
`^1` refers to the last element (`Length - 1`), and `^0` refers to the position just past the last element (`Length`).

```csharp
int[] digits = new[] { 10, 20, 30, 40 };
int last = digits[^1];          // 40, equivalent to digits[digits.Length - 1]
int secondFromLast = digits[^2]; // 30
```

On .NET Framework, `System.Index` is not provided by default.
Without an additional reference or polyfill, the `^` operator cannot be used.
An explicit index calculation such as `array[array.Length - 1]` is the alternative.

#### `..` (range operator) — C# 8.0 and later

This operator creates a `System.Range` value from a start index and an end index, and it enables intuitive slicing of arrays and strings.
The start index is included, and the end index is excluded.

```csharp
int[] dataset = new[] { 0, 1, 2, 3, 4, 5 };
int[] sliced = dataset[1..4]; // [1, 2, 3]

// Omitting start or end
int[] continuous = dataset[2..];  // [2, 3, 4, 5]
int[] allButLast = dataset[..^1]; // [0, 1, 2, 3, 4]
```

The `..` operator requires `System.Range`.
On .NET Framework, it cannot be used without an additional reference or polyfill.
A common alternative is `array.Skip(start).Take(count).ToArray()` with LINQ.

---

### 3. Type operators

#### `is` and `as` (type check and type cast operators)

`is` checks whether an object is compatible with a specific type.
From C# 7.0 onward, it can be combined with pattern matching to declare and assign a variable when the type matches.

`as` attempts a type conversion and returns the cast object on success.
If the conversion fails, it returns `null` instead of throwing an exception.

```csharp
object element = "Hello WPF";

// is pattern matching (C# 7.0 and later)
if (element is string message)
{
    Console.WriteLine(message.Length); // Treated as string within this block
}

// as operator
var stream = element as System.IO.Stream; // stream becomes null because conversion fails
```

A direct cast such as `(Type)obj` throws `InvalidCastException` when the conversion fails.
By contrast, `as` returns `null`, which makes it suitable when type compatibility is uncertain.

---

### 4. Data manipulation and other operators

#### `=>` (lambda operator / expression-bodied members) — C# 3.0 / C# 6.0 and later

C# 3.0 introduced `=>` as the syntax for lambda expressions.
C# 6.0 later extended it to expression-bodied members, which allow properties and methods to be written in a single expression.

```csharp
public class Rectangle
{
    private readonly double _width;
    private readonly double _height;

    public Rectangle(double width, double height)
    {
        _width = width;
        _height = height;
    }

    // Expression-bodied property (C# 6.0)
    public double Area => _width * _height;

    // Expression-bodied method (C# 6.0)
    public void PrintArea() => Console.WriteLine($"Area: {Area}");
}
```

Expression-bodied members are effective when the logic can be expressed as a single expression.
They reduce boilerplate and improve readability in small members.

#### `nameof` — C# 6.0 and later

This operator returns the identifier name of a variable, type, property, or method as a string at compile time.
It avoids hard-coded string literals, which improves refactor safety and reduces typos.

```csharp
public void UpdateText(string? newText)
{
    if (newText == null)
    {
        throw new ArgumentNullException(nameof(newText));
    }
}
```

The result of `nameof` is a compile-time constant.
For that reason, it can also be used in `case` labels and attribute arguments.

#### `with` expression — C# 9.0 for a record class, C# 10.0 for structs

This expression creates a copy of an existing `record` or struct, changing only selected properties.
The original instance itself remains unchanged.

What it produces, however, is a **shallow copy**: only the accessible instance properties and fields are duplicated, and reference-type members keep pointing at the same objects.
Mutating a nested mutable object through the copy is therefore visible from the original as well.

```csharp
public record WindowSettings(string Title, double Width, double Height);

var defaultSettings = new WindowSettings("Main", 800, 600);
var tallSettings = defaultSettings with { Height = 1000 };
```

The C# version that allows `with` depends on the shape of the target type.
A `record` class works from C# 9.0; a `struct` and a `record struct` require C# 10.0 (writing `with` against a `struct` at `LangVersion` 9.0 fails with `CS8773`).
`IsExternalInit` is required **only when the target type has `init` accessors**.
A `record` class and a `readonly record struct` generate them and therefore need it.
A mutable `struct` and a positional `record struct` do not, so raising `LangVersion` to 10.0 is enough for them on .NET Framework.

---

### 5. Initialization syntax sugar

#### Target-typed `new` — C# 9.0 and later

When the instantiation type can be inferred from the target type on the left-hand side or from a method parameter, the type name after `new` can be omitted.

```csharp
public class Example
{
    public void Run()
    {
        // Traditional form
        Dictionary<string, List<string>> map1 = new Dictionary<string, List<string>>();

        // Target-typed new
        Dictionary<string, List<string>> map2 = new();

        // Applied to method arguments
        RegisterNumbers(new() { 1, 2, 3 });
    }

    private void RegisterNumbers(List<int> numbers) { }
}
```

The type can be omitted only when it is unambiguous from the left-hand side or from the parameter type.
`var` cannot be combined with this syntax, because the right-hand side type would no longer be inferable.
For that reason, `var map = new();` is a compile error.

#### Collection expressions — C# 12.0 and later

This syntax provides a unified `[...]` notation for initializing arrays, `List<T>`, `Span<T>`, and other custom collections.

```csharp
int[] row = [1, 2, 3];                     // Array
List<string> tags = ["C#", "WPF", ".NET"]; // List<T>
ReadOnlySpan<byte> data = [0x00, 0x01];    // Span<T>
```

Inside a collection expression, the `..` spread operator can be used to flatten and combine another collection’s elements.

```csharp
int[] left = [1, 2];
int[] right = [5, 6];

int[] result = [.. left, 3, 4, .. right]; // Produces [1, 2, 3, 4, 5, 6]
```

Collection expressions were introduced in C# 12.0 and require a compiler and SDK that support C# 12.0 or later.

#### `required` property — C# 11.0 and later

A property marked with `required` must be initialized when the object is created through an object initializer.
This prevents missed initialization without adding extra constructor parameters.

```csharp
public class AppTheme
{
    public required string ThemeName { get; init; } // Required at initialization time
    public string Author { get; init; } = "Unknown"; // Optional, because a default value exists
}

// Compiles successfully
var lightTheme = new AppTheme { ThemeName = "Light Mode" };

// Compile error because ThemeName is not specified
// var invalidTheme = new AppTheme { Author = "s-iguchi" };
```

`required` was introduced in C# 11.0. Starting with .NET 7, the attributes it needs are provided by the platform.

On .NET Framework, three types are missing. Compiling the example above against `net48` reports all three at once.

- `System.Runtime.CompilerServices.RequiredMemberAttribute`
- `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`
- `System.Runtime.CompilerServices.IsExternalInit` (because the example uses an `init` accessor)

Defining `RequiredMemberAttribute` alone does not resolve it.
All three definitions appear under "Option 3: Supply the Missing Types Yourself".

#### Primary constructor — C# 12.0 and later

Starting with C# 12, `class` and `struct` types can define constructor parameters directly after the type name.
This removes the need for a constructor body or boilerplate field assignments.

```csharp
public class LogWriter(string logFilePath, LogLevel minimumLevel)
{
    // Parameters can be referenced directly from within the class.
    public void WriteLog(string message, LogLevel level)
    {
        if (level >= minimumLevel)
        {
            System.IO.File.AppendAllText(logFilePath, $"[{level}] {message}\n");
        }
    }
}
```

The values passed as parameters, such as `logFilePath` and `minimumLevel`, can be referenced directly from any member of the class.
Primary constructors were introduced in C# 12.0 and require a compiler and SDK that support C# 12.0 or later.

---

## Notes

- Even when targeting .NET Framework, language features such as `??=` and `!` can be used if the compiler and `LangVersion` support them.
- `!` suppresses compile-time warnings only and does not perform a runtime null check.
  If `null` is actually passed to the marked location, a `NullReferenceException` can still occur.
- Target-typed `new`, collection expressions, and primary constructors are pure language features and work on .NET Framework once `LangVersion` is raised (confirmed against `net48`).
  **`init` is not a pure language feature**, and neither is `with` when its target has `init` accessors. Both require `IsExternalInit`, so raising `LangVersion` alone does not make them compile on .NET Framework. A `with` expression on a mutable `struct` or a positional `record struct` does compile, because those generate ordinary setters rather than `init`.
  `required` additionally requires `RequiredMemberAttribute` and `CompilerFeatureRequiredAttribute`.
- The `..` spread operator in collection expressions uses the same symbol as the C# 8.0 range operator, but the purpose is different.
  In collection expressions, it expands elements within the collection literal.

---

## Comparing the Approaches

The following table compares the approaches for handling compile errors caused by new C# syntax. The three approaches above are split into five rows, one per decision point.

| Approach | Pros | Cons | Best suited for |
| --- | --- | --- | --- |
| Raise `LangVersion` | New syntax can be used directly. Code remains concise. | Requires updates to the build environment such as Visual Studio or the SDK. | Projects where compiler settings can be changed. |
| Update the build environment | Provides the latest language features and tooling support. | May have a wider impact on existing projects. | New development or environments that can be updated. |
| Rewrite to older syntax | Works without changing the environment. | Code becomes more verbose and newer features cannot be used. | Legacy environments where updates are not allowed. |
| Define the missing types yourself | Enables `^`, `..`, `init`, `with`, and `required` on .NET Framework. No extra package needed. | The definitions need maintaining and must be removed when retargeting to .NET 5+. `RuntimeHelpers` shadows the BCL type of the same name. | Projects that need BCL-dependent syntax but must stay on .NET Framework. |
| Reference the `IndexRange` package | Supplies `Index` / `Range` and makes `a[^1]` work. | Does not cover array slicing `a[1..3]`, since it omits `GetSubArray`; that still needs a local definition. | Projects where `^` alone is sufficient. |

---

## Summary

C# operators and initialization syntax have been added gradually across language versions.
Whether a feature can be used depends mainly on the compiler configuration (`LangVersion`) and on any runtime-side types or attributes the feature requires.

The following selection criteria are practical guidelines.

- **.NET Framework without compiler updates**: `??` (C# 2.0), `?.` (C# 6.0), `nameof` (C# 6.0), and `is` pattern matching (C# 7.0) are the upper baseline.
- **.NET Framework with `LangVersion` raised**: `??=`, `!`, target-typed `new`, collection expressions, and primary constructors become available (confirmed against `net48`). `^`, `..`, `init`, `with`, and `required` remain unusable until the missing BCL types are defined.
- **.NET 5 to 6 (C# 9 to 10)**: all C# 9 to 10 features are available, including the required supporting BCL types.
- **.NET 7 (C# 11) and later**: `required` properties are available.
- **.NET 8 (C# 12) and later**: collection expressions and primary constructors are available.

Compiling against `net48` showed that **whether a construct is a pure language feature cannot be inferred from how it looks**.
`with` reads like an operator yet requires `IsExternalInit`, while larger additions such as primary constructors and collection expressions require no BCL type at all.

The reliable way to tell whether raising `LangVersion` is enough, or whether types must be supplied, is to **compile against the target framework and see**.
The compiler names the missing types in `CS0518` / `CS0656`, which can then be defined directly.
