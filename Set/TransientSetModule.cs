using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Set;

/// <summary>
///     The static interface to <see cref="TransientSet{T}" />.
///
///     It is <see cref="SetModule" />'s interface with the writes turned inside out: where the
///     persistent set answers a new set, the transient one mutates and answers nothing. A
///     transient is a <see cref="Set{T}" /> being edited in place — the nodes it owns are its
///     own, so an edit costs no copy — and <see cref="ToPersistent{T}" /> hands the result back
///     as an immutable set in O(1).
/// </summary>
public static class TransientSetModule
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransientSet<T> Empty<T>() => Set<T>.Empty.ToTransient();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransientSet<T> FromPersistent<T>(Set<T> set) => set.ToTransient();

    /// <summary>
    ///     The set as it stands. The transient stays usable afterwards and disowns the nodes it
    ///     handed over, so a later edit copies rather than corrupting the snapshot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Set<T> ToPersistent<T>(TransientSet<T> set) => set.ToImmutable();

    public static TransientSet<T> FromEnumerable<T>(IEnumerable<T> source)
    {
        var transient = Set<T>.Empty.ToTransient();
        foreach (var key in source) transient.Add(key);
        return transient;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(TransientSet<T> set, T key) => set.Add(key);

    public static void AddRange<T>(TransientSet<T> set, IEnumerable<T> range)
    {
        foreach (var key in range) set.Add(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Remove<T>(TransientSet<T> set, T key) => set.Remove(key);

    public static void RemoveRange<T>(TransientSet<T> set, IEnumerable<T> range)
    {
        foreach (var key in range) set.Remove(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<T>(TransientSet<T> set, T key) => set.Contains(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<T>(TransientSet<T> set) => set.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty<T>(TransientSet<T> set) => set.IsEmpty;
}
