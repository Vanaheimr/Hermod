/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// In-memory simulated SunSpec 3-phase Energy Meter (Common Model 1 + Meter
/// Model 213 subset). All access is mutex-guarded by the base class. A
/// background task simulates live measurements every second.
/// </summary>
public sealed class SunSpecMeterDevice : ASunSpecDeviceBase
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _simTask;

    // Scale factors (as int16) - decimal exponent
    private const short SF_Current   = -2; // 1 LSB = 0.01 A
    private const short SF_Voltage   = -1; // 1 LSB = 0.1 V
    private const short SF_Frequency = -2; // 1 LSB = 0.01 Hz
    private const short SF_Power     =  0; // 1 LSB = 1 W
    private const short SF_Energy    =  0; // 1 LSB = 1 Wh

    public override ushort BaseAddress => SunSpecMeterMap.BaseAddress;
    public override string DisplayName => "SunSpec 3-Phase Meter (M213)";
    public override IReadOnlySet<ushort> ProtectedAddresses => SunSpecMeterMap.ProtectedAddresses;
    public override IReadOnlySet<ushort> CommandedAddresses => SunSpecMeterMap.CommandedAddresses;

    public SunSpecMeterDevice(string serialNumber)
        : base(SunSpecMeterMap.RegisterCount)
    {
        InitCommonModel(
            manufacturer: "Vanaheimr",
            model:        "DemoMeter-3P",
            options:      "tls-only",
            version:      "1.0.0",
            serialNumber: serialNumber);

        InitMeter();
        _simTask = Task.Run(() => SimulateAsync(_cts.Token));
    }

    // ---- Init ----

    private void InitMeter()
    {
        Registers[SunSpecMeterMap.OffMeterId]  = SunSpecMeterMap.MeterModelId;
        Registers[SunSpecMeterMap.OffMeterLen] = SunSpecMeterMap.MeterModelLength;

        // Constants must be cast through `unchecked` because negative literals
        // can't be implicitly narrowed to ushort at compile time (CS0221).
        Registers[SunSpecMeterMap.OffMeterA_SF]  = unchecked((ushort)SF_Current);
        Registers[SunSpecMeterMap.OffMeterV_SF]  = unchecked((ushort)SF_Voltage);
        Registers[SunSpecMeterMap.OffMeterHz_SF] = unchecked((ushort)SF_Frequency);
        Registers[SunSpecMeterMap.OffMeterW_SF]  = unchecked((ushort)SF_Power);
        Registers[SunSpecMeterMap.OffMeterWh_SF] = unchecked((ushort)SF_Energy);

        Registers[SunSpecMeterMap.OffMeterMeterMode]   = 0;
        Registers[SunSpecMeterMap.OffMeterResetEnergy] = 0;

        WriteEndModelMarker(SunSpecMeterMap.OffEndModelId);
    }

    // ---- Read-only / command-register hooks ----

    protected override bool IsReadOnlyOffset(ushort offset)
    {
        // Only explicit command/config registers are writable. Measurements,
        // scale factors, identity strings, model headers, and markers are stable
        // simulator state from the client's perspective.
        return offset switch
        {
            SunSpecMeterMap.OffCommonDA         => false,
            SunSpecMeterMap.OffMeterMeterMode   => false,
            SunSpecMeterMap.OffMeterResetEnergy => false,
            _                                   => true,
        };
    }

    protected override void OnRegistersWritten(ushort startAddress, ReadOnlySpan<ushort> values)
    {
        // Magic value to clear energy counters: write 0xCAFE to OffMeterResetEnergy.
        var resetAddr = SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterResetEnergy);
        if (startAddress <= resetAddr && resetAddr < startAddress + values.Length)
        {
            if (Registers[SunSpecMeterMap.OffMeterResetEnergy] == 0xCAFE)
            {
                WriteUInt32(SunSpecMeterMap.OffMeterTotWhExp, 0);
                WriteUInt32(SunSpecMeterMap.OffMeterTotWhImp, 0);
            }

            Registers[SunSpecMeterMap.OffMeterResetEnergy] = 0;
        }
    }





    // ---- Simulator ----

    private async Task SimulateAsync(CancellationToken ct)
    {
        var rng = new Random();
        while (!ct.IsCancellationRequested)
        {
            lock (Lock)
            {
                // Currents (A) ~ 10.00 A +/- 0.5
                var iA = (short)(rng.Next(950, 1050));
                var iB = (short)(rng.Next(950, 1050));
                var iC = (short)(rng.Next(950, 1050));
                Registers[SunSpecMeterMap.OffMeterAphA] = (ushort)iA;
                Registers[SunSpecMeterMap.OffMeterAphB] = (ushort)iB;
                Registers[SunSpecMeterMap.OffMeterAphC] = (ushort)iC;
                Registers[SunSpecMeterMap.OffMeterA]    = (ushort)(short)((iA + iB + iC) / 3);

                // Voltages (V) ~ 230.0 +/- 1.0
                var uA = (short)rng.Next(2290, 2310);
                var uB = (short)rng.Next(2290, 2310);
                var uC = (short)rng.Next(2290, 2310);
                Registers[SunSpecMeterMap.OffMeterPhVphA] = (ushort)uA;
                Registers[SunSpecMeterMap.OffMeterPhVphB] = (ushort)uB;
                Registers[SunSpecMeterMap.OffMeterPhVphC] = (ushort)uC;
                Registers[SunSpecMeterMap.OffMeterPhV]    = (ushort)(short)((uA + uB + uC) / 3);

                // Frequency 50.00 Hz +/- 0.05
                Registers[SunSpecMeterMap.OffMeterHz] = (ushort)(short)rng.Next(4995, 5005);

                // Power per phase (W)
                short pA = (short)(uA * iA / 1000);
                short pB = (short)(uB * iB / 1000);
                short pC = (short)(uC * iC / 1000);
                Registers[SunSpecMeterMap.OffMeterWphA] = (ushort)pA;
                Registers[SunSpecMeterMap.OffMeterWphB] = (ushort)pB;
                Registers[SunSpecMeterMap.OffMeterWphC] = (ushort)pC;
                Registers[SunSpecMeterMap.OffMeterW]    = (ushort)(short)(pA + pB + pC);

                // Increment exported energy each tick
                var wh = ReadUInt32(SunSpecMeterMap.OffMeterTotWhExp);
                wh += (uint)Math.Max(0, pA + pB + pC) / 3600u + 1u;
                WriteUInt32(SunSpecMeterMap.OffMeterTotWhExp, wh);
            }

            try { await Task.Delay(1000, ct).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
        }
    }

    public override void Dispose()
    {
        _cts.Cancel();
        try { _simTask.Wait(2000); } catch { /* ignore */ }
        _cts.Dispose();
        base.Dispose();
    }

}
