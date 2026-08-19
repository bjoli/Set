using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Set;

namespace Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SetBenchmarks
{
    private const int N = 100_000;
    
    private Set<int> _champSet = Set<int>.Empty;
    private ImmutableHashSet<int> _msSet = ImmutableHashSet<int>.Empty;
    private LanguageExt.HashSet<int> _langExtSet = LanguageExt.HashSet<int>.Empty;

    private Set<int> _champSet2 = Set<int>.Empty;
    private ImmutableHashSet<int> _msSet2 = ImmutableHashSet<int>.Empty;
    private LanguageExt.HashSet<int> _langExtSet2 = LanguageExt.HashSet<int>.Empty;
    
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < N; i++)
        {
            _champSet = SetModule.Add(_champSet, i);
            _msSet = _msSet.Add(i);
            _langExtSet = _langExtSet.Add(i);

            // Shifted by half N to create overlap for Union/Merge benchmarks
            _champSet2 = SetModule.Add(_champSet2, i + (N / 2));
            _msSet2 = _msSet2.Add(i + (N / 2));
            _langExtSet2 = _langExtSet2.Add(i + (N / 2));
        }
    }

    [Benchmark(Baseline = true)]
    public Set<int> Add_Champ()
    {
        var s = Set<int>.Empty;
        for (int i = 0; i < N; i++)
            s = SetModule.Add(s, i);
        return s;
    }

    [Benchmark]
    public ImmutableHashSet<int> Add_MS()
    {
        var s = ImmutableHashSet<int>.Empty;
        for (int i = 0; i < N; i++)
            s = s.Add(i);
        return s;
    }
    
    [Benchmark]
    public LanguageExt.HashSet<int> Add_LangExt()
    {
        var s = LanguageExt.HashSet<int>.Empty;
        for (int i = 0; i < N; i++)
            s = s.Add(i);
        return s;
    }

    [Benchmark]
    public Set<int> Builder_Champ()
    {
        var transient = SetModule.ToTransient(Set<int>.Empty);
        for (int i = 0; i < N; i++)
            transient.Add(i);
        return transient.ToImmutable();
    }

    [Benchmark]
    public ImmutableHashSet<int> Builder_MS()
    {
        var builder = ImmutableHashSet.CreateBuilder<int>();
        for (int i = 0; i < N; i++)
            builder.Add(i);
        return builder.ToImmutable();
    }

    [Benchmark]
    public Set<int> Remove_Champ()
    {
        var s = _champSet;
        for (int i = 0; i < N; i++)
            s = SetModule.Remove(s, i);
        return s;
    }

    [Benchmark]
    public ImmutableHashSet<int> Remove_MS()
    {
        var s = _msSet;
        for (int i = 0; i < N; i++)
            s = s.Remove(i);
        return s;
    }

    [Benchmark]
    public LanguageExt.HashSet<int> Remove_LangExt()
    {
        var s = _langExtSet;
        for (int i = 0; i < N; i++)
            s = s.Remove(i);
        return s;
    }
    
    [Benchmark]
    public int Contains_Champ()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (SetModule.Contains(_champSet, i)) hits++;
        return hits;
    }

    [Benchmark]
    public int Contains_MS()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (_msSet.Contains(i)) hits++;
        return hits;
    }

    [Benchmark]
    public int Contains_LangExt()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (_langExtSet.Contains(i)) hits++;
        return hits;
    }

    [Benchmark]
    public Set<int> Merge_Champ() => SetModule.Merge(_champSet, _champSet2);

    [Benchmark]
    public ImmutableHashSet<int> Union_MS() => _msSet.Union(_msSet2);

    [Benchmark]
    public LanguageExt.HashSet<int> Union_LangExt() => _langExtSet.Union(_langExtSet2);

    [Benchmark]
    public int Iterate_Champ()
    {
        int sum = 0;
        foreach (var i in _champSet) sum += i;
        return sum;
    }

    [Benchmark]
    public int Iterate_MS()
    {
        int sum = 0;
        foreach (var i in _msSet) sum += i;
        return sum;
    }

    [Benchmark]
    public int Iterate_LangExt()
    {
        int sum = 0;
        foreach (var i in _langExtSet) sum += i;
        return sum;
    }
}

class Program
{
    static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
