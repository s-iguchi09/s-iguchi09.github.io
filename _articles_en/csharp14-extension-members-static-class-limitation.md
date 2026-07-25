---
layout: article-en
title: "C# 14 Extension Members on Static Classes: Static Members Only"
date: 2026-06-17
category: C#
excerpt: "A C# 14 extension block can target a static class such as Directory, but only static members can be added. Instance members are rejected with CS0721 or CS9303. This article maps the boundary to the receiver form and covers the alternatives for instance-style calls."
---

## Overview

This article clarifies what does and does not work when a C# 14 (.NET 10) `extension` block targets a static class such as `System.IO.Directory`.

The short answer: a static class *can* be the target of an `extension` block, but only static members can be added. Declaring an instance member is a compile error.
Which of the two you get is decided by the receiver form — whether you write the type alone or give the receiver a parameter name.

The article also covers the alternatives when instance-style calls are required, and the evolution of extension member support from C# 3.0 to C# 14.

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/csharp14-extension-members-static-class-limitation/extension-receiver-form-matrix.svg" alt="A table showing which extension block declarations are accepted for the static class Directory. With a type-only receiver, a static member compiles and an instance member fails with CS9303. With a named receiver parameter, the block itself fails with CS0721." width="880" height="322" loading="lazy">
  <figcaption>What an <code>extension</code> block can declare when the target is a static class, verified by building against .NET 10 SDK 10.0.302 with <code>LangVersion 14.0</code>. Once the receiver has a name, the block fails with <code>CS0721</code> regardless of the members that follow.</figcaption>
</figure>

---

## Prerequisites / Environment

- Language: C# 14 (`LangVersion` set to `14.0`)
- Framework: .NET 10 (verified with SDK 10.0.302)
- Target feature: Extension members (`extension` block syntax)
- Reference: Classic extension methods (`this` parameter syntax)

---

## Problem

Whether an `extension` block targeting a static class compiles depends on how it is written.
Here is the form that does not compile.

```csharp
using System.IO;

public static class DirectoryExtensions
{
    // error CS0721: 'Directory': static types cannot be used as parameters
    extension(Directory directory)
    {
        public void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
```

The error is reported at `Directory` inside `extension(Directory directory)`.
In other words, the receiver declaration itself is rejected, regardless of what the members contain.

Remove the parameter name `directory` from the receiver and make the member `static`, and the same `Directory` target compiles.

```csharp
using System.IO;

public static class DirectoryExtensions
{
    // This one compiles
    extension(Directory)
    {
        public static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
```

At the call site it resolves as though it were a static method that `Directory` always had.

```csharp
Directory.DeleteIfExists(@"C:\Temp\TargetDir");
```

The restriction is sometimes summarized as "extension members cannot be added to static classes," but what actually cannot be added is instance members.

---

## Cause

### The receiver can be written in two forms

The receiver of an `extension` block can be written either as a **type alone** or as a **parameter**.
This is not a stylistic choice: it changes which kinds of members can be declared.

| Receiver form | Members that can be declared | Static class allowed as target |
|---|---|---|
| `extension(Directory)` | Static members only | Yes |
| `extension(Directory directory)` | Instance members (and static members) | No |

The language specification states that if the receiver parameter is named, the receiver type may not be static.
Once the receiver has a name it is an ordinary parameter, so the existing rule that a static class cannot be used as a parameter type applies unchanged.

### Why a static class cannot be a parameter type

C# forbids using the name of a `static class` as the type of anything that holds a value.
All of the following are compile errors.

| Usage | Error |
|---|---|
| `Directory myDir;` | `CS0723`: cannot declare a variable of static type |
| `List<Directory> list;` | `CS0718`: static types cannot be used as type arguments |
| `void M(Directory d)` | `CS0721`: static types cannot be used as parameters |

`extension(Directory directory)` is the same situation as the last row, and the reported error is likewise `CS0721`.

Note that the name of a static class is not entirely unusable as a type.
`typeof(Directory)` is legal, and member access such as `Directory.Exists(path)` obviously works.
What is forbidden is using it as the type of a value-holding location: a variable, a parameter, or a type argument.
`extension(Directory)` compiles precisely because it declares no location that receives the receiver value.

### An unnamed receiver cannot carry instance members

Conversely, declaring an instance member inside the `extension(Directory)` form produces a different error.

```csharp
extension(Directory)
{
    // error CS9303: cannot declare instance members in an extension block
    //               with an unnamed receiver parameter
    public void DeleteIfExists(string path) { }
}
```

The body of an instance member refers to the receiver value, so a name for that value is required.
Without a name, no instance member can be declared.

Caught between these two errors, there is no way to write an instance-style extension member on a static class.

---

## Solution

The right option depends on what you are trying to achieve.

- **Option A**: Call it as `Directory.Xxx(...)` — declare a static extension member with a type-only receiver.
- **Option B**: Call it as `xxx.Yyy()` — declare an extension member on the corresponding instance type (`DirectoryInfo`).
- **Option C**: Support C# 13 or earlier — use a plain static helper class.

---

## Implementation

### Option A: Static extension member

On C# 14 or later this is the most direct approach. It adds a member that looks as though `Directory` always had it.

```csharp
using System.IO;

namespace MyLib;

public static class DirectoryExtensions
{
    extension(Directory)
    {
        /// <summary>
        /// Deletes the directory at the specified path if it exists.
        /// </summary>
        public static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        /// <summary>The default working directory.</summary>
        public static string DefaultRoot => @"C:\Temp";
    }
}
```

The call site looks as follows.

```csharp
using MyLib; // omit this and you get CS0117

Directory.DeleteIfExists(@"C:\Temp\TargetDir");
Console.WriteLine(Directory.DefaultRoot);
```

As the comment notes, without a `using` for the namespace that declares the extension members, the compiler reports `CS0117` — `Directory` does not contain a definition for `DeleteIfExists`.
This is the same constraint that applies to classic extension methods, not something specific to static extension members.

### Option B: Extension member on `DirectoryInfo`

`DirectoryInfo` is an ordinary instantiable type, so its receiver can carry a parameter name.
This is the option to use when calls should be consistently instance-style.

```csharp
using System.IO;

public static class DirectoryInfoExtensions
{
    // DirectoryInfo is not a static class, so the parameter form is allowed
    extension(DirectoryInfo directoryInfo)
    {
        /// <summary>
        /// Deletes the directory if it exists.
        /// </summary>
        public void DeleteIfExists()
        {
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
        }
    }
}
```

The call site looks as follows.

```csharp
var dir = new DirectoryInfo(@"C:\Temp\TargetDir");
dir.DeleteIfExists();
```

Creating a `DirectoryInfo` instance is an extra step, but it is convenient when the path is resolved once and several operations follow.

### Option C: Static helper class

When C# 13 or earlier must be supported, `extension` blocks are unavailable, so a conventional static class is the fallback.

```csharp
using System.IO;

public static class DirectoryHelper
{
    /// <summary>
    /// Deletes the directory at the specified path if it exists.
    /// </summary>
    public static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
```

```csharp
DirectoryHelper.DeleteIfExists(@"C:\Temp\TargetDir");
```

The `Directory.DeleteIfExists(...)` form is not available, but the location of the method is unambiguous.

---

## Notes

- The `extension` block syntax is available only in C# 14 (.NET 10) and later.
  Earlier versions are limited to the classic `this`-parameter extension method syntax.
- Static extension members also require the declaring namespace to be imported at the call site.
  Forgetting it yields `CS0117` (no such definition), which does not obviously indicate a failed extension member lookup.
- An `extension` block that extends a static class cannot contain user-defined operators (`CS9321`).
  An operator must take the extended type as a parameter, and a static class cannot be one.
- The API shapes of `Directory` (static methods) and `DirectoryInfo` (instance members) differ.
  The counterpart of `Directory.Exists(path)` is the `directoryInfo.Exists` property — one takes an argument and the other does not.

---

## Alternatives / Comparison

| Approach | Call form | Pros | Cons | Best suited for |
|---|---|---|---|---|
| Static extension member (Option A) | `Directory.DeleteIfExists(path)` | Looks identical to the standard API | C# 14 or later only; a missing `using` is easy to misdiagnose | Filling gaps in a static class API on C# 14 or later |
| Extension member on `DirectoryInfo` (Option B) | `dir.DeleteIfExists()` | Consistent instance-style calls | Requires an instance; C# 14 or later only | Resolving a path once and performing several operations |
| Static helper class (Option C) | `DirectoryHelper.DeleteIfExists(path)` | No version dependency; obvious location | Call form differs from the standard API | Supporting C# 13 or earlier |

---

## Supplementary: Evolution of Extension Members from C# 3.0 to C# 14

Support for attaching members to existing types has expanded incrementally across C# versions.

| Version | Platform | Key change |
|---|---|---|
| C# 3.0 | .NET Framework 3.5 | **Extension methods introduced.** A `this`-prefixed first parameter inside a `public static class` allows instance methods to be attached to existing types. This feature underpins LINQ. |
| C# 7.2 | .NET Core 2.0 / .NET Framework 4.7.2 | **Improved support for value types.** The `ref this` and `in this` modifiers became available, enabling extension methods on large structs to pass the receiver by reference without copying. |
| C# 14 | .NET 10 | **Extension member syntax (`extension` block) introduced.** In addition to the `this`-parameter form, members can be declared inside an `extension(Type)` block, which supports properties, indexers, and operators as well as methods — and static members. |

The `this`-parameter syntax can only declare instance extension methods; before C# 14 there was no way to attach a static member at all.
Adding a member such as `Directory.DeleteIfExists(...)` to a static class first became possible with the C# 14 `extension` block.

---

## Summary

Targeting a static class with an `extension` block is possible.
What is not possible is adding an instance member: naming the receiver turns the static class into a parameter type, which produces `CS0721`.

- To call it as `Directory.Xxx(...)`, declare static members inside `extension(Directory)` (Option A).
- To call it instance-style, define the members on a corresponding instance type such as `DirectoryInfo` (Option B).
- To support C# 13 or earlier, fall back to a conventional static helper class (Option C).

---

<!-- Related articles -->
