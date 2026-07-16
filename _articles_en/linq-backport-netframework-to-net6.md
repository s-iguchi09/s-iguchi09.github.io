---
layout: article-en
title: "Backporting Chunk, MaxBy, MinBy and DistinctBy to .NET Framework"
date: 2026-07-13
category: C#
excerpt: "Backporting Chunk, MaxBy, MinBy, and DistinctBy to .NET Framework as polyfills, using #nullable enable and conditional compilation for a zero-cost migration."
---

## Overview

The LINQ methods added in .NET 6 — `Chunk`, `MaxBy`, `MinBy` and `DistinctBy` — each express a "key-based or fixed-size" operation in a single call. Because .NET Framework lacks them, the equivalent must be spelled out with verbose combinations of `GroupBy` or `OrderByDescending().First()`.

This article enumerates the **four LINQ methods added in .NET 6** (`Chunk`, `MaxBy`, `MinBy`, `DistinctBy`) and explains how to implement them as extension methods (polyfills) that behave identically to the originals. It also covers a modern implementation style using `#nullable enable` and a conditional-compilation technique that eliminates migration cost when eventually upgrading to .NET 6 or later.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 / .NET 6+
- APIs: LINQ `Chunk`, `MaxBy`, `MinBy`, `DistinctBy`
- Migration guard: `#if !NET6_0_OR_GREATER`

---

## Problem

The following LINQ methods added in .NET 6 are unavailable in .NET Framework environments.

| Method | Added in | Description |
| --- | --- | --- |
| `Chunk<T>` | .NET 6.0 | Splits a sequence into chunks of a specified maximum size |
| `MaxBy<T, TKey>` | .NET 6.0 | Returns the element with the maximum key value |
| `MinBy<T, TKey>` | .NET 6.0 | Returns the element with the minimum key value |
| `DistinctBy<T, TKey>` | .NET 6.0 | Filters elements based on the uniqueness of a specified key |

Without these methods, developers are forced to write workarounds such as the following.

- Simulate `Chunk` using index-based `GroupBy`.
- Replace `MaxBy` with `OrderByDescending(x => x.Key).FirstOrDefault()`, which sorts the entire sequence.
- Replace `DistinctBy` with `GroupBy(x => x.Key).Select(g => g.First())`.

These workarounds reduce code readability and introduce unnecessary overhead — the `MaxBy` replacement, for example, performs a full sort when only a single linear pass is required.

---

## Background

.NET Framework 4.8 was the final release of the framework. The period from .NET Core through .NET 5 focused mainly on rewriting LINQ internals for performance rather than adding large numbers of new APIs.

.NET 6 introduced a significant batch of convenience methods. `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy` were all first added in .NET 6.0 and are not available in any earlier runtime.

The four methods added between .NET Framework 4.8 and .NET 5 (`Append`, `Prepend`, `TakeLast`, `SkipLast`) are covered in a [separate article](/articles/linq-backport-netframework-to-net5/).

---

## Solution

By placing extension methods in the same namespace as the original LINQ (`System.Linq`), existing source files pick up the polyfills automatically without any changes — any file that already has `using System.Linq;` gains the missing methods transparently.

A `#if !NET6_0_OR_GREATER` guard ensures the implementation is automatically disabled when the project is later upgraded to .NET 6 or later. No file deletions or code rewrites are needed at migration time.

---

## Implementation

The following is a complete polyfill for all four methods. Copy it into a file such as `LinqExtensions.Net6.cs` in your project.

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET6_0_OR_GREATER // Active only in environments below .NET 6 (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Backports .NET 6.0 LINQ methods to older target frameworks.
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
        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MaxBy(source, keySelector, comparer: null);
        }

        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
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
        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MinBy(source, keySelector, comparer: null);
        }

        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
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

The class is active only when the `NET6_0_OR_GREATER` symbol is not defined — that is, in any runtime below .NET 6, including .NET Framework.

---

## Design: Eager vs. Lazy Evaluation

The four methods split into two evaluation strategies, each requiring a different method structure.

**Lazy-evaluation methods (`Chunk` / `DistinctBy`)** are implemented as iterator blocks using `yield return`. They do not process any data until the caller iterates the result via `foreach` or `.ToList()`. Because argument validation is also deferred, the public method and the private `~Iterator` method must be kept separate.

**Eager-evaluation methods (`MaxBy` / `MinBy`)** contain no `yield return`. They traverse the entire sequence immediately when called, so argument validation also runs immediately. Method splitting is not required.

The following illustrates what goes wrong when a lazy method is incorrectly merged into one.

```csharp
// Bad design — validation and iterator merged into one method
public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
    this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
{
    if (source == null) throw new ArgumentNullException(nameof(source)); // (1)

    var knownKeys = new HashSet<TKey>();
    foreach (var item in source) // Because yield return exists, (1) only executes here
    {
        if (knownKeys.Add(keySelector(item)))
        {
            yield return item;
        }
    }
}
```

A method containing `yield return` does not execute any code when it is called. Passing `null` to the above method returns without throwing — the null check at `(1)` is skipped — and the `ArgumentNullException` only surfaces when the caller later enumerates the result. The error origin and the exception site are separated, making debugging difficult.

Splitting the method into a public wrapper and a private iterator ensures that argument validation runs at the call site. This pattern is used throughout the .NET runtime's own LINQ implementation.

---

## Method Walkthroughs

### `Chunk<T>` — Split into fixed-size groups

Splits a sequence into chunks of at most `size` elements each.

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.Chunk(2);
// result: [1, 2], [3, 4], [5]
```

#### How Chunk works

An array of `size` elements is pre-allocated, then filled sequentially.

```csharp
var chunk = new TSource[size]; // Pre-allocate for the full chunk size
chunk[0] = enumerator.Current;
int count = 1;

while (count < size && enumerator.MoveNext())
{
    chunk[count++] = enumerator.Current;
}

if (count < size)
{
    Array.Resize(ref chunk, count); // Resize only for the final partial chunk
}

yield return chunk;
```

`Array.Resize` is called only for the last chunk when the total element count is not a multiple of `size`. All other chunks are returned using the pre-allocated array as-is, keeping unnecessary allocations to a minimum.

---

### `MaxBy<T, TKey>` / `MinBy<T, TKey>` — Element with the maximum or minimum key

Returns the element whose key is the largest or smallest. Unlike `Max()` and `Min()`, which return the key value itself, `MaxBy` and `MinBy` return **the original element** corresponding to that key.

```csharp
var products = new[]
{
    new { Name = "A", Price = 300 },
    new { Name = "B", Price = 100 },
    new { Name = "C", Price = 200 },
};

var mostExpensive = products.MaxBy(p => p.Price);
// mostExpensive: { Name = "A", Price = 300 }
```

#### How MaxBy works

A single pass through the sequence tracks the current maximum key and the corresponding element, updating them whenever a larger key is found.

```csharp
var maxElement = enumerator.Current;
var maxKey = keySelector(maxElement);

while (enumerator.MoveNext())
{
    var currentElement = enumerator.Current;
    var currentKey = keySelector(currentElement);

    if (comparer.Compare(currentKey, maxKey) > 0) // Does the current key exceed the maximum?
    {
        maxElement = currentElement;
        maxKey = currentKey;
    }
}

return maxElement;
```

This avoids the $O(n \log n)$ full sort required by `OrderByDescending(...).FirstOrDefault()`, performing the work in a single linear pass — $O(n)$. `MinBy` is structurally identical, with only the comparison direction changed from `> 0` to `< 0`.

---

### `DistinctBy<T, TKey>` — Deduplicate by key

Where `Distinct()` filters on element identity, `DistinctBy` filters on the **uniqueness of a specified key**, returning the first element encountered for each distinct key value.

```csharp
var products = new[]
{
    new { Name = "A", Category = "Food" },
    new { Name = "B", Category = "Tech" },
    new { Name = "C", Category = "Food" },
};

var result = products.DistinctBy(p => p.Category);
// result: { Name = "A", Category = "Food" }, { Name = "B", Category = "Tech" }
```

#### How DistinctBy works

A `HashSet<TKey>` tracks seen keys. An element is yielded only if its key has not been seen before.

```csharp
var knownKeys = new HashSet<TKey>(comparer); // O(1) membership test and insertion

foreach (var item in source)
{
    if (knownKeys.Add(keySelector(item))) // Returns true if the key was newly added
    {
        yield return item; // Emit only the first occurrence of each key
    }
}
```

`HashSet.Add` returns `true` when the key did not previously exist and `false` when it did. This yields unique elements in their original order in a single pass, with space complexity $O(\text{distinct count})$.

---

## Choosing the Right Conditional-Compilation Symbol

This implementation uses `#if !NET6_0_OR_GREATER`, which differs from the `#if !NETCOREAPP` guard used in the [.NET 5 backport article](/articles/linq-backport-netframework-to-net5/).

The `NETCOREAPP` symbol is defined for .NET Core and all versions of .NET 5 and later. Using it would disable the polyfill on .NET 5 builds — but `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy` do not exist in .NET 5, so that would cause a compile error.

| Symbol | .NET Framework | .NET 5 | .NET 6+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | Polyfill enabled | **Polyfill disabled (compile error)** | Polyfill disabled |
| `!NET6_0_OR_GREATER` | Polyfill enabled | Polyfill enabled | Polyfill disabled |

`!NET6_0_OR_GREATER` enables the polyfill on all runtimes below .NET 6, including .NET 5, and disables it automatically once the project targets .NET 6 or later.

---

## Caveats

- **Empty sequences with `MaxBy` / `MinBy`**: Passing an empty sequence throws `InvalidOperationException`. This matches the behavior of the .NET 6 originals. Use `.Any()` to verify the sequence has elements before calling, or wrap the call in a `try-catch`.
- **The last chunk from `Chunk`**: When the total element count is not a multiple of `size`, the final chunk is smaller than `size`. Code that assumes all chunks are a fixed size is incorrect.
- **`null` keys in `DistinctBy`**: If the key selector returns `null`, all `null` keys are treated as equal. Only the first element with a `null` key is yielded.
- **`#nullable enable` scope**: The directive at the top of the file enables nullable analysis for the entire file. If the project already sets `<Nullable>enable</Nullable>` globally, including the directive again is harmless.

---

## Alternatives

| Approach | Pros | Cons | Best for |
| --- | --- | --- | --- |
| Custom polyfill (this article) | No external dependencies; full control over the code | Requires implementation effort | Projects that minimize dependencies |
| MoreLINQ (NuGet) | Rich method set; already tested | Adds an external dependency | Projects that need many additional LINQ methods |
| Upgrade to .NET 6 | Resolves the root cause; gains performance improvements | Migration cost | When migration is technically and organizationally feasible |

`MoreLINQ` (NuGet package name: `morelinq`) includes equivalents of `Chunk` and `MaxBy`, among many others. However, package management in .NET Framework projects can be complex. If the requirement is limited to the four methods covered here, a self-contained polyfill is simpler to maintain.

---

## Summary

This article covered `Chunk`, `MaxBy`, `MinBy`, and `DistinctBy` — the four LINQ methods added in .NET 6 — and how to backport them safely to .NET Framework.

Three implementation points are worth remembering.

- **Distinguish evaluation strategies**: `Chunk` and `DistinctBy` are lazy; their public methods and private iterators must be kept separate. `MaxBy` and `MinBy` are eager; method splitting is not required.
- **Use `#if !NET6_0_OR_GREATER`**: These four methods are also absent from .NET 5, so `!NETCOREAPP` would cause a compile error on .NET 5 builds.
- **Watch out for empty-sequence exceptions**: `MaxBy` and `MinBy` throw `InvalidOperationException` on an empty sequence. This is identical to the .NET 6 originals.

| Method | Evaluation | Space complexity | Algorithm summary |
| --- | --- | --- | --- |
| `Chunk` | Lazy | $O(size)$ | Pre-allocate array per chunk; resize only the final partial chunk |
| `MaxBy` / `MinBy` | Eager | $O(1)$ | Single linear pass, tracking the running maximum/minimum |
| `DistinctBy` | Lazy | $O(\text{distinct count})$ | `HashSet` for key tracking; emit in original order |

---

## Related Articles

- [Backporting Append, Prepend, TakeLast and SkipLast to .NET Framework](/articles/linq-backport-netframework-to-net5/)
