using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApiCommandQueueTests
{
    [Fact]
    public void PriorityCommandRunsBeforeRegularCommands()
    {
        var queue = new ApiCommandQueue(2);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false, out int depth));
        Assert.Equal(1, depth);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("halt", "payload-halt", true, out depth));
        Assert.Equal(2, depth);

        Assert.True(queue.TryDequeue(out string commandId, out string payload));
        Assert.Equal("halt", commandId);
        Assert.Equal("payload-halt", payload);
        queue.Complete(commandId);

        Assert.True(queue.TryDequeue(out commandId, out payload));
        Assert.Equal("one", commandId);
    }

    [Fact]
    public void PrioritySlotRemainsAvailableWhenRegularQueueIsFull()
    {
        var queue = new ApiCommandQueue(1);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false, out _));
        Assert.Equal(ApiCommandQueueResult.Full, queue.TryEnqueue("two", "payload-two", false, out _));
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("halt", "payload-halt", true, out int depth));
        Assert.Equal(2, depth);
    }

    [Fact]
    public void DuplicateAndConflictIncludeActiveCommand()
    {
        var queue = new ApiCommandQueue(1);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false, out _));
        Assert.True(queue.TryDequeue(out string commandId, out _));

        Assert.Equal(ApiCommandQueueResult.Duplicate, queue.TryEnqueue("one", "payload-one", false, out _));
        Assert.Equal(ApiCommandQueueResult.Conflict, queue.TryEnqueue("one", "different", false, out _));

        queue.Complete(commandId);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "different", false, out _));
    }

    [Fact]
    public void ReportsQueuedAndRunningLifecycleStates()
    {
        var queue = new ApiCommandQueue(1);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false));

        Assert.True(queue.TryGetState("one", out string state));
        Assert.Equal("queued", state);
        Assert.True(queue.TryDequeue(out string commandId, out _));
        Assert.True(queue.TryGetState("one", out state));
        Assert.Equal("running", state);

        queue.Complete(commandId);
        Assert.False(queue.TryGetState("one", out state));
        Assert.Null(state);
    }
}
