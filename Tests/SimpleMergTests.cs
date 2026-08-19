using Set;

namespace Tests;

public class PersistentMapSimpleLargeTests
{
    private const int LargeCount = 33_000;

    [Fact]
    public void Merge_LargeData_AbsolutelyNoKeyOverlap()
    {
        // Arrange
        var map1 = Set<int>.Empty;
        var map2 = Set<int>.Empty;
        var oracle = new Dictionary<int, int>();

        // Evens to Map1, Odds to Map2 -> Complete interleaving but 0 key collisions
        for (var i = 0; i < LargeCount; i++)
        {
            var evenKey = i * 2;
            var oddKey = i * 2 + 1;

            map1 = map1.Add(evenKey);
            oracle[evenKey] = 1;

            map2 = map2.Add(oddKey);
            oracle[oddKey] = 0;
        }

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.Equal(oracle.Count, merged.Count);
        foreach (var kvp in oracle)
        {
            Assert.True(merged.Contains(kvp.Key), $"Missing key {kvp.Key}");
            
        }
    }

    [Fact]
    public void Merge_LargeData_CompleteKeyOverlap_AllOnesAndZeros()
    {
        // Arrange
        var map1 = Set<int>.Empty;
        var map2 = Set<int>.Empty;
        var oracle = new Dictionary<int, int>();

        // Both maps contain identical keys [0 ... 33000)
        for (var i = 0; i < LargeCount; i++)
        {
            map1 = map1.Add(i); // Left has 1s
            map2 = map2.Add(i); // Right has 0s
            oracle[i] = 0; // Prefer Right defaults to 0
        }

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.Equal(LargeCount, merged.Count);
        for (var i = 0; i < LargeCount; i++)
        {
            Assert.True(merged.Contains(i), $"Missing key {i}");
            
        }
    }

    [Fact]
    public void Merge_LargeData_CompleteKeyOverlap_SumResolver()
    {
        // Arrange
        var map1 = Set<int>.Empty;
        var map2 = Set<int>.Empty;

        for (var i = 0; i < LargeCount; i++)
        {
            map1 = map1.Add(i);
            map2 = map2.Add(i);
        }

        // Act
        var merged = map1.Merge(map2);

        // Assert
        Assert.Equal(LargeCount, merged.Count);
        for (var i = 0; i < LargeCount; i++)
        {
            Assert.True(merged.Contains(i), $"Missing key {i}");

        }
    }
}