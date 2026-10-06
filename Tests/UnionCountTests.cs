using Set;

namespace Tests;

// Union: Count, contents, which of two equal elements is kept, and sharing with the left set.
public class UnionCountTests
{
    private static Set<int> Make(IEqualityComparer<int> cmp, IEnumerable<int> xs)
    {
        var s = new Set<int>(cmp);
        foreach (var x in xs) s = s.Add(x);
        return s;
    }

    private static void AssertUnion(Set<int> a, Set<int> b)
    {
        foreach (var (l, r) in new[] { (a, b), (b, a) })
        {
            var want = new HashSet<int>(l, l.Comparer);
            want.UnionWith(r);
            var u = l.Merge(r);
            Assert.Equal(want.Count, u.Count);
            Assert.True(want.SetEquals(u), "contents differ");
            Assert.Equal(want.Count, u.ToList().Count);
            CollisionTests.AssertWellFormed(u);
        }
    }

    // Default hashes, and hash = x & ~1024 so x and x | 1024 collide below the root.
    public static TheoryData<int> Masks => new() { 0, 1024 };

    [Theory]
    [MemberData(nameof(Masks))]
    public void Shapes(int mask)
    {
        var cmp = new MaskComparer(mask);
        var evens = Make(cmp, Enumerable.Range(0, 3000).Select(i => 2 * i));
        var odds = Make(cmp, Enumerable.Range(0, 3000).Select(i => 2 * i + 1));
        var mixed = Make(cmp, Enumerable.Range(0, 3000).Select(i => i | ((i & 1) << 10)));
        var empty = new Set<int>(cmp);

        AssertUnion(evens, odds);
        AssertUnion(evens, Make(cmp, Enumerable.Range(1000, 5000)));
        AssertUnion(evens, empty);
        AssertUnion(empty, empty);
        AssertUnion(evens, evens);
        AssertUnion(evens, Make(cmp, evens));
        AssertUnion(evens, evens.Add(-1));
        AssertUnion(evens, evens.Remove(10));
        AssertUnion(evens, evens.Remove(10).Add(11).Add(10 | 1024));
        AssertUnion(evens, Make(cmp, [4]));
        AssertUnion(evens, Make(cmp, [5]));
        AssertUnion(evens, Make(cmp, [4 | 1024]));
        AssertUnion(mixed, evens);
        AssertUnion(mixed, odds);
        AssertUnion(mixed, mixed.Add(4000).Remove(1 | 1024));
        AssertUnion(Make(cmp, [3, 1027]), Make(cmp, [3]));
        AssertUnion(Make(cmp, [3, 1027]), Make(cmp, [3, 1027, 35]));
        AssertUnion(Make(cmp, [3, 1027]), Make(cmp, [1027, 3]));
    }

    [Fact]
    public void AddsNothing_ReturnsLeftSet()
    {
        var big = SetModule.FromEnumerable(Enumerable.Range(0, 100_000));
        Assert.Same(big, big.Merge(big));
        Assert.Same(big, big.Merge(big.Remove(77)));
        Assert.Same(big, big.Merge(SetModule.FromEnumerable([1, 2, 3, 40_000])));
        Assert.Same(big, big.Merge(big.Add(5)));

        var plus = big.Merge(big.Add(-3));
        Assert.Equal(100_001, plus.Count);
        Assert.True(plus.Contains(-3));
    }

    [Fact]
    public void AddsNothing_ReturnsLeftSet_WithCollisions()
    {
        var cmp = new MaskComparer(1024);
        var a = Make(cmp, Enumerable.Range(0, 2000).Select(i => i | ((i & 1) << 10)).Append(2));
        Assert.Same(a, a.Merge(Make(cmp, [2, 2 | 1024])));
        Assert.Same(a, a.Merge(Make(cmp, [1025])));
        Assert.Same(a, a.Merge(a.Remove(2)));
    }

    [Fact]
    public void KeepsLeftElement()
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var lower = SetModule.FromEnumerable(Enumerable.Range(0, 2000).Select(i => "k" + i), cmp);
        var upper = SetModule.FromEnumerable(Enumerable.Range(1000, 2000).Select(i => "K" + i), cmp);

        var lu = lower.Merge(upper);
        Assert.Equal(3000, lu.Count);
        var listed = lu.ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < 2000; i++) Assert.Contains("k" + i, listed);
        for (var i = 2000; i < 3000; i++) Assert.Contains("K" + i, listed);

        var ul = upper.Merge(lower);
        Assert.Equal(3000, ul.Count);
        listed = ul.ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < 1000; i++) Assert.Contains("k" + i, listed);
        for (var i = 1000; i < 3000; i++) Assert.Contains("K" + i, listed);

        // A single right element equal to a left one.
        Assert.Same(lower, lower.Merge(new Set<string>(cmp).Add("K5")));
        Assert.Contains("A", new Set<string>(cmp).Add("A").Merge(new Set<string>(cmp).Add("a")).ToList());
    }

    // Left set and right set of tagged items, randomly overlapping, independent or derived from each other.
    [Fact]
    public void Random_MatchesOracle()
    {
        foreach (var mask in new[] { 0, 1024, 1 << 24 })
        {
            var cmp = new ItemComparer(mask);
            for (var seed = 0; seed < 150; seed++)
            {
                var rng = new Random(seed);
                var range = rng.Next(1, 5000);
                Item Next(string tag) => new(rng.Next(range) | (rng.Next(2) * mask), tag);

                var a = new Set<Item>(cmp);
                for (int i = 0, n = rng.Next(0, 3000); i < n; i++) a = a.Add(Next("L"));

                Set<Item> b;
                if (rng.Next(2) == 0)
                {
                    b = new Set<Item>(cmp);
                    for (int i = 0, n = rng.Next(0, 3000); i < n; i++) b = b.Add(Next("R"));
                }
                else
                {
                    b = a;
                    for (int i = 0, n = rng.Next(0, 20); i < n; i++)
                        b = rng.Next(2) == 0 ? b.Add(Next("R")) : b.Remove(Next(""));
                }

                CheckUnion(a, b);
                CheckUnion(b, a);
            }
        }
    }

    private static void CheckUnion(Set<Item> l, Set<Item> r)
    {
        var want = new Dictionary<int, Item>();
        foreach (var x in l) want.Add(x.Id, x);
        foreach (var x in r) want.TryAdd(x.Id, x);

        var u = l.Merge(r);
        Assert.Equal(want.Count, u.Count);
        var listed = u.ToList();
        Assert.Equal(want.Count, listed.Count);
        foreach (var x in listed) Assert.Equal(want[x.Id], x);
        CollisionTests.AssertWellFormed(u);
    }
}
