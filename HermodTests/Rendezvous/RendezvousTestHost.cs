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

using Microsoft.Extensions.Logging.Abstractions;
using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// A rendezvous manager using a controllable clock, bound to the loopback
    /// interface, for tests.
    /// </summary>
    internal sealed class RendezvousTestHost : IAsyncDisposable
    {

        #region Properties

        /// <summary>
        /// The configuration of this test host.
        /// </summary>
        public RendezvousOptions   Configuration    { get; }

        /// <summary>
        /// The controllable clock of this test host.
        /// </summary>
        public FakeTimeProvider    Time             { get; }

        /// <summary>
        /// The rendezvous manager under test.
        /// </summary>
        public RendezvousManager   Manager          { get; }

        /// <summary>
        /// The one and only rendezvous of this test host.
        /// </summary>
        public RendezvousSession   Session
            => Manager.Sessions.Single();

        #endregion

        #region Constructor(s)

        private RendezvousTestHost(RendezvousOptions  Configuration,
                                   FakeTimeProvider   Time,
                                   RendezvousManager  Manager)
        {

            this.Configuration  = Configuration;
            this.Time           = Time;
            this.Manager        = Manager;

        }

        #endregion


        #region (static) Create(Configure = null)

        /// <summary>
        /// Create a new test host.
        /// </summary>
        /// <param name="Configure">An optional delegate to change the default configuration.</param>
        public static RendezvousTestHost Create(Action<RendezvousOptions>? Configure = null)
        {

            var configuration = new RendezvousOptions {
                                    ControlAddress   = "127.0.0.1",
                                    ControlPort      = IPPort.Parse(0),
                                    DataAddress      = "127.0.0.1",

                                    // The tests drive the maintenance themselves,
                                    // so that the timeouts stay deterministic.
                                    AutoMaintenance  = false
                                };

            Configure?.Invoke(configuration);

            var errors = configuration.Validate();
            Assert.That(errors, Is.Empty, $"Invalid test configuration: {String.Join(" ", errors)}");

            var time = new FakeTimeProvider();

            return new RendezvousTestHost(
                       configuration,
                       time,
                       new RendezvousManager(
                           configuration,
                           time,
                           NullLoggerFactory.Instance
                       )
                   );

        }

        #endregion

        #region Execute(CommandLine, Authorization = null)

        /// <summary>
        /// Execute the given control command.
        /// </summary>
        /// <param name="CommandLine">A text representation of a control command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse Execute(String                 CommandLine,
                                       ControlAuthorization?  Authorization   = null)

            => Manager.Execute(CommandLine, Authorization);

        #endregion

        #region ExecuteOk(CommandLine, Authorization = null)

        /// <summary>
        /// Execute the given control command and expect it to succeed.
        /// </summary>
        /// <param name="CommandLine">A text representation of a control command.</param>
        /// <param name="Authorization">Who is asking, a trusted caller within the same process otherwise.</param>
        public CommandResponse ExecuteOk(String                 CommandLine,
                                         ControlAuthorization?  Authorization   = null)
        {

            var response = Manager.Execute(CommandLine, Authorization);
            Assert.That(response.IsSuccess, Is.True, $"'{CommandLine}' failed: {response.ToProtocolLine()}");

            return response;

        }

        #endregion


        #region DisposeAsync()

        public async ValueTask DisposeAsync()
        {
            await Manager.DisposeAsync();
        }

        #endregion

    }

}
