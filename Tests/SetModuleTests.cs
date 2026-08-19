using System;
using System.Collections.Generic;
using Set;
using Xunit;

namespace Tests;

public class SetModuleTests
{
    [Fact]
    public void TestStandardOperations()
    {
        var set = Set<int>.Empty;
        
        // Add & Contains
        set = SetModule.Add(set, 10);
        Assert.True(SetModule.Contains(set, 10));
        Assert.False(SetModule.Contains(set, 20));
        
        // Count & IsEmpty
        Assert.False(SetModule.IsEmpty(set));
        Assert.Equal(1, SetModule.Count(set));
        
        // AddRange
        set = SetModule.AddRange(set, new[] { 20, 30 });
        Assert.Equal(3, SetModule.Count(set));
        Assert.True(SetModule.Contains(set, 20));
        
        // Remove
        set = SetModule.Remove(set, 10);
        Assert.False(SetModule.Contains(set, 10));
        Assert.Equal(2, SetModule.Count(set));
        
        // RemoveRange
        set = SetModule.RemoveRange(set, new[] { 20, 30 });
        Assert.True(SetModule.IsEmpty(set));
        
        // Clear
        set = SetModule.Add(set, 42);
        set = SetModule.Clear(set);
        Assert.True(SetModule.IsEmpty(set));
    }

    [Fact]
    public void TestMerge()
    {
        var set1 = Set<int>.Empty.Add(1).Add(2);
        var set2 = Set<int>.Empty.Add(2).Add(3);
        
        var merged = SetModule.Merge(set1, set2);
        Assert.Equal(3, SetModule.Count(merged));
        Assert.True(SetModule.Contains(merged, 1));
        Assert.True(SetModule.Contains(merged, 2));
        Assert.True(SetModule.Contains(merged, 3));
    }

    [Fact]
    public void TestHigherOrderFunctions()
    {
        var set = Set<int>.Empty;
        set = SetModule.AddRange(set, new[] { 1, 2, 3, 4, 5 });
        
        // Map
        var mapped = SetModule.Map(x => x * 10, set);
        Assert.True(SetModule.Contains(mapped, 10));
        Assert.True(SetModule.Contains(mapped, 50));
        Assert.Equal(5, SetModule.Count(mapped));
        
        // Filter
        var evens = SetModule.Filter(x => x % 2 == 0, set);
        Assert.Equal(2, SetModule.Count(evens));
        Assert.True(SetModule.Contains(evens, 2));
        Assert.True(SetModule.Contains(evens, 4));
        Assert.False(SetModule.Contains(evens, 1));
        
        // Fold
        var sum = SetModule.Fold((state, x) => state + x, 0, set);
        Assert.Equal(15, sum);
        
        // Exists
        Assert.True(SetModule.Exists(x => x == 3, set));
        Assert.False(SetModule.Exists(x => x == 10, set));
        
        // FindKey
        var key = SetModule.FindKey(x => x > 3, set);
        Assert.True(key == 4 || key == 5);
        
        // ForEach
        int count = 0;
        SetModule.ForEach(x => count++, set);
        Assert.Equal(5, count);
        
        // Mutate
        var mutated = SetModule.Mutate(transient => 
        {
            transient.Add(6);
            transient.Remove(1);
        }, set);
        Assert.Equal(5, SetModule.Count(mutated));
        Assert.True(SetModule.Contains(mutated, 6));
        Assert.False(SetModule.Contains(mutated, 1));
    }

    [Fact]
    public void TestAddChecked()
    {
        var set = Set<int>.Empty;
        set = SetModule.AddChecked(set, 1);
        
        Assert.Throws<ArgumentException>(() => SetModule.AddChecked(set, 1));
    }
}
