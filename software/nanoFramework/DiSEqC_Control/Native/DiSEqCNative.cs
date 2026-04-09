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

        /// <summary>
        /// Set SWD-readable bring-up status word for off-site diagnostics.
        /// </summary>
        /// <param name="statusWord">Packed status value.</param>
        public static void SetBringupStatus(uint statusWord)
        {
            NativeSetBringupStatus(statusWord);
        }

        /// <summary>
        /// Read current bring-up status word from native mailbox.
        /// </summary>
        /// <returns>Packed status value.</returns>
        public static uint GetBringupStatus()
        {
            return NativeGetBringupStatus();
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

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeSetBringupStatus(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern uint NativeGetBringupStatus();
    }
}
