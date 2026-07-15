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

        private const int StatusFaultMask = 0x03; // OCP/OTP bits

        public static void Main()
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
            int enableWindowRc = 0;
            int disableAfterWindowRc = 0;

            try
            {
                bool enable = SmokeProfile.Enable;
                int voltage = SmokeProfile.Voltage;
                int polarization = SmokeProfile.Polarization;
                int band = SmokeProfile.Band;
                int samples = SmokeProfile.Samples;
                int expectedStatus = -1;
                int phase = SmokeProfile.Phase;
                int armBreakMs = SmokeProfile.ArmBreakMs;
                int enableWindowMs = SmokeProfile.EnableWindowMs;
                int scopeLoopMs = SmokeProfile.ScopeLoopMs;

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

                if (enableWindowMs < 0)
                {
                    enableWindowMs = 0;
                }

                if (enableWindowMs > 10000)
                {
                    enableWindowMs = 10000;
                }

                if (scopeLoopMs < 0)
                {
                    scopeLoopMs = 0;
                }

                if (scopeLoopMs > 5000)
                {
                    scopeLoopMs = 5000;
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

                Debug.WriteLine("[LNB-SMOKE] profile=" + SmokeProfile.Name +
                    " enable=" + (enable ? "1" : "0") +
                    " voltage=" + voltage.ToString() +
                    " polarization=" + polarization.ToString() +
                    " band=" + band.ToString() +
                    " samples=" + samples.ToString() +
                    " expectedStatus=" + expectedStatus.ToString() +
                    " phase=" + phase.ToString() +
                    " armBreakMs=" + armBreakMs.ToString() +
                    " enableWindowMs=" + enableWindowMs.ToString() +
                    " scopeLoopMs=" + scopeLoopMs.ToString());

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

                if (initRc == 0 && scopeLoopMs > 0)
                {
                    bool tone = false;
                    int beat = 0;
                    Debug.WriteLine("[LNB-SMOKE] scope loop mode active ms=" + scopeLoopMs.ToString() +
                        " (writes/reads repeat; output stays disabled)");

                    while (true)
                    {
                        int loopDisableRc = LNBH26.NativeSetEnable(false);
                        int loopToneRc = LNBH26.NativeSetTone(tone);
                        int loopStatusValue;
                        int loopStatusRc = LNBH26.NativeReadStatus(out loopStatusValue);

                        byte loopDetail =
                            loopDisableRc != 0 ? (byte)(loopDisableRc & 0xFF) :
                            loopToneRc != 0 ? (byte)(loopToneRc & 0xFF) :
                            loopStatusRc != 0 ? (byte)(loopStatusRc & 0xFF) :
                            (byte)(loopStatusValue & 0xFF);

                        WriteStatus(StageWrite,
                            (loopDisableRc == 0 && loopToneRc == 0 && loopStatusRc == 0) ? ResultPass : ResultFail,
                            loopDetail);

                        Debug.WriteLine("[LNB-SMOKE] scope loop beat=" + beat.ToString() +
                            " disableRc=" + loopDisableRc.ToString() +
                            " tone=" + (tone ? "1" : "0") +
                            " toneRc=" + loopToneRc.ToString() +
                            " statusRc=" + loopStatusRc.ToString() +
                            " status=0x" + loopStatusValue.ToString("X2") +
                            " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                        tone = !tone;
                        beat++;
                        Thread.Sleep(scopeLoopMs);
                    }
                }

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

                    if (enableWindowMs > 0)
                    {
                        enableWindowRc = LNBH26.NativeSetEnable(true);
                        Debug.WriteLine("[LNB-SMOKE] measurement window enable rc=" + enableWindowRc.ToString() +
                            " ms=" + enableWindowMs.ToString() +
                            " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));

                        if (enableWindowRc == 0)
                        {
                            Debug.WriteLine("[LNB-SMOKE] OUTPUT WINDOW ACTIVE NOW ms=" + enableWindowMs.ToString());
                            Thread.Sleep(enableWindowMs);
                            Debug.WriteLine("[LNB-SMOKE] OUTPUT WINDOW ENDING NOW");
                            disableAfterWindowRc = LNBH26.NativeSetEnable(false);
                            Debug.WriteLine("[LNB-SMOKE] measurement window disable rc=" + disableAfterWindowRc.ToString() +
                                " lastNative=0x" + BringupStatus.NativeGetLastNativeError().ToString("X8"));
                        }
                    }

                    // Phase 2 stops after init + disable + basic writes.
                    if (phase == 2)
                    {
                        bool phase2Ok =
                            disableRc == 0 &&
                            setVoltageRc == 0 &&
                            setPolarizationRc == 0 &&
                            setBandRc == 0 &&
                            enableWindowRc == 0 &&
                            disableAfterWindowRc == 0;
                        WriteStatus(StageFinal, phase2Ok ? ResultPass : ResultFail,
                            (byte)((disableRc != 0 ? disableRc :
                                   setVoltageRc != 0 ? setVoltageRc :
                                   setPolarizationRc != 0 ? setPolarizationRc :
                                   setBandRc != 0 ? setBandRc :
                                   enableWindowRc != 0 ? enableWindowRc : disableAfterWindowRc) & 0xFF));
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
                        writesOk = writesOk && enableWindowRc == 0 && disableAfterWindowRc == 0;
                        WriteStatus(StageWrite, writesOk ? ResultPass : ResultFail,
                            (byte)((disableRc != 0 ? disableRc :
                                   setVoltageRc != 0 ? setVoltageRc :
                                   setPolarizationRc != 0 ? setPolarizationRc :
                                   setBandRc != 0 ? setBandRc :
                                   enableWindowRc != 0 ? enableWindowRc : disableAfterWindowRc) & 0xFF));

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
                        setBandRc == 0 &&
                        enableWindowRc == 0 &&
                        disableAfterWindowRc == 0;

                    failDetail =
                        initRc != 0 ? StageInit :
                        disableRc != 0 ? StageWrite :
                        setVoltageRc != 0 ? StageWrite :
                        setPolarizationRc != 0 ? StageWrite :
                        setBandRc != 0 ? StageWrite :
                        enableWindowRc != 0 ? StageWrite :
                        (disableAfterWindowRc != 0 ? StageWrite : (byte)0x02);
                }
                else if (phase == 3)
                {
                    ok =
                        initRc == 0 &&
                        disableRc == 0 &&
                        setVoltageRc == 0 &&
                        setPolarizationRc == 0 &&
                        setBandRc == 0 &&
                        enableWindowRc == 0 &&
                        disableAfterWindowRc == 0 &&
                        statusReadRc == 0 &&
                        statusDeterministic &&
                        statusDefaultOk;

                    failDetail =
                        initRc != 0 ? StageInit :
                        disableRc != 0 ? StageWrite :
                        setVoltageRc != 0 ? StageWrite :
                        setPolarizationRc != 0 ? StageWrite :
                        setBandRc != 0 ? StageWrite :
                        enableWindowRc != 0 ? StageWrite :
                        disableAfterWindowRc != 0 ? StageWrite :
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
                        enableWindowRc == 0 &&
                        disableAfterWindowRc == 0 &&
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
                        enableWindowRc != 0 ? StageWrite :
                        disableAfterWindowRc != 0 ? StageWrite :
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
