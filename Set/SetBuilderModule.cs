using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Set;

/// <summary>
///     The static interface to <see cref="SetBuilder{T}" />: the same operations the type
///     carries, as free functions taking the builder first.
/// </summary>
public static class SetBuilderModule
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SetBuilder<T> Empty<T>(IEqualityComparer<T>? comparer = null) =>
        new SetBuilder<T>(comparer);

    /// <summary>
    ///     A builder holding what <paramref name="set" /> holds. The set is walked, so this costs
    ///     its size — <see cref="Set{T}.ToTransient" /> is the O(1) route to a mutable view.
    /// </summary>
    public static SetBuilder<T> FromSet<T>(Set<T> set)
    {
        var builder = new SetBuilder<T>(null, set.Count == 0 ? 16 : set.Count);
        builder.AddRange(set);
        return builder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(SetBuilder<T> builder, T key) => builder.Add(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<T>(SetBuilder<T> builder, IEnumerable<T> range) =>
        builder.AddRange(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<T>(SetBuilder<T> builder) => builder.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> Build<T>(SetBuilder<T> builder) => builder.ToImmutable();
}
