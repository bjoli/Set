using System.Numerics;
using Set;

namespace Tests;

// Set.Split: the parts hold each element once, are sets of their own, and leave the set as it was.
public class SplitTests
{
    private static Set<int> Of(IEnumerable<int> keys, IEqualityComparer<int>? comparer = null)
    {
        var set = comparer == null ? Set<int>.Empty : new Set<int>(comparer);
        foreach (var k in keys) set = set.Add(k);
        return set;
    }

    // Each key lies under the bits of its hash: the bit of a data entry or a child at its level
    // is the hash's 5 bits there. A collision node holds one full hash. Counts and bitmaps agree.
    private static int CheckNode(NodeBase node, int shift, IEqualityComparer<int> comparer)
    {
        var flags = NodeOps.GetFlags(node.Meta);
        if (flags == NodeFlags.Collision)
        {
            var slots = ((CollisionNode<int>)node).Slots;
            Assert.True(slots.Length >= 2);
            return slots.Length;
        }

        var dataMap = (uint)node.Map;
        ReadOnlySpan<DataSlot<int>> data = flags == NodeFlags.None
            ? NodeOps.GetLeafDataSpan<int>(node)
            : NodeOps.GetDataArray<int>(node);
        Assert.Equal(BitOperations.PopCount(dataMap), data.Length);
        var i = 0;
        for (var m = dataMap; m != 0; m &= m - 1)
        {
            var bit = BitOperations.TrailingZeroCount(m);
            Assert.Equal(bit, (comparer.GetHashCode(data[i++].Key) >>> shift) & 0x1F);
        }

        var n = data.Length;
        if (flags != NodeFlags.Internal) return n;
        var nodeMap = (uint)(node.Map >> 32);
        var children = NodeOps.GetChildSpan<int>(node);
        Assert.Equal(BitOperations.PopCount(nodeMap), children.Length);
        Assert.Equal(0u, dataMap & nodeMap);
        i = 0;
        for (var m = nodeMap; m != 0; m &= m - 1)
        {
            var child = children[i++];
            var bit = BitOperations.TrailingZeroCount(m);
            foreach (var key in Keys(child))
                Assert.Equal(bit, (comparer.GetHashCode(key) >>> shift) & 0x1F);
            n += CheckNode(child, shift + 5, comparer);
        }

        return n;
    }

    private static IEnumerable<int> Keys(NodeBase node)
    {
        var keys = new List<int>();
        TrieOps.Iter<int>(node, k =>
        {
            keys.Add(k);
            return true;
        });
        return keys;
    }

    private static void CheckParts(Set<int> set, int[] keys, int k)
    {
        var parts = set.Split(k);
        Assert.InRange(parts.Length, 1, Math.Max(1, k));
        if (keys.Length >= k) Assert.True(parts.Length >= Math.Min(k, 2) || keys.Length < 2);

        var seen = new HashSet<int>();
        foreach (var part in parts)
        {
            var root = part.Root;
            Assert.Equal(part.Count, root == null ? 0 : CheckNode(root, 0, set.Comparer));
            Assert.Equal(part.Count, part.Count());
            Assert.Same(set.Comparer, part.Comparer);
            foreach (var key in part)
            {
                Assert.True(seen.Add(key), $"key {key} is in two parts");
                Assert.True(part.Contains(key));
            }
        }

        Assert.Equal(keys.Length, seen.Count);
        foreach (var key in keys) Assert.Contains(key, seen);

        // A key is found only in its own part.
        foreach (var part in parts)
            foreach (var key in keys.Take(200))
                Assert.Equal(part.Contains(key), part.Any(e => e == key));

        // Merged back, the parts are the set.
        var merged = parts.Aggregate(new Set<int>(set.Comparer), (acc, p) => acc.Merge(p));
        Assert.True(merged.Equals(set));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(33)]
    [InlineData(1000)]
    [InlineData(20_000)]
    [InlineData(100_000)]
    public void Split_PartsHoldEachEntryOnce(int n)
    {
        var keys = Enumerable.Range(0, n).Select(i => i * 7919 + 3).ToArray();
        var set = Of(keys);
        foreach (var k in new[] { 1, 2, 3, 7, 96, 1000 }) CheckParts(set, keys, k);
    }

    [Fact]
    public void Split_WithCollisions()
    {
        var comparer = new HashFuncComparer(x => x < 0 ? -x % 3 * 32 : x);
        var keys = Enumerable.Range(1, 30).Select(i => -i).Concat(Enumerable.Range(0, 20_000)).ToArray();
        var set = Of(keys, comparer);
        foreach (var k in new[] { 2, 7, 96, 5000 }) CheckParts(set, keys, k);
    }

    [Fact]
    public void Split_ManyKeysOnFewHashBits()
    {
        // Hashes that agree in their low 10 bits put every key two levels down.
        var comparer = new HashFuncComparer(x => x << 10);
        var keys = Enumerable.Range(0, 5000).ToArray();
        var set = Of(keys, comparer);
        foreach (var k in new[] { 2, 7, 96 }) CheckParts(set, keys, k);
    }

    [Fact]
    public void Split_PartsAreClose()
    {
        var set = Of(Enumerable.Range(0, 100_000));
        foreach (var k in new[] { 2, 8, 96 })
        {
            var parts = set.Split(k);
            Assert.Equal(k, parts.Length);
            Assert.All(parts, p => Assert.InRange(p.Count, 100_000 / k / 2, 100_000 / k * 2));
        }
    }

    [Fact]
    public void Split_OneOrEmptyIsTheSet()
    {
        var set = Of(Enumerable.Range(0, 100));
        Assert.Same(set, Assert.Single(set.Split(1)));
        Assert.Same(set, Assert.Single(set.Split(0)));
        var empty = Set<int>.Empty;
        Assert.Same(empty, Assert.Single(empty.Split(8)));
    }

    [Fact]
    public void Split_PartsChangeLikeSets()
    {
        var keys = Enumerable.Range(0, 20_000).ToArray();
        var set = Of(keys);
        var parts = set.Split(16);
        foreach (var part in parts)
        {
            var own = part.ToArray();
            var p = part;
            foreach (var key in own.Where((_, i) => i % 2 == 0)) p = p.Remove(key);
            p = p.Add(-1);
            Assert.Equal(own.Length - (own.Length + 1) / 2 + 1, p.Count);
            Assert.True(p.Contains(-1));
            foreach (var (key, i) in own.Select((key, i) => (key, i)))
                Assert.Equal(i % 2 != 0, p.Contains(key));

            var t = part.ToTransient();
            foreach (var key in own) t.Remove(key);
            Assert.Equal(0, t.Count);
            Assert.Equal(own.Length, part.Count);
        }

        Assert.Equal(keys.Length, set.Count);
        foreach (var key in keys) Assert.True(set.Contains(key));
    }
}
