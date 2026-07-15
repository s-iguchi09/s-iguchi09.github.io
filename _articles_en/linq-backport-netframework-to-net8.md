---
layout: article-en
title: "Backporting Missing LINQ Methods from .NET 8 to .NET Framework"
date: 2026-07-15
category: C#
excerpt: "Backporting the .NET 8 selector-free ToDictionary overloads (KeyValuePair and tuple) to .NET Framework with #nullable enable and conditional compilation."
---

## Overview

When migrating from .NET Framework to modern .NET (.NET 8 or later) incrementally, or when a codebase must remain on .NET Framework for the foreseeable future, a persistent source of friction is the set of LINQ methods that exist in modern .NET but not in .NET Framework.

This article covers the **`ToDictionary` overloads added in .NET 8** (the selector-free `KeyValuePair` and value-tuple forms) and explains how to implement them as extension methods (polyfills) that behave identically to the originals.
It also covers a modern implementation using `#nullable enable` and a conditional-compilation technique that eliminates migration cost when eventually upgrading to .NET 8 or later.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 / .NET 8+
- APIs: the selector-free `ToDictionary` overloads (`KeyValuePair` and value-tuple forms, each with an `IEqualityComparer<TKey>` overload) — four signatures in total
- Nullable context: `#nullable enable`
- Migration guard: `#if !NET8_0_OR_GREATER`
- Project settings (such as the C# language version in `.csproj`) are left unchanged

---

## Problem

.NET 8 adds almost no new operators to the public `Enumerable` class.
The only practical addition is a set of `ToDictionary` overloads that require neither a key selector nor a value selector, and these are unavailable in .NET Framework environments.

| Method | Added in | Description |
| --- | --- | --- |
| `ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>)` | .NET 8.0 | Converts a sequence of `KeyValuePair` directly into a dictionary |
| `ToDictionary<TKey, TValue>(this IEnumerable<(TKey, TValue)>)` | .NET 8.0 | Converts a sequence of two-element tuples directly into a dictionary |

Each has an overload that accepts an `IEqualityComparer<TKey>`, for four signatures in total.

Without these methods, even when the elements are already in `KeyValuePair` or tuple form, an explicit identity-style selector is required through the existing `ToDictionary`.

- Write `pairs.ToDictionary(p => p.Key, p => p.Value)` instead of `pairs.ToDictionary()`.
- For a sequence of tuples, write `items.ToDictionary(t => t.Item1, t => t.Item2)`.

The `p => p.Key` / `p => p.Value` selectors are boilerplate that is not the essence of the conversion.
They also create room for minor mistakes, such as swapping the key and value selectors.

---

## Background

.NET 6 added `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy`, and .NET 7 added `Order` and `OrderDescending`, but .NET 8 added essentially no new operators to the public `Enumerable` class.
The only practical addition is the set of `ToDictionary` overloads that convert a sequence of `KeyValuePair` or tuples into a dictionary without a selector.

These were first added in .NET 8.0 and are not available in any earlier runtime.
When filtering a dictionary and rebuilding it, or turning tuples returned by `Select` into a dictionary, the elements are already key/value pairs.
The selector-free overloads were standardized to remove the boilerplate of writing an identity-style selector in those cases.

The methods added between .NET Framework 4.8 and .NET 5 (`Append`, `Prepend`, `TakeLast`, `SkipLast`) are covered in a [separate article](/articles/linq-backport-netframework-to-net5/), the four methods added in .NET 6 in [another](/articles/linq-backport-netframework-to-net6/), and `Order` / `OrderDescending` from .NET 7 in [another](/articles/linq-backport-netframework-to-net7/).

---

## Solution

By placing extension methods in the same namespace as the original LINQ (`System.Linq`), existing source files pick up the polyfills automatically without any changes — any file that already has `using System.Linq;` gains the missing methods transparently.

A `#if !NET8_0_OR_GREATER` guard ensures the implementation is automatically disabled when the project is later upgraded to .NET 8 or later.
No file deletions or code rewrites are needed at migration time.

There are two key implementation details.
The first is to return `Dictionary<TKey, TValue>`, and the second is to match the original `where TKey : notnull` constraint.
The original overloads carry this constraint, so applying the same constraint to the polyfill keeps nullable-reference analysis consistent before and after migration.

---

## Implementation

The following is a complete polyfill for all four signatures (`KeyValuePair` and tuple forms, each with an `IEqualityComparer<TKey>` overload).
For sources that implement `ICollection<T>`, the dictionary capacity is reserved up front from the element count, avoiding unnecessary rehashing just as the originals do.
Copy it into a file such as `LinqExtensions.Net8.cs` in your project.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET8_0_OR_GREATER // Active only in environments below .NET 8 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Backports .NET 8.0 LINQ methods to older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. IEnumerable<KeyValuePair<TKey, TValue>>.ToDictionary
        // ==========================================
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source)
            where TKey : notnull
            => source.ToDictionary(comparer: null); // The named argument resolves to this overload.

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dictionary = source is ICollection<KeyValuePair<TKey, TValue>> collection
                ? new Dictionary<TKey, TValue>(collection.Count, comparer)
                : new Dictionary<TKey, TValue>(comparer);

            foreach (var pair in source)
            {
                dictionary.Add(pair.Key, pair.Value);
            }

            return dictionary;
        }

        // ==========================================
        // 2. IEnumerable<(TKey, TValue)>.ToDictionary
        // ==========================================
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<(TKey Key, TValue Value)> source)
            where TKey : notnull
            => source.ToDictionary(comparer: null);

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<(TKey Key, TValue Value)> source,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dictionary = source is ICollection<(TKey Key, TValue Value)> collection
                ? new Dictionary<TKey, TValue>(collection.Count, comparer)
                : new Dictionary<TKey, TValue>(comparer);

            foreach (var pair in source)
            {
                dictionary.Add(pair.Key, pair.Value);
            }

            return dictionary;
        }
    }
}

#endif
```

The class is active only when the `NET8_0_OR_GREATER` symbol is not defined — that is, in any runtime below .NET 8, including .NET Framework.

---

## Method Walkthroughs

### Converting from a sequence of `KeyValuePair`

When filtering an existing dictionary to build a new one, the elements are already `KeyValuePair`.
Calling `ToDictionary()` with no selector reconstructs the dictionary while preserving each key/value mapping.

```csharp
var source = new Dictionary<string, int>
{
    ["apple"] = 3,
    ["banana"] = 5,
    ["cherry"] = 2,
};

// Keep only entries whose value is 3 or greater
var filtered = source.Where(pair => pair.Value >= 3)
                     .ToDictionary();
// filtered: { "apple": 3, "banana": 5 }
```

Because `Dictionary<TKey, TValue>` implements `IEnumerable<KeyValuePair<TKey, TValue>>`, the result of `Where` is a sequence of `KeyValuePair`.
The selector-free `ToDictionary()` removes the need to spell out `pair => pair.Key` / `pair => pair.Value`.

### Converting from a sequence of two-element tuples

When `Select` returns two-element tuples, the result can be turned into a dictionary directly.

```csharp
var files = new[] { "report.pdf", "photo.jpg", "notes.txt" };

// Key by file name, value is the extension
var byName = files.Select(name => (name, System.IO.Path.GetExtension(name)))
                  .ToDictionary();
// byName: { "report.pdf": ".pdf", "photo.jpg": ".jpg", "notes.txt": ".txt" }
```

The first tuple element becomes the key and the second becomes the value.
Tuple element names (such as `(name, ext)`) do not affect the type, so the method works with or without names.

### Specifying a comparison with `IEqualityComparer<TKey>`

The overload that swaps the key equality comparison can build, for example, a case-insensitive dictionary.

```csharp
var pairs = new[] { ("Alpha", 1), ("BETA", 2) };

var dictionary = pairs.ToDictionary(StringComparer.OrdinalIgnoreCase);

bool found = dictionary.ContainsKey("alpha"); // true
```

The overload without a comparer uses `EqualityComparer<TKey>.Default`.
Swapping the comparer does not change the return type, which remains `Dictionary<TKey, TValue>`.

### Immediate execution

Unlike `Where` or `Order`, `ToDictionary` is **not** deferred; it executes immediately.
At the call site it enumerates the source to completion, builds the dictionary, and returns it.

```csharp
var query = Enumerable.Range(1, 3).Select(n => (n, n * n));

var dictionary = query.ToDictionary(); // The source is enumerated here.
// dictionary: { 1: 1, 2: 4, 3: 9 }
```

As a result, even when the source is a deferred query, `ToDictionary` yields a materialized dictionary at the call site.
Later changes to the underlying data of the source do not affect the already-built dictionary.

---

## Choosing the Right Conditional-Compilation Symbol

This implementation uses `#if !NET8_0_OR_GREATER`, which differs from the `#if !NETCOREAPP` guard used in the [.NET 5 backport article](/articles/linq-backport-netframework-to-net5/).

These `ToDictionary` overloads do not exist prior to .NET 8.
Guarding with `NETCOREAPP` or `NET7_0_OR_GREATER` would therefore disable the polyfill on .NET 6 and .NET 7 builds, causing a compile error there.

| Symbol | .NET Framework | .NET 7 | .NET 8+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET7_0_OR_GREATER` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET8_0_OR_GREATER` | Polyfill enabled | Polyfill enabled | Polyfill disabled |

`!NET8_0_OR_GREATER` enables the polyfill on all runtimes below .NET 8, including .NET 7, and disables it automatically once the project targets .NET 8 or later.

---

## Caveats

- **Distinguishing from the existing `ToDictionary`**: These overloads apply only when the elements are already in `KeyValuePair` or tuple form. To build a dictionary from an arbitrary element type `TSource`, keep using the existing `ToDictionary` with a key selector (and an element selector when needed). There is no need to force elements that are not pairs into these overloads.
- **No name collision occurs**: The polyfill's signatures differ from the existing `ToDictionary` overloads that take a `Func<...>` as the first argument, so there is no ambiguity in overload resolution. Calls that pass a selector resolve to the original implementation, while calls on a `KeyValuePair` or tuple sequence with no selector resolve to the polyfill. Where the parameterless overload delegates to the comparer overload, the `comparer:` named argument ensures resolution to the intended method.
- **Immediate execution**: As noted, `ToDictionary` executes immediately and enumerates `source` to completion. Do not use it on infinite sequences or on sources whose enumeration causes side effects. The `ArgumentNullException` for a `null` source is thrown immediately at the call site.
- **Duplicate keys throw**: The implementation uses `Dictionary<TKey, TValue>.Add`, so a duplicate key raises `ArgumentException`. This matches the behavior of the original `ToDictionary`. To allow duplicates with last-write-wins semantics, use `dictionary[key] = value` manually rather than `ToDictionary`.
- **Null keys throw**: A `null` key raises `ArgumentNullException`. The `where TKey : notnull` constraint assumes reference-type keys are non-null, but a `null` that slips in at runtime is caught by this exception.

---

## Alternatives

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Custom polyfill (this article) | No external dependencies; return type and constraint match the original | Implementation and maintenance effort | Projects that minimize dependencies |
| Inline `ToDictionary(p => p.Key, p => p.Value)` | No additional code | Verbose; requires a bulk replacement when migrating | Few call sites and no planned migration |
| Upgrade to .NET 8 | Resolves the root cause; gains language features | Migration cost | When migration is technically and organizationally feasible |

Writing the selectors inline requires no extra code, but it leaves behind the work of finding and replacing every call site later when standardizing on the selector-free `ToDictionary` after a .NET 8 migration.
Introducing the polyfill from this article allows code to be written without selectors before migration; at migration time, the file can stay in place while conditional compilation switches automatically to the original.

---

## Summary

This article covered the selector-free `ToDictionary` overloads added in .NET 8 and how to backport them safely to .NET Framework.

Three implementation points are worth remembering.

- **Match the return type and constraint to the original**: Return `Dictionary<TKey, TValue>` with a `where TKey : notnull` constraint so that nullable analysis stays consistent before and after migration.
- **Use `#if !NET8_0_OR_GREATER`**: These overloads are absent from .NET 7 and earlier, so `!NETCOREAPP` or `!NET7_0_OR_GREATER` would cause a compile error on .NET 7 builds.
- **Understand immediate execution and duplicate-key behavior**: `ToDictionary` executes immediately, and duplicate or `null` keys throw. For elements that are not already pairs, use the selector-based `ToDictionary`.

| Method | Evaluation | Return type | Best input |
| --- | --- | --- | --- |
| `ToDictionary` (`KeyValuePair` form) | Immediate | `Dictionary<TKey, TValue>` | A sequence of `KeyValuePair` |
| `ToDictionary` (tuple form) | Immediate | `Dictionary<TKey, TValue>` | A sequence of two-element tuples |

---

## Related Articles

- [Backporting Missing LINQ Methods from .NET 7 to .NET Framework](/articles/linq-backport-netframework-to-net7/)
- [Backporting Missing LINQ Methods from .NET 6 to .NET Framework](/articles/linq-backport-netframework-to-net6/)
- [Backporting Missing LINQ Methods from .NET 5 to .NET Framework](/articles/linq-backport-netframework-to-net5/)
