using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class RestDiagnosticsTests
{
    [Fact]
    public void SnapshotTracksTransportLifecycle()
    {
        var diagnostics = new RestDiagnostics();

        diagnostics.RecordConnectionAccepted();
        diagnostics.RecordTlsHandshakeStarted();
        diagnostics.RecordTlsHandshakeSucceeded();
        diagnostics.RecordRequestStarted();
        diagnostics.RecordRequestStarted();
        diagnostics.RecordRequestCompleted();
        diagnostics.RecordResponse(202);
        diagnostics.RecordResponse(404);
        diagnostics.RecordResponse(503);
        diagnostics.RecordResponseWriteFailure();
        diagnostics.RecordClose(true);
        diagnostics.RecordClose(false);
        diagnostics.RecordQueueAccepted(2);
        diagnostics.RecordQueueAccepted(1);
        diagnostics.RecordQueueRejected();

        RestDiagnosticsSnapshot snapshot = diagnostics.Snapshot();

        Assert.Equal(1, snapshot.ConnectionsAccepted);
        Assert.Equal(1, snapshot.TlsHandshakesStarted);
        Assert.Equal(1, snapshot.TlsHandshakesSucceeded);
        Assert.Equal(0, snapshot.TlsHandshakesFailed);
        Assert.Equal(2, snapshot.RequestsStarted);
        Assert.Equal(1, snapshot.RequestsActive);
        Assert.Equal(2, snapshot.RequestsActiveHighWater);
        Assert.Equal(1, snapshot.Responses2xx);
        Assert.Equal(1, snapshot.Responses4xx);
        Assert.Equal(1, snapshot.Responses5xx);
        Assert.Equal(1, snapshot.ResponseWriteFailures);
        Assert.Equal(1, snapshot.CleanCloses);
        Assert.Equal(1, snapshot.AbortedCloses);
        Assert.Equal(2, snapshot.QueueAccepted);
        Assert.Equal(1, snapshot.QueueRejected);
        Assert.Equal(2, snapshot.QueueHighWater);
    }

    [Fact]
    public void RequestCompletionDoesNotUnderflow()
    {
        var diagnostics = new RestDiagnostics();

        diagnostics.RecordRequestCompleted();

        Assert.Equal(0, diagnostics.Snapshot().RequestsActive);
    }
}
