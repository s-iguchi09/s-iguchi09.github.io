---
layout: article-en
title: "Designing LINQ Polyfills That Preserve Lazy Evaluation — Implementing Append, Prepend, TakeLast and SkipLast"
date: 2026-07-10
category: C#
excerpt: "Three design principles for hand-rolled LINQ polyfills, demonstrated through the Append, Prepend, TakeLast and SkipLast implementations."
---

## Overview

.NET Framework 4.8 continues to ship with Windows, but feature development stopped in 2019, so any LINQ method added since then is unavailable.
The missing methods can be hand-rolled as extension methods (polyfills), yet reproducing the exact feel of standard LINQ takes more than matching signatures.

This article uses the four methods added during the .NET Core 2.0–3.0 era — `Append`, `Prepend`, `TakeLast` and `SkipLast` — to establish three design principles for writing LINQ polyfills.

1. **Split argument validation from the iterator body** — keep lazy evaluation while making exceptions fire at call time
2. **Minimize buffering** — choose between pass-through and sliding-window algorithms
3. **Guard migration with conditional compilation** — switch to the built-in implementation without touching code

These principles apply beyond the four methods covered here: every backport of the methods added in .NET 6 and later (see the related articles at the end) builds on them.
As the foundation article of the series, this piece explains the rationale behind each principle from first causes.

---

## Prerequisites / Environment

- Frameworks: .NET Framework 4.8 (backport target) / .NET 5+ (future migration target)
- APIs: LINQ `Append`, `Prepend`, `TakeLast`, `SkipLast`
- Approach: split public methods from iterators; disable automatically on migration via `#if !NETCOREAPP`

---

## Why the Polyfill Lives in the System.Linq Namespace

The hand-rolled extension methods are deliberately defined in the same `System.Linq` namespace as the originals.
Existing source files already contain `using System.Linq;`, so no additional `using` directive is needed and the missing methods become available without changing a single line of calling code.

This placement works in tandem with the migration guard described later (principle 3): when the project is eventually upgraded, the built-in implementation takes over without any edits at call sites.
Had the methods lived in a custom namespace, migration would require hunting down and deleting every `using` directive and call site, and the transparent switch would not hold.

---

## The Four Operators Added During the .NET Core Era

Between .NET Framework 4.8 and .NET 5 (.NET Core 2.0–3.1), LINQ development focused on rewriting internals for performance rather than adding operators.
The practical additions from that window are the following four methods.

| Method | Added in | Description |
| --- | --- | --- |
| `Append<T>` | .NET Core 2.0 | Appends one element to the end of a sequence |
| `Prepend<T>` | .NET Core 2.0 | Prepends one element to the beginning of a sequence |
| `TakeLast<T>` | .NET Core 3.0 | Returns the last N elements of a sequence |
| `SkipLast<T>` | .NET Core 3.0 | Returns all elements except the last N |

Without them, .NET Framework code keeps resorting to workarounds: allocating an array for `Concat(new[] { element })` instead of `Append`, or counting the sequence first and subtracting to simulate `TakeLast`.
Both obscure intent and invite unnecessary allocations or double enumeration.

Note that methods such as `Chunk` and `MaxBy` arrived in .NET 6, not in this window.
Their implementation is covered in a [separate article](/articles/linq-backport-netframework-to-net6/).

---

## Implementation

The following code makes all four methods available in a .NET Framework project with the same call syntax as the originals.
Copy it into a file such as `LinqExtensions.Net5.cs`.

```csharp
using System;
using System.Collections.Generic;

#if !NETCOREAPP // Active only outside .NET Core / .NET 5+ (e.g. .NET Framework)

namespace System.Linq
{
    /// <summary>
    /// Backports .NET 5-equivalent LINQ methods to .NET Framework environments.
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Append
        // ==========================================
        public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return AppendIterator(source, element);
        }

        private static IEnumerable<TSource> AppendIterator<TSource>(IEnumerable<TSource> source, TSource element)
        {
            foreach (var item in source)
            {
                yield return item;
            }
            yield return element;
        }

        // ==========================================
        // 2. Prepend
        // ==========================================
        public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> source, TSource element)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return PrependIterator(source, element);
        }

        private static IEnumerable<TSource> PrependIterator<TSource>(IEnumerable<TSource> source, TSource element)
        {
            yield return element;
            foreach (var item in source)
            {
                yield return item;
            }
        }

        // ==========================================
        // 3. TakeLast
        // ==========================================
        public static IEnumerable<TSource> TakeLast<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) return Enumerable.Empty<TSource>();

            return TakeLastIterator(source, count);
        }

        private static IEnumerable<TSource> TakeLastIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            var queue = new Queue<TSource>(count);
            foreach (var item in source)
            {
                if (queue.Count == count)
                {
                    queue.Dequeue();
                }
                queue.Enqueue(item);
            }

            foreach (var item in queue)
            {
                yield return item;
            }
        }

        // ==========================================
        // 4. SkipLast
        // ==========================================
        public static IEnumerable<TSource> SkipLast<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) return source;

            return SkipLastIterator(source, count);
        }

        private static IEnumerable<TSource> SkipLastIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            var queue = new Queue<TSource>(count);
            foreach (var item in source)
            {
                if (queue.Count == count)
                {
                    yield return queue.Dequeue();
                }
                queue.Enqueue(item);
            }
        }
    }
}

#endif
```

Two structural traits carry the design principles explained below: every method is split into a validating public method and a private iterator containing `yield return`, and `TakeLast` / `SkipLast` are built on a `Queue<T>`.

---

## Principle 1: Split Argument Validation from the Iterator

Merging the public method and the private `~Iterator` method into one looks simpler.
The split exists because of how lazy evaluation changes the timing of exceptions in C#.

### The Special Behavior of `yield return`

A method containing `yield return` (an iterator block) executes **none of its body at call time**.
The code only starts running when the data is actually needed — when the result is iterated with `foreach` or materialized with `.ToList()`.

Combining the argument check and the loop in one method causes the following problem.

```csharp
// Anti-pattern: validation and iteration in one method
public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
{
    if (source == null) throw new ArgumentNullException(nameof(source)); // (1)

    foreach (var item in source)
    {
        yield return item;
    }
    yield return element;
}
```

Calling this implementation with `null` behaves as follows.

```csharp
IEnumerable<int> numbers = null;

var result = numbers.Append(5); // No error here

Console.WriteLine("Subsequent logic runs...");

foreach (var item in result) // ArgumentNullException is finally thrown here
```

At the call site, even the null check at (1) never runs.
The crash happens much later, at the `foreach`.
The root cause (passing `null`) sits several lines earlier — possibly in another class — while the exception surfaces at a seemingly unrelated loop, making diagnosis difficult.

### Immediate Validation Through Separation

A regular method without `yield return` executes immediately when called.
Placing the argument checks in a regular method and delegating to the iterator afterwards keeps the benefits of lazy evaluation while raising exceptions right at the source of the bug.
Standard LINQ uses this two-stage pattern consistently, and a polyfill must follow it to reproduce the same exception timing as the originals.

---

## Principle 2: Minimize Buffering

All four methods accept `IEnumerable<T>`, so the algorithm must work under the constraint that the sequence length is unknown until fully enumerated.
Two strategies emerge.

### Pass-Through: Append and Prepend

`Append` streams the source through unchanged and emits the extra element once the source is exhausted.

```csharp
foreach (var item in source)
{
    yield return item; // Pass the original data through
}
yield return element; // Emit the appended element at the end
```

`Prepend` is the mirror image: emit the new element first, then follow with the source.

```csharp
yield return element; // Emit the prepended element first

foreach (var item in source)
{
    yield return item; // Then pass the original data through
}
```

Neither stores anything, so space complexity is $O(1)$.
Compared with the `Concat(new[] { element })` workaround, no array is allocated for a single element.

```csharp
var appended = new[] { 1, 2, 3 }.Append(4);   // 1, 2, 3, 4
var prepended = new[] { 1, 2, 3 }.Prepend(0); // 0, 1, 2, 3
```

### Sliding Window: TakeLast and SkipLast

The two tail-based methods cannot be implemented as pass-throughs.
Whether the current element belongs to the last N cannot be decided until later elements arrive.
Both therefore use a `Queue<T>` as a sliding window holding only the most recent `count` elements.

`TakeLast` keeps updating the queue, evicting the oldest element when full; whatever remains after the source ends is exactly the last N.

```csharp
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    if (queue.Count == count)
    {
        queue.Dequeue(); // Push out the oldest element
    }
    queue.Enqueue(item);
}

foreach (var item in queue)
{
    yield return item; // What remains is the last N elements
}
```

`SkipLast` runs the same window in reverse.
When the queue is full and a new element arrives, the evicted front element is guaranteed *not* to be among the last N — so that is the moment to emit it.

```csharp
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    if (queue.Count == count)
    {
        yield return queue.Dequeue(); // Confirmed not among the last N — emit it
    }
    queue.Enqueue(item);
}
// The last N elements left in the queue are never emitted (= skipped)
```

The output always trails the input by `count` elements — that is the crux.
Regardless of the source size, only `count` elements are held at any time, keeping space complexity at $O(count)$.
The double enumeration of the `Count()`-then-`Skip`/`Take` workaround also disappears.

```csharp
var taken = new[] { 1, 2, 3, 4, 5 }.TakeLast(3);   // 3, 4, 5
var skipped = new[] { 1, 2, 3, 4, 5 }.SkipLast(2); // 1, 2, 3
```

---

## Principle 3: Guard Migration with Conditional Compilation

Leaving hand-rolled methods in `System.Linq` and then upgrading the project to .NET 5 or later produces the compile error "The call is ambiguous (CS0121)" because the built-in methods now collide with the polyfill.
The conditional compilation wrapper around the file prevents this.

```csharp
#if !NETCOREAPP
namespace System.Linq
{
    // Extension method implementations...
}
#endif
```

The compiler defines the `NETCOREAPP` symbol automatically when building for .NET Core or .NET 5+.

| Build environment | Result of `#if !NETCOREAPP` | Behavior |
| --- | --- | --- |
| .NET Framework | Condition holds | The polyfill compiles and fills the gap |
| .NET 5 / .NET 6+ | Condition fails | The file is treated as empty; built-in LINQ is used |

When the framework is upgraded, the switch to the built-in implementation happens automatically — no file deletion, no code edits.
This is the migration guard that pairs with the namespace strategy described earlier.

Note that `!NETCOREAPP` is only correct because the methods here were added in .NET Core 2.0–3.0.
Guarding methods added in .NET 6 or later with this condition would disable the polyfill on .NET 5 and break the build.
The choice of version-specific symbols (`NET6_0_OR_GREATER` and friends) is covered in the [.NET 6 backport article](/articles/linq-backport-netframework-to-net6/).

---

## Caveats

- **`TakeLast` / `SkipLast` consume memory proportional to `count`**: the sliding window is frugal, but a huge `count` still allocates a buffer of that size. The approach brings no benefit when the window effectively spans the whole sequence.
- **Behavior for `count <= 0`**: `TakeLast` returns an empty sequence and `SkipLast` returns the source itself. No exception is thrown, matching the built-in behavior.
- **Re-execution on every enumeration**: all four methods are lazy, so enumerating the same query twice walks the source twice. Materialize with `.ToList()` when the result is reused.
- **Performance optimizations are simpler than the originals**: the built-in implementations special-case `IList<T>` sources and more. This polyfill is generic-only; results and exceptions match, but certain collection types may run slower than on modern .NET.

---

## Summary

Using `Append`, `Prepend`, `TakeLast` and `SkipLast` as the subject, this article established three design principles for LINQ polyfills.

| Principle | Purpose |
| --- | --- |
| Split validation from the iterator | Keep lazy evaluation while throwing at call time |
| Minimize buffering | $O(1)$ for pass-through, $O(count)$ for tail-based operations |
| Guard migration with conditional compilation | Switch to built-in LINQ on upgrade without code changes |

| Method | Space complexity | Algorithm |
| --- | --- | --- |
| `Append` / `Prepend` | $O(1)$ | Pass-through |
| `TakeLast` / `SkipLast` | $O(count)$ | Sliding window over a `Queue<T>` |

If these four methods are all that is missing, a hand-rolled polyfill avoids adding an external dependency.
When methods from .NET 6 and later are also needed, the same principles carry over with version-specific guards layered on top (see the related articles).

---

## Related Articles

- [Replacing GroupBy and Full-Sort Workarounds — Implementing Chunk, MaxBy, MinBy and DistinctBy](/articles/linq-backport-netframework-to-net6/)
- [Order and OrderDescending by Pure Delegation — A Minimal Polyfill with IOrderedEnumerable Compatibility](/articles/linq-backport-netframework-to-net7/)
- [Selector-Free ToDictionary — Designing for Overload Resolution and the notnull Constraint](/articles/linq-backport-netframework-to-net8/)
- [Key-Based Aggregation Without GroupBy — Dictionary-Backed CountBy, AggregateBy and Index](/articles/linq-backport-netframework-to-net9/)
- [Expressing SQL Outer Joins in LINQ — Implementing LeftJoin, RightJoin and Shuffle](/articles/linq-backport-netframework-to-net10/)
