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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The signed payload of a control response:
    ///
    ///     payload = {
    ///         1: int,              ; the response code, 0 = OK
    ///         2: tstr,             ; a human readable message
    ///       ? 3: [* uint],         ; the TCP ports of the rendezvous
    ///       ? 4: tstr,             ; the effective transfer profile
    ///       ? 5: bstr,             ; the nonce of the request this answers
    ///         6: uint,             ; seconds since the Unix epoch
    ///       ? 7: tstr,             ; the description of the rendezvous
    ///       ? 8: uint,             ; when the rendezvous was opened
    ///       ? 9: [* tstr]          ; the keys that opened the rendezvous
    ///     }
    ///
    /// The nonce of the request is echoed, so that a client can tell which
    /// request a response belongs to - and that a captured response can not
    /// be replayed as the answer to a different request.
    ///
    /// Keys 7 to 9 report back what the service recorded about a rendezvous:
    /// what it is for, when it was opened and who owns it.
    /// </summary>
    public sealed class ControlResponse
    {

        #region Data

        private const Int64 keyCode          = 1;
        private const Int64 keyMessage       = 2;
        private const Int64 keyPorts         = 3;
        private const Int64 keyProfile       = 4;
        private const Int64 keyRequestNonce  = 5;
        private const Int64 keyTimestamp     = 6;
        private const Int64 keyDescription   = 7;
        private const Int64 keyCreated       = 8;
        private const Int64 keyCreatedBy     = 9;

        private readonly Byte[]? requestNonce;

        #endregion

        #region Properties

        /// <summary>
        /// The response code.
        /// </summary>
        public ResponseCode           Code            { get; }

        /// <summary>
        /// A human readable message.
        /// </summary>
        public String                 Message         { get; }

        /// <summary>
        /// The TCP ports of the rendezvous, if any.
        /// </summary>
        public IReadOnlyList<IPPort>  Ports           { get; }

        /// <summary>
        /// The effective transfer profile, if any.
        /// </summary>
        public TransferProfile?       Profile         { get; }

        /// <summary>
        /// The nonce of the request this response answers, if it could be read.
        /// </summary>
        public Byte[]?                RequestNonce
            => requestNonce is null ? null : [.. requestNonce];

        /// <summary>
        /// When this response was created.
        /// </summary>
        public DateTimeOffset         Timestamp       { get; }

        /// <summary>
        /// The description of the rendezvous, if any.
        /// </summary>
        public String?                Description     { get; }

        /// <summary>
        /// When the rendezvous was opened, if known.
        /// </summary>
        public DateTimeOffset?        Created         { get; }

        /// <summary>
        /// The identifications of the keys that opened the rendezvous,
        /// and that may therefore close it again.
        /// </summary>
        public IReadOnlyList<String>  CreatedBy       { get; }

        /// <summary>
        /// Whether the command was executed successfully.
        /// </summary>
        public Boolean                IsSuccess
            => Code == ResponseCode.OK;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control response.
        /// </summary>
        /// <param name="Code">The response code.</param>
        /// <param name="Message">A human readable message.</param>
        /// <param name="Ports">The TCP ports of the rendezvous, if any.</param>
        /// <param name="Profile">The effective transfer profile, if any.</param>
        /// <param name="RequestNonce">The nonce of the request this response answers.</param>
        /// <param name="Timestamp">An optional timestamp, the current time is used otherwise.</param>
        /// <param name="Description">The description of the rendezvous, if any.</param>
        /// <param name="Created">When the rendezvous was opened, if known.</param>
        /// <param name="CreatedBy">The identifications of the keys that opened the rendezvous.</param>
        public ControlResponse(ResponseCode           Code,
                               String                 Message,
                               IEnumerable<IPPort>?   Ports          = null,
                               TransferProfile?       Profile        = null,
                               Byte[]?                RequestNonce   = null,
                               DateTimeOffset?        Timestamp      = null,
                               String?                Description    = null,
                               DateTimeOffset?        Created        = null,
                               IEnumerable<String>?   CreatedBy      = null)
        {

            this.Code          = Code;
            this.Message       = Message ?? String.Empty;
            this.Ports         = Ports is null ? [] : [.. Ports];
            this.Profile       = Profile;
            this.requestNonce  = RequestNonce is null ? null : [.. RequestNonce];

            this.Timestamp     = DateTimeOffset.FromUnixTimeSeconds(
                                     (Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
                                 );

            this.Description   = Description;

            this.Created       = Created.HasValue
                                     ? DateTimeOffset.FromUnixTimeSeconds(Created.Value.ToUnixTimeSeconds())
                                     : null;

            this.CreatedBy     = CreatedBy is null ? [] : [.. CreatedBy];

        }

        #endregion


        #region ToCBOR() / ToByteArray()

        /// <summary>
        /// Return the CBOR representation of this control response.
        /// </summary>
        public CBORValue ToCBOR()
        {

            var entries = new List<KeyValuePair<CBORValue, CBORValue>> {
                new (CBORValue.FromInt64(keyCode),     CBORValue.FromInt64((Int64) Code)),
                new (CBORValue.FromInt64(keyMessage),  CBORValue.FromText (Message))
            };

            if (Ports.Count > 0)
                entries.Add(new (CBORValue.FromInt64(keyPorts),
                                 CBORValue.FromArray(Ports.Select(port => CBORValue.FromInt64(port.ToUInt16())))));

            if (Profile.HasValue)
                entries.Add(new (CBORValue.FromInt64(keyProfile),
                                 CBORValue.FromText (Profile.Value.AsText())));

            if (requestNonce is not null)
                entries.Add(new (CBORValue.FromInt64(keyRequestNonce),
                                 CBORValue.FromBytes(requestNonce)));

            entries.Add(new (CBORValue.FromInt64(keyTimestamp),
                             CBORValue.FromInt64(Timestamp.ToUnixTimeSeconds())));

            if (Description is not null)
                entries.Add(new (CBORValue.FromInt64(keyDescription),
                                 CBORValue.FromText (Description)));

            if (Created.HasValue)
                entries.Add(new (CBORValue.FromInt64(keyCreated),
                                 CBORValue.FromInt64(Created.Value.ToUnixTimeSeconds())));

            if (CreatedBy.Count > 0)
                entries.Add(new (CBORValue.FromInt64(keyCreatedBy),
                                 CBORValue.FromArray(CreatedBy.Select(CBORValue.FromText))));

            return CBORValue.FromMap(entries);

        }

        /// <summary>
        /// Return the deterministically encoded CBOR data of this control response.
        /// These very bytes are what gets signed and verified.
        /// </summary>
        public Byte[] ToByteArray()

            => ToCBOR().ToByteArray(CBORWriterOptions.Canonical);

        #endregion

        #region (static) From(CommandResponse, Session = null, RequestNonce = null, Timestamp = null)

        /// <summary>
        /// Create a control response from the result of a control command.
        /// </summary>
        /// <param name="CommandResponse">The result of a control command.</param>
        /// <param name="Session">The rendezvous the command was about, if any.</param>
        /// <param name="RequestNonce">The nonce of the request this response answers.</param>
        /// <param name="Timestamp">An optional timestamp.</param>
        public static ControlResponse From(CommandResponse     CommandResponse,
                                           RendezvousSession?  Session        = null,
                                           Byte[]?             RequestNonce   = null,
                                           DateTimeOffset?     Timestamp      = null)

            => new (CommandResponse.Code,
                    CommandResponse.Text,
                    Session?.Ports,
                    Session?.Profile,
                    RequestNonce,
                    Timestamp,
                    Session?.Description,
                    Session?.CreatedUtc,
                    Session?.CreatedBy);

        #endregion

        #region (static) TryParse(Bytes, out Response, out ErrorResponse)

        /// <summary>
        /// Try to parse the given CBOR data as a control response.
        /// </summary>
        /// <param name="Bytes">The CBOR data to be parsed.</param>
        /// <param name="Response">The parsed control response.</param>
        /// <param name="ErrorResponse">An error response, when parsing failed.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                         Bytes,
                                       [NotNullWhen(true)]  out ControlResponse?  Response,
                                       [NotNullWhen(false)] out String?           ErrorResponse)
        {

            Response = null;

            if (!CBORValue.TryParse(Bytes, out var cbor, out ErrorResponse))
                return false;

            if (cbor.Kind != CBORValueKind.Map)
            {
                ErrorResponse = "A control response must be a CBOR map!";
                return false;
            }

            if (!cbor.TryGetValue(CBORValue.FromInt64(keyCode), out var codeCBOR) ||
                codeCBOR.Kind is not (CBORValueKind.UnsignedInteger or CBORValueKind.NegativeInteger))
            {
                ErrorResponse = "A control response must contain its response code!";
                return false;
            }

            if (!cbor.ParseMandatoryText(keyMessage, "message", out var message, out ErrorResponse))
                return false;

            var ports = new List<IPPort>();

            if (cbor.TryGetValue(CBORValue.FromInt64(keyPorts), out var portsCBOR) &&
                portsCBOR.Kind == CBORValueKind.Array)
            {
                foreach (var port in portsCBOR.AsArray())
                {

                    if (port.Kind != CBORValueKind.UnsignedInteger || port.AsUInt64() > UInt16.MaxValue)
                    {
                        ErrorResponse = "Invalid TCP port within the control response!";
                        return false;
                    }

                    ports.Add(IPPort.Parse((UInt16) port.AsUInt64()));

                }
            }

            TransferProfile? profile = null;

            if (cbor.TryGetValue(CBORValue.FromInt64(keyProfile), out var profileCBOR) &&
                profileCBOR.Kind == CBORValueKind.TextString &&
                TransferProfileExtensions.TryParse(profileCBOR.AsText(), out var parsedProfile))
            {
                profile = parsedProfile;
            }

            Byte[]? requestNonce = null;

            if (cbor.TryGetValue(CBORValue.FromInt64(keyRequestNonce), out var nonceCBOR) &&
                nonceCBOR.Kind == CBORValueKind.ByteString)
            {
                requestNonce = nonceCBOR.AsBytes();
            }

            if (!cbor.ParseMandatoryUInt64(keyTimestamp, "timestamp", out var unixTimestamp, out ErrorResponse))
                return false;

            if (unixTimestamp > (UInt64) DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                ErrorResponse = "The timestamp is out of range!";
                return false;
            }

            String? description = null;

            if (cbor.TryGetValue(CBORValue.FromInt64(keyDescription), out var descriptionCBOR) &&
                descriptionCBOR.Kind == CBORValueKind.TextString)
            {
                description = descriptionCBOR.AsText();
            }

            DateTimeOffset? created = null;

            if (cbor.TryGetValue(CBORValue.FromInt64(keyCreated), out var createdCBOR) &&
                createdCBOR.Kind == CBORValueKind.UnsignedInteger &&
                createdCBOR.AsUInt64() <= (UInt64) DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                created = DateTimeOffset.FromUnixTimeSeconds((Int64) createdCBOR.AsUInt64());
            }

            var createdBy = new List<String>();

            if (cbor.TryGetValue(CBORValue.FromInt64(keyCreatedBy), out var createdByCBOR) &&
                createdByCBOR.Kind == CBORValueKind.Array)
            {
                foreach (var keyId in createdByCBOR.AsArray())
                {
                    if (keyId.Kind == CBORValueKind.TextString)
                        createdBy.Add(keyId.AsText());
                }
            }

            Response       = new ControlResponse(
                                 (ResponseCode) codeCBOR.AsInt64(),
                                 message,
                                 ports,
                                 profile,
                                 requestNonce,
                                 DateTimeOffset.FromUnixTimeSeconds((Int64) unixTimestamp),
                                 description,
                                 created,
                                 createdBy
                             );

            ErrorResponse  = null;
            return true;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this control response.
        /// </summary>
        public override String ToString()

            => $"{Code}{(Ports.Count > 0 ? $" [{String.Join(", ", Ports)}]" : "")}{(Profile.HasValue ? $", {Profile.Value.AsText()}" : "")}: {Message}";

        #endregion

    }

}
