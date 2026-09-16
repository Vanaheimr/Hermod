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

using System.Diagnostics;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// In-memory simulated SunSpec 3-phase Energy Meter (Common Model 1 + Meter
/// Model 213 subset). All access is mutex-guarded by the base class. A
/// background task advances the simulation every second.
///
/// What is simulated is a site with a load and a photovoltaic generator, and
/// what this meter reads of it is decided by where it sits in that site -
/// which is what the mode register (<see cref="SunSpecMeterMode"/>) says:
///
///   * Net        - at the grid connection point: load minus generation, so
///                  the power is signed and both counters move.
///   * ImportOnly - in front of the load: power is never negative and only
///                  the imported counter moves.
///   * ExportOnly - in front of the generator: power is never positive, only
///                  the exported counter moves, and at night it is zero
///                  because the sun is down.
///
/// Sign convention, which all of that rests on: positive real power means
/// energy flowing INTO the site (imported, the meter running forwards),
/// negative means out of it (exported). Currents are magnitudes and stay
/// positive either way, the way a real meter reports them - the direction is
/// in the sign of the power alone.
///
/// The load and the generation follow the time of day, so a meter left
/// running looks like a day: quiet at night, exporting around noon. The
/// simulated day can be compressed for a demo, which only speeds up those
/// two curves - the counters always count real seconds, so that anything
/// watching in real time can check them against the power it is reading.
/// </summary>
public sealed class SunSpecMeterDevice : ASunSpecDeviceBase
{

    /// <summary>
    /// How often the background task advances the simulation.
    /// </summary>
    public static readonly TimeSpan DefaultSimulationInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The value that clears both energy counters when it is written to
    /// <see cref="SunSpecMeterMap.OffMeterResetEnergy"/>.
    /// </summary>
    public const ushort ResetEnergyMagicValue = 0xCAFE;

    // Scale factors (as int16) - decimal exponent
    private const short SF_Current   = -2; // 1 LSB = 0.01 A
    private const short SF_Voltage   = -1; // 1 LSB = 0.1 V
    private const short SF_Frequency = -2; // 1 LSB = 0.01 Hz
    private const short SF_Power     =  0; // 1 LSB = 1 W
    private const short SF_Energy    =  0; // 1 LSB = 1 Wh

    // The simulated site, in watts for the whole meter, before the split
    // across the three phases. Sized so that a sunny midday exports a few kW
    // and an evening draws about as much - the two situations a controller
    // reading this meter is written for.
    private const double PeakLoadW        = 6900.0;
    private const double PeakGenerationW  = 9200.0;
    private const double SunriseHour      =    6.0;
    private const double SunsetHour       =   20.0;

    private const int    NominalVoltage   = 2300; // 230.0 V in 0.1 V
    private const int    NominalFrequency = 5000; // 50.00 Hz in 0.01 Hz

    private readonly CancellationTokenSource  _cts = new();
    private readonly Random                   _rng = new();
    private readonly Task?                    _simTask;
    private readonly double                   _dayScale;

    private DateTime                          _simulatedTime;
    private SunSpecMeterMode                  _mode;

    // The watt-hours that have flowed but do not yet add up to a whole one.
    // Kept rather than truncated, because a meter that threw away the
    // remainder of every step would lose most of a small load entirely.
    private double                            _importedRest;
    private double                            _exportedRest;

    private int                               _loadW;
    private int                               _generationW;

    public override ushort BaseAddress => SunSpecMeterMap.BaseAddress;
    public override string DisplayName => "SunSpec 3-Phase Meter (M213)";
    public override IReadOnlySet<ushort> ProtectedAddresses => SunSpecMeterMap.ProtectedAddresses;
    public override IReadOnlySet<ushort> CommandedAddresses => SunSpecMeterMap.CommandedAddresses;

    /// <summary>
    /// What kind of meter this is at the moment, as the mode register says.
    /// </summary>
    public SunSpecMeterMode Mode
    {
        get { lock (Lock) return _mode; }
    }

    /// <summary>
    /// How much real time one simulated day takes.
    /// </summary>
    public TimeSpan SimulatedDayLength => TimeSpan.FromDays(1) / _dayScale;

    /// <summary>
    /// The time of day the simulation has reached. The real one, unless the
    /// day was compressed.
    /// </summary>
    public DateTime SimulatedTime
    {
        get { lock (Lock) return _simulatedTime; }
    }

    /// <summary>
    /// What the simulated site was drawing at the last step, in watts. Always
    /// positive, and seen by the meter only in the modes that look at it.
    /// </summary>
    public int LoadW
    {
        get { lock (Lock) return _loadW; }
    }

    /// <summary>
    /// What the simulated generator was producing at the last step, in watts.
    /// Always positive, and zero at night.
    /// </summary>
    public int GenerationW
    {
        get { lock (Lock) return _generationW; }
    }

    /// <summary>
    /// What the two energy counters stand at, as the registers hold them:
    /// raw, unscaled, and with the scale factor that gives them meaning.
    /// </summary>
    /// <remarks>
    /// For a host that keeps these across a restart. A real meter's energy
    /// register is monotonic and survives losing power - that is most of what
    /// makes it a meter rather than a sensor - and a simulation that starts at
    /// zero every time it comes up cannot be used to exercise anything that
    /// spans a restart: a charging session, a rollover, a bill.
    ///
    /// Raw rather than scaled, because the scale factor is what makes the
    /// number mean something and a host writing one down without the other is
    /// writing down a number it cannot read back.
    /// </remarks>
    public (UInt32 ImportedWh, UInt32 ExportedWh, Int16 ScaleFactor) EnergyCounters
    {
        get
        {
            lock (Lock)
                return (
                           ReadUInt32(SunSpecMeterMap.OffMeterTotWhImp),
                           ReadUInt32(SunSpecMeterMap.OffMeterTotWhExp),
                           (Int16) Registers[SunSpecMeterMap.OffMeterWh_SF]
                       );
        }
    }

    /// <summary>
    /// Put the energy counters back where a previous run left them.
    /// </summary>
    /// <remarks>
    /// Refused unless the scale factor matches the one this device is running
    /// with: the same number under a different scale factor is a different
    /// amount of energy, and quietly taking it would move a meter reading by a
    /// factor of ten.
    ///
    /// Only ever called before a device starts measuring. Nothing stops a host
    /// calling it later, and nothing should want to: an energy counter that
    /// jumps is the one thing a meter must never do.
    /// </remarks>
    /// <param name="ImportedWh">The imported counter, as the register holds it.</param>
    /// <param name="ExportedWh">The exported counter, as the register holds it.</param>
    /// <param name="ScaleFactor">The scale factor those were written down under.</param>
    /// <returns>Whether they were taken.</returns>
    public Boolean RestoreEnergyCounters(UInt32  ImportedWh,
                                         UInt32  ExportedWh,
                                         Int16   ScaleFactor)
    {

        lock (Lock)
        {

            if ((Int16) Registers[SunSpecMeterMap.OffMeterWh_SF] != ScaleFactor)
                return false;

            WriteUInt32(SunSpecMeterMap.OffMeterTotWhImp, ImportedWh);
            WriteUInt32(SunSpecMeterMap.OffMeterTotWhExp, ExportedWh);

            // The fractions of a watt-hour that had not yet become one start
            // again from nothing. Less than a watt-hour of what came before is
            // lost, which is the right side to err on: a counter that came back
            // slightly high would be a meter billing for energy it never saw.
            _importedRest = 0;
            _exportedRest = 0;

            return true;

        }

    }

    /// <summary>
    /// Raised when the mode register was written with a different mode, with
    /// the mode before and the mode after.
    /// </summary>
    /// <remarks>
    /// Raised while the register lock is held, so that a host writing these
    /// down gets them in the order they happened. Reading the device from a
    /// handler is fine - the lock is reentrant - but waiting on another
    /// thread in one is not. A handler that throws is ignored: a meter that
    /// stopped answering because somebody's log was full would be worse than
    /// a missing line.
    /// </remarks>
    public event Action<SunSpecMeterMode, SunSpecMeterMode>? OnModeChanged;

    /// <summary>
    /// Create a simulated SunSpec energy meter.
    /// </summary>
    /// <param name="serialNumber">The serial number in SunSpec Common Model 1.</param>
    /// <param name="mode">What kind of meter this is, and therefore which way energy flows through it.</param>
    /// <param name="simulatedDayLength">How much real time one simulated day takes (default: a day).</param>
    /// <param name="runSimulation">Whether a background task advances the simulation; false leaves that to <see cref="Advance"/>.</param>
    public SunSpecMeterDevice(string            serialNumber,
                              SunSpecMeterMode  mode                = SunSpecMeterMode.Net,
                              TimeSpan?         simulatedDayLength  = null,
                              bool              runSimulation       = true)

        : base(SunSpecMeterMap.RegisterCount)

    {

        // An unknown mode is not one: a meter that reported a number
        // nothing can interpret would be worse than one that says Net.
        _mode           = mode is SunSpecMeterMode.Net        or
                                  SunSpecMeterMode.ImportOnly or
                                  SunSpecMeterMode.ExportOnly
                              ? mode
                              : SunSpecMeterMode.Net;

        _simulatedTime  = DateTime.Now;
        _dayScale       = simulatedDayLength is TimeSpan day && day > TimeSpan.Zero
                              ? TimeSpan.FromDays(1) / day
                              : 1.0;

        InitCommonModel(
            manufacturer: "Vanaheimr",
            model:        "DemoMeter-3P",
            options:      "tls-only",
            version:      "1.0.0",
            serialNumber: serialNumber);

        InitMeter();

        // Once before anything can read, so that a client connecting in the
        // first second - or a host that drives the simulation itself - never
        // sees a meter reading nothing at all.
        Advance(TimeSpan.Zero);

        if (runSimulation)
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

        Registers[SunSpecMeterMap.OffMeterMeterMode]   = (ushort)_mode;
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
            if (Registers[SunSpecMeterMap.OffMeterResetEnergy] == ResetEnergyMagicValue)
            {
                WriteUInt32(SunSpecMeterMap.OffMeterTotWhExp, 0);
                WriteUInt32(SunSpecMeterMap.OffMeterTotWhImp, 0);

                // The remainders as well, or a cleared meter would still be
                // carrying most of a watt-hour of what came before.
                _importedRest = 0;
                _exportedRest = 0;
            }

            Registers[SunSpecMeterMap.OffMeterResetEnergy] = 0;
        }

        // The mode register takes three values and no others. A fourth is put
        // back rather than refused, because a Modbus write here can only be
        // answered with "illegal address", which would be a lie; reading the
        // register back and finding the old mode is the older and plainer way
        // of being told no.
        var modeAddr = SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode);
        if (startAddress <= modeAddr && modeAddr < startAddress + values.Length)
        {

            var written = (SunSpecMeterMode)Registers[SunSpecMeterMap.OffMeterMeterMode];

            if (written is not SunSpecMeterMode.Net        and
                           not SunSpecMeterMode.ImportOnly and
                           not SunSpecMeterMode.ExportOnly)
            {
                Registers[SunSpecMeterMap.OffMeterMeterMode] = (ushort)_mode;
                return;
            }

            if (written != _mode)
            {
                var before = _mode;
                _mode = written;
                try { OnModeChanged?.Invoke(before, written); } catch { /* not our problem */ }
            }

        }

    }

    // ---- Simulator ----

    /// <summary>
    /// Advance the simulation by <paramref name="elapsed"/>: sample the
    /// measurements again, and add what flowed during that time to the energy
    /// counters.
    /// </summary>
    /// <remarks>
    /// Public so that a test, or a host that wants the simulation on a clock
    /// of its own, can step it without waiting for the background task. The
    /// energy added is what the power AFTER this step carries over the whole
    /// interval, which is what makes the reading and the counters agree: the
    /// counter grows by exactly |W| * elapsed, and that can be checked from
    /// the outside.
    /// </remarks>
    /// <param name="elapsed">How much time flowed. Zero only re-samples; negative counts as zero.</param>
    public void Advance(TimeSpan elapsed)
    {
        lock (Lock)
        {

            var seconds = elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 0.0;

            _simulatedTime = _simulatedTime.AddSeconds(seconds * _dayScale);

            var hour = _simulatedTime.TimeOfDay.TotalHours;

            // What the site is doing, and what this meter therefore sees of it.
            _loadW       = (int)Math.Round(PeakLoadW       * LoadShape(hour)  * (0.95 + 0.10 * _rng.NextDouble()));
            _generationW = (int)Math.Round(PeakGenerationW * SolarShape(hour) * (0.80 + 0.20 * _rng.NextDouble()));

            var totalW = _mode switch {
                             SunSpecMeterMode.ImportOnly =>  _loadW,
                             SunSpecMeterMode.ExportOnly => -_generationW,
                             _                           =>  _loadW - _generationW
                         };

            // Voltages (V) ~ 230.0 +/- 1.0
            var uA = (short)_rng.Next(NominalVoltage - 10, NominalVoltage + 10);
            var uB = (short)_rng.Next(NominalVoltage - 10, NominalVoltage + 10);
            var uC = (short)_rng.Next(NominalVoltage - 10, NominalVoltage + 10);
            Registers[SunSpecMeterMap.OffMeterPhVphA] = (ushort)uA;
            Registers[SunSpecMeterMap.OffMeterPhVphB] = (ushort)uB;
            Registers[SunSpecMeterMap.OffMeterPhVphC] = (ushort)uC;
            Registers[SunSpecMeterMap.OffMeterPhV]    = (ushort)(short)((uA + uB + uC) / 3);

            // Frequency 50.00 Hz +/- 0.05
            Registers[SunSpecMeterMap.OffMeterHz] = (ushort)(short)_rng.Next(NominalFrequency - 5, NominalFrequency + 5);

            // Three phases, never quite equal, and the third takes what
            // rounding left over so that the total really is their sum.
            var pA = (short)Math.Round(totalW * (0.340 + 0.02 * (_rng.NextDouble() - 0.5)));
            var pB = (short)Math.Round(totalW * (0.332 + 0.02 * (_rng.NextDouble() - 0.5)));
            var pC = (short)(totalW - pA - pB);
            Registers[SunSpecMeterMap.OffMeterWphA] = (ushort)pA;
            Registers[SunSpecMeterMap.OffMeterWphB] = (ushort)pB;
            Registers[SunSpecMeterMap.OffMeterWphC] = (ushort)pC;
            Registers[SunSpecMeterMap.OffMeterW]    = (ushort)(short)totalW;

            // Current follows the power it carries: I = |P| / U. A magnitude,
            // positive whichever way the power is going.
            var iA = CurrentOf(pA, uA);
            var iB = CurrentOf(pB, uB);
            var iC = CurrentOf(pC, uC);
            Registers[SunSpecMeterMap.OffMeterAphA] = (ushort)iA;
            Registers[SunSpecMeterMap.OffMeterAphB] = (ushort)iB;
            Registers[SunSpecMeterMap.OffMeterAphC] = (ushort)iC;

            // SunSpec "A" is the TOTAL AC current, which is the sum of the
            // three phases and not their average.
            Registers[SunSpecMeterMap.OffMeterA]    = (ushort)(short)(iA + iB + iC);

            // And what flowed while that was happening.
            var wh = Math.Abs((double)totalW) * (seconds / 3600.0);

            if (totalW > 0)
            {
                _importedRest += wh;
                Accumulate(SunSpecMeterMap.OffMeterTotWhImp, ref _importedRest);
            }

            else if (totalW < 0)
            {
                _exportedRest += wh;
                Accumulate(SunSpecMeterMap.OffMeterTotWhExp, ref _exportedRest);
            }

        }
    }

    /// <summary>
    /// Move the whole watt-hours out of <paramref name="rest"/> into the
    /// counter at <paramref name="offset"/>, keeping the fraction for later.
    /// </summary>
    private void Accumulate(ushort offset, ref double rest)
    {

        if (rest < 1.0)
            return;

        var whole = Math.Floor(rest);
        rest -= whole;

        // A SunSpec energy counter is 32 bits and rolls over, as the meters
        // it stands in for do. The modulo keeps the cast defined even after a
        // step of implausible length.
        unchecked
        {
            WriteUInt32(offset, ReadUInt32(offset) + (uint)(whole % 4294967296.0));
        }

    }

    /// <summary>
    /// The current a phase carries, in 0.01 A, from its power in W and its
    /// voltage in 0.1 V.
    /// </summary>
    private static short CurrentOf(short powerW, short voltage_dV)
        => voltage_dV == 0
               ? (short)0
               : (short)Math.Min(short.MaxValue, Math.Abs((long)powerW) * 1000L / voltage_dV);

    /// <summary>
    /// What fraction of its peak the site is drawing at this hour: a floor it
    /// never goes below, a bump when people get up, a larger one when they
    /// come home.
    /// </summary>
    private static double LoadShape(double hour)
        => 0.18
         + 0.45 * Bell(hour,  7.5, 1.3)
         + 0.70 * Bell(hour, 19.0, 2.2);

    /// <summary>
    /// What fraction of its peak the generator is producing at this hour:
    /// nothing before sunrise or after sunset, most of it around noon.
    /// </summary>
    private static double SolarShape(double hour)
        => hour <= SunriseHour || hour >= SunsetHour
               ? 0.0
               : Math.Sin(Math.PI * (hour - SunriseHour) / (SunsetHour - SunriseHour));

    private static double Bell(double x, double centre, double width)
        => Math.Exp(-((x - centre) * (x - centre)) / (2 * width * width));

    private async Task SimulateAsync(CancellationToken ct)
    {

        var last = Stopwatch.GetTimestamp();

        while (!ct.IsCancellationRequested)
        {

            try { await Task.Delay(DefaultSimulationInterval, ct).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }

            // Measured rather than assumed: Task.Delay(1s) is at least a
            // second and on a busy machine rather more, and a simulation that
            // counted every tick as exactly one would invent energy.
            var now = Stopwatch.GetTimestamp();
            Advance(Stopwatch.GetElapsedTime(last, now));
            last = now;

        }

    }

    public override void Dispose()
    {
        _cts.Cancel();
        try { _simTask?.Wait(2000); } catch { /* ignore */ }
        _cts.Dispose();
        base.Dispose();
    }

}
