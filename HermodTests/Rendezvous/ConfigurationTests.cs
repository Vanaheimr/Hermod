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

#region Usings

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for the configuration and the transfer profiles.
    /// </summary>
    [TestFixture]
    public class ConfigurationTests
    {

        #region Transfer profiles

        [Test]
        public void TransferProfiles_HaveSensibleDefaults()
        {

            var interactive  = TransferProfileSettings.Defaults(TransferProfile.Interactive);
            var balanced     = TransferProfileSettings.Defaults(TransferProfile.Balanced);
            var bulk         = TransferProfileSettings.Defaults(TransferProfile.Bulk);

            Assert.Multiple(() => {

                // Latency: small buffers, no Nagle, early keep-alive probes.
                Assert.That(interactive.NoDelay,                 Is.True);
                Assert.That(interactive.RelayBufferSize,         Is.LessThan(balanced.RelayBufferSize));
                Assert.That(interactive.KeepAliveTime,           Is.LessThan(bulk.KeepAliveTime));
                Assert.That(interactive.SocketReceiveBufferSize, Is.Not.Null);

                // Throughput: large buffers, Nagle, and no explicit socket buffer
                // sizes, so that the receive window auto-tuning keeps working.
                Assert.That(bulk.NoDelay,                        Is.False);
                Assert.That(bulk.RelayBufferSize,                Is.GreaterThan(balanced.RelayBufferSize));
                Assert.That(bulk.SocketReceiveBufferSize,        Is.Null);
                Assert.That(bulk.SocketSendBufferSize,           Is.Null);

                // Chat: an interactive rendezvous queues more (but smaller) messages.
                Assert.That(interactive.BroadcastQueueLength,    Is.GreaterThan(bulk.BroadcastQueueLength));

            });

        }

        [Test]
        public void TransferProfiles_AreValid()
        {

            Assert.Multiple(() => {
                foreach (var profile in Enum.GetValues<TransferProfile>())
                    Assert.That(TransferProfileSettings.Defaults(profile).Validate(profile.AsText()),
                                Is.Empty,
                                $"The default settings of the {profile} profile are invalid!");
            });

        }

        [Test]
        public void TransferProfiles_RoundTripTheirTextRepresentation()
        {

            Assert.Multiple(() => {
                foreach (var profile in Enum.GetValues<TransferProfile>())
                {

                    var success = TransferProfileExtensions.TryParse(profile.AsText(), out var parsed);

                    Assert.That(success, Is.True,             $"Could not parse '{profile.AsText()}'!");
                    Assert.That(parsed,  Is.EqualTo(profile));

                }
            });

        }

        [Test]
        public void TransferProfileOptions_ReturnTheSettingsOfEachProfile()
        {

            var options = new TransferProfilesOptions();

            Assert.Multiple(() => {
                Assert.That(options[TransferProfile.Balanced],    Is.SameAs(options.Balanced));
                Assert.That(options[TransferProfile.Interactive], Is.SameAs(options.Interactive));
                Assert.That(options[TransferProfile.Bulk],        Is.SameAs(options.Bulk));
            });

        }

        [Test]
        public async Task TransferProfiles_CanBeOverriddenByConfiguration()
        {

            await using var host = RendezvousTestHost.Create(options => {
                                       options.Profiles.Interactive.RelayBufferSize  = 4096;
                                       options.Profiles.Interactive.NoDelay          = true;
                                   });

            host.ExecuteOk("ConnectPorts([?,?], Interactive)");

            Assert.That(host.Session.ProfileSettings.RelayBufferSize, Is.EqualTo(4096));

        }

        #endregion

        #region Configuration validation

        [Test]
        public void DefaultConfiguration_IsValid()
        {

            Assert.That(new RendezvousOptions().Validate(), Is.Empty);

        }

        [Test]
        public void InvalidIPAddress_IsRejected()
        {

            var errors = new RendezvousOptions { ControlAddress = "not an IP address" }.Validate();

            Assert.That(errors, Has.Exactly(1).Contains("ControlAddress"));

        }

        [Test]
        public void InvalidPortRange_IsRejected()
        {

            var errors = new RendezvousOptions {
                             MinDataPort  = IPPort.Parse(40000),
                             MaxDataPort  = IPPort.Parse(30000)
                         }.Validate();

            Assert.That(errors, Has.Exactly(1).Contains("MinDataPort"));

        }

        [Test]
        [TestCase("RendezvousTimeout")]
        [TestCase("IdleTimeout")]
        [TestCase("MaintenanceInterval")]
        [TestCase("ControlIdleTimeout")]
        public void NonPositiveTimeouts_AreRejected(String Property)
        {

            var options = new RendezvousOptions();

            typeof(RendezvousOptions).GetProperty(Property)!.SetValue(options, TimeSpan.Zero);

            Assert.That(options.Validate(), Has.Exactly(1).Contains(Property));

        }

        [Test]
        public void InvalidProfileSettings_AreRejected()
        {

            var options = new RendezvousOptions();
            options.Profiles.Bulk.RelayBufferSize = 7;

            Assert.That(options.Validate(), Has.Exactly(1).Contains("RelayBufferSize"));

        }

        [Test]
        public void TooFewPortsPerSession_AreRejected()
        {

            var errors = new RendezvousOptions { MaxPortsPerSession = 1 }.Validate();

            Assert.That(errors, Has.Exactly(1).Contains("MaxPortsPerSession"));

        }

        [Test]
        public void SeveralInvalidValues_AreAllReported()
        {

            var errors = new RendezvousOptions {
                             ControlAddress  = "nonsense",
                             MaxSessions     = 0
                         }.Validate();

            Assert.That(errors, Has.Exactly(2).Items,
                        "Validation must not stop at the first problem!");

        }

        [Test]
        public void IsAllowedDataPort_RespectsThePortRange()
        {

            var options = new RendezvousOptions {
                              MinDataPort  = IPPort.Parse(20000),
                              MaxDataPort  = IPPort.Parse(30000),
                              ControlPort  = IPPort.Parse(25000)
                          };

            Assert.Multiple(() => {
                Assert.That(options.IsAllowedDataPort(IPPort.Parse(19999)), Is.False);
                Assert.That(options.IsAllowedDataPort(IPPort.Parse(20000)), Is.True);
                Assert.That(options.IsAllowedDataPort(IPPort.Parse(30000)), Is.True);
                Assert.That(options.IsAllowedDataPort(IPPort.Parse(30001)), Is.False);
                Assert.That(options.IsAllowedDataPort(IPPort.Parse(25000)), Is.False, "The control port must never be used for a rendezvous!");
            });

        }

        #endregion

    }

}
