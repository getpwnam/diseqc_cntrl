using nanoFramework.Hardware.Esp32;
using nanoFramework.Hardware.Esp32.Rmt;
using System;
using System.Device.Gpio;
using System.Threading;

namespace DiseqC.Manager
{
    internal class RotorManager
    {
        public float CurrentAngle;

        private const int Frequency = 22000;
        private const int MaxAngle = 80;
        private const int DataPin = 0;

        private readonly MotorEnablerManager _motorEnabler;
        private readonly TransmitterChannel _txChannel;

        public RotorManager(GpioController gpioController, MotorEnablerManager motorEnabler)
        {
            _motorEnabler = motorEnabler;
            Configuration.SetPinFunction(DataPin, DeviceFunction.PWM1);

            var txChannelSettings = new TransmitChannelSettings(pinNumber: DataPin)
            {
                ClockDivider = 80,
                EnableCarrierWave = true,
                IdleLevel = false,
                CarrierWaveFrequency = Frequency
            };

            _txChannel = new TransmitterChannel(txChannelSettings);
        }

        public void TrackAndGoToAngle(float angle)
        {
            _motorEnabler.StartTracking();
            MoveToAngle(angle);
        }

        public void StopTracking()
        {
            _motorEnabler.StopTracking();
        }

        public void GotoAngle(float angle, int expectedTravelTimeSec)
        {
            _motorEnabler.TurnOnMotor(expectedTravelTimeSec);

            MoveToAngle(angle);
        }

        private void MoveToAngle(float angle)
        {
            CurrentAngle = angle;

            angle = angle switch
            {
                > MaxAngle => MaxAngle,
                < MaxAngle * -1 => MaxAngle * -1,
                _ => angle
            };

            var n1 = angle < 0 ? (byte)0xE0 : (byte)0xD0;

            var a16 = (int)(16.0f * Math.Abs(angle) + 0.5f);
            var n2 = (byte)((a16 & 0xF00) >> 8);
            var d2 = (byte)(a16 & 0xFF);
            var d1 = (byte)(n1 | n2);

            _txChannel.ClearCommands();
            WriteByteWithParity(0xE0);
            WriteByteWithParity(0x31);
            WriteByteWithParity(0x6E);
            WriteByteWithParity(d1);
            WriteByteWithParity(d2);
            _txChannel.Send(true);
        }

        private void Write0()
        {
            _txChannel.AddCommand(new RmtCommand(1000, true, 500, false));
        }

        private void Write1()
        {
            _txChannel.AddCommand(new RmtCommand(500, true, 1000, false));
        }

        private void WriteByteWithParity(byte x)
        {
            WriteByte(x);
            WriteParity(x);
        }

        private void WriteParity(byte x)
        {
            if (ParityHelper.ParityEvenBit(x) == ParityHelper.Parity.EVEN)
                Write1();
            else
                Write0();
        }

        private void WriteByte(byte x)
        {
            for (var j = 7; j >= 0; j--)
            {
                if ((x & (1 << j)) != 0)
                    Write1();
                else
                    Write0();
            }
        }
    }
}
