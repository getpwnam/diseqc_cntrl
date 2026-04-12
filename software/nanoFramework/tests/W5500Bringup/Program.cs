using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Threading;
using Cubley.Interop;

namespace W5500Bringup
{
    public static class Program
    {
        private const int StatusLedPin = 2;

        // Network config applied directly to native W5500 driver.
        private const string LocalIp = "192.168.1.160";
        private const string SubnetMask = "255.255.255.0";
        private const string Gateway = "192.168.1.1";
        private const string MacAddress = "02:24:C1:00:00:51";

        // Probe endpoint: use an always-on TCP service in your LAN.
        private const string ProbeHost = "192.168.1.60";
        private const int ProbePort = 1883;
        private const int ConnectTimeoutMs = 5000;
        private const int ReceiveTimeoutMs = 2000;

        private const int FailCodeConfigureNetwork = 1;
        private const int FailCodeOpenSocket = 2;
        private const int FailCodeConnect = 3;
        private const int FailCodeConnectedCheck = 4;
        private const int FailCodeSend = 5;
        private const int FailCodeReceive = 6;
        private const int FailCodeClose = 7;
        private const int FailCodeException = 8;

        private const byte BringupResultRunning = 0;
        private const byte BringupResultPass = 1;
        private const byte BringupResultWarn = 2;
        private const byte BringupResultFail = 14;
        private const byte BringupResultException = 15;

        private const byte DetailOpenBegin = 0x10;
        private const byte DetailOpenDone = 0x11;
        private const byte DetailConfigureBegin = 0x20;
        private const byte DetailConfigureDone = 0x21;
        private const byte DetailConnectBegin = 0x30;
        private const byte DetailConnectDone = 0x31;
        private const byte DetailConnectedCheckBegin = 0x40;
        private const byte DetailConnectedCheckDone = 0x41;
        private const byte DetailSendBegin = 0x50;
        private const byte DetailSendDone = 0x51;
        private const byte DetailReceiveBegin = 0x60;
        private const byte DetailReceiveDone = 0x61;
        private const byte DetailCloseBegin = 0x70;
        private const byte DetailCloseDone = 0x71;

        // Diagnostic mode: keep refreshing PHY status for SWD while cable is unplugged/replugged.
        private const bool EnablePhyMonitorMode = true;
        private const bool EnablePhyModeSweep = true;
        private const int PhyMonitorIterations = 240;
        private const int PhyMonitorIntervalMs = 500;

        private static GpioController _gpio;

        public static void Main()
        {
            bool ledReady = InitializeLed();
            if (!ledReady)
            {
                Debug.WriteLine("[LED] Failed to initialize status LED");
            }

            Debug.WriteLine("[W5500] Bring-up test starting");
            Debug.WriteLine("[W5500] Local IP " + LocalIp + " Gateway " + Gateway);
            Debug.WriteLine("[W5500] Probe target " + ProbeHost + ":" + ProbePort);
            byte startupLedDetail = ledReady ? (byte)0xA0 : (byte)0xA1;

            // Stage 9 is reserved for startup diagnostics: detail 0xA0=LED init ok, 0xA1=failed.
            ReportBringupStatus(9, BringupResultRunning, startupLedDetail);
            Thread.Sleep(300);
            ReportBringupStatus(1, BringupResultRunning, startupLedDetail);

            // Startup signature to prove managed app execution.
            Blink(3, 120, 120);

            int failCode = 0;
            int warnCode = 0;
            int socketHandle = -1;
            int currentStage = 0;
            int exceptionStage = 0;
            uint lastNativeError = 0;

            try
            {
                currentStage = 1;
                ReportBringupStatus((byte)currentStage, BringupResultRunning, startupLedDetail);
                StageMarker(1);
                try
                {
                    uint statusEcho = BringupStatus.NativeGet();
                    Debug.WriteLine("[INTEROP] BringupStatus.NativeGet => 0x" + statusEcho.ToString("X8"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[INTEROP] BringupStatus.NativeGet unavailable: " + ex.Message);
                }

                currentStage = 2;
                ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailOpenBegin);
                StageMarker(2);
                var status = (W5500Socket.Status)W5500Socket.NativeOpen(out socketHandle);
                Debug.WriteLine("[W5500] Open => " + status + " handle=" + socketHandle);
                ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailOpenDone, (int)status));
                LogPhyStatus("after-open");
                try
                {
                    lastNativeError = BringupStatus.NativeGetLastNativeError();
                    Debug.WriteLine("[W5500] LastNativeError after Open => 0x" + lastNativeError.ToString("X8"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[W5500] LastNativeError unavailable: " + ex.Message);
                }
                if (status != W5500Socket.Status.Ok)
                {
                    failCode = FailCodeOpenSocket;
                }

                if (failCode == 0 && EnablePhyMonitorMode)
                {
                    currentStage = 10;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, 0xA0);
                    if (EnablePhyModeSweep)
                    {
                        RunPhyModeSweep();
                    }
                    RunPhyMonitorLoop(PhyMonitorIterations, PhyMonitorIntervalMs);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, 0xAF);
                }

                if (failCode == 0)
                {
                    currentStage = 3;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailConfigureBegin);
                    StageMarker(3);
                    status = (W5500Socket.Status)W5500Socket.NativeConfigureNetwork(LocalIp, SubnetMask, Gateway, MacAddress);
                    Debug.WriteLine("[W5500] ConfigureNetwork => " + status);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailConfigureDone, (int)status));
                    LogPhyStatus("after-config");
                    if (status != W5500Socket.Status.Ok)
                    {
                        failCode = FailCodeConfigureNetwork;
                    }
                }

                if (failCode == 0)
                {
                    currentStage = 4;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailConnectBegin);
                    StageMarker(4);
                    status = (W5500Socket.Status)W5500Socket.NativeConnect(socketHandle, ProbeHost, ProbePort, ConnectTimeoutMs);
                    Debug.WriteLine("[W5500] Connect => " + status);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailConnectDone, (int)status));
                    LogPhyStatus("after-connect");
                    if (status != W5500Socket.Status.Ok)
                    {
                        failCode = FailCodeConnect;
                    }
                }

                if (failCode == 0)
                {
                    currentStage = 5;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailConnectedCheckBegin);
                    bool connected = W5500Socket.NativeIsConnected(socketHandle);
                    Debug.WriteLine("[W5500] IsConnected => " + connected);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, connected ? DetailConnectedCheckDone : (byte)(DetailConnectedCheckDone + 1));
                    LogPhyStatus("after-isconnected");
                    if (!connected)
                    {
                        failCode = FailCodeConnectedCheck;
                    }
                }

                if (failCode == 0)
                {
                    currentStage = 6;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailSendBegin);
                    StageMarker(5);
                    byte[] payload = BuildProbePayload();
                    status = (W5500Socket.Status)W5500Socket.NativeSend(socketHandle, payload, 0, payload.Length, out int sentBytes);
                    Debug.WriteLine("[W5500] Send => " + status + " sent=" + sentBytes);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailSendDone, (int)status));
                    if (status != W5500Socket.Status.Ok || sentBytes <= 0)
                    {
                        failCode = FailCodeSend;
                    }
                }

                if (failCode == 0)
                {
                    currentStage = 7;
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailReceiveBegin);
                    StageMarker(6);
                    byte[] buffer = new byte[128];
                    status = (W5500Socket.Status)W5500Socket.NativeReceive(socketHandle, buffer, 0, buffer.Length, ReceiveTimeoutMs, out int receivedBytes);
                    Debug.WriteLine("[W5500] Receive => " + status + " bytes=" + receivedBytes);
                    ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailReceiveDone, (int)status));

                    if (status == W5500Socket.Status.Timeout)
                    {
                        // Timeout is acceptable for first bring-up if target service does not send data back.
                        warnCode = 1;
                    }
                    else if (status != W5500Socket.Status.Ok)
                    {
                        failCode = FailCodeReceive;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[W5500] Exception: " + ex.Message);
                exceptionStage = currentStage;
                Debug.WriteLine("[W5500] Exception stage: " + exceptionStage);
                failCode = FailCodeException;
                ReportBringupStatus(15, BringupResultException, (byte)exceptionStage);
            }
            finally
            {
                if (socketHandle >= 0)
                {
                    try
                    {
                        currentStage = 8;
                        ReportBringupStatus((byte)currentStage, BringupResultRunning, DetailCloseBegin);
                        var closeStatus = (W5500Socket.Status)W5500Socket.NativeClose(socketHandle);
                        Debug.WriteLine("[W5500] Close => " + closeStatus);
                        ReportBringupStatus((byte)currentStage, BringupResultRunning, CombineStatusDetail(DetailCloseDone, (int)closeStatus));
                        if (failCode == 0 && closeStatus != W5500Socket.Status.Ok)
                        {
                            failCode = FailCodeClose;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[W5500] Close exception: " + ex.Message);
                        if (failCode == 0)
                        {
                            failCode = FailCodeException;
                            exceptionStage = 8;
                        }
                    }
                }
            }

            if (failCode == 0 && warnCode == 0)
            {
                Debug.WriteLine("[LED] PASS pattern");
                ReportBringupStatus(15, BringupResultPass, 0);
                RunPassLoop();
            }

            if (failCode == 0 && warnCode != 0)
            {
                Debug.WriteLine("[LED] WARN pattern");
                ReportBringupStatus(15, BringupResultWarn, (byte)warnCode);
                RunWarnLoop();
            }

            Debug.WriteLine("[LED] FAIL code: " + failCode);
            if (failCode == FailCodeException && exceptionStage > 0)
            {
                Debug.WriteLine("[LED] Exception stage pulses: " + exceptionStage);
            }

            if (lastNativeError != 0)
            {
                Debug.WriteLine("[W5500] Final LastNativeError => 0x" + lastNativeError.ToString("X8"));
            }

            ReportBringupStatus(15, failCode == FailCodeException ? BringupResultException : BringupResultFail, (byte)(failCode == FailCodeException ? exceptionStage : failCode));

            RunFailLoop(failCode, exceptionStage, lastNativeError);
        }

        private static uint EncodeBringupStatus(byte stage, byte result, byte detail)
        {
            return (uint)((0xD5 << 24) | (stage << 16) | (result << 8) | detail);
        }

        private static byte CombineStatusDetail(byte major, int status)
        {
            int clamped = status;
            if (clamped < 0)
            {
                clamped = 0;
            }
            if (clamped > 15)
            {
                clamped = 15;
            }

            return (byte)(major | (byte)clamped);
        }

        private static void ReportBringupStatus(byte stage, byte result, byte detail)
        {
            try
            {
                BringupStatus.NativeSet(EncodeBringupStatus(stage, result, detail));
            }
            catch
            {
                // Keep bring-up flow alive even if mailbox setter interop is unavailable.
            }
        }

        private static void LogPhyStatus(string marker)
        {
            try
            {
                uint packed = W5500Socket.NativeGetVersionPhyStatus();
                uint version = (packed >> 8) & 0xFFU;
                uint phy = packed & 0xFFU;
                bool linkUp = (phy & 0x01U) != 0;
                bool speed100 = (phy & 0x02U) != 0;
                bool fullDuplex = (phy & 0x04U) != 0;

                Debug.WriteLine(
                    "[W5500] PHY " + marker +
                    " ver=0x" + version.ToString("X2") +
                    " raw=0x" + phy.ToString("X2") +
                    " link=" + (linkUp ? "UP" : "DOWN") +
                    " speed=" + (speed100 ? "100M" : "10M") +
                    " duplex=" + (fullDuplex ? "FULL" : "HALF"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[W5500] PHY " + marker + " unavailable: " + ex.Message);
            }
        }

        private static void RunPhyMonitorLoop(int iterations, int intervalMs)
        {
            Debug.WriteLine("[W5500] PHY monitor mode running: iterations=" + iterations + " intervalMs=" + intervalMs);

            for (int i = 0; i < iterations; i++)
            {
                uint packed = W5500Socket.NativeGetVersionPhyStatus();
                uint version = (packed >> 8) & 0xFFU;
                uint phy = packed & 0xFFU;
                bool linkUp = (phy & 0x01U) != 0;
                bool speed100 = (phy & 0x02U) != 0;
                bool fullDuplex = (phy & 0x04U) != 0;

                Debug.WriteLine(
                    "[W5500] PHY loop i=" + i.ToString() +
                    " ver=0x" + version.ToString("X2") +
                    " raw=0x" + phy.ToString("X2") +
                    " link=" + (linkUp ? "UP" : "DOWN") +
                    " speed=" + (speed100 ? "100M" : "10M") +
                    " duplex=" + (fullDuplex ? "FULL" : "HALF"));

                ReportBringupStatus(10, BringupResultRunning, (byte)(linkUp ? 0xE1 : 0xE0));
                Thread.Sleep(intervalMs);
            }
        }

        private static void RunPhyModeSweep()
        {
            int[] modeCodes = { 7, 0, 1, 2, 3, 6 };
            string[] modeNames =
            {
                "all-auto(7)",
                "10H(0)",
                "10F(1)",
                "100H(2)",
                "100F(3)",
                "auto(6)"
            };

            Debug.WriteLine("[W5500] PHY mode sweep start");

            for (int m = 0; m < modeCodes.Length; m++)
            {
                int mode = modeCodes[m];
                uint afterSet = W5500Socket.NativeSetPhyMode(mode);
                Debug.WriteLine("[W5500] PHY set mode " + modeNames[m] + " => raw=0x" + afterSet.ToString("X2"));
                ReportBringupStatus(10, BringupResultRunning, (byte)(0xC0 | (byte)(mode & 0x0F)));

                for (int i = 0; i < 12; i++)
                {
                    uint packed = W5500Socket.NativeGetVersionPhyStatus();
                    uint phy = packed & 0xFFU;
                    bool linkUp = (phy & 0x01U) != 0;

                    Debug.WriteLine(
                        "[W5500] PHY sweep mode=" + modeNames[m] +
                        " i=" + i.ToString() +
                        " raw=0x" + phy.ToString("X2") +
                        " link=" + (linkUp ? "UP" : "DOWN"));

                    ReportBringupStatus(10, BringupResultRunning, (byte)(linkUp ? 0xE1 : 0xE0));
                    Thread.Sleep(500);
                }
            }

            Debug.WriteLine("[W5500] PHY mode sweep end");
        }

        private static byte[] BuildProbePayload()
        {
            return new byte[]
            {
                (byte)'W', (byte)'5', (byte)'5', (byte)'0', (byte)'0', (byte)'-',
                (byte)'B', (byte)'R', (byte)'I', (byte)'N', (byte)'G', (byte)'U', (byte)'P',
                (byte)'\r', (byte)'\n'
            };
        }

        private static bool InitializeLed()
        {
            try
            {
                _gpio = new GpioController();
                _gpio.OpenPin(StatusLedPin, PinMode.Output);
                _gpio.SetPinMode(StatusLedPin, PinMode.Output);
                _gpio.Write(StatusLedPin, PinValue.Low);
                Debug.WriteLine("[LED] Initialized on PA2");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LED] Initialization failed: " + ex.Message);
                return false;
            }
        }

        private static void RunPassLoop()
        {
            while (true)
            {
                // PASS latch: one long + two short, then heartbeat cadence.
                Pulse(800, 250);
                Pulse(180, 180);
                Pulse(180, 600);

                Pulse(500, 500);
                Pulse(500, 1000);
            }
        }

        private static void RunWarnLoop()
        {
            while (true)
            {
                // WARN latch: one medium + one short, then 1.5 s pause.
                Pulse(450, 180);
                Pulse(160, 1500);
            }
        }

        private static void RunFailLoop(int code, int exceptionStage, uint lastNativeError)
        {
            if (code < 1)
            {
                code = 1;
            }

            while (true)
            {
                if (lastNativeError != 0)
                {
                    // Keep a stable SWD-readable record of native error op/code in failure latch loop.
                    ReportBringupStatus(13, BringupResultFail, (byte)((lastNativeError >> 16) & 0xFF));
                    ReportBringupStatus(14, BringupResultFail, (byte)((lastNativeError >> 8) & 0xFF));
                }

                // Clear separator before each fail code burst.
                // Always keep fail code visible via stage 15 for SWD diagnostics.
                ReportBringupStatus(15, BringupResultFail, (byte)code);

                Pulse(900, 900);

                for (int i = 0; i < code; i++)
                {
                    Pulse(280, 280);
                }

                if (code == FailCodeException && exceptionStage > 0)
                {
                    Thread.Sleep(900);
                    for (int i = 0; i < exceptionStage; i++)
                    {
                        Pulse(140, 220);
                    }
                }

                Thread.Sleep(3500);
            }
        }

        private static void Blink(int pulses, int onMs, int offMs)
        {
            for (int i = 0; i < pulses; i++)
            {
                Pulse(onMs, offMs);
            }
        }

        private static void StageMarker(int stage)
        {
            if (stage < 1)
            {
                stage = 1;
            }

            // Distinct short pulse count before each critical step.
            Blink(stage, 140, 220);
            Thread.Sleep(900);
        }

        private static void Pulse(int onMs, int offMs)
        {
            if (_gpio == null)
            {
                Thread.Sleep(onMs + offMs);
                return;
            }

            _gpio.Write(StatusLedPin, PinValue.High);
            Thread.Sleep(onMs);
            _gpio.Write(StatusLedPin, PinValue.Low);
            Thread.Sleep(offMs);
        }
    }
}
