using System;
using System.Diagnostics;
using System.Threading;
using Cubley.Interop;

namespace CubleySmokeTier2_FRAM_LNBH26
{
    public static class Program
    {
        private const byte ResultEnter = 0;
        private const byte ResultPass = 1;
        private const byte ResultFail = 14;

        private const byte StageStart = 0xC0;
        private const byte StageFramInit = 0xC1;
        private const byte StageFramWrite = 0xC2;
        private const byte StageFramRead = 0xC3;
        private const byte StageLnbInit = 0xC4;
        private const byte StageLnbSet = 0xC5;
        private const byte StageLnbGet = 0xC6;
        private const byte StageFinal = 0xCF;

        private const int FramProbeAddress = 0x0100;
        private const int FramReadbackRetries = 5;
        private const int FramReadbackRetryDelayMs = 8;

        public static void Main()
        {
            WriteStatus(StageStart, ResultEnter, 0xA0);

            try
            {
                RunFramChecks();
                RunLnbChecks();

                WriteStatus(StageFinal, ResultPass, 0xFF);
                Debug.WriteLine("[SMOKE] PASS");
            }
            catch (Exception ex)
            {
                WriteStatus(StageFinal, ResultFail, 0xEE);
                Debug.WriteLine("[SMOKE] FAIL: " + ex.Message);
            }

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void RunFramChecks()
        {
            WriteStatus(StageFramInit, ResultEnter, 0x10);
            int framInit = Fram24C128.NativeInit();
            if (framInit != (int)Fram24C128.Status.Ok)
            {
                WriteStatus(StageFramInit, ResultFail, (byte)framInit);
                throw new InvalidOperationException("FRAM init failed rc=" + framInit.ToString());
            }
            WriteStatus(StageFramInit, ResultPass, 0x11);

            byte[] payload = new byte[] { 0x46, 0x52, 0x41, 0x4D, 0xAA, 0x55, 0x10, 0x21 };

            WriteStatus(StageFramWrite, ResultEnter, (byte)payload.Length);
            int writeRc = Fram24C128.NativeWrite(FramProbeAddress, payload, 0, payload.Length);
            uint writeNativeTrace = DiagMailbox.NativeGetLastNativeError();
            Debug.WriteLine("[SMOKE] FRAM native write trace=0x" + writeNativeTrace.ToString("X8"));
            if (writeRc != (int)Fram24C128.Status.Ok)
            {
                WriteStatus(StageFramWrite, ResultFail, (byte)writeRc);
                throw new InvalidOperationException("FRAM write failed rc=" + writeRc.ToString());
            }
            WriteStatus(StageFramWrite, ResultPass, 0x21);

            byte[] readBack = new byte[payload.Length];
            WriteStatus(StageFramRead, ResultEnter, (byte)FramReadbackRetries);

            bool matched = false;
            int readRc = (int)Fram24C128.Status.InvalidParam;

            for (int attempt = 0; attempt < FramReadbackRetries; attempt++)
            {
                Thread.Sleep(FramReadbackRetryDelayMs);
                readRc = Fram24C128.NativeRead(FramProbeAddress, readBack, 0, readBack.Length);
                uint readNativeTrace = DiagMailbox.NativeGetLastNativeError();
                Debug.WriteLine("[SMOKE] FRAM native read trace=0x" + readNativeTrace.ToString("X8") +
                                " attempt=" + attempt.ToString());

                if (readRc != (int)Fram24C128.Status.Ok)
                {
                    continue;
                }

                if (BufferEquals(payload, readBack))
                {
                    matched = true;
                    break;
                }
            }

            if (readRc != (int)Fram24C128.Status.Ok)
            {
                WriteStatus(StageFramRead, ResultFail, (byte)readRc);
                throw new InvalidOperationException("FRAM read failed rc=" + readRc.ToString());
            }

            if (!matched)
            {
                int mismatchIndex = FindFirstMismatch(payload, readBack);
                Debug.WriteLine("[SMOKE] FRAM expected=" + BytesToHex(payload));
                Debug.WriteLine("[SMOKE] FRAM readback=" + BytesToHex(readBack));
                Debug.WriteLine("[SMOKE] FRAM mismatch index=" + mismatchIndex.ToString());

                if (mismatchIndex >= 0 && mismatchIndex < payload.Length)
                {
                    Debug.WriteLine("[SMOKE] FRAM expected byte=0x" + payload[mismatchIndex].ToString("X2") +
                                    " actual byte=0x" + readBack[mismatchIndex].ToString("X2"));
                }

                WriteStatus(StageFramRead, ResultFail, 0xE4);
                throw new InvalidOperationException("FRAM readback mismatch");
            }

            WriteStatus(StageFramRead, ResultPass, 0x31);
            Debug.WriteLine("[SMOKE] FRAM read/write OK");
        }

        private static void RunLnbChecks()
        {
            WriteStatus(StageLnbInit, ResultEnter, 0x40);
            int lnbInit = LNBH26.NativeInit();
            if (lnbInit != (int)LNBH26.Status.Ok)
            {
                WriteStatus(StageLnbInit, ResultFail, (byte)lnbInit);
                throw new InvalidOperationException("LNB init failed rc=" + lnbInit.ToString());
            }
            WriteStatus(StageLnbInit, ResultPass, 0x41);

            WriteStatus(StageLnbSet, ResultEnter, 0x50);
            int setDisableRc = LNBH26.NativeSetEnable(false);
            LogLnbNativeTrace("set-enable disabled");
            int setDisableVoltageRc = LNBH26.NativeSetVoltage((int)LNBH26.Voltage.V13);
            LogLnbNativeTrace("set-voltage 13V (disabled profile)");
            int setDisableToneRc = LNBH26.NativeSetTone(false);
            LogLnbNativeTrace("set-tone off (disabled profile)");

            if (setDisableRc != (int)LNBH26.Status.Ok ||
                setDisableVoltageRc != (int)LNBH26.Status.Ok ||
                setDisableToneRc != (int)LNBH26.Status.Ok)
            {
                byte detail = (byte)(setDisableRc != 0 ? setDisableRc : (setDisableVoltageRc != 0 ? setDisableVoltageRc : setDisableToneRc));
                WriteStatus(StageLnbSet, ResultFail, detail);
                throw new InvalidOperationException("LNB set (disabled profile) failed");
            }

            int disabledStatus1;
            int readDisabledStatusRc = LNBH26.NativeReadStatus(out disabledStatus1);
            if (readDisabledStatusRc != (int)LNBH26.Status.Ok)
            {
                WriteStatus(StageLnbSet, ResultFail, (byte)readDisabledStatusRc);
                throw new InvalidOperationException("LNB read status (disabled profile) failed");
            }

            DumpLnbRegisters("disabled profile", disabledStatus1);

            int setEnableRc = LNBH26.NativeSetEnable(true);
            LogLnbNativeTrace("set-enable enabled");
            int setEnableVoltageRc = LNBH26.NativeSetVoltage((int)LNBH26.Voltage.V13);
            LogLnbNativeTrace("set-voltage 13V (enabled profile)");
            int setEnableToneRc = LNBH26.NativeSetTone(false);
            LogLnbNativeTrace("set-tone off (enabled profile)");

            if (setEnableRc != (int)LNBH26.Status.Ok ||
                setEnableVoltageRc != (int)LNBH26.Status.Ok ||
                setEnableToneRc != (int)LNBH26.Status.Ok)
            {
                byte detail = (byte)(setEnableRc != 0 ? setEnableRc : (setEnableVoltageRc != 0 ? setEnableVoltageRc : setEnableToneRc));
                WriteStatus(StageLnbSet, ResultFail, detail);
                throw new InvalidOperationException("LNB set (enabled profile) failed");
            }

            WriteStatus(StageLnbSet, ResultPass, 0x51);

            WriteStatus(StageLnbGet, ResultEnter, 0x60);
            int readVoltage = LNBH26.NativeGetVoltage();
            bool readTone = LNBH26.NativeGetTone();
            int status1;
            int readStatusRc = LNBH26.NativeReadStatus(out status1);

            if (readVoltage != (int)LNBH26.Voltage.V13 || readTone || readStatusRc != (int)LNBH26.Status.Ok)
            {
                byte detail = (byte)(readStatusRc != 0 ? readStatusRc : 0xE6);
                WriteStatus(StageLnbGet, ResultFail, detail);
                throw new InvalidOperationException("LNB get/readback failed");
            }

            WriteStatus(StageLnbGet, ResultPass, (byte)(status1 & 0xFF));
            DumpLnbRegisters("enabled profile", status1);

            int setToneOnRc = LNBH26.NativeSetTone(true);
            LogLnbNativeTrace("set-tone on (tone-on profile)");
            if (setToneOnRc != (int)LNBH26.Status.Ok)
            {
                WriteStatus(StageLnbGet, ResultFail, (byte)setToneOnRc);
                throw new InvalidOperationException("LNB set tone-on profile failed");
            }

            int toneOnStatus1;
            int readToneOnStatusRc = LNBH26.NativeReadStatus(out toneOnStatus1);
            if (readToneOnStatusRc != (int)LNBH26.Status.Ok)
            {
                WriteStatus(StageLnbGet, ResultFail, (byte)readToneOnStatusRc);
                throw new InvalidOperationException("LNB read status (tone-on profile) failed");
            }

            DumpLnbRegisters("tone-on profile", toneOnStatus1);

            int data2ToneOn = ReadLnbRegisterOrThrow(LNBH26.Register.Data2);
            if ((data2ToneOn & (int)LNBH26.Data2Flags.TenA) == 0)
            {
                WriteStatus(StageLnbGet, ResultFail, 0xE7);
                throw new InvalidOperationException("LNB tone-on DATA2 TEN_A bit not set");
            }

            bool readToneOn = LNBH26.NativeGetTone();
            if (!readToneOn)
            {
                WriteStatus(StageLnbGet, ResultFail, 0xE8);
                throw new InvalidOperationException("LNB tone-on state not reported by NativeGetTone");
            }

            Debug.WriteLine("[SMOKE] LNB init/set/get OK status1=0x" + status1.ToString("X2"));
        }

        private static void DumpLnbRegisters(string profileName, int status1FromNativeReadStatus)
        {
            int status1 = ReadLnbRegisterOrThrow(LNBH26.Register.Status1);
            int status2 = ReadLnbRegisterOrThrow(LNBH26.Register.Status2);
            int data1 = ReadLnbRegisterOrThrow(LNBH26.Register.Data1);
            int data2 = ReadLnbRegisterOrThrow(LNBH26.Register.Data2);
            int data3 = ReadLnbRegisterOrThrow(LNBH26.Register.Data3);
            int data4 = ReadLnbRegisterOrThrow(LNBH26.Register.Data4);

            Debug.WriteLine("[SMOKE] LNB " + profileName + " regs STATUS1=0x" + status1.ToString("X2") +
                            " STATUS2=0x" + status2.ToString("X2") +
                            " DATA1=0x" + data1.ToString("X2") +
                            " DATA2=0x" + data2.ToString("X2") +
                            " DATA3=0x" + data3.ToString("X2") +
                            " DATA4=0x" + data4.ToString("X2"));

            Debug.WriteLine("[SMOKE] LNB " + profileName + " STATUS1 flags: " + DescribeStatus1(status1));
            Debug.WriteLine("[SMOKE] LNB " + profileName + " STATUS2 flags: " + DescribeStatus2(status2));
            Debug.WriteLine("[SMOKE] LNB " + profileName + " DATA1 decode: " + DescribeData1(data1));
            Debug.WriteLine("[SMOKE] LNB " + profileName + " DATA2 flags: " + DescribeData2(data2));
            Debug.WriteLine("[SMOKE] LNB " + profileName + " DATA3 flags: " + DescribeData3(data3));
            Debug.WriteLine("[SMOKE] LNB " + profileName + " DATA4 flags: " + DescribeData4(data4));

            if ((status1FromNativeReadStatus & 0xFF) != status1)
            {
                Debug.WriteLine("[SMOKE] LNB " + profileName + " note: NativeReadStatus=0x" + status1FromNativeReadStatus.ToString("X2") +
                                " register STATUS1=0x" + status1.ToString("X2"));
            }
        }

        private static int ReadLnbRegisterOrThrow(LNBH26.Register register)
        {
            int value;
            int rc = LNBH26Registers.NativeReadRegister((int)register, out value);
            uint trace = DiagMailbox.NativeGetLastNativeError();
            Debug.WriteLine("[SMOKE] LNB native trace read-reg " + ((int)register).ToString() +
                            " rc=" + rc.ToString() +
                            " value=0x" + (value & 0xFF).ToString("X2") +
                            " trace=0x" + trace.ToString("X8"));

            if (rc != (int)LNBH26.Status.Ok)
            {
                throw new InvalidOperationException("LNB read register failed reg=" +
                                                    ((int)register).ToString() + " rc=" + rc.ToString());
            }

            return value & 0xFF;
        }

        private static string DescribeStatus1(int status1)
        {
            string text = string.Empty;
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.OlfA, "OLF_A");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.OlfB, "OLF_B");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.VmonA, "VMON_A");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.VmonB, "VMON_B");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.PdoA, "PDO_A");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.PdoB, "PDO_B");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.Otf, "OTF");
            AppendFlag(ref text, status1, (int)LNBH26.Status1Flags.Png, "PNG");
            return text == string.Empty ? "none" : text;
        }

        private static string DescribeStatus2(int status2)
        {
            string text = string.Empty;
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.TdetA, "TDET_A");
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.TdetB, "TDET_B");
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.TmonA, "TMON_A");
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.TmonB, "TMON_B");
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.ImonA, "IMON_A");
            AppendFlag(ref text, status2, (int)LNBH26.Status2Flags.ImonB, "IMON_B");
            return text == string.Empty ? "none" : text;
        }

        private static string DescribeData1(int data1)
        {
            int vsel = data1 & 0x0F;
            string channelA;

            if (vsel == 0x00)
            {
                channelA = "disabled";
            }
            else if (vsel == 0x01)
            {
                channelA = "13V";
            }
            else if (vsel == 0x08)
            {
                channelA = "18V";
            }
            else
            {
                channelA = "unknown(0x" + vsel.ToString("X2") + ")";
            }

            return "VSEL_A=" + channelA + " raw=0x" + data1.ToString("X2");
        }

        private static string DescribeData2(int data2)
        {
            string text = string.Empty;
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.TenA, "TEN_A");
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.LpmA, "LPM_A");
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.ExtmA, "EXTM_A");
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.TenB, "TEN_B");
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.LpmB, "LPM_B");
            AppendFlag(ref text, data2, (int)LNBH26.Data2Flags.ExtmB, "EXTM_B");
            return text == string.Empty ? "none" : text;
        }

        private static string DescribeData3(int data3)
        {
            string text = string.Empty;
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.IsetA, "ISET_A");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.IswA, "ISW_A");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.PclA, "PCL_A");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.TimerA, "TIMER_A");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.IsetB, "ISET_B");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.IswB, "ISW_B");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.PclB, "PCL_B");
            AppendFlag(ref text, data3, (int)LNBH26.Data3Flags.TimerB, "TIMER_B");
            return text == string.Empty ? "none" : text;
        }

        private static string DescribeData4(int data4)
        {
            string text = string.Empty;
            AppendFlag(ref text, data4, (int)LNBH26.Data4Flags.EnImonA, "EN_IMON_A");
            AppendFlag(ref text, data4, (int)LNBH26.Data4Flags.Olr, "OLR");
            AppendFlag(ref text, data4, (int)LNBH26.Data4Flags.EnImonB, "EN_IMON_B");
            AppendFlag(ref text, data4, (int)LNBH26.Data4Flags.Therm, "THERM");
            AppendFlag(ref text, data4, (int)LNBH26.Data4Flags.Comp, "COMP");
            return text == string.Empty ? "none" : text;
        }

        private static void AppendFlag(ref string text, int value, int mask, string name)
        {
            if ((value & mask) == 0)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += ",";
            }

            text += name;
        }

        private static void LogLnbNativeTrace(string label)
        {
            uint trace = DiagMailbox.NativeGetLastNativeError();
            Debug.WriteLine("[SMOKE] LNB native trace " + label + "=0x" + trace.ToString("X8"));
        }

        private static bool BufferEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int FindFirstMismatch(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return -1;
            }

            int minLength = left.Length < right.Length ? left.Length : right.Length;

            for (int i = 0; i < minLength; i++)
            {
                if (left[i] != right[i])
                {
                    return i;
                }
            }

            return left.Length == right.Length ? -1 : minLength;
        }

        private static string BytesToHex(byte[] data)
        {
            if (data == null)
            {
                return "<null>";
            }

            string text = string.Empty;

            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    text += " ";
                }

                text += data[i].ToString("X2");
            }

            return text;
        }

        private static void WriteStatus(byte stage, byte result, byte detail)
        {
            try
            {
                uint word = ((uint)0xD5 << 24) | ((uint)stage << 16) | ((uint)result << 8) | detail;
                DiagMailbox.NativeSet(word);
            }
            catch
            {
                // Do not fail smoke flow if diagnostics write is unavailable.
            }
        }
    }
}
