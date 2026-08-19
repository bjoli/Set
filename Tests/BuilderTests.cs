using Set;

namespace Tests;

public class BuilderTests
{
    [Fact]
    public void BuilderShouldBuildSmallMapsCorrectly()
    {
        var my = new SetBuilder<int>();
        for (var i = 0; i < 21400; i++) my.Add(i);

        var imm = my.ToImmutable();
        for (var i = 0; i < 21400; i++) Assert.True(imm.Contains(i));
    }

    [Fact]
    public void BuilderShouldBuildLargeMapsCorrectly()
    {
        var my = new SetBuilder<int>();
        for (var i = 0; i < 1500000; i++) my.Add(i);

        var imm = my.ToImmutable();
        for (var i = 0; i < 1500000; i++) Assert.True(imm.Contains(i));
    }
}