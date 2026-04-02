using System;
using System.Device.Gpio;
using System.Threading;

namespace BlinkBringup
{
    public static class Program
    {
        // Board status LED is wired to PA2, which maps to pin number 2.
        private const int LedPin = 2;

        public static void Main()
        {
            var gpio = new GpioController();
            gpio.OpenPin(LedPin, PinMode.Output);

            var state = PinValue.Low;

            while (true)
            {
                state = state == PinValue.Low ? PinValue.High : PinValue.Low;
                gpio.Write(LedPin, state);
                Thread.Sleep(250);
            }
        }
    }
}
