using System.Numerics;
using Set;

namespace Tests;

// Equality on the int, hash with `mask` cleared: x and x | mask collide.
internal sealed class MaskComparer(int mask) : IEqualityComparer<int>
{
    public bool Equals(int a, int b) => a == b;
    public int GetHashCode(int x) => x & ~mask;
}

internal readonly record struct Item(int Id, string Tag);

// Equality on Id only, so which of two equal items survives is visible in Tag.
internal sealed class ItemComparer(int mask) : IEqualityComparer<Item>
{
    public bool Equals(Item a, Item b) => a.Id == b.Id;
    public int GetHashCode(Item x) => x.Id & ~mask;
}

public class CollisionTests
{
    private const int Collide = 1 << 24;
    private static readonly MaskComparer Cmp = new(Collide);

    // Every element sits where its hash leads, collision nodes hold one full hash and at least
    // two elements, and the tree is no deeper than the enumerator's stack.
    private static void AssertWellFormed<T>(Set<T> set)
    {
        if (set.Root == null)
        {
            Assert.Equal(0, set.Count);
            return;
        }

        var seen = 0;
        Walk(set.Root, 0, 1, [], set.Comparer, ref seen);
        Assert.Equal(set.Count, seen);
    }

    private static void AssertPath(int hash, List<int> path, int shift, int bit)
    {
        for (var k = 0; k < path.Count; k++) Assert.Equal(path[k], (hash >> (5 * k)) & 0x1F);
        if (bit >= 0) Assert.Equal(bit, (hash >> shift) & 0x1F);
    }

    private static void Walk<T>(NodeBase node, int shift, int depth, List<int> path, IEqualityComparer<T> cmp,
        ref int seen)
    {
        Assert.True(depth <= 8, "deeper than the enumerator stack");
        var flags = NodeOps.GetFlags(node.Meta);

        if (flags == NodeFlags.Collision)
        {
            var col = (CollisionNode<T>)node;
            Assert.True(col.Slots.Length >= 2, "collision node with fewer than two elements");
            var hash = cmp.GetHashCode(col.Slots[0].Key!);
            foreach (var s in col.Slots) Assert.Equal(hash, cmp.GetHashCode(s.Key!));
            AssertPath(hash, path, shift, -1);
            seen += col.Slots.Length;
            return;
        }

        var dataMap = (uint)node.Map;
        var data = flags == NodeFlags.Internal
            ? NodeOps.GetDataArray<T>(node)!.AsSpan()
            : NodeOps.GetLeafDataSpan<T>(node);
        Assert.Equal(BitOperations.PopCount(dataMap), data.Length);

        var i = 0;
        for (var m = dataMap; m != 0; m &= m - 1)
            AssertPath(cmp.GetHashCode(data[i++].Key!), path, shift, BitOperations.TrailingZeroCount(m));
        seen += data.Length;

        if (flags != NodeFlags.Internal) return;

        var nodeMap = (uint)(node.Map >> 32);
        Assert.Equal(0u, nodeMap & dataMap);
        var children = NodeOps.GetChildSpan<T>(node);
        Assert.Equal(BitOperations.PopCount(nodeMap), children.Length);

        var c = 0;
        for (var m = nodeMap; m != 0; m &= m - 1)
        {
            path.Add(BitOperations.TrailingZeroCount(m));
            Walk(children[c++], shift + 5, depth + 1, path, cmp, ref seen);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static void AssertSetIs(Set<int> set, IEnumerable<int> expected)
    {
        var want = expected.ToHashSet();
        Assert.Equal(want.Count, set.Count);
        foreach (var x in want) Assert.True(set.Contains(x), $"missing {x}");

        var listed = set.ToList();
        Assert.Equal(want.Count, listed.Count);
        Assert.True(want.SetEquals(listed), "iteration differs");
        AssertWellFormed(set);
    }

    // 0 and Collide share a full hash and are added first, so their collision node sits at depth 1
    // under every later element whose low 5 bits are 0.
    private static int[] Elements(int n) => [0, Collide, .. Enumerable.Range(1, n - 1)];

    private static void RemoveAndCheck(Func<Set<int>, IEnumerable<int>, Set<int>> remove, Set<int> set, int n)
    {
        var all = Elements(n).ToHashSet();
        var gone = all.Where(x => x % 3 == 0 || x == Collide).ToList();
        var after = remove(set, gone);
        AssertSetIs(after, all.Except(gone));
        Assert.False(after.Contains(Collide));
        Assert.False(after.Contains(0));

        // Removing one of the pair leaves the other findable.
        var one = remove(set, [Collide]);
        AssertSetIs(one, all.Where(x => x != Collide));
    }

    [Fact]
    public void Immutable_CollisionAmongManyElements()
    {
        const int n = 5000;
        var set = new Set<int>(Cmp);
        foreach (var x in Elements(n)) set = set.Add(x);
        AssertSetIs(set, Elements(n));

        RemoveAndCheck((s, xs) =>
        {
            foreach (var x in xs) s = s.Remove(x);
            return s;
        }, set, n);
    }

    [Fact]
    public void Transient_CollisionAmongManyElements()
    {
        const int n = 5000;
        var t = new Set<int>(Cmp).ToTransient();
        foreach (var x in Elements(n)) t.Add(x);
        Assert.Equal(n + 1, t.Count);
        var set = t.ToImmutable();
        AssertSetIs(set, Elements(n));

        RemoveAndCheck((s, xs) =>
        {
            var tr = s.ToTransient();
            foreach (var x in xs) tr.Remove(x);
            return tr.ToImmutable();
        }, set, n);
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(40000)] // the sorting builder
    public void Builder_CollisionAmongManyElements(int n)
    {
        var b = new SetBuilder<int>(Cmp);
        foreach (var x in Elements(n)) b.Add(x);
        var set = b.ToImmutable();
        AssertSetIs(set, Elements(n));

        // A later insert of a non-colliding element must not join the collision node.
        var more = set.Add(n).Add(n + 32);
        AssertSetIs(more, Elements(n).Append(n).Append(n + 32));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(20000)] // 3 * 20000 appends takes the sorting builder
    public void Builder_Duplicates(int n)
    {
        var b = new SetBuilder<int>(Cmp);
        for (var round = 0; round < 3; round++)
            foreach (var x in Elements(n))
                b.Add(x);
        AssertSetIs(b.ToImmutable(), Elements(n));
    }

    [Fact]
    public void Builder_OnlyDuplicates()
    {
        var b = new SetBuilder<int>(Cmp);
        for (var i = 0; i < 5; i++) b.Add(7);
        var one = b.ToImmutable();
        AssertSetIs(one, [7]);
        Assert.Equal(NodeFlags.None, NodeOps.GetFlags(one.Root!.Meta));

        b.Add(7 | Collide);
        b.Add(7 | Collide);
        var pair = b.ToImmutable();
        AssertSetIs(pair, [7, 7 | Collide]);
        AssertSetIs(pair.Add(39), [7, 7 | Collide, 39]);
        AssertSetIs(pair.Remove(7), [7 | Collide]);
    }

    [Fact]
    public void Builder_DuplicateKeepsLastAdded()
    {
        var b = new SetBuilder<Item>(new ItemComparer(1024));
        b.Add(new Item(3, "first"));
        b.Add(new Item(1027, "x"));
        b.Add(new Item(3, "last"));
        var set = b.ToImmutable();
        Assert.Equal(2, set.Count);
        Assert.Contains(new Item(3, "last"), set.ToList());
    }

    // hash = x & ~1024: 3 and 1027 collide below the root (low 5 bits 3, next 5 bits 0).
    private static readonly MaskComparer Below = new(1024);

    private static Set<int> Make(IEqualityComparer<int> cmp, params int[] xs)
    {
        var s = new Set<int>(cmp);
        foreach (var x in xs) s = s.Add(x);
        return s;
    }

    [Fact]
    public void Union_CollisionNodeAgainstSubtree_BothOrders()
    {
        var withCollision = Make(Below, 3, 1027, 1, 2);
        var withSubtree = Make(Below, 3, 35, 67, 2, 4);
        AssertWellFormed(withCollision);
        AssertWellFormed(withSubtree);

        int[] union = [1, 2, 3, 4, 35, 67, 1027];
        AssertSetIs(withCollision.Merge(withSubtree), union);
        AssertSetIs(withSubtree.Merge(withCollision), union);
        AssertSetIs(SetModule.Merge(withCollision, withSubtree), union);
    }

    [Fact]
    public void Union_TwoCollisionNodes_BothOrders()
    {
        // 2051 = 3 | 2048 shares 3's path down to bit 11 but not its full hash.
        var a = Make(Below, 3, 1027);
        var b = Make(Below, 1027, 3, 2051);
        int[] union = [3, 1027, 2051];
        AssertSetIs(a.Merge(b), union);
        AssertSetIs(b.Merge(a), union);

        var c = Make(Below, 35 | 1024, 35);
        AssertSetIs(a.Merge(c), [3, 1027, 35, 35 | 1024]);
        AssertSetIs(c.Merge(a), [3, 1027, 35, 35 | 1024]);
    }

    [Fact]
    public void Union_KeepsLeftElement_AcrossCollisionNodes()
    {
        var cmp = new ItemComparer(1024);
        var left = new Set<Item>(cmp).Add(new Item(3, "L")).Add(new Item(1027, "L"));
        var right = new Set<Item>(cmp).Add(new Item(3, "R")).Add(new Item(35, "R"));

        var lr = left.Merge(right).ToList();
        Assert.Equal(3, lr.Count);
        Assert.Contains(new Item(3, "L"), lr);
        Assert.Contains(new Item(1027, "L"), lr);
        Assert.Contains(new Item(35, "R"), lr);

        var rl = right.Merge(left).ToList();
        Assert.Equal(3, rl.Count);
        Assert.Contains(new Item(3, "R"), rl);
    }

    [Fact]
    public void Union_RandomWithManyCollisions_MatchesOracle()
    {
        var rng = new Random(1234);
        foreach (var mask in new[] { 1024, Collide, 0x7FFF_FC00 })
        {
            var cmp = new MaskComparer(mask);
            for (var round = 0; round < 40; round++)
            {
                var xs = Enumerable.Range(0, rng.Next(0, 400)).Select(_ => rng.Next(0, 4096) | (rng.Next(2) * mask))
                    .ToList();
                var ys = Enumerable.Range(0, rng.Next(0, 400)).Select(_ => rng.Next(0, 4096) | (rng.Next(2) * mask))
                    .ToList();
                var a = SetModule.FromEnumerable(xs, cmp);
                var b = Make(cmp, ys.ToArray());
                AssertSetIs(a, xs);
                AssertSetIs(b, ys);
                AssertSetIs(a.Merge(b), xs.Concat(ys));
                AssertSetIs(b.Merge(a), xs.Concat(ys));
            }
        }
    }

    [Fact]
    public void Union_DifferentComparers_UsesLeftComparer()
    {
        var ordinal = Set<string>.Empty.Add("a").Add("B");
        var ignoreCase = new Set<string>(StringComparer.OrdinalIgnoreCase).Add("A").Add("c");

        var u = ordinal.Merge(ignoreCase);
        Assert.Same(ordinal.Comparer, u.Comparer);
        Assert.Equal(4, u.Count);
        foreach (var s in new[] { "a", "B", "A", "c" }) Assert.True(u.Contains(s), s);
        Assert.False(u.Contains("b"));

        var v = ignoreCase.Merge(ordinal);
        Assert.Same(StringComparer.OrdinalIgnoreCase, v.Comparer);
        Assert.Equal(3, v.Count);
        foreach (var s in new[] { "a", "A", "b", "C" }) Assert.True(v.Contains(s), s);
        Assert.Contains("A", v.ToList()); // the left one of "a" / "A"
    }

    [Fact]
    public void Union_DifferentComparers_EmptySides()
    {
        var ignoreCase = new Set<string>(StringComparer.OrdinalIgnoreCase).Add("A").Add("c");

        var u = Set<string>.Empty.Merge(ignoreCase);
        Assert.Same(Set<string>.Empty.Comparer, u.Comparer);
        Assert.Equal(2, u.Count);
        Assert.True(u.Contains("A"));
        Assert.False(u.Contains("a"));

        var v = ignoreCase.Merge(Set<string>.Empty);
        Assert.Same(ignoreCase, v);

        var w = new Set<string>(StringComparer.OrdinalIgnoreCase).Merge(Set<string>.Empty.Add("x"));
        Assert.Same(StringComparer.OrdinalIgnoreCase, w.Comparer);
        Assert.True(w.Contains("X"));
    }

    [Fact]
    public void Union_DifferentComparers_Large()
    {
        var left = SetModule.FromEnumerable(Enumerable.Range(0, 1000).Select(i => "k" + i));
        // Lowercase: for all-uppercase strings OrdinalIgnoreCase hashes like the default comparer.
        var right = SetModule.FromEnumerable(Enumerable.Range(0, 1000).Select(i => "x" + i),
            StringComparer.OrdinalIgnoreCase);
        var u = left.Merge(right);
        Assert.Equal(2000, u.Count);
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(u.Contains("k" + i));
            Assert.True(u.Contains("x" + i));
        }

        AssertWellFormed(u);
    }
}
