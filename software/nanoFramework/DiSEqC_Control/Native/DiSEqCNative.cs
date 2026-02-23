using System;
using System.Runtime.CompilerServices;

namespace DiSEqC_Control.Native
{
    /// <summary>
    /// Native DiSEqC controller - uses STM32 TIM1 + ChibiOS for precise timing
    /// </summary>
    public static class DiSEqC
    {
        /// <summary>
        /// DiSEqC status codes
        /// </summary>
        public enum Status
        {
            Ok = 0,
            Busy = 1,
            InvalidParam = 2,
            Timeout = 3
        }

        /// <summary>
        /// Send GotoX command to position rotor
        /// </summary>
        /// <param name="angle">Target angle in degrees (-80 to +80)</param>
        /// <returns>Status code</returns>
        public static Status GotoAngle(float angle)
        {
            int result = NativeGotoAngle(angle);
            return (Status)result;
        }

        /// <summary>
        /// Transmit raw DiSEqC command bytes
        /// </summary>
        /// <param name="data">Command bytes (1-6 bytes)</param>
        /// <returns>Status code</returns>
        public static Status Transmit(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > 6)
            {
                return Status.InvalidParam;
            }

            int result = NativeTransmit(data);
            return (Status)result;
        }

        /// <summary>
        /// Send halt command to stop rotor movement
        /// </summary>
        /// <returns>Status code</returns>
        public static Status Halt()
        {
            int result = NativeHalt();
            return (Status)result;
        }

        /// <summary>
        /// Drive motor East continuously (call Halt to stop)
        /// </summary>
        /// <returns>Status code</returns>
        public static Status DriveEast()
        {
            int result = NativeDriveEast();
            return (Status)result;
        }

        /// <summary>
        /// Drive motor West continuously (call Halt to stop)
        /// </summary>
        /// <returns>Status code</returns>
        public static Status DriveWest()
        {
            int result = NativeDriveWest();
            return (Status)result;
        }

        /// <summary>
        /// Step motor East by specified number of steps
        /// </summary>
        /// <param name="steps">Number of steps (1-128, typically 1 step = ~1 degree)</param>
        /// <returns>Status code</returns>
        public static Status StepEast(byte steps = 1)
        {
            int result = NativeStepEast(steps);
            return (Status)result;
        }

        /// <summary>
        /// Step motor West by specified number of steps
        /// </summary>
        /// <param name="steps">Number of steps (1-128, typically 1 step = ~1 degree)</param>
        /// <returns>Status code</returns>
        public static Status StepWest(byte steps = 1)
        {
            int result = NativeStepWest(steps);
            return (Status)result;
        }

        /// <summary>
        /// Check if DiSEqC transmission is in progress
        /// </summary>
        /// <returns>True if busy</returns>
        public static bool IsBusy()
        {
            return NativeIsBusy();
        }

        /// <summary>
        /// Get the last commanded angle
        /// </summary>
        /// <returns>Angle in degrees</returns>
        public static float GetCurrentAngle()
        {
            return NativeGetCurrentAngle();
        }

        /* Native method declarations - implemented in C++ */
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeGotoAngle(float angle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeTransmit(byte[] data);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeHalt();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeDriveEast();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeDriveWest();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeStepEast(byte steps);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern int NativeStepWest(byte steps);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool NativeIsBusy();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern float NativeGetCurrentAngle();
    }

    /// <summary>
    /// Motor enable control - manages rotor power
    /// </summary>
    public static class MotorEnable
    {
        /// <summary>
        /// Turn on motor for specified duration
        /// </summary>
        /// <param name="travelTimeSec">Expected travel time in seconds</param>
        public static void TurnOn(uint travelTimeSec)
        {
            NativeTurnOn(travelTimeSec);
        }

        /// <summary>
        /// Start tracking mode (continuous motor enable)
        /// </summary>
        public static void StartTracking()
        {
            NativeStartTracking();
        }

        /// <summary>
        /// Stop tracking mode (disable motor)
        /// </summary>
        public static void StopTracking()
        {
            NativeStopTracking();
        }

        /// <summary>
        /// Force motor off immediately (emergency stop)
        /// </summary>
        public static void ForceOff()
        {
            NativeForceOff();
        }

        /// <summary>
        /// Check if motor is currently enabled
        /// </summary>
        /// <returns>True if motor is on</returns>
        public static bool IsOn()
        {
            return NativeIsOn();
        }

        /* Native method declarations */
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeTurnOn(uint travelTimeSec);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeStartTracking();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeStopTracking();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeForceOff();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool NativeIsOn();
    }
}
