using System.Threading;

namespace CubleyControl
{
    public static partial class Program
    {
        public static void Main()
        {
            EmitBootResetCauseLog();
            _ledReady = TryInitializeStatusLed();

            var heartbeatThread = new Thread(HeartbeatLoop);
            heartbeatThread.Start();

            InitializeNetworkConfiguration();
            InitializeMqttConfiguration();
            InitializeLnbSafeDefaults();
            InitializeLnbFaultMonitor();

            var usbConsoleThread = new Thread(UsbConsoleLoop);
            usbConsoleThread.Start();

            if (_lnbFaultReady)
            {
                var lnbFaultPollThread = new Thread(LnbFaultPollLoop);
                lnbFaultPollThread.Start();
            }

            var mqttThread = new Thread(MqttLoop);
            mqttThread.Start();

            var lnbHealthThread = new Thread(LnbHealthLoop);
            lnbHealthThread.Start();

            var diseqcMotionThread = new Thread(DiseqcMotionMonitorLoop);
            diseqcMotionThread.Start();

            while (true)
            {
                Thread.Sleep(MainLoopSleepMs);
            }
        }
    }
}
