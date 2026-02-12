using DiseqC.Native;
using System;

namespace DiseqC.Manager
{
    /// <summary>
    /// High-level Rotor Manager - uses native DiSEqC driver
    /// 
    /// Note: Motor enable functionality removed - not needed for this hardware.
    /// LNBH26 provides power automatically, DiSEqC commands control rotor directly.
    /// </summary>
    internal class RotorManager
    {
        public float CurrentAngle => DiSEqC.GetCurrentAngle();

        private const int MaxAngle = 80;

        public RotorManager()
        {
            // Native driver is initialized in board startup
        }

        /// <summary>
        /// Go to angle - simplified (no motor enable needed)
        /// </summary>
        /// <param name="angle">Target angle (-80 to +80)</param>
        public void GotoAngle(float angle)
        {
            MoveToAngle(angle);
        }

        /// <summary>
        /// Track and move to angle
        /// Note: Tracking mode not supported without motor enable pin
        /// This just sends the command
        /// </summary>
        public void TrackAndGoToAngle(float angle)
        {
            MoveToAngle(angle);
        }

        /// <summary>
        /// Stop tracking - not applicable (no motor control)
        /// </summary>
        public void StopTracking()
        {
            // No-op - rotor controls its own motor
        }

        private void MoveToAngle(float angle)
        {
            // Clamp angle
            if (angle > MaxAngle) angle = MaxAngle;
            if (angle < -MaxAngle) angle = -MaxAngle;

            // Call native DiSEqC driver
            var status = DiSEqC.GotoAngle(angle);

            if (status != DiSEqC.Status.Ok)
            {
                System.Diagnostics.Debug.WriteLine($"DiSEqC error: {status}");
            }
        }

        /// <summary>
        /// Check if rotor is busy (DiSEqC transmission only)
        /// </summary>
        public bool IsBusy()
        {
            return DiSEqC.IsBusy();
        }

        /// <summary>
        /// Send halt command
        /// </summary>
        public void Halt()
        {
            DiSEqC.Halt();
        }

        /// <summary>
        /// Step motor East (manual control)
        /// </summary>
        /// <param name="steps">Number of steps (default 1, typically 1 step = ~1 degree)</param>
        public void StepEast(byte steps = 1)
        {
            DiSEqC.StepEast(steps);
        }

        /// <summary>
        /// Step motor West (manual control)
        /// </summary>
        /// <param name="steps">Number of steps (default 1, typically 1 step = ~1 degree)</param>
        public void StepWest(byte steps = 1)
        {
            DiSEqC.StepWest(steps);
        }

        /// <summary>
        /// Drive motor East continuously (must call Halt to stop)
        /// </summary>
        public void DriveEast()
        {
            DiSEqC.DriveEast();
        }

        /// <summary>
        /// Drive motor West continuously (must call Halt to stop)
        /// </summary>
        public void DriveWest()
        {
            DiSEqC.DriveWest();
        }
    }
}
