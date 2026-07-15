using System.Diagnostics;
using System.Threading;
using Cubley.Interop;

namespace CubleySmokeTier2_LNBH26
{
    public static class Program
    {
        private const byte ResultEnter = 0;
        private const byte ResultPass = 1;
        private const byte ResultWarn = 2;
        private const byte ResultFail = 14;

        private const byte StageStart = 0xC0;
        private const byte StageInit = 0xC1;
        private const byte StageWrite = 0xC2;
        private const byte StageReadback = 0xC3;
        private const byte StageStatus = 0xC4;
        private const byte StageFinal = 0xCF;

        private const bool DefaultEnable = false;
        private const int DefaultVoltage = (int)LNBH26.Voltage.V13;
        private const int DefaultPolarization = (int)LNBH26.Polarization.Vertical;
        private const int DefaultBand = (int)LNBH26.Band.Low;
        private const int DefaultSamples = 8;
        private const int DefaultArmBreakMs = 0;
        private const int StatusFaultMask = 0x03; // OCP/OTP bits

        public static void Main(string[] args)
        {
            int initRc = -1;
            int disableRc = -1;
            int setVoltageRc = -1;
            int setPolarizationRc = -1;
            int setBandRc = -1;
            int readbackOk = 0;
            int statusReadRc = -1;
            int statusBaseline = -1;
            bool statusDeterministic = true;
            bool statusDefaultOk = false;
            int initRcObserved = -1;
            int lastNativeObserved = -1;
            int preInitSetEnableRc = -1;

            try
            {
                bool enable = TryParseEnable(args, DefaultEnable);
                int voltage = TryParseEnum(args, "--voltage", DefaultVoltage);
                int polarization = TryParseEnum(args, "--polarization", DefaultPolarization);
                int band = TryParseEnum(args, "--band", DefaultBand);
                int samples = TryParseEnum(args, "--samples", DefaultSamples);
                int expectedStatus = TryParseEnum(args, "--expected-status", -1);
                int phase = TryParseEnum(args, "--phase", 4);
                int armBreakMs = TryParseEnum(args, "--arm-break-ms", DefaultArmBreakMs);

                Debug.WriteLine("[LNB-SMOKE] startup");

                if (samples < 2)
                {
                    samples = 2;
                }

                if (phase < 0 || phase > 4)
                {
                    phase = 4;
                }

                if (armBreakMs < 0)
                {
                    armBreakMs = 0;
                }

                if (armBreakMs > 15000)
                {
                    armBreakMs = 15000;
                }

                // Tier-0 safety gate: this smoke keeps LNB output disabled.
                enable = false;

                // Phase 0 is intentionally interop-free to prove managed entrypoint stability.
                // Do not call WriteStatus()/NativeSet in this mode.
                if (phase == 0)
                {
                    Debug.WriteLine("[LNB-SMOKE] phase0 baseline mode: managed-only, no ICALLs");
                    int beat = 0;
                    while (true)
                    {
                        Debug.WriteLine("[LNB-SMOKE] phase0 heartbeat=" + beat.ToString());
                        beat++;
                        Thread.Sleep(1000);
                    }
                }

                WriteStatus(StageStart, ResultEnter, 0xA0);

                Debug.WriteLine("[LNB-SMOKE] args enable=" + (enable ? "1" : "0") +
                    " voltage=" + voltage.ToString() +
                    " polarization=" + polarization.ToString() +
                    " band=" + band.ToString() +
                    " samples=" + samples.ToString() +
                    " expectedStatus=" + expectedStatus.ToString() +
                    " phase=" + phase.ToString() +
                    " armBreakMs=" + armBreakMs.ToString());

                if (phase >= 1 && armBreakMs > 0)
                {
                    Debug.WriteLine("[LNB-SMOKE] pre-init debugger arm window ms=" + armBreakMs.ToString());
                    int waitSlices = armBreakMs / 100;
                    if (waitSlices < 1)
                    {
                        waitSlices = 1;
                    }

                    for (int wait = 0; wait < waitSlices; wait++)
                    {
                        Thread.Sleep(100);
                    }
                }

                // Non-debugger dispatch probe:
                // Before NativeInit, NativeSetEnable(false) should return NotInitialized (2)
                // if the InternalCall binding reached the real LNB native implementation.
                preInitSetEnableRc = LNBH26.NativeSetEnable(false);
                lastNativeObserved = (int)BringupStatus.NativeGetLastNativeError();
                Debug.WriteLine("[LNB-SMOKE] pre-init probe NativeSetEnable(false) rc=" + preInitSetEnableRc.ToString() +
                    " lastNative=0x" + lastNativeObserved.ToString("X8"));

                if (phase >= 1 && preInitSetEnableRc == -5)
                {
                    // 0xF5 marks suspected InternalCall dispatch fallback.
                    WriteStatus(StageFinal, ResultFail, 0xF5);
                    Debug.WriteLine("[LNB-SMOKE] dispatch probe failed rc=-5; halting before NativeInit");
                    while (true)
                    {
                        Thread.Sleep(1000);
                    }
                }

                WriteStatus(StageInit, ResultEnter, 0x01);
                Debug.WriteLine("[LNB-SMOKE] stage=C1 init enter");
                initRc = LNBH26.NativeInit();
                initRcObserved = initRc;
                lastNativeObserved = (int)BringupStatus.NativeGetLastNativeError();
                WriteStatus(StageInit, initRc == 0 ? ResultPass : ResultFail, (byte)(initRc & 0xFF));
                Debug.WriteLine("[LNB-SMOKE] stage=C1 init rc=" + initRc.ToString() +
                    " lastNative=0x" + lastNativeObserved.ToString("X8"));

                if (initRc == 0 && phase >= 2)
                {
                    WriteStatus(StageWrite, ResultEnter, 0x01);
                    Debug.WriteLine("[LNB-SMOKE] stage=C2 writes enter");

                    disableRc = LNBH26.NativeSetEnable(false);
                    Debug.WriteLine("[LNB-SMOKE] write NativeSetEnable(false) rc=" + disableRc.ToString() +
                        " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                    setVoltageRc = LNBH26.NativeSetVoltage(voltage);
                    Debug.WriteLine("[LNB-SMOKE] write NativeSetVoltage(" + voltage.ToString() + ") rc=" + setVoltageRc.ToString() +
                        " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                    int polarizationVoltage = polarization == (int)LNBH26.Polarization.Horizontal ?
                        (int)LNBH26.Voltage.V18 : (int)LNBH26.Voltage.V13;
                    setPolarizationRc = LNBH26.NativeSetVoltage(polarizationVoltage);
                    Debug.WriteLine("[LNB-SMOKE] write polarization via NativeSetVoltage(" + polarizationVoltage.ToString() + ") rc=" + setPolarizationRc.ToString() +
                        " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                    bool bandTone = band == (int)LNBH26.Band.High;
                    setBandRc = LNBH26.NativeSetTone(bandTone);
                    Debug.WriteLine("[LNB-SMOKE] write band via NativeSetTone(" + (bandTone ? "1" : "0") + ") rc=" + setBandRc.ToString() +
                        " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                    // Phase 2 stops after init + disable + basic writes.
                    if (phase == 2)
                    {
                        bool phase2Ok = disableRc == 0 && setVoltageRc == 0 && setPolarizationRc == 0 && setBandRc == 0;
                        WriteStatus(StageFinal, phase2Ok ? ResultPass : ResultFail,
                            (byte)((disableRc != 0 ? disableRc :
                                   setVoltageRc != 0 ? setVoltageRc :
                                   setPolarizationRc != 0 ? setPolarizationRc : setBandRc) & 0xFF));
                        Debug.WriteLine("[LNB-SMOKE] phase2 complete ok=" + (phase2Ok ? "1" : "0"));
                        while (true)
                        {
                            Thread.Sleep(1000);
                        }
                    }

                    // Readback verification only in full phase.
                    if (phase >= 4)
                    {
                        bool writesOk = disableRc == 0 && setVoltageRc == 0 && setPolarizationRc == 0 && setBandRc == 0;
                        WriteStatus(StageWrite, writesOk ? ResultPass : ResultFail,
                            (byte)((disableRc != 0 ? disableRc :
                                   setVoltageRc != 0 ? setVoltageRc :
                                   setPolarizationRc != 0 ? setPolarizationRc : setBandRc) & 0xFF));

                        WriteStatus(StageReadback, ResultEnter, 0x01);
                        int gotVoltage = LNBH26.NativeGetVoltage();
                        bool gotTone = LNBH26.NativeGetTone();
                        int gotPolarization = gotVoltage == (int)LNBH26.Voltage.V18 ?
                            (int)LNBH26.Polarization.Horizontal : (int)LNBH26.Polarization.Vertical;
                        int gotBand = gotTone ? (int)LNBH26.Band.High : (int)LNBH26.Band.Low;

                        readbackOk = (gotVoltage == voltage && gotPolarization == polarization && gotBand == band && gotTone == (band == (int)LNBH26.Band.High)) ? 1 : 0;
                        Debug.WriteLine("[LNB-SMOKE] readback voltage=" + gotVoltage.ToString() +
                            " polarization=" + gotPolarization.ToString() +
                            " band=" + gotBand.ToString() +
                            " tone=" + (gotTone ? "1" : "0") +
                            " ok=" + readbackOk.ToString());
                        WriteStatus(StageReadback, readbackOk == 1 ? ResultPass : ResultFail, (byte)gotBand);
                    }

                    if (phase >= 3)
                    {
                    WriteStatus(StageStatus, ResultEnter, 0x01);
                    for (int i = 0; i < samples; i++)
                    {
                        int sampleStatus;
                        statusReadRc = LNBH26.NativeReadStatus(out sampleStatus);
                        if (statusReadRc != 0)
                        {
                            statusDeterministic = false;
                            Debug.WriteLine("[LNB-SMOKE] status sample=" + i.ToString() +
                                " rc=" + statusReadRc.ToString() +
                                " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));
                            break;
                        }

                        if (statusBaseline < 0)
                        {
                            statusBaseline = sampleStatus;
                        }
                        else if (sampleStatus != statusBaseline)
                        {
                            statusDeterministic = false;
                        }

                        bool ocp = (sampleStatus & 0x01) != 0;
                        bool otp = (sampleStatus & 0x02) != 0;
                        bool vmon = (sampleStatus & 0x04) != 0;
                        Debug.WriteLine("[LNB-SMOKE] status sample=" + i.ToString() +
                            " value=0x" + sampleStatus.ToString("X2") +
                            " OCP=" + (ocp ? "1" : "0") +
                            " OTP=" + (otp ? "1" : "0") +
                            " VMON=" + (vmon ? "1" : "0"));

                        Thread.Sleep(100);
                    }
                    }

                    if (statusReadRc == 0 && statusBaseline >= 0)
                    {
                        int faultBits = statusBaseline & StatusFaultMask;
                        statusDefaultOk = faultBits == 0;
                        if (expectedStatus >= 0)
                        {
                            statusDefaultOk = statusDefaultOk && statusBaseline == expectedStatus;
                        }
                    }

                    byte statusDetail = statusBaseline >= 0 ? (byte)(statusBaseline & 0xFF) : (byte)0xEE;
                    bool statusOk = statusReadRc == 0 && statusDeterministic && statusDefaultOk;
                    WriteStatus(StageStatus, statusOk ? ResultPass : ResultFail, statusDetail);
                    Debug.WriteLine("[LNB-SMOKE] status summary baseline=0x" + statusBaseline.ToString("X2") +
                        " deterministic=" + (statusDeterministic ? "1" : "0") +
                        " defaultOk=" + (statusDefaultOk ? "1" : "0") +
                        " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                    if (phase == 3)
                    {
                        bool phase3Ok = disableRc == 0 && statusReadRc == 0 && statusDeterministic && statusDefaultOk;
                        WriteStatus(StageFinal, phase3Ok ? ResultPass : ResultFail,
                            (byte)(phase3Ok ? 0x03 : 0xE3));
                        Debug.WriteLine("[LNB-SMOKE] phase3 complete ok=" + (phase3Ok ? "1" : "0"));
                        while (true)
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }

                bool ok;
                byte failDetail;

                if (phase <= 1)
                {
                    ok = initRc == 0;
                    failDetail = ok ? (byte)0x01 : StageInit;
                }
                else if (phase == 2)
                {
                    ok =
                        initRc == 0 &&
                        disableRc == 0 &&
                        setVoltageRc == 0 &&
                        setPolarizationRc == 0 &&
                        setBandRc == 0;

                    failDetail =
                        initRc != 0 ? StageInit :
                        disableRc != 0 ? StageWrite :
                        setVoltageRc != 0 ? StageWrite :
                        setPolarizationRc != 0 ? StageWrite :
                        (setBandRc != 0 ? StageWrite : (byte)0x02);
                }
                else if (phase == 3)
                {
                    ok =
                        initRc == 0 &&
                        disableRc == 0 &&
                        setVoltageRc == 0 &&
                        setPolarizationRc == 0 &&
                        setBandRc == 0 &&
                        statusReadRc == 0 &&
                        statusDeterministic &&
                        statusDefaultOk;

                    failDetail =
                        initRc != 0 ? StageInit :
                        disableRc != 0 ? StageWrite :
                        setVoltageRc != 0 ? StageWrite :
                        setPolarizationRc != 0 ? StageWrite :
                        setBandRc != 0 ? StageWrite :
                        !statusDeterministic ? StageStatus :
                        !statusDefaultOk ? StageStatus :
                        (statusReadRc != 0 ? StageStatus : (byte)0x03);
                }
                else
                {
                    ok =
                        initRc == 0 &&
                        disableRc == 0 &&
                        setVoltageRc == 0 &&
                        setPolarizationRc == 0 &&
                        setBandRc == 0 &&
                        readbackOk == 1 &&
                        statusReadRc == 0 &&
                        statusDeterministic &&
                        statusDefaultOk;

                    failDetail =
                        initRc != 0 ? StageInit :
                        disableRc != 0 ? StageWrite :
                        setVoltageRc != 0 ? StageWrite :
                        setPolarizationRc != 0 ? StageWrite :
                        setBandRc != 0 ? StageWrite :
                        readbackOk != 1 ? StageReadback :
                        !statusDeterministic ? StageStatus :
                        !statusDefaultOk ? StageStatus :
                        (statusReadRc != 0 ? StageStatus : (byte)0xFF);
                }

                WriteStatus(StageFinal, ok ? ResultPass : ResultFail, failDetail);
                Debug.WriteLine("[LNB-SMOKE] final ok=" + (ok ? "1" : "0") +
                    " failDetail=0x" + failDetail.ToString("X2") +
                    " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("[LNB-SMOKE] exception=" + ex.Message +
                    " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));
                WriteStatus(StageFinal, ResultFail, 0xEE);
            }

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static bool TryParseEnable(string[] args, bool fallback)
        {
            if (args == null)
            {
                return fallback;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (arg == "--enable" && i + 1 < args.Length)
                {
                    return ParseBoolToken(args[i + 1], fallback);
                }

                if (arg.StartsWith("--enable="))
                {
                    return ParseBoolToken(arg.Substring(9), fallback);
                }
            }

            return fallback;
        }

        private static int TryParseEnum(string[] args, string key, int fallback)
        {
            if (args == null)
            {
                return fallback;
            }

            string keyPrefix = key + "=";
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (arg == key && i + 1 < args.Length)
                {
                    return ParseIntToken(args[i + 1], fallback);
                }

                if (arg.StartsWith(keyPrefix))
                {
                    return ParseIntToken(arg.Substring(keyPrefix.Length), fallback);
                }
            }

            return fallback;
        }

        private static bool ParseBoolToken(string token, bool fallback)
        {
            if (token == null)
            {
                return fallback;
            }

            string normalized = token.Trim().ToLower();
            if (normalized == "1" || normalized == "true" || normalized == "on" || normalized == "yes")
            {
                return true;
            }

            if (normalized == "0" || normalized == "false" || normalized == "off" || normalized == "no")
            {
                return false;
            }

            return fallback;
        }

        private static int ParseIntToken(string token, int fallback)
        {
            if (token == null)
            {
                return fallback;
            }

            string normalized = token.Trim();
            if (normalized.Length == 0)
            {
                return fallback;
            }

            int value = fallback;
            bool ok = int.TryParse(normalized, out value);
            return ok ? value : fallback;
        }

        private static void WriteStatus(byte stage, byte result, byte detail)
        {
            try
            {
                uint word = ((uint)0xD5 << 24) | ((uint)stage << 16) | ((uint)result << 8) | detail;
                BringupStatus.NativeSet(word);
            }
            catch
            {
            }
        }

    }
}
