/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

#region Usings

using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus;

/// <summary>
/// What the simulated meter does, without a socket anywhere near it: the
/// device is built with its background task switched off and stepped by hand,
/// so that a day takes microseconds and every assertion is about the model
/// rather than about timing.
/// </summary>
public class SunSpecMeterSimulationTests
{

    #region (helpers)

    /// <summary>
    /// A meter nobody else is stepping.
    /// </summary>
    private static SunSpecMeterDevice NewMeter(SunSpecMeterMode Mode)
        => new ($"sim-test-{Mode}",
                Mode,
                runSimulation: false);

    private static Int16 Reg(SunSpecMeterDevice Meter, UInt16 Offset)
        => (Int16) Meter.ReadHolding(SunSpecMeterMap.Addr(Offset), 1)![0];

    private static UInt32 Counter(SunSpecMeterDevice Meter, UInt16 Offset)
    {
        var registers = Meter.ReadHolding(SunSpecMeterMap.Addr(Offset), 2)!;
        return ((UInt32) registers[0] << 16) | registers[1];
    }

    private static UInt32 Imported(SunSpecMeterDevice Meter)
        => Counter(Meter, SunSpecMeterMap.OffMeterTotWhImp);

    private static UInt32 Exported(SunSpecMeterDevice Meter)
        => Counter(Meter, SunSpecMeterMap.OffMeterTotWhExp);

    /// <summary>
    /// Step the meter until its simulated clock is inside the given hour. The
    /// only way in, and the only way needed: the simulation has no setter for
    /// its clock, it just walks.
    /// </summary>
    private static void AdvanceToHour(SunSpecMeterDevice Meter, Int32 Hour)
    {

        for (var step = 0; step < 24 && Meter.SimulatedTime.Hour != Hour; step++)
            Meter.Advance(TimeSpan.FromHours(1));

        Assert.That(Meter.SimulatedTime.Hour, Is.EqualTo(Hour), "could not walk the simulated clock to that hour");

    }

    /// <summary>
    /// Clear both energy counters the way a client does it, and check that
    /// they really are clear before a test starts counting.
    /// </summary>
    private static void ClearCounters(SunSpecMeterDevice Meter)
    {

        Assert.That(Meter.WriteHolding(SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterResetEnergy),
                                       SunSpecMeterDevice.ResetEnergyMagicValue),
                    Is.True);

        Assert.Multiple(() => {
            Assert.That(Imported(Meter), Is.Zero);
            Assert.That(Exported(Meter), Is.Zero);
        });

    }

    #endregion


    #region AMeterInFrontOfALoad_OnlyImports()

    /// <summary>
    /// Import-only is what a charging station's meter is, and the counter it
    /// is read for is the imported one - which stayed at zero for as long as
    /// this simulator only ever knew how to export.
    /// </summary>
    [Test]
    public void AMeterInFrontOfALoad_OnlyImports()
    {

        using var meter = NewMeter(SunSpecMeterMode.ImportOnly);

        AdvanceToHour(meter, 12);
        ClearCounters(meter);

        meter.Advance(TimeSpan.FromMinutes(6));

        var power = Reg(meter, SunSpecMeterMap.OffMeterW);

        Assert.Multiple(() => {

            Assert.That(meter.Mode,        Is.EqualTo(SunSpecMeterMode.ImportOnly));
            Assert.That(power,             Is.GreaterThan(0), "a meter in front of a load draws");
            Assert.That(meter.GenerationW, Is.GreaterThan(0), "the sun is up; this meter just does not see it");

            // Six minutes is a tenth of an hour, and that is the whole
            // arithmetic: the counter says what the reading said it would.
            Assert.That(Imported(meter), Is.EqualTo((UInt32) (power / 10)).Within(1));
            Assert.That(Exported(meter), Is.Zero, "nothing flowed the other way");

        });

    }

    #endregion

    #region AMeterInFrontOfAGenerator_OnlyExports_AndIsDarkAtNight()

    [Test]
    public void AMeterInFrontOfAGenerator_OnlyExports_AndIsDarkAtNight()
    {

        using var meter = NewMeter(SunSpecMeterMode.ExportOnly);

        AdvanceToHour(meter, 12);
        ClearCounters(meter);

        meter.Advance(TimeSpan.FromMinutes(6));

        var middayPower = Reg(meter, SunSpecMeterMap.OffMeterW);

        Assert.Multiple(() => {
            Assert.That(middayPower,     Is.LessThan(0), "a meter in front of a generator feeds back");
            Assert.That(Exported(meter), Is.EqualTo((UInt32) (-middayPower / 10)).Within(1));
            Assert.That(Imported(meter), Is.Zero);
        });

        // And the same meter at two in the morning, when there is no sun to
        // export. Zero here is the right answer, not a broken simulation.
        AdvanceToHour(meter, 1);
        ClearCounters(meter);

        meter.Advance(TimeSpan.FromMinutes(6));

        Assert.Multiple(() => {
            Assert.That(meter.SimulatedTime.Hour,                Is.InRange(1, 2));
            Assert.That(meter.GenerationW,                       Is.Zero);
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterW),   Is.Zero);
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterA),   Is.Zero);
            Assert.That(Exported(meter),                         Is.Zero);
            Assert.That(Imported(meter),                         Is.Zero);
        });

    }

    #endregion

    #region AMeterAtTheGridConnection_ImportsAtNightAndExportsAtNoon()

    [Test]
    public void AMeterAtTheGridConnection_ImportsAtNightAndExportsAtNoon()
    {

        using var meter = NewMeter(SunSpecMeterMode.Net);

        AdvanceToHour(meter, 1);
        ClearCounters(meter);
        meter.Advance(TimeSpan.FromMinutes(6));

        var nightPower = Reg(meter, SunSpecMeterMap.OffMeterW);

        Assert.Multiple(() => {
            Assert.That(nightPower,      Is.GreaterThan(0), "no sun, so the site draws from the grid");
            Assert.That(Imported(meter), Is.GreaterThan(0u));
            Assert.That(Exported(meter), Is.Zero);
        });

        AdvanceToHour(meter, 12);
        ClearCounters(meter);
        meter.Advance(TimeSpan.FromMinutes(6));

        var middayPower = Reg(meter, SunSpecMeterMap.OffMeterW);

        Assert.Multiple(() => {
            Assert.That(middayPower,       Is.LessThan(0), "more sun than load, so the site feeds back");
            Assert.That(meter.LoadW,       Is.GreaterThan(0));
            Assert.That(meter.GenerationW, Is.GreaterThan(meter.LoadW));
            Assert.That(Exported(meter),   Is.GreaterThan(0u));
            Assert.That(Imported(meter),   Is.Zero);
        });

    }

    #endregion

    #region WritingTheModeRegister_ChangesWhatTheMeterDoes()

    /// <summary>
    /// The point of the whole exercise: the register a Modbus client writes
    /// has to mean something by the next step.
    /// </summary>
    [Test]
    public void WritingTheModeRegister_ChangesWhatTheMeterDoes()
    {

        using var meter = NewMeter(SunSpecMeterMode.Net);

        var changes = new List<(SunSpecMeterMode From, SunSpecMeterMode To)>();
        meter.OnModeChanged += (from, to) => changes.Add((from, to));

        AdvanceToHour(meter, 12);
        ClearCounters(meter);
        meter.Advance(TimeSpan.FromMinutes(6));

        Assert.That(Reg(meter, SunSpecMeterMap.OffMeterW), Is.LessThan(0), "midday, so a net meter exports");

        Assert.That(meter.WriteHolding(SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode),
                                       (UInt16) SunSpecMeterMode.ImportOnly),
                    Is.True);

        ClearCounters(meter);
        meter.Advance(TimeSpan.FromMinutes(6));

        Assert.Multiple(() => {

            Assert.That(meter.Mode,                                          Is.EqualTo(SunSpecMeterMode.ImportOnly));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterMeterMode),       Is.EqualTo((Int16) SunSpecMeterMode.ImportOnly));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterW),               Is.GreaterThan(0), "same hour, same sun, other meter");
            Assert.That(Imported(meter),                                     Is.GreaterThan(0u));
            Assert.That(Exported(meter),                                     Is.Zero);

            Assert.That(changes,                                             Has.Count.EqualTo(1));
            Assert.That(changes[0],                                          Is.EqualTo((SunSpecMeterMode.Net, SunSpecMeterMode.ImportOnly)));

        });

    }

    #endregion

    #region WritingTheModeRegisterWithNonsense_LeavesItAsItWas()

    [Test]
    public void WritingTheModeRegisterWithNonsense_LeavesItAsItWas()
    {

        using var meter = NewMeter(SunSpecMeterMode.ImportOnly);

        var changes = 0;
        meter.OnModeChanged += (from, to) => changes++;

        // The write itself is answered - the address is real and writable -
        // but reading the register back is how a client is told no.
        meter.WriteHolding(SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode), 7);

        Assert.Multiple(() => {
            Assert.That(meter.Mode,                                    Is.EqualTo(SunSpecMeterMode.ImportOnly));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterMeterMode), Is.EqualTo((Int16) SunSpecMeterMode.ImportOnly));
            Assert.That(changes,                                       Is.Zero);
        });

    }

    #endregion

    #region ASmallLoadIsNotLostToRounding()

    /// <summary>
    /// A second of 1200 W is a third of a watt-hour, and a counter that can
    /// only add whole ones has to keep the rest or it will be wrong by
    /// whatever it threw away - in the direction the code happened to round.
    /// Stepped one second at a time, which is how the meter actually runs.
    /// </summary>
    [Test]
    public void ASmallLoadIsNotLostToRounding()
    {

        using var meter = NewMeter(SunSpecMeterMode.ImportOnly);

        AdvanceToHour(meter, 1);
        ClearCounters(meter);

        var expectedWh = 0.0;

        for (var second = 0; second < 600; second++)
        {
            meter.Advance(TimeSpan.FromSeconds(1));
            expectedWh += Reg(meter, SunSpecMeterMap.OffMeterW) / 3600.0;
        }

        Assert.Multiple(() => {
            Assert.That(expectedWh,      Is.GreaterThan(100), "ten minutes of a night load is not nothing");
            Assert.That(Imported(meter), Is.EqualTo((UInt32) expectedWh).Within(1));
        });

    }

    #endregion

    #region TheTotalsAreTheSumOfThePhases()

    /// <summary>
    /// SunSpec "A" is the total AC current, which is the three phases added
    /// up; "PhV" is the line-to-neutral average. A client that multiplies the
    /// two to sanity-check the power has to get the power back.
    /// </summary>
    [Test]
    public void TheTotalsAreTheSumOfThePhases()
    {

        using var meter = NewMeter(SunSpecMeterMode.ImportOnly);

        meter.Advance(TimeSpan.FromMinutes(1));

        var phaseCurrents = new[] { Reg(meter, SunSpecMeterMap.OffMeterAphA),   Reg(meter, SunSpecMeterMap.OffMeterAphB),   Reg(meter, SunSpecMeterMap.OffMeterAphC)   };
        var phaseVoltages = new[] { Reg(meter, SunSpecMeterMap.OffMeterPhVphA), Reg(meter, SunSpecMeterMap.OffMeterPhVphB), Reg(meter, SunSpecMeterMap.OffMeterPhVphC) };
        var phasePowers   = new[] { Reg(meter, SunSpecMeterMap.OffMeterWphA),   Reg(meter, SunSpecMeterMap.OffMeterWphB),   Reg(meter, SunSpecMeterMap.OffMeterWphC)   };

        Assert.Multiple(() => {

            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterA),   Is.EqualTo(phaseCurrents.Sum(current => current)));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterW),   Is.EqualTo(phasePowers.  Sum(power   => power)));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterPhV), Is.EqualTo(phaseVoltages.Sum(voltage => voltage) / 3));

            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterHz),  Is.InRange(4990, 5010));

            for (var phase = 0; phase < 3; phase++)
            {
                Assert.That(phaseVoltages[phase], Is.InRange(2280, 2320));
                Assert.That(phaseCurrents[phase], Is.GreaterThan(0));

                // I = |P| / U, in the units the registers are in.
                Assert.That(phaseCurrents[phase],
                            Is.EqualTo(Math.Abs(phasePowers[phase]) * 1000 / phaseVoltages[phase]).Within(1),
                            $"the current of phase {phase + 1} does not follow its power");
            }

        });

    }

    #endregion

    #region ANewMeterAlreadyReadsSomething()

    /// <summary>
    /// A client that connects in the first second must not find a meter full
    /// of zeroes that it cannot tell apart from a dead one.
    /// </summary>
    [Test]
    public void ANewMeterAlreadyReadsSomething()
    {

        using var meter = NewMeter(SunSpecMeterMode.ImportOnly);

        Assert.Multiple(() => {
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterPhV), Is.InRange(2280, 2320));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterHz),  Is.InRange(4990, 5010));
            Assert.That(Reg(meter, SunSpecMeterMap.OffMeterW),   Is.GreaterThan(0));

            // ... and nothing has flowed yet, because no time has passed.
            Assert.That(Imported(meter), Is.Zero);
            Assert.That(Exported(meter), Is.Zero);
        });

    }

    #endregion

    #region ACompressedDayRunsTheCurvesFasterAndTheCountersNotAtAll()

    /// <summary>
    /// The demo knob. It moves the load and the sun; it must not touch the
    /// watt-hours, which are what somebody watching in real time checks the
    /// power against.
    /// </summary>
    [Test]
    public void ACompressedDayRunsTheCurvesFasterAndTheCountersNotAtAll()
    {

        using var meter = new SunSpecMeterDevice("sim-test-fast-day",
                                                 SunSpecMeterMode.ImportOnly,
                                                 simulatedDayLength: TimeSpan.FromMinutes(10),
                                                 runSimulation:      false);

        var startedAt = meter.SimulatedTime;

        ClearCounters(meter);
        meter.Advance(TimeSpan.FromMinutes(1));

        var power = Reg(meter, SunSpecMeterMap.OffMeterW);

        Assert.Multiple(() => {

            Assert.That(meter.SimulatedDayLength, Is.EqualTo(TimeSpan.FromMinutes(10)));

            // One real minute is a tenth of the ten-minute day: 2.4 hours.
            Assert.That(meter.SimulatedTime - startedAt,
                        Is.EqualTo(TimeSpan.FromHours(2.4)).Within(TimeSpan.FromSeconds(1)));

            // But still only one real minute of energy.
            Assert.That(Imported(meter), Is.EqualTo((UInt32) (power / 60)).Within(1));

        });

    }

    #endregion

}
