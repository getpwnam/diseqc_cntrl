using System;
using diseqc_interop;

namespace MailboxSmoke
{
    public static class Program
    {
        public static void Main()
        {
            uint counter = 0;

            while (true)
            {
                uint status = 0xD5200000u | ((counter & 0xFFu) << 8) | 0x01u;

                try
                {
                    int openStatus = W5500Socket.NativeOpen(out int handle);
                    if (handle >= 0)
                    {
                        W5500Socket.NativeClose(handle);
                    }
                }
                catch
                {
                    // Keep running so we can still probe managed/runtime behavior.
                }

                try
                {
                    DiseqC.NativeSetBringupStatus(status);
                    status = DiseqC.NativeGetBringupStatus();
                }
                catch
                {
                    // If setter throws, keep looping to avoid app exit.
                }

                counter++;

                for (int i = 0; i < 250000; i++)
                {
                    // Busy loop for coarse pacing without external assembly dependencies.
                }
            }
        }
    }
}
