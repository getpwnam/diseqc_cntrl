using System;
using System.Device.Gpio;
using System.Threading;

namespace BlinkBringup
{
    public static class Program
    {
        // STM32 managed pin encoding in nanoFramework is linear: (portIndex * 16) + pin.
        // PA2 = 2.
        private const int StatusLedPin = 2;

        public static void Main()
        {
            var gpio = new GpioController();
            gpio.OpenPin(StatusLedPin, PinMode.Output);
            gpio.SetPinMode(StatusLedPin, PinMode.Output);

            while (true)
            {
                gpio.Write(StatusLedPin, PinValue.High);
                Thread.Sleep(1000);

                gpio.Write(StatusLedPin, PinValue.Low);
                Thread.Sleep(1000);
            }
        }
    }
}
