---
layout: article-en
title: "Replacing GroupBy and Full-Sort Workarounds — Implementing Chunk, MaxBy, MinBy and DistinctBy"
date: 2026-07-13
category: C#
excerpt: "Measuring the runtime cost of GroupBy and full-sort workaround idioms, then replacing them with Chunk, MaxBy, MinBy and DistinctBy polyfills."
---

## Overview

`OrderByDescending(x => x.Price).First()` and `GroupBy(x => x.Key).Select(g => g.First())` appear over and over in real .NET Framework codebases.
They are workaround idioms for "element with the maximum key" and "distinct by key" — and their runtime cost is out of proportion to what they compute.
The code sorts the entire sequence to fetch one element, and builds per-key element lists just to discard everything but the first.

.NET 6 added `Chunk`, `MaxBy`, `MinBy` and `DistinctBy` as dedicated operators that eliminate both the verbosity and the cost of these idioms.
This article measures what each workaround actually pays, then implements polyfills that bring the same improvement to .NET Framework.
It also covers the version-specific migration guard that first becomes necessary with this batch of methods.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 (backport target) / .NET 6+ (future migration target)
- APIs: LINQ `Chunk`, `MaxBy`, `MinBy`, `DistinctBy`
- Approach: apply `#nullable enable`; disable automatically on migration via `#if !NET6_0_OR_GREATER`
- Language version: the implementation uses `#nullable enable` and `using var`, requiring C# 8.0 or later. The .NET Framework 4.8 default is C# 7.3, so set `LangVersion` to `8.0` or later in the `.csproj` (to stay on C# 7.3, replace `using var` with a plain `using` and drop `#nullable enable`)

---

## Problem: What the Workaround Idioms Cost

The following methods added in .NET 6 are unavailable on .NET Framework.

| Method | Added in | Description |
| --- | --- | --- |
| `Chunk<T>` | .NET 6.0 | Splits a sequence into chunks of at most the given size |
| `MaxBy<T, TKey>` | .NET 6.0 | Returns the element whose key is the maximum |
| `MinBy<T, TKey>` | .NET 6.0 | Returns the element whose key is the minimum |
| `DistinctBy<T, TKey>` | .NET 6.0 | Filters elements by key uniqueness |

Producing the same results without them requires workaround idioms.

| Goal | Workaround idiom | Runtime cost |
| --- | --- | --- |
| Split into chunks | Indexed `Select` + `GroupBy(t => t.i / size)` | Groups all elements on first enumeration, plus intermediate tuples |
| Max/min by key | `OrderByDescending(x => x.Key).First()` | Full $O(n \log n)$ sort |
| Distinct by key | `GroupBy(x => x.Key).Select(g => g.First())` | Full per-key element lists |

The problem is not only verbosity.
It is the entrenched mismatch between goal and cost: paying for a full sort when one element is needed, and keeping every element of a group whose head is the only element used.

---

## Root Cause / Background

After feature development for .NET Framework 4.8 ended, LINQ changes through .NET 5 centered on internal performance work, with few new operators.
.NET 6 was the first release to ship this batch — `Chunk`, `MaxBy`, `MinBy`, `DistinctBy` — and none of them exist in .NET 5 or earlier.
`MaxBy`, `MinBy` and `DistinctBy` operate by key, while `Chunk` splits elements by size; the criteria differ, but all four previously required composing several methods.
The fact that they are missing from .NET 5 as well directly drives the migration-guard symbol choice discussed later.

---

## Solution

The extension methods are defined in the original `System.Linq` namespace so existing code picks them up transparently.
The namespace strategy and the validation/iterator split are justified in the [series foundation article](/articles/linq-backport-netframework-to-net5/); this article focuses on what is specific to these four methods.

Two points are specific here.
First, the four methods split into two evaluation strategies — lazy (`Chunk`, `DistinctBy`) and eager (`MaxBy`, `MinBy`) — and the method structure must follow each.
Second, the migration guard must be `!NET6_0_OR_GREATER`, not `!NETCOREAPP`.

---

## Implementation

The following is the complete polyfill for all four methods.
Add it to the project as, for example, `LinqExtensions.Net6.cs`.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET6_0_OR_GREATER // Active only below .NET 6.0 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Provides extension methods that backfill LINQ methods introduced in .NET 6.0 for older target frameworks.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Chunk
        // ==========================================
        public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");

            return ChunkIterator(source, size);
        }

        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size)
        {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var chunk = new TSource[size];
                chunk[0] = enumerator.Current;
                int count = 1;

                while (count < size && enumerator.MoveNext())
                {
                    chunk[count++] = enumerator.Current;
                }

                if (count < size)
                {
                    Array.Resize(ref chunk, count);
                }

                yield return chunk;
            }
        }

        // ==========================================
        // 2. MaxBy
        // ==========================================
        public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MaxBy(source, keySelector, comparer: null);
        }

        public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                // Reference / nullable value types return default (null); only non-nullable value types throw (matches .NET 6)
                if (default(TSource) is null) return default;
                throw new InvalidOperationException("Sequence contains no elements.");
            }

            var maxElement = enumerator.Current;
            var maxKey = keySelector(maxElement);

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;
                var currentKey = keySelector(currentElement);

                if (comparer.Compare(currentKey, maxKey) > 0)
                {
                    maxElement = currentElement;
                    maxKey = currentKey;
                }
            }

            return maxElement;
        }

        // ==========================================
        // 3. MinBy
        // ==========================================
        public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MinBy(source, keySelector, comparer: null);
        }

        public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                // Reference / nullable value types return default (null); only non-nullable value types throw (matches .NET 6)
                if (default(TSource) is null) return default;
                throw new InvalidOperationException("Sequence contains no elements.");
            }

            var minElement = enumerator.Current;
            var minKey = keySelector(minElement);

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;
                var currentKey = keySelector(currentElement);

                if (comparer.Compare(currentKey, minKey) < 0)
                {
                    minElement = currentElement;
                    minKey = currentKey;
                }
            }

            return minElement;
        }

        // ==========================================
        // 4. DistinctBy
        // ==========================================
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return DistinctByIterator(source, keySelector, comparer: null);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return DistinctByIterator(source, keySelector, comparer);
        }

        private static IEnumerable<TSource> DistinctByIterator<TSource, TKey>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            var knownKeys = new HashSet<TKey>(comparer);
            foreach (var item in source)
            {
                if (knownKeys.Add(keySelector(item)))
                {
                    yield return item;
                }
            }
        }
    }
}

#endif
```

The class is active only when the `NET6_0_OR_GREATER` symbol is undefined — that is, on any environment below .NET 6, including .NET Framework.

---

## What Each Replacement Buys

### `MaxBy` / `MinBy`: From Full Sort to Single Pass

The `OrderByDescending(...).First()` idiom sorts everything to fetch one element.

```csharp
var products = new[]
{
    new { Name = "A", Price = 300 },
    new { Name = "B", Price = 100 },
    new { Name = "C", Price = 200 },
};

// Workaround: O(n log n) full sort
var before = products.OrderByDescending(p => p.Price).First();

// MaxBy: O(n) single pass
var after = products.MaxBy(p => p.Price);
// after: { Name = "A", Price = 300 }
```

Internally, the polyfill walks the sequence once, tracking the current maximum key and its element.

```csharp
var maxElement = enumerator.Current;
var maxKey = keySelector(maxElement);

while (enumerator.MoveNext())
{
    var currentElement = enumerator.Current;
    var currentKey = keySelector(currentElement);

    if (comparer.Compare(currentKey, maxKey) > 0) // Does the current key beat the maximum?
    {
        maxElement = currentElement;
        maxKey = currentKey;
    }
}

return maxElement;
```

Removing the sort drops the complexity from $O(n \log n)$ to $O(n)$ and eliminates the sort workspace.
`MinBy` merely flips the comparison from `> 0` to `< 0`.
Note that where `Max()` / `Min()` return the key value itself, `MaxBy` / `MinBy` return the **original element** that carries the key.

### `DistinctBy`: From Group Building to `HashSet` Checks

The `GroupBy(...).Select(g => g.First())` idiom makes every group hold all its elements even though only the head is used.

```csharp
var products = new[]
{
    new { Name = "A", Category = "Food" },
    new { Name = "B", Category = "Tech" },
    new { Name = "C", Category = "Food" },
};

// Workaround: build per-key element lists, then take each head
var before = products.GroupBy(p => p.Category).Select(g => g.First());

// DistinctBy: stream elements through a seen-key check
var after = products.DistinctBy(p => p.Category);
// after: { Name = "A", Category = "Food" }, { Name = "B", Category = "Tech" }
```

Internally, the polyfill adds each key to a `HashSet<TKey>` and yields only the elements whose key was not seen before.

```csharp
var knownKeys = new HashSet<TKey>(comparer); // O(1) registration and lookup

foreach (var item in source)
{
    if (knownKeys.Add(keySelector(item))) // true when the key is new
    {
        yield return item;
    }
}
```

Only keys are retained — element bodies are never accumulated (space complexity $O(\text{unique keys})$).
Unlike `GroupBy`, nothing is read ahead: elements stream out lazily, one at a time, in their original order.

### `Chunk`: From Index Arithmetic to Sequential Slicing

Grouping by `index / size` allocates intermediate tuples, and `GroupBy` reads the entire source on first enumeration.
`Chunk` slices arrays of at most `size` elements as it enumerates.

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.Chunk(2);
// result: [1, 2], [3, 4], [5]
```

Internally, an array of `size` elements is pre-allocated and filled; only a trailing partial chunk is trimmed with `Array.Resize`.

```csharp
var chunk = new TSource[size]; // Pre-allocate size elements
chunk[0] = enumerator.Current;
int count = 1;

while (count < size && enumerator.MoveNext())
{
    chunk[count++] = enumerator.Current;
}

if (count < size)
{
    Array.Resize(ref chunk, count); // Resize only the trailing partial chunk
}

yield return chunk;
```

Each chunk is built only after the previous one is yielded, so the up-front whole-sequence grouping disappears.

---

## Two Evaluation Strategies, Two Method Shapes

The four methods split into two evaluation strategies, and their structure follows suit.

**Lazy methods (`Chunk` / `DistinctBy`)** are iterators built on `yield return` and process nothing until enumerated.
Because `yield return` also defers argument checks, the public method and the `~Iterator` method are separated to make exceptions immediate.
The rationale and the failure mode of skipping this split are covered in [principle 1 of the foundation article](/articles/linq-backport-netframework-to-net5/).

**Eager methods (`MaxBy` / `MinBy`)** contain no `yield return`; they scan the whole sequence at call time and return a single result.
Argument checks also run at call time, so no separation is needed.

Whether the return value is a sequence or a single element determines the evaluation strategy, and the strategy determines the method shape.
That is the order in which to make the structural decision when writing a polyfill.

---

## Migration Guard: The First Case That Needs a Version-Specific Symbol

The `#if !NETCOREAPP` guard used in the [foundation article](/articles/linq-backport-netframework-to-net5/) does not work for this batch.
The `NETCOREAPP` symbol is also defined on .NET Core and .NET 5, so `!NETCOREAPP` would disable the polyfill in a .NET 5 build.
`Chunk` and the other three methods do not exist in .NET 5, so the build breaks at that point.

| Symbol | .NET Framework | .NET 5 | .NET 6+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | Polyfill active | **Polyfill disabled (build error)** | Polyfill disabled |
| `!NET6_0_OR_GREATER` | Polyfill active | Polyfill active | Polyfill disabled |

The correct rule is to disable at and above the version that introduced the methods, which here means `#if !NET6_0_OR_GREATER`.
Generalized: for methods added in .NET X, guard with `#if !NETX_0_OR_GREATER`.
The backports for later versions (such as `Order` in .NET 7) apply the same rule.

---

## Caveats

- **`MaxBy` / `MinBy` on an empty sequence**: for a reference or nullable value element type, an empty source returns `default` (`null`); only a non-nullable value type (such as `int` or a `struct`) throws `InvalidOperationException`. This matches the .NET 6 built-in behavior (the return type is `TSource?`). When the element type is a non-nullable value type and the sequence may be empty, check with `.Any()` beforehand or wrap in `try-catch`.
- **Size of the trailing chunk**: when the element count is not a multiple of `size`, the last chunk is smaller. Caller code that assumes uniform chunk sizes is incorrect.
- **`DistinctBy` and `null` keys**: `null` keys compare equal to each other, so only the first element with a `null` key is emitted.
- **Scope of `#nullable enable`**: the directive at the top of the file enables the annotation context file-wide. Repeating it is harmless even when the project sets `<Nullable>enable</Nullable>` globally.

---

## Alternatives / Comparison

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Hand-rolled polyfill (this article) | No dependency; also fixes the complexity problems | Implementation effort | Projects minimizing dependencies |
| Keep the workaround idioms | No extra code | Complexity and allocation waste persists | Data that is always tiny |
| MoreLINQ (NuGet) | Rich, battle-tested method set | Adds an external dependency | Projects needing many extra methods |
| Upgrade to .NET 6 | Root fix plus performance gains | Migration cost | When migration is feasible |

`MoreLINQ` (NuGet package `morelinq`) covers `Chunk` and `MaxBy` equivalents, but when only these four methods are needed, a dependency-free polyfill is simpler to maintain.

---

## Summary

Whether to keep the workaround idioms or replace them comes down to how much data the code processes and how often.
A few dozen items rendered per screen refresh will not expose the problem; as volume and frequency grow, the cost of full sorts and intermediate group building stops being ignorable.

| Method | Workaround cost | Polyfill cost |
| --- | --- | --- |
| `MaxBy` / `MinBy` | $O(n \log n)$ full sort | $O(n)$ single pass |
| `DistinctBy` | Per-key element lists | Key set only ($O(\text{unique keys})$) |
| `Chunk` | Up-front whole-sequence grouping | Sequential per-chunk build ($O(size)$) |

The replacement itself is just adding polyfills with the original names and signatures, and the `#if !NET6_0_OR_GREATER` guard switches to the built-in implementations automatically upon migrating to .NET 6.

---

## Related Articles

- [Designing LINQ Polyfills That Preserve Lazy Evaluation — Implementing Append, Prepend, TakeLast and SkipLast](/articles/linq-backport-netframework-to-net5/)
- [Order and OrderDescending by Pure Delegation — A Minimal Polyfill with IOrderedEnumerable Compatibility](/articles/linq-backport-netframework-to-net7/)
- [Selector-Free ToDictionary — Designing for Overload Resolution and the notnull Constraint](/articles/linq-backport-netframework-to-net8/)
- [Key-Based Aggregation Without GroupBy — Dictionary-Backed CountBy, AggregateBy and Index](/articles/linq-backport-netframework-to-net9/)
- [Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle](/articles/linq-backport-netframework-to-net10/)
