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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The configuration of the rendezvous service.
    /// </summary>
    public sealed class RendezvousOptions
    {

        #region Data

        /// <summary>
        /// The name of the configuration section.
        /// </summary>
        public const String SectionName = "Rendezvous";

        #endregion

        #region Properties

        /// <summary>
        /// The IP address the control endpoint is listening on.
        /// Defaults to the loopback interface, as the control protocol allows
        /// everyone who can reach it to open TCP listeners on this machine.
        /// </summary>
        public String            ControlAddress          { get; set; } = "127.0.0.1";

        /// <summary>
        /// The TCP port of the control endpoint.
        /// A zero asks the operating system for a free port, which is mostly
        /// useful for tests. The chosen port is written to the log.
        /// </summary>
        public IPPort            ControlPort             { get; set; } = IPPort.Parse(8500);

        /// <summary>
        /// The IP address the rendezvous listeners are bound to.
        /// </summary>
        public String            DataAddress             { get; set; } = "0.0.0.0";

        /// <summary>
        /// A rendezvous where not all clients have arrived within this time span
        /// is closed and its listeners are removed.
        /// </summary>
        public TimeSpan          RendezvousTimeout       { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// An established rendezvous without any payload within this time span
        /// is closed and its listeners are removed.
        /// </summary>
        public TimeSpan          IdleTimeout             { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// How often the service looks for timed out rendezvous.
        /// </summary>
        public TimeSpan          MaintenanceInterval     { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Whether a rendezvous manager looks after the timeouts itself.
        /// Disable this to drive the maintenance from somewhere else, e.g. from
        /// an own scheduler, and call RendezvousManager.Sweep() instead.
        /// </summary>
        public Boolean           AutoMaintenance         { get; set; } = true;

        /// <summary>
        /// A control connection without any command within this time span is closed.
        /// </summary>
        public TimeSpan          ControlIdleTimeout      { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// The lowest TCP port that may be used for a rendezvous.
        /// </summary>
        public IPPort            MinDataPort             { get; set; } = IPPort.Parse( 1024);

        /// <summary>
        /// The highest TCP port that may be used for a rendezvous.
        /// </summary>
        public IPPort            MaxDataPort             { get; set; } = IPPort.Parse(65535);

        /// <summary>
        /// The maximum number of concurrent rendezvous.
        /// </summary>
        public Int32             MaxSessions             { get; set; } = 256;

        /// <summary>
        /// The maximum number of ports (and therefore clients) per rendezvous.
        /// </summary>
        public Int32             MaxPortsPerSession      { get; set; } = 16;

        /// <summary>
        /// The maximum number of concurrent control connections.
        /// </summary>
        public Int32             MaxControlConnections   { get; set; } = 64;

        /// <summary>
        /// The maximum length of a single control command in bytes.
        /// </summary>
        public Int32             MaxFrameLength          { get; set; } = 65536;

        /// <summary>
        /// How many *distinct* valid signatures a control request must carry.
        /// Two is the usual hybrid stance during the post-quantum migration:
        /// one Ed25519 or Ed448 signature and one ML-DSA signature, so that a
        /// break of either family alone does not authorize anything.
        /// </summary>
        public Int32             RequiredSignatures      { get; set; } = 1;

        /// <summary>
        /// How far the timestamp of a control request may differ from the clock
        /// of this service. It also decides how long a nonce has to be remembered,
        /// therefore a large value costs memory.
        /// </summary>
        public TimeSpan          MaxClockSkew            { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The transfer profile used when a ConnectPorts command does not request one.
        /// </summary>
        public TransferProfile   DefaultProfile          { get; set; } = TransferProfile.Balanced;

        /// <summary>
        /// The buffer sizes and TCP parameters of the transfer profiles.
        /// </summary>
        public TransferProfilesOptions  Profiles         { get; set; } = new();

        /// <summary>
        /// The public keys that may authorize control commands.
        /// Without at least one key the control endpoint rejects everything.
        /// </summary>
        public List<ControlKeyOptions>  ControlKeys      { get; set; } = [];

        #endregion


        #region ControlIPAddress

        /// <summary>
        /// The parsed IP address of the control endpoint.
        /// </summary>
        public IIPAddress ControlIPAddress
            => IPAddress.Parse(ControlAddress);

        #endregion

        #region DataIPAddress

        /// <summary>
        /// The parsed IP address of the rendezvous listeners.
        /// </summary>
        public IIPAddress DataIPAddress
            => IPAddress.Parse(DataAddress);

        #endregion

        #region IsAllowedDataPort(Port)

        /// <summary>
        /// Whether the given TCP port may be used for a rendezvous.
        /// The control port is always excluded, as the rendezvous listeners
        /// may be bound to a wildcard address overlapping the control endpoint.
        /// </summary>
        /// <param name="Port">A TCP port.</param>
        public Boolean IsAllowedDataPort(IPPort Port)

            => Port >= MinDataPort &&
               Port <= MaxDataPort &&
               Port != ControlPort;

        #endregion

        #region Validate()

        /// <summary>
        /// Validate this configuration and return a human readable error message
        /// for every invalid value.
        /// </summary>
        public IReadOnlyList<String> Validate()
        {

            var errors = new List<String>();

            if (!IPAddress.TryParse(ControlAddress, out _))
                errors.Add($"{SectionName}.{nameof(ControlAddress)} is not a valid IP address: '{ControlAddress}'!");

            if (!IPAddress.TryParse(DataAddress,    out _))
                errors.Add($"{SectionName}.{nameof(DataAddress)} is not a valid IP address: '{DataAddress}'!");

            if (RendezvousTimeout    <= TimeSpan.Zero)
                errors.Add($"{SectionName}.{nameof(RendezvousTimeout)} must be positive, but is {RendezvousTimeout}!");

            if (IdleTimeout          <= TimeSpan.Zero)
                errors.Add($"{SectionName}.{nameof(IdleTimeout)} must be positive, but is {IdleTimeout}!");

            if (MaintenanceInterval  <= TimeSpan.Zero)
                errors.Add($"{SectionName}.{nameof(MaintenanceInterval)} must be positive, but is {MaintenanceInterval}!");

            if (ControlIdleTimeout   <= TimeSpan.Zero)
                errors.Add($"{SectionName}.{nameof(ControlIdleTimeout)} must be positive, but is {ControlIdleTimeout}!");

            if (MinDataPort.IsZero)
                errors.Add($"{SectionName}.{nameof(MinDataPort)} must not be zero!");

            if (MinDataPort > MaxDataPort)
                errors.Add($"{SectionName}.{nameof(MinDataPort)} ({MinDataPort}) must not be greater than {nameof(MaxDataPort)} ({MaxDataPort})!");

            if (MaxSessions           < 1)
                errors.Add($"{SectionName}.{nameof(MaxSessions)} must be at least 1, but is {MaxSessions}!");

            if (MaxPortsPerSession    < 2)
                errors.Add($"{SectionName}.{nameof(MaxPortsPerSession)} must be at least 2, but is {MaxPortsPerSession}!");

            if (MaxControlConnections < 1)
                errors.Add($"{SectionName}.{nameof(MaxControlConnections)} must be at least 1, but is {MaxControlConnections}!");

            // Two ML-DSA-87 signatures alone are more than 9 KByte.
            if (MaxFrameLength        < 16384)
                errors.Add($"{SectionName}.{nameof(MaxFrameLength)} must be at least 16384 bytes, but is {MaxFrameLength}!");

            if (RequiredSignatures    < 1 || RequiredSignatures > SignedMessage.MaxSignatures)
                errors.Add($"{SectionName}.{nameof(RequiredSignatures)} must be 1..{SignedMessage.MaxSignatures}, but is {RequiredSignatures}!");

            if (MaxClockSkew         <= TimeSpan.Zero)
                errors.Add($"{SectionName}.{nameof(MaxClockSkew)} must be positive, but is {MaxClockSkew}!");

            for (var i = 0; i < ControlKeys.Count; i++)
                errors.AddRange(ControlKeys[i].Validate($"{SectionName}.{nameof(ControlKeys)}[{i}]"));

            if (ControlKeys.Select(key => key.Id).Distinct().Count() != ControlKeys.Count)
                errors.Add($"{SectionName}.{nameof(ControlKeys)} contains duplicate key identifications!");

            errors.AddRange(Profiles.Balanced.   Validate($"{SectionName}.{nameof(Profiles)}.{nameof(TransferProfilesOptions.Balanced)}"));
            errors.AddRange(Profiles.Interactive.Validate($"{SectionName}.{nameof(Profiles)}.{nameof(TransferProfilesOptions.Interactive)}"));
            errors.AddRange(Profiles.Bulk.       Validate($"{SectionName}.{nameof(Profiles)}.{nameof(TransferProfilesOptions.Bulk)}"));

            return errors;

        }

        #endregion

    }

}
