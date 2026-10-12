namespace CubleyControl
{
    internal static class ApiCommandQueueResult
    {
        internal const int Accepted = 0;
        internal const int Duplicate = 1;
        internal const int Conflict = 2;
        internal const int Full = 3;
    }

    internal sealed class ApiCommandQueue
    {
        private readonly object _sync = new object();
        private readonly string[] _commandIds;
        private readonly string[] _identities;
        private readonly string[] _payloads;
        private int _head;
        private int _tail;
        private int _count;
        private string _priorityCommandId;
        private string _priorityIdentity;
        private string _priorityPayload;
        private string _activeCommandId;
        private string _activeIdentity;
        private string _activePayload;

        internal ApiCommandQueue(int capacity)
        {
            _commandIds = new string[capacity];
            _identities = new string[capacity];
            _payloads = new string[capacity];
        }

        internal int TryEnqueue(
            string commandId,
            string identity,
            string payload,
            bool priority,
            out int depth)
        {
            lock (_sync)
            {
                depth = _count + (_priorityCommandId == null ? 0 : 1);
                string existingIdentity;
                if (TryFindLocked(commandId, out existingIdentity))
                {
                    return existingIdentity == identity
                        ? ApiCommandQueueResult.Duplicate
                        : ApiCommandQueueResult.Conflict;
                }

                if (priority)
                {
                    if (_priorityCommandId != null)
                    {
                        return ApiCommandQueueResult.Full;
                    }

                    _priorityCommandId = commandId;
                    _priorityIdentity = identity;
                    _priorityPayload = payload;
                    depth++;
                    return ApiCommandQueueResult.Accepted;
                }

                if (_count >= _commandIds.Length)
                {
                    return ApiCommandQueueResult.Full;
                }

                _commandIds[_tail] = commandId;
                _identities[_tail] = identity;
                _payloads[_tail] = payload;
                _tail = (_tail + 1) % _commandIds.Length;
                _count++;
                depth++;
                return ApiCommandQueueResult.Accepted;
            }
        }

        internal bool TryDequeue(out string commandId, out string identity, out string payload)
        {
            lock (_sync)
            {
                commandId = null;
                identity = null;
                payload = null;
                if (_activeCommandId != null)
                {
                    return false;
                }

                if (_priorityCommandId != null)
                {
                    commandId = _priorityCommandId;
                    identity = _priorityIdentity;
                    payload = _priorityPayload;
                    _priorityCommandId = null;
                    _priorityIdentity = null;
                    _priorityPayload = null;
                }
                else if (_count > 0)
                {
                    commandId = _commandIds[_head];
                    identity = _identities[_head];
                    payload = _payloads[_head];
                    _commandIds[_head] = null;
                    _identities[_head] = null;
                    _payloads[_head] = null;
                    _head = (_head + 1) % _commandIds.Length;
                    _count--;
                }

                if (commandId == null)
                {
                    return false;
                }

                _activeCommandId = commandId;
                _activeIdentity = identity;
                _activePayload = payload;
                return true;
            }
        }

        internal void Complete(string commandId)
        {
            lock (_sync)
            {
                if (_activeCommandId == commandId)
                {
                    _activeCommandId = null;
                    _activeIdentity = null;
                    _activePayload = null;
                }
            }
        }

        internal bool TryGetState(string commandId, out string state)
        {
            lock (_sync)
            {
                if (_activeCommandId == commandId)
                {
                    state = "running";
                    return true;
                }

                if (_priorityCommandId == commandId)
                {
                    state = "queued";
                    return true;
                }

                for (int offset = 0; offset < _count; offset++)
                {
                    int slot = (_head + offset) % _commandIds.Length;
                    if (_commandIds[slot] == commandId)
                    {
                        state = "queued";
                        return true;
                    }
                }

                state = null;
                return false;
            }
        }

        private bool TryFindLocked(string commandId, out string payload)
        {
            if (_activeCommandId == commandId)
            {
                payload = _activeIdentity;
                return true;
            }

            if (_priorityCommandId == commandId)
            {
                payload = _priorityIdentity;
                return true;
            }

            for (int offset = 0; offset < _count; offset++)
            {
                int slot = (_head + offset) % _commandIds.Length;
                if (_commandIds[slot] == commandId)
                {
                    payload = _identities[slot];
                    return true;
                }
            }

            payload = null;
            return false;
        }
    }
}
