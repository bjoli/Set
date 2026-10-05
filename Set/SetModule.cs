using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Set;

public static class SetModule
{
    // Standard Operations (Set is the first argument)

    /// <summary>What the set compares its elements with.</summary>
    public static IEqualityComparer<T> Comparer<T>(Set<T> set) => set.Comparer;

    /// <summary>
    ///     The empty set comparing elements with <paramref name="comparer" />: the shared one when
    ///     that is the default.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Empty<T>(IEqualityComparer<T>? comparer = null) =>
        comparer is null || ReferenceEquals(comparer, EqualityComparer<T>.Default)
            ? Set<T>.Empty
            : new Set<T>(comparer);

    /// <summary>The set of everything <paramref name="source" /> yields.</summary>
    public static Set<T> FromEnumerable<T>(IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
    {
        var builder = new SetBuilder<T>(comparer);
        builder.AddRange(source);
        return builder.ToImmutable();
    }

    /// <summary>
    ///     The elements, in the trie's own order. A <see cref="Set{T}" /> already is an
    ///     <see cref="IEnumerable{T}" />; this is the name a caller reaching for the static
    ///     interface rather than for the type looks for.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> AsEnumerable<T>(Set<T> set) => set;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Add<T>(Set<T> set, T key) => set.Add(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> AddChecked<T>(Set<T> set, T key) => set.AddChecked(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> AddRange<T>(Set<T> set, IEnumerable<T> range) => set.AddRange(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Remove<T>(Set<T> set, T key) => set.Remove(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> RemoveRange<T>(Set<T> set, IEnumerable<T> range) => set.RemoveRange(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Clear<T>(Set<T> set) => set.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<T>(Set<T> set, T key) => set.Contains(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty<T>(Set<T> set) => set.IsEmpty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<T>(Set<T> set) => set.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Merge<T>(Set<T> set1, Set<T> set2) => set1.Merge(set2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransientSet<T> ToTransient<T>(Set<T> set) => set.ToTransient();

    // Higher-Order Functions (Function is the first argument, Set is the last)
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEach<T>(Action<T> action, Set<T> set) => set.ForEach(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Iter<T>(Func<T, bool> action, Set<T> set) => set.Iter(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<TNew> Map<T, TNew>(Func<T, TNew> action, Set<T> set, IEqualityComparer<TNew>? comparer = null) =>
        set.Map(action, comparer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Filter<T>(Func<T, bool> predicate, Set<T> set) => set.Filter(predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TState Fold<T, TState>(Func<T, TState, TState> action, TState seed, Set<T> set) => set.Fold(seed, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Exists<T>(Func<T, bool> predicate, Set<T> set) => set.Exists(predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FindKey<T>(Func<T, bool> predicate, Set<T> set) => set.FindKey(predicate);

    /// <summary>
    ///     The first element satisfying <paramref name="predicate" />, without the exception
    ///     <see cref="FindKey{T}" /> throws when there is none — "no such element" is an answer a
    ///     caller can be handed rather than one it has to catch.
    ///
    ///     The out is <c>[MaybeNullWhen(false)]</c>: it holds nothing usable when the answer is
    ///     false. Bjolang imports this with <c>(out T)</c> and gets an <c>Option</c>.
    /// </summary>
    public static bool TryFindKey<T>(Func<T, bool> predicate, Set<T> set, [MaybeNullWhen(false)] out T key)
    {
        var result = default(T);
        var hit = !set.Iter(k =>
        {
            if (!predicate(k)) return true;
            result = k;
            return false;
        });

        key = result;
        return hit;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Mutate<T>(Action<TransientSet<T>> action, Set<T> set) => set.Mutate(action);

    // --- Walking -----------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SetCursor<T> Cursor<T>(Set<T> set) => new SetCursor<T>(set);

    /// <summary>
    ///     Advances the cursor, and answers whether it ran off the end.
    ///
    ///     The advance happens *here* rather than in a separate step: a walk asks "is there
    ///     more?" exactly once per element, so folding the two together is what lets the whole
    ///     traversal allocate nothing after the cursor itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CursorDone<T>(SetCursor<T> cursor) => !cursor.Enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CursorCurrent<T>(SetCursor<T> cursor) => cursor.Enumerator.Current;
}

/// <summary>
///     A position in a walk of a <see cref="Set{T}" />.
///
///     <see cref="SetEnumerator{T}" /> is a struct, which is what keeps a <c>foreach</c>
///     allocation-free — and exactly what makes it useless to a caller that has to *hold* the
///     position, since every copy advances independently. This is that struct in a heap cell:
///     one allocation for the walk, none per element.
/// </summary>
public sealed class SetCursor<T>
{
    public SetEnumerator<T> Enumerator;

    public SetCursor(Set<T> set)
    {
        Enumerator = set.GetEnumerator();
    }
}
