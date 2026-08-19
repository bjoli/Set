using System.Collections.Immutable;
using Set;

namespace Tests;

public class MapTests
{
    [Fact]
    public void RegularOps_EmptyMap_ShouldBehaveSanely()
    {
        var map = Set<int>.Empty;

        Assert.Equal(0, map.Count);
        Assert.True(map.IsEmpty);
        Assert.False(map.Contains(42));
        Assert.False(map.Contains(42));
    }

    [Fact]
    public void RegularOps_AddAndSet_ShouldInsertAndModifyContiguously()
    {
        var map0 = Set<int>.Empty;
        var map1 = map0.Add(1);
        var map2 = map1.Add(2);

        // Verify structural immutability isolation
        Assert.Equal(0, map0.Count);
        Assert.Equal(1, map1.Count);
        Assert.Equal(2, map2.Count);

        // Verify Lookups
        Assert.True(map2.Contains(1));
        Assert.True(map2.Contains(2));
        Assert.True(map2.Contains(1));

        // Verify Value Overwrite
        var map3 = map2.Add(1);
        Assert.Equal(2, map3.Count);
        Assert.True(map3.Contains(1));
    }

    [Fact]
    public void RegularOps_Remove_ShouldDeleteAndTriggerCompaction()
    {
        var map = Set<int>.Empty
            .Add(1)
            .Add(2)
            .Add(3);

        var removed1 = map.Remove(2);
        Assert.Equal(2, removed1.Count);
        Assert.False(removed1.Contains(2));
        Assert.True(removed1.Contains(1));

        // Non-existent key removal should maintain exact reference optimization
        var removedNone = removed1.Remove(99);
        Assert.Same(removed1, removedNone);

        // Clear down to empty invariants
        var emptyResult = removed1.Remove(1).Remove(3);
        Assert.Equal(0, emptyResult.Count);
        Assert.True(emptyResult.IsEmpty);
    }

    [Fact]
    public void TransientOps_ShouldMutateInPlaceAndIsolateOnFreeze()
    {
        var map = Set<int>.Empty.Add(1);
        var transient = map.ToTransient();

        // Perform fast transient mutations
        transient.Add(2);
        transient.Add(3);

        Assert.True(transient.Contains(2));
        

        transient.Remove(1);

        // Freeze the configuration
        var immutable = transient.ToImmutable();

        // Verify isolation boundaries
        Assert.Equal(1, map.Count); // Original remains untouched
        Assert.False(immutable.Contains(1));
        Assert.True(immutable.Contains(2));
        Assert.True(immutable.Contains(3));
    }

    [Fact]
    public void FuzzTest_DifferentialWithImmutableHashSet()
    {
        // Deterministic seed for reproducible testing sequences
        var rand = new Random(1337);
        var truth = ImmutableHashSet<int>.Empty;
        var map = Set<int>.Empty;

        const int OperationsCount = 500000;
        var trackedKeys = new List<int>();

        for (var i = 0; i < OperationsCount; i++)
        {
            var op = rand.Next(100);

            if (op < 55) // 55% Insert/Update mutations
            {
                var key = rand.Next(1, 20000);
                var val = $"v_{i}";

                if (!truth.Contains(key)) trackedKeys.Add(key);

                truth = truth.Add(key);
                map = map.Add(key);
            }
            else if (op < 90) // 35% Removals
            {
                if (trackedKeys.Count > 0)
                {
                    var poolIndex = rand.Next(trackedKeys.Count);
                    var key = trackedKeys[poolIndex];
                    trackedKeys.RemoveAt(poolIndex);

                    truth = truth.Remove(key);
                    map = map.Remove(key);
                }
            }
            else // 10% Batch Transient Mutations
            {
                var transient = map.ToTransient();
                var batchSize = rand.Next(5);

                for (var j = 0; j < batchSize; j++)
                {
                    var subOp = rand.Next(2);
                    if (subOp == 0) // Add inside transient frame
                    {
                        var key = rand.Next(20001); // Disjoint key space
                        var val = $"t_{i}_{j}";

                        transient.Add(key);
                        truth = truth.Add(key);
                    }
                    else if (trackedKeys.Count > 0) // Remove inside transient frame
                    {
                        var poolIndex = rand.Next(trackedKeys.Count);
                        var key = trackedKeys[poolIndex];
                        trackedKeys.RemoveAt(poolIndex);

                        transient.Remove(key);
                        truth = truth.Remove(key);
                    }
                }

                map = transient.ToImmutable();
            }

            // Periodic verification check across full tree contents
            if (i % 10000 == 0) VerifyStateEquality(truth, map);
        }

        // Final thorough validation check
        VerifyStateEquality(truth, map);
    }

    private static void VerifyStateEquality(ImmutableHashSet<int> truth, Set<int> map)
    {
        // Assert deep value lookups match exactly
        foreach (var kvp in truth)
        {
            Assert.True(map.Contains(kvp));
            
        }

        // Assert no phantom keys exist in the structure
        var countedElements = 0;
        foreach (var kvp in map)
        {
            countedElements++;
            Assert.True(truth.Contains(kvp));
            
        }

        Assert.Equal(truth.Count, countedElements);
    }
}