---
layout: article-en
title: "Backporting Missing LINQ Methods from .NET 9 to .NET Framework"
date: 2026-07-16
category: C#
excerpt: "Backporting .NET 9 LINQ methods CountBy, AggregateBy, and Index to .NET Framework, preserving deferred execution with conditional compilation."
---

## Overview

When migrating from .NET Framework to modern .NET (.NET 9 or later) incrementally, or when a codebase must remain on .NET Framework for the foreseeable future, a persistent source of friction is the set of LINQ methods that exist in modern .NET but not in .NET Framework.

This article enumerates the **three LINQ methods added in .NET 9** (`CountBy`, `AggregateBy`, `Index`) and explains how to implement them as extension methods (polyfills) that behave identically to the originals.
Because all three use deferred execution, it also covers preserving the boundary between deferred and eager behavior and a conditional-compilation technique that eliminates migration cost when eventually upgrading to .NET 9 or later.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 / .NET 9+
- APIs: the LINQ methods `CountBy`, `AggregateBy`, `Index`, and their overloads
- Nullable context: `#nullable enable`
- Migration guard: `#if !NET9_0_OR_GREATER`
- Project settings (such as the C# language version in `.csproj`) are left unchanged

---

## Problem

The following LINQ methods added in .NET 9 are unavailable in .NET Framework environments.

| Method | Added in | Description |
| --- | --- | --- |
| `CountBy` | .NET 9.0 | Counts elements per key and returns a sequence of `KeyValuePair<TKey, int>` |
| `AggregateBy` | .NET 9.0 | Accumulates state per key and returns a sequence of `KeyValuePair<TKey, TAccumulate>` |
| `Index` | .NET 9.0 | Pairs each element with its index and returns a sequence of `(int Index, TSource Item)` tuples |

Without these methods, obtaining the same aggregates forces a verbose `GroupBy`-based expression.

- Write `GroupBy(keySelector).Select(g => new { g.Key, Count = g.Count() })` instead of `CountBy(keySelector)`.
- Write `Select((item, index) => (index, item))` instead of `Index()`.

This boilerplate is not the essence of the aggregation and reduces readability as it grows.

---

## Background

Following `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy` in .NET 6, `Order` and `OrderDescending` in .NET 7, and the selector-free `ToDictionary` in .NET 8, .NET 9 added methods that simplify per-key aggregation and indexed enumeration.
`CountBy`, `AggregateBy`, and `Index` were all first added in .NET 9.0 and are not available in any earlier runtime.

The goal of `CountBy` and `AggregateBy` is to avoid allocating an intermediate grouping (the element lists held per key) merely to aggregate.
The original implementation uses an internal `Dictionary` to keep only per-key state, so it is more memory-efficient than a grouping that retains the elements themselves.
Because of this dictionary-based design, the original `CountBy` and `AggregateBy` constrain the key type with `where TKey : notnull` and throw during enumeration if a `null` key is produced.

The methods added between .NET Framework 4.8 and .NET 5 (`Append`, `Prepend`, `TakeLast`, `SkipLast`) are covered in a [separate article](/articles/linq-backport-netframework-to-net5/), the four methods added in .NET 6 in [another](/articles/linq-backport-netframework-to-net6/), `Order` / `OrderDescending` from .NET 7 in [another](/articles/linq-backport-netframework-to-net7/), and the `ToDictionary` overloads from .NET 8 in [another](/articles/linq-backport-netframework-to-net8/).

---

## Solution

By placing extension methods in the same namespace as the original LINQ (`System.Linq`), existing source files pick up the polyfills automatically without any changes — any file that already has `using System.Linq;` gains the missing methods transparently.

A `#if !NET9_0_OR_GREATER` guard ensures the implementation is automatically disabled when the project is later upgraded to .NET 9 or later.
No file deletions or code rewrites are needed at migration time.

The implementation of `CountBy` and `AggregateBy` accumulates per-key state directly in an internal `Dictionary`, just as the originals do.
Delegating to `GroupBy` is a simpler option, but it allocates the intermediate grouping the originals avoid, and its handling of `null` keys (`GroupBy` accepts a `null` key as one group) diverges from the originals (which throw).
Accumulating into a dictionary keeps allocations low while matching the `where TKey : notnull` constraint and the `null`-key behavior of the originals.

The key point is to validate arguments eagerly while keeping execution deferred.
The public method validates its arguments and then delegates to a `private` iterator that contains the `yield return`.
Turning the entire body into a `yield` method would defer the `ArgumentNullException` for `source` and other arguments until enumeration, diverging from the originals.
`Index` simply delegates to the indexed overload of `Select`, which already applies the same two-stage pattern.

---

## Implementation

The following is a complete polyfill for the three methods, including the `seed` and `seedSelector` overloads of `AggregateBy` — four signatures in total.
Each public method validates its arguments eagerly and then delegates to a deferred iterator body.
Copy it into a file such as `LinqExtensions.Net9.cs` in your project.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET9_0_OR_GREATER // Active only in environments below .NET 9 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Backports .NET 9.0 LINQ methods to older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. CountBy
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return CountByIterator(source, keySelector, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, int>> CountByIterator<TSource, TKey>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var counts = new Dictionary<TKey, int>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // A null key throws ArgumentNullException on the next line.
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }

            foreach (KeyValuePair<TKey, int> entry in counts)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 2. AggregateBy (seed / seedSelector)
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            TAccumulate seed,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, key => seed, func, keyComparer);
        }

        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (seedSelector == null) throw new ArgumentNullException(nameof(seedSelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, seedSelector, func, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var accumulators = new Dictionary<TKey, TAccumulate>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // A null key throws ArgumentNullException on the next line.
                if (!accumulators.TryGetValue(key, out var acc))
                {
                    acc = seedSelector(key);
                }
                accumulators[key] = func(acc!, item);
            }

            foreach (KeyValuePair<TKey, TAccumulate> entry in accumulators)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 3. Index
        // ==========================================
        public static IEnumerable<(int Index, TSource Item)> Index<TSource>(
            this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Select((item, index) => (index, item));
        }
    }
}

#endif
```

The class is active only when the `NET9_0_OR_GREATER` symbol is not defined — that is, in any runtime below .NET 9, including .NET Framework.
The `seed` overload delegates to the shared accumulation body by reading the seed as a constant `seedSelector` (`key => seed`).

---

## Method Walkthroughs

### `CountBy` — Element counts per key

`CountBy` counts elements per key produced by the key selector and returns a sequence of `KeyValuePair<TKey, int>`.

```csharp
var words = new[] { "apple", "banana", "apple", "cherry", "banana", "apple" };

foreach (var pair in words.CountBy(word => word))
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// apple: 3
// banana: 2
// cherry: 1
```

Results are returned in first-appearance order of the keys (absent removals, the internal dictionary enumerates in insertion order).
The overload that accepts an `IEqualityComparer<TKey>` swaps the comparison, for example to count case-insensitively.

### `AggregateBy` — Folding per key

`AggregateBy` accumulates state per key.
The following sums sales per region.

```csharp
var sales = new[]
{
    ("Tokyo", 100),
    ("Osaka", 80),
    ("Tokyo", 120),
    ("Osaka", 60),
};

var totals = sales.AggregateBy(
    keySelector: s => s.Item1,
    seed: 0,
    func: (total, s) => total + s.Item2);

foreach (var pair in totals)
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// Tokyo: 220
// Osaka: 140
```

To vary the initial value per key, use the overload that accepts a `seedSelector` (`Func<TKey, TAccumulate>`) instead of a `seed`.
It starts the fold from a different initial state depending on the key.

### `Index` — Indexed enumeration

`Index` pairs each element with a zero-based index and returns a sequence of `(int Index, TSource Item)` tuples.

```csharp
var items = new[] { "a", "b", "c" };

foreach (var (index, item) in items.Index())
{
    Console.WriteLine($"{index}: {item}");
}
// 0: a
// 1: b
// 2: c
```

The first tuple element is the index and the second is the value.
Note that the order is reversed relative to the `Select((item, index) => ...)` lambda — in `Index`, the index comes first.

### Deferred execution

`CountBy`, `AggregateBy`, and `Index` are all deferred; the computation runs only when the result is enumerated via `foreach` or `.ToList()`.
The `ArgumentNullException` for a `null` source or a `null` delegate, however, is thrown immediately at the call site.
This is because the public method validates arguments first and then returns the deferred iterator.

```csharp
IEnumerable<int> numbers = null!;

// ArgumentNullException is thrown on this line, before any enumeration
var query = numbers.CountBy(n => n);
```

The `ArgumentNullException` for a `null` key returned by the key selector during enumeration, by contrast, is thrown when the key is first used to look up the internal dictionary (the `TryGetValue` call).
This matches the .NET 9 behavior, where the internal dictionary rejects `null` keys.

---

## Choosing the Right Conditional-Compilation Symbol

This implementation uses `#if !NET9_0_OR_GREATER`, which differs from the `#if !NETCOREAPP` guard used in the [.NET 5 backport article](/articles/linq-backport-netframework-to-net5/).

`CountBy`, `AggregateBy`, and `Index` do not exist prior to .NET 9.
Guarding with `NETCOREAPP` or `NET8_0_OR_GREATER` would therefore disable the polyfill on .NET 8 builds, causing a compile error there.

| Symbol | .NET Framework | .NET 8 | .NET 9+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET8_0_OR_GREATER` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET9_0_OR_GREATER` | Polyfill enabled | Polyfill enabled | Polyfill disabled |

`!NET9_0_OR_GREATER` enables the polyfill on all runtimes below .NET 9, including .NET 8, and disables it automatically once the project targets .NET 9 or later.

---

## Caveats

- **Deferred execution**: All three methods are deferred; nothing is computed until enumeration. The return value is a deferred sequence, not a `Dictionary`, so enumerating the same result multiple times recomputes it each time. To materialize and reuse the result, call `.ToList()` or `.ToDictionary()`. The `ArgumentNullException` for a `null` source or delegate is thrown at the call site.
- **The key type is `notnull`**: The original `CountBy` and `AggregateBy` carry a `where TKey : notnull` constraint, so the polyfill applies the same constraint. This keeps nullable-reference analysis consistent before and after migration. If the key selector returns a `null` key at runtime, the internal dictionary throws `ArgumentNullException` during enumeration, which also matches the originals.
- **Memory profile**: The polyfill accumulates only per-key state in an internal `Dictionary`, so it does not retain the elements themselves the way a naive `GroupBy`-based implementation would. Its allocation profile is close to the .NET 9 originals.
- **`Index` tuple order and a confusable type name**: The tuple returned by `Index` is `(Index, Item)`, with the index first. The method name `Index` is also easy to confuse with the `System.Index` type introduced in C# 8 (the element type of index/range syntax), but a method and a type do not collide.
- **No name collision**: The polyfill defines the same signatures as the originals under `System.Linq`, so it resolves transparently in files with `using System.Linq;`. A collision can occur only if a method of the same name and shape is also defined elsewhere.

---

## Alternatives

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Custom polyfill (this article) | No external dependencies; identical usage and allocation profile to the originals | Implementation and maintenance effort | Projects that minimize dependencies |
| Inline `GroupBy(...).Select(...)` | No additional code | Verbose; allocates an intermediate grouping; requires a bulk replacement when migrating | Few call sites and no planned migration |
| Upgrade to .NET 9 | Resolves the root cause; benefits from reduced allocation | Migration cost | When migration is technically and organizationally feasible |

Writing `GroupBy(...).Select(...)` inline requires no extra code, but it allocates an intermediate grouping merely to aggregate and leaves behind the work of finding and replacing every call site later when standardizing on `CountBy` and friends after a .NET 9 migration.
Introducing the polyfill from this article allows code to be written with the original method names before migration; at migration time, the file can stay in place while conditional compilation switches automatically to the originals.

---

## Summary

This article covered `CountBy`, `AggregateBy`, and `Index` — the three LINQ methods added in .NET 9 — and how to backport them safely to .NET Framework.

Three implementation points are worth remembering.

- **Preserve deferred execution**: Validate arguments eagerly in the public method, then delegate to a `private` iterator that contains the `yield return`. The return value is a deferred sequence rather than a `Dictionary`; materialize it with `.ToList()` when a fixed result is needed.
- **Use `#if !NET9_0_OR_GREATER`**: These methods are absent from .NET 8 and earlier, so `!NETCOREAPP` or `!NET8_0_OR_GREATER` would cause a compile error on .NET 8 builds.
- **Match the `where TKey : notnull` constraint and `null`-key behavior**: Accumulating directly in an internal `Dictionary` keeps the constraint, the `null`-key exception, and the allocation profile consistent with the originals, so the switch at migration requires no code changes.

| Method | Evaluation | Return type | Implementation summary |
| --- | --- | --- | --- |
| `CountBy` | Lazy | `IEnumerable<KeyValuePair<TKey, int>>` | Counts per key in an internal `Dictionary` |
| `AggregateBy` | Lazy | `IEnumerable<KeyValuePair<TKey, TAccumulate>>` | Folds per key in an internal `Dictionary` (seed / seedSelector) |
| `Index` | Lazy | `IEnumerable<(int Index, TSource Item)>` | Delegates to `Select((item, index) => (index, item))` |

---

## Related Articles

- [Backporting Missing LINQ Methods from .NET 8 to .NET Framework](/articles/linq-backport-netframework-to-net8/)
- [Backporting Missing LINQ Methods from .NET 7 to .NET Framework](/articles/linq-backport-netframework-to-net7/)
- [Backporting Missing LINQ Methods from .NET 6 to .NET Framework](/articles/linq-backport-netframework-to-net6/)
- [Backporting Missing LINQ Methods from .NET 5 to .NET Framework](/articles/linq-backport-netframework-to-net5/)
