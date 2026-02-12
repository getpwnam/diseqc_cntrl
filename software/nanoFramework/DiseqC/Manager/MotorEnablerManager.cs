using System;
using System.Device.Gpio;
using System.Threading;

namespace DiseqC.Manager
{
    internal class MotorEnablerManager
    {
        private const int Pin = 1;
        private const int StartupTimeMs = 2000;

        private bool _motorEnableForever;
        private readonly GpioPin _motorEnablePin;
        private Thread _motorThread;

        public MotorEnablerManager(GpioController gpioController)
        {
            _motorEnablePin = gpioController.OpenPin(Pin, PinMode.Output);
            _motorEnablePin.Write(PinValue.Low);
        }

        public void StartTracking()
        {
            _motorEnableForever = true;
            _motorEnablePin.Write(PinValue.High);
        }

        public void StopTracking()
        {
            _motorEnableForever = false;
            _motorEnablePin.Write(PinValue.Low);
        }

        public void TurnOnMotor(int expectedTravelTimeSec)
        {
            if (_motorEnableForever) return;
            if (_motorThread != null && _motorThread.IsAlive)
            {
                _motorThread.Abort();
                _motorThread = null;
            }

            _motorThread = new Thread(()=> MotorLoop(expectedTravelTimeSec + (int)TimeSpan.FromMilliseconds(StartupTimeMs).TotalSeconds));
            _motorThread.Start();

            Thread.Sleep(StartupTimeMs);
        }

        private void MotorLoop(int durationInSec)
        {
            try
            {
                _motorEnablePin.Write(PinValue.High);

                Thread.Sleep(durationInSec * 1000);

                _motorEnablePin.Write(PinValue.Low);
            }
            catch (ThreadAbortException)
            {
                _motorEnablePin.Write(PinValue.High);
            }
        }
    }
}
