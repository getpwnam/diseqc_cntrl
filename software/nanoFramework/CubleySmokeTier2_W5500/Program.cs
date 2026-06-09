using System.Threading;
using Cubley.Interop;

namespace CubleySmokeTier2_W5500
{
    public static class Program
    {
        private const byte ResultEnter = 0;
        private const byte ResultPass = 1;
        private const byte ResultWarn = 2;
        private const byte ResultFail = 14;

        private const byte StageStart = 0xB0;
        private const byte StageVersion = 0xB1;
        private const byte StageVersionNativeTag = 0xB7;
        private const byte StageVersionNativeOp = 0xB8;
        private const byte StagePhy = 0xB2;
        private const byte StageConfig = 0xB3;
        private const byte StageOpen = 0xB4;
        private const byte StageConnect = 0xB5;
        private const byte StageClose = 0xB6;
        private const byte StageFinal = 0xBF;

        // Keep defaults aligned with DiSEqC_Control runtime configuration.
        private const string LocalIp = "172.17.129.253";
        private const string LocalSubnet = "255.255.255.0";
        private const string LocalGateway = "172.17.129.1";
        private const string LocalMac = "02:24:C1:00:00:51";
        private const string ProbeHost = "172.17.132.50";
        private const int ProbePort = 1883;
        private const int ConnectTimeoutMs = 2500;
        private const int DefaultPhyModeCode = 6; // All-capable auto-negotiation.

        public static void Main(string[] args)
        {
            WriteStatus(StageStart, ResultEnter, 0xA0);

            int socketHandle = -1;
            bool connectPathPassed = false;

            try
            {
                int phyModeCode = TryParsePhyModeCode(args, DefaultPhyModeCode);

                WriteStatus(StageVersion, ResultEnter, 0x01);

                uint versionRaw = W5500Socket.NativeGetVersion();
                byte version = (byte)(versionRaw & 0xFF);
                uint nativeError = BringupStatus.NativeGetLastNativeError();

                WriteStatus(StageVersionNativeTag, ResultEnter, (byte)((nativeError >> 24) & 0xFF));
                WriteStatus(StageVersionNativeOp, ResultEnter, (byte)((nativeError >> 16) & 0xFF));
                WriteStatus(StageVersion, version == 0x04 ? ResultPass : ResultWarn, version);

                WriteStatus(StagePhy, ResultEnter, (byte)(0x20 | (byte)(phyModeCode & 0x07)));
                uint phyAfterModeSet = W5500Socket.NativeSetPhyMode(phyModeCode);
                WriteStatus(StagePhy, ResultPass, (byte)(phyAfterModeSet & 0xFF));

                uint phy = W5500Socket.NativeGetPhyStatus();
                WriteStatus(StagePhy, ResultPass, (byte)(phy & 0xFF));

                WriteStatus(StageConfig, ResultEnter, 0x30);
                int configureRc = W5500Socket.NativeConfigureNetwork(LocalIp, LocalSubnet, LocalGateway, LocalMac);
                WriteStatus(StageConfig, configureRc == 0 ? ResultPass : ResultFail, (byte)(configureRc & 0xFF));

                int openRc = -1;
                int connectRc = -1;

                if (configureRc == 0)
                {
                    WriteStatus(StageOpen, ResultEnter, 0x40);
                    openRc = W5500Socket.NativeOpen(out socketHandle);
                    WriteStatus(StageOpen, openRc == 0 ? ResultPass : ResultFail, (byte)(openRc & 0xFF));

                    if (openRc == 0)
                    {
                        WriteStatus(StageConnect, ResultEnter, 0x50);
                        connectRc = W5500Socket.NativeConnect(socketHandle, ProbeHost, ProbePort, ConnectTimeoutMs);
                        WriteStatus(StageConnect, connectRc == 0 ? ResultPass : ResultFail, (byte)(connectRc & 0xFF));

                        if (connectRc == 0)
                        {
                            bool connected = W5500Socket.NativeIsConnected(socketHandle);
                            WriteStatus(StageConnect, connected ? ResultPass : ResultWarn, connected ? (byte)0x51 : (byte)0x52);
                            connectPathPassed = connected;
                        }
                    }
                }

                WriteStatus(StageClose, ResultEnter, 0x60);
                int closeRc = W5500Socket.NativeClose(socketHandle);
                WriteStatus(StageClose, closeRc == 0 ? ResultPass : ResultWarn, (byte)(closeRc & 0xFF));

                byte finalDetail = (byte)(configureRc != 0 ? 0xB3 : (openRc != 0 ? 0xB4 : (connectRc != 0 ? 0xB5 : 0xE3)));
                WriteStatus(StageFinal, connectPathPassed ? ResultPass : ResultFail, connectPathPassed ? (byte)0xFF : finalDetail);
            }
            catch
            {
                WriteStatus(StageVersion, ResultFail, 0xE1);
                WriteStatus(StageFinal, ResultFail, 0xEE);
            }

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void WriteStatus(byte stage, byte result, byte detail)
        {
            try
            {
                uint word = ((uint)0xD5 << 24) | ((uint)stage << 16) | ((uint)result << 8) | detail;
                BringupStatus.NativeSet(word);
            }
            catch
            {
                // Keep smoke harness resilient even if diagnostics write fails.
            }
        }

        private static int TryParsePhyModeCode(string[] args, int fallback)
        {
            if (args == null)
            {
                return fallback;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i];
                if (raw == null)
                {
                    continue;
                }

                string arg = raw.Trim();
                if (arg.Length == 0)
                {
                    continue;
                }

                if (arg.StartsWith("--phy-mode="))
                {
                    return ParsePhyModeToken(arg.Substring(11), fallback);
                }

                if (arg == "--phy-mode" && i + 1 < args.Length)
                {
                    return ParsePhyModeToken(args[i + 1], fallback);
                }
            }

            return fallback;
        }

        private static int ParsePhyModeToken(string token, int fallback)
        {
            if (token == null)
            {
                return fallback;
            }

            string normalized = token.Trim().ToLower();
            if (normalized.Length == 0)
            {
                return fallback;
            }

            // W5500 OPMDC mode codes:
            // 0=10HD, 1=10FD, 2=100HD no-AN, 3=100FD no-AN,
            // 4=100HD AN, 5=power-down, 6=all-capable AN, 7=reserved.
            switch (normalized)
            {
                case "0":
                case "10hd":
                case "10-half":
                case "10h":
                    return 0;
                case "1":
                case "10fd":
                case "10-full":
                case "10f":
                    return 1;
                case "2":
                case "100hd":
                case "100-half":
                case "100h":
                case "100hd-noan":
                    return 2;
                case "3":
                case "100fd":
                case "100-full":
                case "100f":
                case "100fd-noan":
                    return 3;
                case "4":
                case "100hd-an":
                    return 4;
                case "5":
                case "powerdown":
                    return 5;
                case "6":
                case "auto":
                case "all-auto":
                    return 6;
                default:
                    return fallback;
            }
        }
    }
}
