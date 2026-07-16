namespace CubleyControl.Contracts
{
    public sealed class ResultEnvelope
    {
        public bool Ok;
        public string Code;
        public string Msg;
        public string Ts;
        public string ReqId;

        // Optional metadata fields.
        public string Domain;
        public string Command;

        public static ResultEnvelope Success(string command, string msg, string ts, string reqId)
        {
            return new ResultEnvelope
            {
                Ok = true,
                Code = ResultCodes.Ok,
                Msg = msg,
                Ts = ts,
                ReqId = reqId,
                Domain = InferDomain(command),
                Command = command
            };
        }

        public static ResultEnvelope Failure(string command, string code, string msg, string ts, string reqId)
        {
            return new ResultEnvelope
            {
                Ok = false,
                Code = code,
                Msg = msg,
                Ts = ts,
                ReqId = reqId,
                Domain = InferDomain(command),
                Command = command
            };
        }

        private static string InferDomain(string command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            int separator = command.IndexOf('.');
            if (separator <= 0)
            {
                return string.Empty;
            }

            return command.Substring(0, separator);
        }
    }
}
