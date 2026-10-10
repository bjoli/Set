using Set;

namespace Tests;

// Hashes through a function, so tests can choose which elements collide.
internal sealed class HashFuncComparer(Func<int, int> hash) : IEqualityComparer<int>
{
    public bool Equals(int x, int y) => x == y;
    public int GetHashCode(int x) => hash(x);
}

/// <summary>
///     The struct cursor must give the elements the enumerator gives, in the same order, for
///     every shape of trie: a leaf root, internal nodes with and without elements of their own,
///     collision nodes, and nodes that removals left behind.
/// </summary>
public class CursorTests
{
    private static List<T> Walk<T>(Set<T> set)
    {
        var seen = new List<T>();
        for (var c = SetModule.Cursor(set); !SetModule.CursorDone(c); c = SetModule.CursorNext(c))
            seen.Add(SetModule.CursorCurrent(c));
        return seen;
    }

    private static List<T> Enumerate<T>(Set<T> set)
    {
        var seen = new List<T>();
        foreach (var x in set) seen.Add(x);
        return seen;
    }

    private static void AssertSameWalk<T>(Set<T> set)
    {
        var walked = Walk(set);
        Assert.Equal(Enumerate(set), walked);
        Assert.Equal(set.Count, walked.Count);
    }

    private static Set<int> Ints(int n, IEqualityComparer<int>? comparer = null)
    {
        var set = new Set<int>(comparer ?? EqualityComparer<int>.Default);
        for (var i = 0; i < n; i++) set = set.Add(i * 3);
        return set;
    }

    [Fact]
    public void Empty()
    {
        Assert.True(SetModule.CursorDone(SetModule.Cursor(Set<string>.Empty)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(100_000)]
    public void IntsInEnumeratorOrder(int n)
    {
        AssertSameWalk(Ints(n));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(50_000)]
    public void StringsInEnumeratorOrder(int n)
    {
        var set = Set<string>.Empty;
        for (var i = 0; i < n; i++) set = set.Add("k" + i);
        AssertSameWalk(set);
    }

    [Fact]
    public void Collisions()
    {
        // Every element in one of three hashes: collision nodes at the bottom of the trie.
        AssertSameWalk(Ints(300, new HashFuncComparer(x => x % 3)));
        // All elements in one hash.
        AssertSameWalk(Ints(50, new HashFuncComparer(_ => 0)));
        // Hashes that share long prefixes: deep chains of internal nodes.
        AssertSameWalk(Ints(200, new HashFuncComparer(x => x << 20)));
        AssertSameWalk(Ints(200, new HashFuncComparer(x => (x & 1) == 0 ? int.MinValue | x : x << 27)));
    }

    [Fact]
    public void AfterRemovals()
    {
        // Removals leave internal nodes with no elements of their own.
        var set = Ints(5000);
        for (var i = 0; i < 5000; i += 3) set = set.Remove(i * 3);
        AssertSameWalk(set);
        for (var i = 1; i < 5000; i += 2) set = set.Remove(i * 3);
        AssertSameWalk(set);
    }

    [Fact]
    public void FromTransient()
    {
        var transient = Set<int>.Empty.ToTransient();
        for (var i = 0; i < 3000; i++) transient.Add(i * 7);
        AssertSameWalk(transient.ToImmutable());
    }

    [Fact]
    public void ACursorIsAValue()
    {
        var set = Ints(500);
        var first = SetModule.Cursor(set);
        var element = SetModule.CursorCurrent(first);
        var c = first;
        for (var i = 0; i < 100; i++) c = SetModule.CursorNext(c);

        Assert.Equal(element, SetModule.CursorCurrent(first));
        Assert.Equal(Walk(set).Skip(100).First(), SetModule.CursorCurrent(c));
    }
}
