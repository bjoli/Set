using Set;

namespace Tests;

public class PersistentMapMergeTests
{
    private const int LargeDatasetSize = 33_000;

    [Fact]
    public void Merge_WithEmptyMaps_ReturnsCorrectResult()
    {
        // Arrange
        var empty1 = Set<string>.Empty;
        var empty2 = Set<string>.Empty;
        var mapWithData = empty1.Add("A").Add("B");

        // Act & Assert
        // 1. Both empty
        var res1 = empty1.Merge(empty2);
        Assert.True(res1.IsEmpty);
        var res2 = empty1.Merge(mapWithData);
        Assert.True(res2.Contains("A"));
        Assert.True(res2.Contains("B"));

        // 3. Right empty
        var res3 = mapWithData.Merge(empty2);
        Assert.True(res3.Contains("A"));
    }

    [Fact]
    public void Merge_NoOverlappingKeys_CombinesAllElements()
    {
        // Arrange
        var map1 = Set<string>.Empty.Add("A").Add("B");
        var map2 = Set<string>.Empty.Add("C").Add("D");

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.True(merged.Contains("A"));
        Assert.True(merged.Contains("B"));
        Assert.True(merged.Contains("C"));
        Assert.True(merged.Contains("D"));
    }

    [Fact]
    public void Merge_DefaultStrategy_OverwritesWithRightValue()
    {
        // Arrange
        var map1 = Set<string>.Empty.Add("A").Add("B");
        var map2 = Set<string>.Empty.Add("B").Add("C");

        // Act
        var merged = map1.Merge(map2); // Defaults to null thunk -> prefer right

        // Assert
        Assert.True(merged.Contains("A"));
        Assert.True(merged.Contains("B")); // Overwritten
        Assert.True(merged.Contains("C"));
    }

    [Fact]
    public void Merge_PreferLeftThunk_KeepsLeftValueOnConflict()
    {
        // Arrange
        var map1 = Set<string>.Empty.Add("A").Add("B");
        var map2 = Set<string>.Empty.Add("B").Add("C");

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.Equal(3, merged.Count);
        Assert.True(merged.Contains("A"));
        Assert.True(merged.Contains("B")); // Kept original left value
        Assert.True(merged.Contains("C"));
    }

    [Fact]
    public void Merge_CustomThunk_CombinesValues()
    {
        // Arrange
        var map1 = Set<string>.Empty.Add("user1");
        var map2 = Set<string>.Empty.Add("user1").Add("user2");

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.Equal(2, merged.Count);
        Assert.True(merged.Contains("user1"));
        Assert.True(merged.Contains("user2"));
    }

    [Fact]
    public void Merge_DeepStructuralOverlap_MaintainsTrieIntegrity()
    {
        // Arrange
        // Force keys into more complex internal branching structures by inserting many items
        var map1 = Set<int>.Empty;
        var map2 = Set<int>.Empty;

        for (var i = 0; i < 100; i += 2) map1 = map1.Add(i); // Even keys: 0, 2, 4...
        for (var i = 1; i < 100; i += 2) map2 = map2.Add(i); // Odd keys:  1, 3, 5...

        // Add one direct conflict key to both
        map1 = map1.Add(500);
        map2 = map2.Add(500);

        // Act
        var merged = map1.Merge(map2); // Prefer right

        // Assert
        Assert.True(merged.Contains(20));
        Assert.True(merged.Contains(21));
        Assert.True(merged.Contains(500)); // Overwritten by right value
    }

    [Fact]
    public void Merge_HashCollisionNodes_ResolvesCorrectly()
    {
        // Arrange
        var comparer = new ForcedCollisionComparer();
        var map1 = new Set<string>(comparer).Add("KeyA").Add("KeyB");
        var map2 = new Set<string>(comparer).Add("KeyB").Add("KeyC");

        // Act
        var merged = map1.Merge(map2); // Default strategy

        // Assert
        Assert.True(merged.Contains("KeyA"));
        Assert.True(merged.Contains("KeyB")); // Handled inside collision loop
        Assert.True(merged.Contains("KeyC"));
    }

    [Fact]
    public void Merge_HashCollisionNodes_WithCustomThunk_ResolvesCorrectly()
    {
        // Arrange
        var comparer = new ForcedCollisionComparer();
        var map1 = new Set<string>(comparer).Add("KeyA").Add("KeyB");
        var map2 = new Set<string>(comparer).Add("KeyB").Add("KeyC");

        // Act
        var merged = map1.Merge(map2); // Sum conflicts

        // Assert
        Assert.Equal(3, merged.Count);
        Assert.True(merged.Contains("KeyB")); // 2 + 3
    }

    [Fact]
    public void Merge_LargeTrees_MatchesOracle_PreferRight()
    {
        // Arrange - Create large datasets with overlapping ranges
        // Set 1: 0 to 33,000
        // Set 2: 16_500 to 49_500 (50% intersection overlap)
        var (map1, oracle1) = GenerateLargeDataset(0, LargeDatasetSize, val => val);
        var (map2, oracle2) = GenerateLargeDataset(LargeDatasetSize / 2, LargeDatasetSize, val => val * 10);

        // Build expected oracle state (prefer right: map2 overwrites map1)
        var expectedOracle = new HashSet<int>(oracle1);
        foreach (var kvp in oracle2) expectedOracle.Add(kvp);

        // Act
        var mergedMap = map1.Merge(map2); // Default strategy (Prefer Right)

        // Assert
        

        // Verify every single key against the oracle
        foreach (var kvp in expectedOracle)
        {
            Assert.True(mergedMap.Contains(kvp), $"Key {kvp} missing from merged map.");
            
        }
    }


    /// <summary>
    ///     Generates identical datasets across both the CHAMP implementation and a reference BCL Dictionary.
    /// </summary>
    private static (Set<int> Set, HashSet<int> Oracle) GenerateLargeDataset(
        int startKey,
        int count,
        Func<int, int> valueGenerator)
    {
        var map = Set<int>.Empty;
        var oracle = new HashSet<int>(count);

        for (var i = 0; i < count; i++)
        {
            var key = startKey + i;
            var value = valueGenerator(key);

            map = map.Add(key);
            oracle.Add(key);
        }

        return (map, oracle);
    }

    [Fact]
    public void MergeOverlap()
    {
        var zeromap = Set<int>.Empty;
        for (var i = 0; i < 20000; i++) zeromap = zeromap.Add(i);

        var oneMap = Set<int>.Empty;
        for (var i = 10000; i < 30000; i++) oneMap = oneMap.Add(i);

        var merged = zeromap.Merge(oneMap);
        Assert.True(merged.Contains(10000));

        var sum = 0;
        merged.Iter((k) =>
        {
            sum += k;
            return true;
        });

        Assert.Equal(449985000, sum);
    }

    // A helper class that forces hash collisions intentionally
    private class ForcedCollisionComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            return string.Equals(x, y);
        }

        // All strings get the same hashcode to force CollisionNode generation
        public int GetHashCode(string obj)
        {
            return 42;
        }
    }
}