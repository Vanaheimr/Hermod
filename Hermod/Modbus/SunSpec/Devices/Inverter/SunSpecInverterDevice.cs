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
/// In-memory simulated SunSpec Single-Phase Inverter (Common Model 1 +
/// Inverter Model 101 subset). Demonstrates that the abstraction in
/// <see cref="ISunSpecDevice"/> + <see cref="ASunSpecDeviceBase"/> isn't
/// meter-specific - dispatcher and TLS frontend reuse without changes.
/// </summary>
public sealed class SunSpecInverterDevice : ASunSpecDeviceBase
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _simTask;

    // Scale factors
    private const short SF_AcCurrent   = -2; // 0.01 A
    private const short SF_AcVoltage   = -1; // 0.1 V
    private const short SF_AcPower     =  0; // 1 W
    private const short SF_AcFrequency = -2; // 0.01 Hz
    private const short SF_DcCurrent   = -2; // 0.01 A
    private const short SF_DcVoltage   = -1; // 0.1 V
    private const short SF_DcPower     =  0; // 1 W

    public override ushort BaseAddress => SunSpecInverterMap.BaseAddress;
    public override string DisplayName => "SunSpec Single-Phase Inverter (M101)";
    public override IReadOnlySet<ushort> ProtectedAddresses => SunSpecInverterMap.ProtectedAddresses;
    public override IReadOnlySet<ushort> CommandedAddresses => SunSpecInverterMap.CommandedAddresses;

    public SunSpecInverterDevice(string serialNumber)
        : base(SunSpecInverterMap.RegisterCount)
    {
        InitCommonModel(
            manufacturer: "Vanaheimr",
            model:        "DemoInverter-1P",
            options:      "tls-only",
            version:      "1.0.0",
            serialNumber: serialNumber);

        InitInverter();
        _simTask = Task.Run(() => SimulateAsync(_cts.Token));
    }

    private void InitInverter()
    {
        Registers[SunSpecInverterMap.OffInvId]  = SunSpecInverterMap.InverterModelId;
        Registers[SunSpecInverterMap.OffInvLen] = SunSpecInverterMap.InverterModelLength;

        Registers[SunSpecInverterMap.OffInvA_SF]   = unchecked((ushort)SF_AcCurrent);
        Registers[SunSpecInverterMap.OffInvV_SF]   = unchecked((ushort)SF_AcVoltage);
        Registers[SunSpecInverterMap.OffInvW_SF]   = unchecked((ushort)SF_AcPower);
        Registers[SunSpecInverterMap.OffInvHz_SF]  = unchecked((ushort)SF_AcFrequency);
        Registers[SunSpecInverterMap.OffInvDCA_SF] = unchecked((ushort)SF_DcCurrent);
        Registers[SunSpecInverterMap.OffInvDCV_SF] = unchecked((ushort)SF_DcVoltage);
        Registers[SunSpecInverterMap.OffInvDCW_SF] = unchecked((ushort)SF_DcPower);

        // Initial state: device is in MPPT (producing) mode.
        Registers[SunSpecInverterMap.OffInvSt]    = SunSpecInverterMap.StateMppt;
        Registers[SunSpecInverterMap.OffInvStVnd] = SunSpecInverterMap.VendorCommandNormal;

        WriteEndModelMarker(SunSpecInverterMap.OffEndModelId);
    }

    protected override bool IsReadOnlyOffset(ushort offset)
    {
        return offset switch
        {
            SunSpecCommon.OffCommonDA          => false,
            SunSpecInverterMap.OffInvStVnd     => false,
            _                                  => true,
        };
    }

    protected override void OnRegistersWritten(ushort startAddress, ReadOnlySpan<ushort> values)
    {
        var commandAddress = SunSpecInverterMap.Addr(SunSpecInverterMap.OffInvStVnd);
        if (startAddress <= commandAddress &&
            commandAddress < startAddress + values.Length)
        {
            ApplyVendorStateCommand();
        }
    }

    private ushort NormalizeVendorStateCommand()
    {
        var command = Registers[SunSpecInverterMap.OffInvStVnd];

        if (command == SunSpecInverterMap.VendorCommandNormal  ||
            command == SunSpecInverterMap.VendorCommandCurtail ||
            command == SunSpecInverterMap.VendorCommandStandby ||
            command == SunSpecInverterMap.VendorCommandFault)
        {
            return command;
        }

        Registers[SunSpecInverterMap.OffInvStVnd] = SunSpecInverterMap.VendorCommandNormal;
        return SunSpecInverterMap.VendorCommandNormal;
    }

    private void ApplyVendorStateCommand()
    {
        Registers[SunSpecInverterMap.OffInvSt] = NormalizeVendorStateCommand() switch
        {
            SunSpecInverterMap.VendorCommandCurtail => SunSpecInverterMap.StateThrottled,
            SunSpecInverterMap.VendorCommandStandby => SunSpecInverterMap.StateStandby,
            SunSpecInverterMap.VendorCommandFault   => SunSpecInverterMap.StateFault,
            _                                      => SunSpecInverterMap.StateMppt,
        };
    }

    private async Task SimulateAsync(CancellationToken ct)
    {
        var rng = new Random();
        while (!ct.IsCancellationRequested)
        {
            lock (Lock)
            {
                var command = NormalizeVendorStateCommand();

                // AC voltage L-N ~ 230.0 +/- 1.0
                var uA = (ushort)rng.Next(2290, 2310);
                Registers[SunSpecInverterMap.OffInvPhVphA] = uA;

                // Frequency 50.00 Hz +/- 0.05
                Registers[SunSpecInverterMap.OffInvHz] = (ushort)rng.Next(4995, 5005);

                // DC side: panel voltage ~ 350 V +/- 10, current ~ 8 A +/- 1
                var dcV = (ushort)rng.Next(3400, 3600);
                var dcA = command switch
                {
                    SunSpecInverterMap.VendorCommandStandby => (ushort)0,
                    SunSpecInverterMap.VendorCommandFault   => (ushort)0,
                    SunSpecInverterMap.VendorCommandCurtail => (ushort)rng.Next(200, 300),
                    _                                      => (ushort)rng.Next(700, 900),
                };
                Registers[SunSpecInverterMap.OffInvDCV] = dcV;
                Registers[SunSpecInverterMap.OffInvDCA] = dcA;
                // DC power = (V/10) * (A/100) = V*A/1000
                var dcW = (ushort)(dcV * dcA / 1000);
                Registers[SunSpecInverterMap.OffInvDCW] = dcW;

                // AC power output = ~95% of DC (efficiency)
                var acW = (short)(dcW * 95 / 100);
                Registers[SunSpecInverterMap.OffInvW] = (ushort)acW;
                // AC current = AC power / AC voltage (rough)
                var acA = (ushort)((acW * 1000) / Math.Max(uA, (ushort)1));
                Registers[SunSpecInverterMap.OffInvA] = acA;

                ApplyVendorStateCommand();
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
