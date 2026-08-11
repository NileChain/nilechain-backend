using NileChain.AI;

namespace NileChain.Tests;

public class LlmProviderFailureClassificationTests
{
    [Fact]
    public void IsProviderFailure_ArgumentOutOfRangeIndex_IsTrue()
    {
        var ex = new ArgumentOutOfRangeException("index", "Specified argument was out of the range of valid values.");
        Assert.True(LlmKernelFactory.IsProviderFailure(ex));
    }

    [Fact]
    public void IsProviderFailure_WrappedRefusalIndexBug_IsTrue()
    {
        var inner = new ArgumentOutOfRangeException("index");
        var outer = new InvalidOperationException("chat failed", inner);
        Assert.True(LlmKernelFactory.IsProviderFailure(outer));
    }

    [Fact]
    public void IsProviderFailure_OrdinaryArgumentException_IsFalse()
    {
        var ex = new ArgumentException("bad farm id", "farmId");
        Assert.False(LlmKernelFactory.IsProviderFailure(ex));
    }
}
