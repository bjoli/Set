using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Set;

public static class SetModule
{
    // Standard Operations (Set is the first argument)
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Add<T>(Set<T> set, T key) where T : notnull => set.Add(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> AddChecked<T>(Set<T> set, T key) where T : notnull => set.AddChecked(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> AddRange<T>(Set<T> set, IEnumerable<T> range) where T : notnull => set.AddRange(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Remove<T>(Set<T> set, T key) where T : notnull => set.Remove(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> RemoveRange<T>(Set<T> set, IEnumerable<T> range) where T : notnull => set.RemoveRange(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Clear<T>(Set<T> set) where T : notnull => set.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<T>(Set<T> set, T key) where T : notnull => set.Contains(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty<T>(Set<T> set) where T : notnull => set.IsEmpty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<T>(Set<T> set) where T : notnull => set.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Merge<T>(Set<T> set1, Set<T> set2) where T : notnull => set1.Merge(set2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransientSet<T> ToTransient<T>(Set<T> set) where T : notnull => set.ToTransient();

    // Higher-Order Functions (Function is the first argument, Set is the last)
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEach<T>(Action<T> action, Set<T> set) where T : notnull => set.ForEach(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Iter<T>(Func<T, bool> action, Set<T> set) where T : notnull => set.Iter(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<TNew> Map<T, TNew>(Func<T, TNew> action, Set<T> set) where T : notnull where TNew : notnull => set.Map(action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Filter<T>(Func<T, bool> predicate, Set<T> set) where T : notnull => set.Filter(predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TState Fold<T, TState>(Func<TState, T, TState> action, TState seed, Set<T> set) where T : notnull => set.Fold(seed, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Exists<T>(Func<T, bool> predicate, Set<T> set) where T : notnull => set.Exists(predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FindKey<T>(Func<T, bool> predicate, Set<T> set) where T : notnull => set.FindKey(predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Mutate<T>(Action<TransientSet<T>> action, Set<T> set) where T : notnull => set.Mutate(action);
}
