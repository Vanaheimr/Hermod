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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The signed payload of a rendezvous control command: the command itself,
    /// plus a nonce and a timestamp that make a captured request useless to
    /// anybody who tries to replay it.
    ///
    /// On the wire this is a CBOR map with integer keys (COSE style):
    ///
    ///     payload = {
    ///         1: tstr,             ; "ConnectPorts" | "DisconnectPorts"
    ///         2: [+ port],         ; port = uint / null      (null = '?')
    ///       ? 3: tstr,             ; a description of this rendezvous
    ///       ? 4: tstr,             ; the transfer profile
    ///         5: bstr,             ; nonce, 8..64 bytes
    ///         6: uint,             ; seconds since the Unix epoch
    ///       ? 7: bool              ; echo everything back to its sender
    ///     }
    ///
    /// </summary>
    public sealed class ControlRequest
    {

        #region Data

        /// <summary>
        /// The minimum length of a nonce in bytes.
        /// </summary>
        public const Int32 MinNonceLength      =  8;

        /// <summary>
        /// The maximum length of a nonce in bytes.
        /// </summary>
        public const Int32 MaxNonceLength      = 64;

        /// <summary>
        /// The length of a generated nonce in bytes.
        /// </summary>
        public const Int32 DefaultNonceLength  = 16;

        private const Int64 keyCommand      = 1;
        private const Int64 keyPorts        = 2;
        private const Int64 keyDescription  = 3;
        private const Int64 keyProfile      = 4;
        private const Int64 keyNonce        = 5;
        private const Int64 keyTimestamp    = 6;
        private const Int64 keyEcho         = 7;

        private readonly Byte[] nonce;

        #endregion

        #region Properties

        /// <summary>
        /// The control command.
        /// </summary>
        public RendezvousCommand  Command      { get; }

        /// <summary>
        /// A random nonce, making every request unique.
        /// </summary>
        public Byte[]             Nonce
            => [.. nonce];

        /// <summary>
        /// When this request was created.
        /// </summary>
        public DateTimeOffset     Timestamp    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control request.
        /// </summary>
        /// <param name="Command">A control command.</param>
        /// <param name="Nonce">An optional nonce, a random one is generated otherwise.</param>
        /// <param name="Timestamp">An optional timestamp, the current time is used otherwise.</param>
        public ControlRequest(RendezvousCommand  Command,
                              Byte[]?            Nonce       = null,
                              DateTimeOffset?    Timestamp   = null)
        {

            ArgumentNullException.ThrowIfNull(Command);

            if (Nonce is not null && (Nonce.Length < MinNonceLength || Nonce.Length > MaxNonceLength))
                throw new ArgumentException(
                          $"A nonce must be {MinNonceLength}..{MaxNonceLength} bytes long, but is {Nonce.Length}!",
                          nameof(Nonce)
                      );

            this.Command    = Command;
            this.nonce      = Nonce     ?? RandomNumberGenerator.GetBytes(DefaultNonceLength);

            // Whole seconds only: the timestamp travels as an integer, therefore
            // a parsed request must compare equal to the one that was signed.
            this.Timestamp  = DateTimeOffset.FromUnixTimeSeconds(
                                  (Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
                              );

        }

        #endregion


        #region ToCBOR()

        /// <summary>
        /// Return the CBOR representation of this control request.
        /// </summary>
        public CBORValue ToCBOR()
        {

            var entries = new List<KeyValuePair<CBORValue, CBORValue>> {

                new (CBORValue.FromInt64(keyCommand),
                     CBORValue.FromText (Command.CommandName)),

                new (CBORValue.FromInt64(keyPorts),
                     CBORValue.FromArray(PortsOf(Command)))

            };

            if (Command.Description is not null)
                entries.Add(new (CBORValue.FromInt64(keyDescription),
                                 CBORValue.FromText (Command.Description)));

            if (Command is ConnectPortsCommand { Profile: not null } connectPorts)
                entries.Add(new (CBORValue.FromInt64(keyProfile),
                                 CBORValue.FromText (connectPorts.Profile.Value.AsText())));

            entries.Add(new (CBORValue.FromInt64(keyNonce),
                             CBORValue.FromBytes(nonce)));

            entries.Add(new (CBORValue.FromInt64(keyTimestamp),
                             CBORValue.FromInt64(Timestamp.ToUnixTimeSeconds())));

            // Only sent when asked for: the default travels as the absence of the key.
            if (Command is ConnectPortsCommand { EchoToSender: true })
                entries.Add(new (CBORValue.FromInt64  (keyEcho),
                                 CBORValue.FromBoolean(true)));

            return CBORValue.FromMap(entries);

        }

        #endregion

        #region ToByteArray()

        /// <summary>
        /// Return the deterministically encoded CBOR data of this control request.
        /// These very bytes are what gets signed and verified.
        /// </summary>
        public Byte[] ToByteArray()

            => ToCBOR().ToByteArray(CBORWriterOptions.Canonical);

        #endregion

        #region (private, static) PortsOf(Command)

        private static IEnumerable<CBORValue> PortsOf(RendezvousCommand Command)
        {

            if (Command is ConnectPortsCommand connectPorts)
                return connectPorts.Ports.Select(
                           port => port.Port.HasValue
                                       ? CBORValue.FromInt64(port.Port.Value.ToUInt16())
                                       : CBORValue.FromSimpleValue(CBORSimpleValue.Null)
                       );

            if (Command is DisconnectPortsCommand disconnectPorts)
                return disconnectPorts.Ports.Select(
                           port => CBORValue.FromInt64(port.ToUInt16())
                       );

            return [];

        }

        #endregion


        #region (static) TryParse(CBOR,  out Request, out ErrorResponse)

        /// <summary>
        /// Try to parse the given CBOR value as a control request.
        /// </summary>
        /// <param name="CBOR">The CBOR value to be parsed.</param>
        /// <param name="Request">The parsed control request.</param>
        /// <param name="ErrorResponse">An error response, when parsing failed.</param>
        public static Boolean TryParse(CBORValue                             CBOR,
                                       [NotNullWhen(true)]  out ControlRequest?  Request,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            Request = null;

            if (CBOR.Kind != CBORValueKind.Map)
            {
                ErrorResponse = "The control request must be a CBOR map!";
                return false;
            }

            #region Command name

            if (!CBOR.ParseMandatoryText(keyCommand, "command name", out var commandName, out ErrorResponse))
                return false;

            #endregion

            #region Ports

            if (!CBOR.TryGetValue(CBORValue.FromInt64(keyPorts), out var portsCBOR) ||
                portsCBOR.Kind != CBORValueKind.Array)
            {
                ErrorResponse = "The control request must contain an array of TCP ports!";
                return false;
            }

            var ports = portsCBOR.AsArray();

            #endregion

            #region Description

            String? description = null;

            if (CBOR.TryGetValue(CBORValue.FromInt64(keyDescription), out var descriptionCBOR))
            {

                if (descriptionCBOR.Kind != CBORValueKind.TextString)
                {
                    ErrorResponse = "The description of a control request must be a text string!";
                    return false;
                }

                description = descriptionCBOR.AsText();

                if (description.Length > RendezvousCommand.MaxDescriptionLength)
                {
                    ErrorResponse = $"The description must not be longer than {RendezvousCommand.MaxDescriptionLength} characters!";
                    return false;
                }

            }

            #endregion

            #region Nonce and timestamp

            if (!CBOR.ParseMandatoryBytes(keyNonce, "nonce", out var nonce, out ErrorResponse))
                return false;

            if (nonce.Length < MinNonceLength || nonce.Length > MaxNonceLength)
            {
                ErrorResponse = $"The nonce must be {MinNonceLength}..{MaxNonceLength} bytes long, but is {nonce.Length}!";
                return false;
            }

            if (!CBOR.ParseMandatoryUInt64(keyTimestamp, "timestamp", out var unixTimestamp, out ErrorResponse))
                return false;

            if (unixTimestamp > (UInt64) DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                ErrorResponse = "The timestamp is out of range!";
                return false;
            }

            var timestamp = DateTimeOffset.FromUnixTimeSeconds((Int64) unixTimestamp);

            #endregion

            #region ConnectPorts

            if (commandName.Equals(ConnectPortsCommand.Name, StringComparison.OrdinalIgnoreCase))
            {

                var portSpecifications = new List<PortSpecification>(ports.Count);

                foreach (var port in ports)
                {

                    if (port.Kind == CBORValueKind.Null)
                    {
                        portSpecifications.Add(PortSpecification.Random);
                        continue;
                    }

                    if (!TryParsePort(port, out var tcpPort, out ErrorResponse))
                        return false;

                    if (portSpecifications.Any(portSpecification => portSpecification.Port == tcpPort))
                    {
                        ErrorResponse = $"Duplicate TCP port {tcpPort}!";
                        return false;
                    }

                    portSpecifications.Add(PortSpecification.Fixed(tcpPort.Value));

                }

                if (portSpecifications.Count < CommandParser.MinPortsPerSession)
                {
                    ErrorResponse = $"{ConnectPortsCommand.Name} requires at least {CommandParser.MinPortsPerSession} ports, but got {portSpecifications.Count}!";
                    return false;
                }

                TransferProfile? profile = null;

                if (CBOR.TryGetValue(CBORValue.FromInt64(keyProfile), out var profileCBOR) &&
                    profileCBOR.Kind == CBORValueKind.TextString)
                {

                    if (!TransferProfileExtensions.TryParse(profileCBOR.AsText(), out var parsedProfile))
                    {
                        ErrorResponse = $"Unknown transfer profile '{profileCBOR.AsText()}'!";
                        return false;
                    }

                    profile = parsedProfile;

                }

                var echoToSender = false;

                if (CBOR.TryGetValue(CBORValue.FromInt64(keyEcho), out var echoCBOR))
                {

                    if (echoCBOR.Kind != CBORValueKind.Boolean)
                    {
                        ErrorResponse = "The echo flag of a control request must be a boolean!";
                        return false;
                    }

                    echoToSender = echoCBOR.AsBoolean();

                }

                Request        = new ControlRequest(
                                     new ConnectPortsCommand(portSpecifications, profile, description, echoToSender),
                                     nonce,
                                     timestamp
                                 );

                ErrorResponse  = null;
                return true;

            }

            #endregion

            #region DisconnectPorts

            if (commandName.Equals(DisconnectPortsCommand.Name, StringComparison.OrdinalIgnoreCase))
            {

                var tcpPorts = new List<IPPort>(ports.Count);

                foreach (var port in ports)
                {

                    if (!TryParsePort(port, out var tcpPort, out ErrorResponse))
                        return false;

                    if (tcpPorts.Contains(tcpPort.Value))
                    {
                        ErrorResponse = $"Duplicate TCP port {tcpPort}!";
                        return false;
                    }

                    tcpPorts.Add(tcpPort.Value);

                }

                if (tcpPorts.Count == 0)
                {
                    ErrorResponse = $"{DisconnectPortsCommand.Name} requires at least one TCP port!";
                    return false;
                }

                Request        = new ControlRequest(
                                     new DisconnectPortsCommand(tcpPorts, description),
                                     nonce,
                                     timestamp
                                 );

                ErrorResponse  = null;
                return true;

            }

            #endregion

            ErrorResponse = $"Unknown command '{commandName}', expected '{ConnectPortsCommand.Name}' or '{DisconnectPortsCommand.Name}'!";
            return false;

        }

        #endregion

        #region (static) TryParse(Bytes, out Request, out ErrorResponse)

        /// <summary>
        /// Try to parse the given CBOR data as a control request.
        /// </summary>
        /// <param name="Bytes">The CBOR data to be parsed.</param>
        /// <param name="Request">The parsed control request.</param>
        /// <param name="ErrorResponse">An error response, when parsing failed.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                        Bytes,
                                       [NotNullWhen(true)]  out ControlRequest?  Request,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            Request = null;

            if (!CBORValue.TryParse(Bytes, out var cbor, out ErrorResponse))
                return false;

            return TryParse(cbor, out Request, out ErrorResponse);

        }

        #endregion

        #region (private, static) TryParsePort(CBOR, out Port, out ErrorResponse)

        private static Boolean TryParsePort(CBORValue                         CBOR,
                                            [NotNullWhen(true)] out IPPort?   Port,
                                            [NotNullWhen(false)] out String?  ErrorResponse)
        {

            Port = null;

            if (CBOR.Kind != CBORValueKind.UnsignedInteger)
            {
                ErrorResponse = "A TCP port must be an unsigned integer!";
                return false;
            }

            var number = CBOR.AsUInt64();

            if (number == 0 || number > UInt16.MaxValue)
            {
                ErrorResponse = $"Invalid TCP port {number}, expected a number between 1 and 65535!";
                return false;
            }

            Port           = IPPort.Parse((UInt16) number);
            ErrorResponse  = null;

            return true;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this control request.
        /// </summary>
        public override String ToString()

            => $"{Command} @ {Timestamp:u}";

        #endregion

    }

}
