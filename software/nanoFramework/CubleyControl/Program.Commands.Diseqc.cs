namespace CubleyControl
{
    public static partial class Program
    {
        private static void HandleDiseqcCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc usage", "usage=diseqc goto|step|drive|stop ...");
                return;
            }

            string verb = tokens[1];
            if (verb == "goto" && tokens.Length == 3)
            {
                WriteCommandResult(reqId, false, "unsupported", "diseqc placeholder", "cmd=diseqc goto pos=" + tokens[2]);
                return;
            }

            if (verb == "step" && tokens.Length == 4)
            {
                string dir = tokens[2];
                string steps = tokens[3];
                if (dir != "east" && dir != "west")
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc step dir invalid", "dir=" + dir);
                    return;
                }

                WriteCommandResult(reqId, false, "unsupported", "diseqc placeholder", "cmd=diseqc step dir=" + dir + " steps=" + steps);
                return;
            }

            if (verb == "drive" && tokens.Length == 3)
            {
                string dir = tokens[2];
                if (dir != "east" && dir != "west")
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc drive dir invalid", "dir=" + dir);
                    return;
                }

                WriteCommandResult(reqId, false, "unsupported", "diseqc placeholder", "cmd=diseqc drive dir=" + dir);
                return;
            }

            if (verb == "stop" && tokens.Length == 2)
            {
                WriteCommandResult(reqId, false, "unsupported", "diseqc placeholder", "cmd=diseqc stop");
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "diseqc syntax invalid", "verb=" + verb);
        }
    }
}
