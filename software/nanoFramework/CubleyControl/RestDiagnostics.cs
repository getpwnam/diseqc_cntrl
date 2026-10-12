namespace CubleyControl
{
    internal sealed class RestDiagnosticsSnapshot
    {
        internal long ConnectionsAccepted;
        internal long TlsHandshakesStarted;
        internal long TlsHandshakesSucceeded;
        internal long TlsHandshakesFailed;
        internal long RequestsStarted;
        internal long RequestsActive;
        internal long RequestsActiveHighWater;
        internal long Responses2xx;
        internal long Responses4xx;
        internal long Responses5xx;
        internal long ResponseWriteFailures;
        internal long CleanCloses;
        internal long AbortedCloses;
        internal long QueueAccepted;
        internal long QueueRejected;
        internal long QueueHighWater;
    }

    internal sealed class RestDiagnostics
    {
        private readonly object _sync = new object();
        private long _connectionsAccepted;
        private long _tlsHandshakesStarted;
        private long _tlsHandshakesSucceeded;
        private long _tlsHandshakesFailed;
        private long _requestsStarted;
        private long _requestsActive;
        private long _requestsActiveHighWater;
        private long _responses2xx;
        private long _responses4xx;
        private long _responses5xx;
        private long _responseWriteFailures;
        private long _cleanCloses;
        private long _abortedCloses;
        private long _queueAccepted;
        private long _queueRejected;
        private long _queueHighWater;

        internal void RecordConnectionAccepted()
        {
            lock (_sync)
            {
                _connectionsAccepted++;
            }
        }

        internal void RecordTlsHandshakeStarted()
        {
            lock (_sync)
            {
                _tlsHandshakesStarted++;
            }
        }

        internal void RecordTlsHandshakeSucceeded()
        {
            lock (_sync)
            {
                _tlsHandshakesSucceeded++;
            }
        }

        internal void RecordTlsHandshakeFailed()
        {
            lock (_sync)
            {
                _tlsHandshakesFailed++;
            }
        }

        internal void RecordRequestStarted()
        {
            lock (_sync)
            {
                _requestsStarted++;
                _requestsActive++;
                if (_requestsActive > _requestsActiveHighWater)
                {
                    _requestsActiveHighWater = _requestsActive;
                }
            }
        }

        internal void RecordRequestCompleted()
        {
            lock (_sync)
            {
                if (_requestsActive > 0)
                {
                    _requestsActive--;
                }
            }
        }

        internal void RecordResponse(int statusCode)
        {
            lock (_sync)
            {
                if (statusCode >= 200 && statusCode < 300)
                {
                    _responses2xx++;
                }
                else if (statusCode >= 400 && statusCode < 500)
                {
                    _responses4xx++;
                }
                else if (statusCode >= 500)
                {
                    _responses5xx++;
                }
            }
        }

        internal void RecordResponseWriteFailure()
        {
            lock (_sync)
            {
                _responseWriteFailures++;
            }
        }

        internal void RecordClose(bool clean)
        {
            lock (_sync)
            {
                if (clean)
                {
                    _cleanCloses++;
                }
                else
                {
                    _abortedCloses++;
                }
            }
        }

        internal void RecordQueueAccepted(int depth)
        {
            lock (_sync)
            {
                _queueAccepted++;
                if (depth > _queueHighWater)
                {
                    _queueHighWater = depth;
                }
            }
        }

        internal void RecordQueueRejected()
        {
            lock (_sync)
            {
                _queueRejected++;
            }
        }

        internal RestDiagnosticsSnapshot Snapshot()
        {
            lock (_sync)
            {
                return new RestDiagnosticsSnapshot
                {
                    ConnectionsAccepted = _connectionsAccepted,
                    TlsHandshakesStarted = _tlsHandshakesStarted,
                    TlsHandshakesSucceeded = _tlsHandshakesSucceeded,
                    TlsHandshakesFailed = _tlsHandshakesFailed,
                    RequestsStarted = _requestsStarted,
                    RequestsActive = _requestsActive,
                    RequestsActiveHighWater = _requestsActiveHighWater,
                    Responses2xx = _responses2xx,
                    Responses4xx = _responses4xx,
                    Responses5xx = _responses5xx,
                    ResponseWriteFailures = _responseWriteFailures,
                    CleanCloses = _cleanCloses,
                    AbortedCloses = _abortedCloses,
                    QueueAccepted = _queueAccepted,
                    QueueRejected = _queueRejected,
                    QueueHighWater = _queueHighWater
                };
            }
        }
    }
}
