using System.Threading;
using System.Device.Gpio;
using nanoFramework.Runtime.Events;

namespace DiSEqC_Control
{
    public static class StartupProbe
    {
        public static void Main()
        {
            // Keep a direct Runtime.Events type reference so metadata processing
            // includes the managed assembly required by System.Device.Gpio.
            _ = typeof(NativeEventDispatcher);

            GpioController gpio = null;
            GpioPin led = null;

            try
            {
                gpio = new GpioController();
                led = gpio.OpenPin(2, PinMode.Output);
            }
            catch
            {
            }

            uint counter = 0;
            bool on = false;

            while (true)
            {
                counter++;
                Cubley.Interop.BringupStatus.NativeSet(0xD5E20000u | ((counter & 0xFFu) << 8) | 0x01u);

                if (led != null)
                {
                    on = !on;
                    led.Write(on ? PinValue.High : PinValue.Low);
                }

                Thread.Sleep(300);
            }
        }
    }
}
