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

using System.Buffers.Binary;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Everything a DNS server does to a message between the socket and the zone,
    /// with no opinion about which socket it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verify and strip a transaction signature, hand what is left to the request
    /// handler, then serialize the answer — padded if the question asked to be
    /// padded, signed if the question was signed, shortened if the transport is a
    /// datagram. None of that depends on the transport, and every transport needs
    /// all of it, which is why it lives here rather than in any one listener.
    /// </para>
    /// <para>
    /// <see cref="DNSServer"/> uses it for UDP, TCP and DoT;
    /// <see cref="DNSOverHTTPSServer"/> uses it for the body of an RFC 8484
    /// request. The point is not to save the lines, it is that TSIG verification
    /// has one implementation: a transport added later cannot quietly acquire a
    /// second, subtly different, idea of what a valid signature is.
    /// </para>
    /// </remarks>
    public class DNSMessagePipeline
    {

        #region Data

        private readonly IDNSRequestHandler  requestHandler;
        private readonly ILogger             logger;

        #endregion

        #region Properties

        /// <summary>
        /// The options this pipeline reads its keys, block sizes and limits from.
        /// </summary>
        public DNSServerOptions    Options         { get; }

        /// <summary>
        /// Whatever answers the questions — a zone, a resolver, a stub.
        /// </summary>
        public IDNSRequestHandler  RequestHandler
            => requestHandler;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the transport-independent half of a DNS server.
        /// </summary>
        /// <param name="RequestHandler">Whatever answers the questions. Defaults to the demo zone, exactly as <see cref="DNSServer"/> does.</param>
        /// <param name="Options">The keys, block sizes and limits to apply.</param>
        /// <param name="Logger">Where to report a refused signature or a truncated answer.</param>
        public DNSMessagePipeline(IDNSRequestHandler?  RequestHandler   = null,
                                  DNSServerOptions?    Options          = null,
                                  ILogger?             Logger           = null)
        {

            this.Options         = Options        ?? new DNSServerOptions();
            this.requestHandler  = RequestHandler ?? new AuthoritativeDNSRequestHandler(
                                                         InMemoryDNSZone.CreateDemoZone()
                                                     );
            this.logger          = Logger         ?? NullLogger.Instance;

        }

        #endregion


        #region ProcessRequest           (Request, CancellationToken = default)

        /// <summary>
        /// Put a parsed request to the request handler.
        /// </summary>
        /// <param name="Request">The request, with any transaction signature already verified and removed.</param>
        /// <param name="CancellationToken">A token to cancel this request.</param>
        /// <returns>The response to send, or null to say nothing at all.</returns>
        public Task<DNSResponse?> ProcessRequest(DNSPacket          Request,
                                                 CancellationToken  CancellationToken   = default)
        {

            Request.Questions.ForEachCounted((question, i) => {
                logger.LogDebug(
                    "Question {QuestionIndex}: Name={DomainName}, Type={QueryType}, Class={QueryClass}",
                    i,
                    question.DomainName,
                    question.QueryType,
                    question.QueryClass
                );
            });

            return requestHandler.ProcessDNSRequest(
                       Request,
                       CancellationToken
                   );

        }

        #endregion

        #region (static) BuildFormatErrorResponse(RequestBytes)

        /// <summary>
        /// Build a minimal FORMERR reply for a request that could not be parsed,
        /// or null when even that is not possible.
        /// </summary>
        /// <remarks>
        /// RFC 1035 §4.1.1 defines RCODE 1 as "the name server was unable to
        /// interpret the query". Only the 12-byte header is needed: the
        /// transaction id is echoed from the first two octets, which stay
        /// readable however mangled the rest of the message is. Answering lets a
        /// client tell "malformed request" from "server unreachable" instead of
        /// retrying blindly.
        /// </remarks>
        public static Byte[]? BuildFormatErrorResponse(Byte[] RequestBytes)
        {

            // Too short to even echo a transaction id — stay silent.
            if (RequestBytes.Length < 12)
                return null;

            // Never answer something that is itself a response: two servers
            // exchanging error replies would loop.
            if ((RequestBytes[2] & 0x80) != 0)
                return null;

            var opcode = (Byte) ((RequestBytes[2] >> 3) & 0x0F);

            return [

                       RequestBytes[0],                          // transaction id, echoed
                       RequestBytes[1],

                       (Byte) (0x80 |                            // QR = 1
                               (opcode << 3) |                   // opcode, echoed
                               (RequestBytes[2] & 0x01)),        // RD, echoed

                       (Byte) DNSResponseCodes.FormatError,      // RCODE = 1

                       // The question could not be parsed, so no section is echoed.
                       0, 0,                                     // QDCOUNT
                       0, 0,                                     // ANCOUNT
                       0, 0,                                     // NSCOUNT
                       0, 0                                      // ARCOUNT

                   ];

        }

        #endregion

        #region Serialize               (Response)

        public Byte[] Serialize(DNSPacket Response)
        {

            var memoryStream = new MemoryStream();

            Response.Serialize(
                memoryStream,
                UseCompression:      Options.UseCompression,
                CompressionOffsets:  []
            );

            return memoryStream.ToArray();

        }

        #endregion

        #region SerializeMessageResponse (Response, Request, Context, HonorRequestorPayloadSize = true)

        /// <summary>
        /// Serialize a response for a transport that delivers whole messages —
        /// a length-prefixed stream or an HTTP body: signed if the request was
        /// signed, and padded if the request asked to be padded.
        /// </summary>
        /// <param name="Response">The response to put on the wire.</param>
        /// <param name="Request">The request that prompted it, which decides both.</param>
        /// <param name="Context">What the verified request left behind, or null to leave the response unsigned.</param>
        /// <param name="HonorRequestorPayloadSize">
        /// Whether the requestor's EDNS(0) payload size caps the padded reply, per
        /// RFC 7830 §4. True on every transport that carries a datagram somewhere
        /// underneath. False over DoH, where RFC 8484 §6 says the opposite in as
        /// many words: "DoH servers using this media type MUST ignore the value
        /// given for the EDNS UDP payload size in DNS requests." There is no
        /// datagram to overflow, so the number describes nothing and must not be
        /// allowed to shorten the padding.
        /// </param>
        /// <remarks>
        /// <para>
        /// RFC 7830 §4 leaves the responder no say in the matter: "Responders MUST
        /// pad DNS responses when the respective DNS query included the 'Padding'
        /// option, unless doing so would violate the maximum UDP payload size."
        /// What is up to the responder is how much — RFC 8467 §4.1 recommends a
        /// multiple of 468 octets, and that is a SHOULD.
        /// </para>
        /// <para>
        /// How much padding a message needs depends on how long the message already
        /// is, and that is only known once it has been serialized. So this
        /// serializes twice. The trial run carries an empty Padding option, which
        /// means the four octets of option header are already inside the length
        /// that comes back and cannot be left out of the arithmetic by mistake.
        /// </para>
        /// <para>
        /// The measurement is taken after signing rather than before. What an
        /// observer sees is the finished message, TSIG or SIG(0) record included,
        /// and that is the length which has to land on a block boundary; padding
        /// the message underneath a signature of some other length would leave the
        /// observable length as revealing as before. Both RFCs are silent on the
        /// combination. A transaction signature is a fixed size for a given key and
        /// algorithm, so the trial run costs one extra signature and reports the
        /// length the real one will have.
        /// </para>
        /// <para>
        /// A response with no OPT record of its own is sent as it is. There is
        /// nowhere in it for the option to live, and conjuring an OPT record would
        /// change what the response says about its own EDNS(0) support in order to
        /// pad it.
        /// </para>
        /// </remarks>
        public Byte[] SerializeMessageResponse(DNSPacket                       Response,
                                               DNSPacket                       Request,
                                               DNSTransactionSecurityContext?  Context,
                                               Boolean                         HonorRequestorPayloadSize   = true)
        {

            if (!DNSPadding.IsPadded(Request) ||
                !DNSPadding.HasEDNS (Response))
            {
                return SignIfRequested(Serialize(Response), Context);
            }

            var trial   = SignIfRequested(Serialize(DNSPadding.WithPadding(Response, 0)), Context);

            var octets  = DNSPadding.OctetsFor(
                              trial.Length,
                              DNSPadding.ResponseBlockSize,
                              HonorRequestorPayloadSize
                                  ? DNSPadding.PayloadSizeOf(Request)
                                  : null
                          );

            // Already on a boundary, or held there by the requestor's payload size:
            // the trial run is the answer, and its empty Padding option is an
            // honest statement of how many octets were added.
            return octets == 0
                       ? trial
                       : SignIfRequested(Serialize(DNSPadding.WithPadding(Response, octets)), Context);

        }

        #endregion

        #region AcceptSignedRequest     (Buffer, out Message, out Context, out ErrorResponse)

        /// <summary>
        /// Verify and remove a transaction signature — TSIG or SIG(0) — before
        /// the message is parsed.
        /// </summary>
        /// <param name="Buffer">The datagram or framed message as received.</param>
        /// <param name="Message">What the rest of the server should parse: the request with its signature removed and ARCOUNT corrected.</param>
        /// <param name="Context">What is needed to sign the reply, or null when the request was unsigned.</param>
        /// <param name="ErrorResponse">A ready-to-send refusal when verification failed, otherwise null.</param>
        /// <returns>False when the request must not be served.</returns>
        /// <remarks>
        /// Stripping before parsing rather than after is deliberate. It keeps the
        /// signed bytes exactly as they arrived — which is what the signature
        /// covers — and it means <c>DNSPacket.Parse</c> never meets a meta-RR it
        /// has no case for.
        /// </remarks>
        public Boolean AcceptSignedRequest(Byte[]                              Buffer,
                                           out Byte[]                          Message,
                                           out DNSTransactionSecurityContext?  Context,
                                           out Byte[]?                         ErrorResponse)
        {

            Message        = Buffer;
            Context        = null;
            ErrorResponse  = null;

            // RFC 2931 §3.2: "Requests and responses can either have a single
            // TSIG or one SIG(0) but not both." A message carrying both is
            // malformed rather than unauthentic, so it gets FORMERR — the RFC
            // names no RCODE for this, and answering "your message is wrong" is
            // more useful than "you are not authorized".
            if (SIG0Signer.CarriesBothTSIGAndSIG0(Buffer))
            {

                logger.LogWarning("Rejecting a request that carries both a TSIG and a SIG(0)");

                ErrorResponse = BuildFormatErrorResponse(Buffer);

                return false;

            }

            return AcceptTSIG(Buffer, ref Message, ref Context, ref ErrorResponse) &&
                   AcceptSIG0(Buffer, ref Message, ref Context, ref ErrorResponse);

        }

        #endregion

        #region (private) AcceptTSIG              (Buffer, ref Message, ref Context, ref ErrorResponse)

        private Boolean AcceptTSIG(Byte[]                              Buffer,
                                   ref Byte[]                          Message,
                                   ref DNSTransactionSecurityContext?  Context,
                                   ref Byte[]?                         ErrorResponse)
        {

            var keys = Options.TSIGKeys.ToArray();

            if (keys.Length == 0)
                return true;

            if (!TSIGSigner.TryStripTSIG(Buffer, out var unsigned, out var tsig) ||
                unsigned is null || tsig is null)
                return true;                    // unsigned request: serve it as before

            var result = TSIGSigner.Verify(Buffer, keys);

            if (!result.IsValid)
            {

                logger.LogWarning(
                    "Rejecting a TSIG-signed request under key {KeyName}: {Reason}",
                    tsig.DomainName,
                    result.Description
                );

                // §5.2: answer NOTAUTH and report why in the TSIG error field, so
                // the sender can tell a wrong key from a wrong clock. Silence
                // would leave it retrying forever.
                var key        = keys.FirstOrDefault(k => k.Name.FullName.TrimEnd('.').
                                                            Equals(tsig.DomainName.ToString().TrimEnd('.'),
                                                                   StringComparison.OrdinalIgnoreCase));

                ErrorResponse  = TSIGSigner.BuildErrorResponse(
                                     Buffer,
                                     result.Error,
                                     result.Error == TSIGSigner.BADTIME ? key : null
                                 );

                return false;

            }

            Message  = unsigned;
            Context  = new DNSTransactionSecurityContext {
                           TSIGKey     = keys.First(k => k.Name.FullName.TrimEnd('.').
                                                           Equals(tsig.DomainName.ToString().TrimEnd('.'),
                                                                  StringComparison.OrdinalIgnoreCase)),
                           RequestMAC  = result.MAC!
                       };

            return true;

        }

        #endregion

        #region (private) AcceptSIG0              (Buffer, ref Message, ref Context, ref ErrorResponse)

        /// <summary>
        /// Verify and remove a SIG(0), RFC 2931 §3.
        /// </summary>
        /// <remarks>
        /// Nothing happens without configured keys. §3.2 has a server that does
        /// not implement request SIGs "ignore them without error where they are
        /// optional", and for an ordinary query they are optional — so an
        /// unconfigured server serves a signed request exactly as it serves an
        /// unsigned one, rather than refusing what it cannot check.
        /// </remarks>
        private Boolean AcceptSIG0(Byte[]                              Buffer,
                                   ref Byte[]                          Message,
                                   ref DNSTransactionSecurityContext?  Context,
                                   ref Byte[]?                         ErrorResponse)
        {

            var keys = Options.SIG0Keys.ToArray();

            if (keys.Length == 0)
                return true;

            if (!SIG0Signer.TryStripSIG0(Buffer, out var unsigned, out var sig) ||
                unsigned is null || sig is null || !sig.IsTransactionSignature)
                return true;                    // unsigned request: serve it as before

            var result = SIG0Signer.Verify(Buffer, keys);

            if (!result.IsValid)
            {

                logger.LogWarning(
                    "Rejecting a SIG(0)-signed request from {SignerName} (key tag {KeyTag}): {Reason}",
                    sig.SignerName,
                    sig.KeyTag,
                    result.Description
                );

                // RFC 2931 names no RCODE for a failed request signature — §3.1
                // only says a server is "not required to check" one. Having
                // chosen to check, NOTAUTH is the answer that says why, and it
                // is what TSIG uses for the same situation (RFC 8945 §5.2).
                //
                // The refusal is unsigned. There is nothing to gain by signing
                // it: a sender whose key we just rejected cannot tell our
                // signature apart from anyone else's, and signing costs the
                // public-key operation §2.4 warns about spending on unverified
                // input.
                ErrorResponse = BuildNotAuthorizedResponse(unsigned);

                return false;

            }

            Message  = unsigned;
            Context  = new DNSTransactionSecurityContext {
                           SIG0Key        = Options.SIG0ResponseKey,
                           SignedRequest  = Buffer
                       };

            return true;

        }

        #endregion

        #region (private) BuildNotAuthorizedResponse(Request)

        /// <summary>
        /// The bare NOTAUTH reply for a request whose signature did not verify:
        /// the header and question as they arrived, QR set, and no records.
        /// </summary>
        private static Byte[]? BuildNotAuthorizedResponse(Byte[] Request)
        {

            if (Request.Length < 12)
                return null;

            var response = new Byte[Request.Length];
            Buffer.BlockCopy(Request, 0, response, 0, Request.Length);

            var flags    = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(2, 2));
            flags        = (UInt16) ((flags | 0x8000) & 0xFFF0 | (UInt16) DNSResponseCodes.NotAuthorized);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), flags);

            // No answers of any kind travel with an authentication failure.
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(6,  2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(8,  2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(10, 2), 0);

            // …and neither does anything the request happened to carry after its
            // question, which the counts above have just disowned.
            var end = FindEndOfQuestions(response);

            return end < 0
                       ? null
                       : response[..end];

        }


        /// <summary>
        /// The offset just past the last question, or -1 if they do not parse.
        /// </summary>
        private static Int32 FindEndOfQuestions(Byte[] Message)
        {

            try
            {

                using var stream = new MemoryStream(Message);
                stream.Position  = 12;

                var qdCount = BinaryPrimitives.ReadUInt16BigEndian(Message.AsSpan(4, 2));

                for (var i = 0; i < qdCount; i++)
                {
                    DNSTools.ExtractName(stream);
                    stream.Position += 4;                  // QTYPE + QCLASS
                }

                return stream.Position <= Message.Length
                           ? (Int32) stream.Position
                           : -1;

            }
            catch
            {
                return -1;
            }

        }

        #endregion

        #region (static) SignIfRequested  (ResponseBytes, Context)

        /// <summary>
        /// Sign a response when the request that prompted it was signed.
        /// </summary>
        /// <param name="ResponseBytes">The serialized response.</param>
        /// <param name="Context">What the verified request left behind, or null to leave the response unsigned.</param>
        /// <remarks>
        /// Both mechanisms bind the reply to the question it answers, and both do
        /// it by folding the request into the signature — TSIG through the
        /// request's MAC (RFC 8945 §4.3.1), SIG(0) through the whole query
        /// (RFC 2931 §3.1). Either way a signed response lifted from one exchange
        /// cannot be replayed as the answer to another.
        /// </remarks>
        public static Byte[] SignIfRequested(Byte[]                          ResponseBytes,
                                             DNSTransactionSecurityContext?  Context)
        {

            if (Context is null)
                return ResponseBytes;

            if (Context.TSIGKey is not null)
                return TSIGSigner.Sign(ResponseBytes,
                                       Context.TSIGKey,
                                       RequestMAC: Context.RequestMAC);

            if (Context.SIG0Key is not null)
                return SIG0Signer.Sign(ResponseBytes,
                                       Context.SIG0Key,
                                       Request: Context.SignedRequest);

            return ResponseBytes;

        }

        #endregion

        #region SerializeDatagramResponse (Response, Request)

        /// <summary>
        /// Serialize a response for transmission over UDP, truncating it when it
        /// does not fit into the requestor's buffer.
        /// </summary>
        /// <remarks>
        /// RFC 1035 §4.2.1: "Messages carried by UDP are restricted to 512 bytes.
        /// Longer messages are truncated and the TC bit is set in the header."
        /// RFC 6891 §6.2.3 raises that ceiling to whatever the requestor advertises
        /// in its OPT record (values below 512 are treated as 512), and §6.2.5
        /// forbids exceeding it. Answer records are shed until the message fits;
        /// the OPT record is kept so the response stays EDNS-conformant.
        /// </remarks>
        public Byte[] SerializeDatagramResponse(DNSResponse  Response,
                                                DNSPacket    Request)
        {

            var full = Serialize(Response);

            var requestOPT  = Request.AdditionalRRs.OfType<OPT>().FirstOrDefault();

            var limit       = requestOPT is not null
                                  ? Math.Min(Math.Max(requestOPT.UDPPayloadSize, (UInt16) 512), Options.MaxUDPResponseSize)
                                  : 512;

            if (full.Length <= limit)
                return full;

            var answers      = Response.AnswerRRs.ToArray();
            var responseOPT  = Response.AdditionalRRs.OfType<OPT>().Cast<IDNSResourceRecord>().ToArray();

            // Drop answer records from the end until the message fits. Authority
            // and additional sections go entirely, except for the OPT record.
            for (var count = answers.Length - 1; count >= 0; count--)
            {

                var truncated = new DNSResponse(
                                    Request:               Request,
                                    TransactionId:         Response.TransactionId,
                                    QueryOrResponse:       DNSQueryResponse.Response,
                                    Opcode:                Response.Opcode,
                                    AuthoritativeAnswer:   Response.AuthoritativeAnswer,
                                    Truncation:            true,
                                    RecursionDesired:      Response.RecursionDesired,
                                    RecursionAvailable:    Response.RecursionAvailable,
                                    ResponseCode:          Response.ResponseCode,
                                    Questions:             Response.Questions,
                                    AnswerRRs:             answers.Take(count).ToArray(),
                                    AuthorityRRs:          [],
                                    AdditionalRRs:         responseOPT
                                );

                var bytes = Serialize(truncated);

                if (bytes.Length <= limit)
                {

                    logger.LogDebug(
                        "Truncating a {FullLength}-byte UDP response to {TruncatedLength} bytes ({KeptAnswers} of {TotalAnswers} answers, limit {Limit})",
                        full.Length,
                        bytes.Length,
                        count,
                        answers.Length,
                        limit
                    );

                    return bytes;

                }

            }

            // Even the bare header + question exceeds the limit: send it anyway,
            // flagged truncated, so the client knows to retry over TCP.
            return Serialize(
                       new DNSResponse(
                           Request:               Request,
                           TransactionId:         Response.TransactionId,
                           QueryOrResponse:       DNSQueryResponse.Response,
                           Opcode:                Response.Opcode,
                           AuthoritativeAnswer:   Response.AuthoritativeAnswer,
                           Truncation:            true,
                           RecursionDesired:      Response.RecursionDesired,
                           RecursionAvailable:    Response.RecursionAvailable,
                           ResponseCode:          Response.ResponseCode,
                           Questions:             Response.Questions,
                           AnswerRRs:             [],
                           AuthorityRRs:          [],
                           AdditionalRRs:         []
                       )
                   );

        }

        #endregion

    }

}
