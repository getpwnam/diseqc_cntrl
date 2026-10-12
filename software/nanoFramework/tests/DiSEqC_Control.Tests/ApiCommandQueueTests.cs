using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApiCommandQueueTests
{
    [Fact]
    public void PriorityCommandRunsBeforeRegularCommands()
    {
        var queue = new ApiCommandQueue(2);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false));
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("halt", "payload-halt", true));

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
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false));
        Assert.Equal(ApiCommandQueueResult.Full, queue.TryEnqueue("two", "payload-two", false));
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("halt", "payload-halt", true));
    }

    [Fact]
    public void DuplicateAndConflictIncludeActiveCommand()
    {
        var queue = new ApiCommandQueue(1);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "payload-one", false));
        Assert.True(queue.TryDequeue(out string commandId, out _));

        Assert.Equal(ApiCommandQueueResult.Duplicate, queue.TryEnqueue("one", "payload-one", false));
        Assert.Equal(ApiCommandQueueResult.Conflict, queue.TryEnqueue("one", "different", false));

        queue.Complete(commandId);
        Assert.Equal(ApiCommandQueueResult.Accepted, queue.TryEnqueue("one", "different", false));
    }
}
