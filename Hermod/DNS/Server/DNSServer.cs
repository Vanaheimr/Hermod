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

using System.Buffers;
using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public delegate Task OnDNSServerStartedDelegate               (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSUDPUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSUDPMulticastListenerStartedDelegate (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   String             MCAddr,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSTCPUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSTLSUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSServerStoppedDelegate               (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   CancellationToken  CancellationToken);


    public delegate Task OnDNSRequestReceivedDelegate             (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   String             ServerType,
                                                                   DNSPacket          Request,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSResponseSentDelegate                (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   String             ServerType,
                                                                   DNSPacket          Response,
                                                                   CancellationToken  CancellationToken);


    public class DNSServer
    {

        #region Data

        private readonly         IDNSRequestHandler        requestHandler;
        private readonly         List<Task>                listenerTasks           = [];
        private readonly         ILogger<DNSServer>        logger;
        private readonly         ILoggerFactory            loggerFactory;

        private                  UdpClient?                udpUnicastListener;
        private                  UdpClient?                udpMulticastListener;
        private                  TcpListener?              tcpUnicastListener;
        private                  TcpListener?              tlsUnicastListener;

        private                  CancellationTokenSource?  cancellationTokenSource;

        #endregion

        #region Events

        public event OnDNSServerStartedDelegate?                OnDNSServerStarted;
        public event OnDNSUDPUnicastListenerStartedDelegate?    OnDNSUDPUnicastListenerStarted;
        public event OnDNSUDPMulticastListenerStartedDelegate?  OnDNSUDPMulticastListenerStarted;
        public event OnDNSTCPUnicastListenerStartedDelegate?    OnDNSTCPUnicastListenerStarted;
        public event OnDNSTLSUnicastListenerStartedDelegate?    OnDNSTLSUnicastListenerStarted;
        public event OnDNSServerStoppedDelegate?                OnDNSServerStopped;

        public event OnDNSRequestReceivedDelegate?              OnDNSRequestReceived;
        public event OnDNSResponseSentDelegate?                 OnDNSResponseSent;

        #endregion

        #region Properties

        public DNSServerOptions  Options                  { get; }

        public ILogger<DNSServer>  Logger                 => logger;

        public ILoggerFactory      LoggerFactory          => loggerFactory;

        public IPSocket?         ActiveUDPUnicastSocket   { get; private set; }

        public IPSocket?         ActiveUDPMulticastSocket { get; private set; }

        public IPSocket?         ActiveTCPUnicastSocket   { get; private set; }

        public IPSocket?         ActiveTLSUnicastSocket   { get; private set; }

        public Boolean           IsRunning
            => cancellationTokenSource is not null &&
              !cancellationTokenSource.IsCancellationRequested;

        #endregion

        #region Constructor(s)

        public DNSServer(IDNSRequestHandler?    RequestHandler   = null,
                         DNSServerOptions?      Options          = null,
                         ILogger<DNSServer>?    Logger           = null,
                         ILoggerFactory?        LoggerFactory    = null)
        {

            this.Options        = Options        ?? new DNSServerOptions();
            this.requestHandler = RequestHandler ?? new AuthoritativeDNSRequestHandler(
                                                        InMemoryDNSZone.CreateDemoZone()
                                                    );
            this.loggerFactory  = LoggerFactory  ?? NullLoggerFactory.Instance;
            this.logger         = Logger         ?? this.loggerFactory.CreateLogger<DNSServer>();

        }

        #endregion


        #region (private static) BuildFormatErrorResponse(RequestBytes)

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
        private static Byte[]? BuildFormatErrorResponse(Byte[] RequestBytes)
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

        #region (private) Serialize               (Response)

        private Byte[] Serialize(DNSPacket Response)
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

        #region (private) AcceptSignedRequest     (Buffer, out Message, out Context, out ErrorResponse)

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
        private Boolean AcceptSignedRequest(Byte[]                            Buffer,
                                            out Byte[]                        Message,
                                            out TransactionSecurityContext?   Context,
                                            out Byte[]?                       ErrorResponse)
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

        private Boolean AcceptTSIG(Byte[]                            Buffer,
                                   ref Byte[]                        Message,
                                   ref TransactionSecurityContext?   Context,
                                   ref Byte[]?                       ErrorResponse)
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
            Context  = new TransactionSecurityContext {
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
        private Boolean AcceptSIG0(Byte[]                            Buffer,
                                   ref Byte[]                        Message,
                                   ref TransactionSecurityContext?   Context,
                                   ref Byte[]?                       ErrorResponse)
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
            Context  = new TransactionSecurityContext {
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

        #region (private) SignIfRequested         (ResponseBytes, Context)

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
        private static Byte[] SignIfRequested(Byte[]                          ResponseBytes,
                                              TransactionSecurityContext?     Context)
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

        #region (private) TransactionSecurityContext

        /// <summary>
        /// What a verified request leaves behind for its response to be signed with.
        /// </summary>
        private sealed class TransactionSecurityContext
        {

            /// <summary>
            /// The TSIG key the request was signed with (RFC 8945).
            /// </summary>
            public TSIGKey?  TSIGKey        { get; init; }

            /// <summary>
            /// The MAC of that request, which the reply's MAC folds in.
            /// </summary>
            public Byte[]?   RequestMAC     { get; init; }

            /// <summary>
            /// The key to sign the reply with, when the request carried a SIG(0) and this server has one (RFC 2931).
            /// </summary>
            public SIG0Key?  SIG0Key        { get; init; }

            /// <summary>
            /// The request exactly as received, SIG(0) included — the "full query" of RFC 2931 §3.1.
            /// </summary>
            public Byte[]?   SignedRequest  { get; init; }

        }

        #endregion

        #region (private) SerializeForUDP         (Response, Request)

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
        private Byte[] SerializeForUDP(DNSResponse  Response,
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


        // ToDo: To well-known problems when listing on localhost IPv4+IPv6,
        //       we might need separate listeners for IPv4 and IPv6!

        #region (private) ListenUDPUnicastAsync   (CancellationToken token)

        private async Task ListenUDPUnicastAsync(CancellationToken CancellationToken)
        {

            var localSocket     = Options.UDPUnicastSocket;
            udpUnicastListener  = new UdpClient(localSocket.ToIPEndPoint());
            ActiveUDPUnicastSocket = IPSocket.FromIPEndPoint(udpUnicastListener.Client.LocalEndPoint) ?? localSocket;

            await LogEvent(
                      OnDNSUDPUnicastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          ActiveUDPUnicastSocket ?? localSocket,
                          CancellationToken
                      ),
                      nameof(OnDNSUDPUnicastListenerStarted)
                  );


            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {

                    var dnsPacket = await udpUnicastListener.ReceiveAsync(CancellationToken);

                    if (!AcceptSignedRequest(dnsPacket.Buffer, out var udpBody, out var tsigContext, out var tsigError))
                    {

                        if (tsigError is not null)
                            await udpUnicastListener.SendAsync(
                                      new ReadOnlyMemory<Byte>(tsigError),
                                      dnsPacket.RemoteEndPoint,
                                      CancellationToken
                                  );

                        continue;

                    }

                    DNSPacket dnsRequest;

                    try
                    {
                        dnsRequest = DNSPacket.Parse(
                                         ActiveUDPUnicastSocket ?? localSocket,
                                         IPSocket.FromIPEndPoint(dnsPacket.RemoteEndPoint),
                                         new MemoryStream(udpBody)
                                     );
                    }
                    catch (Exception parseException)
                    {

                        logger.LogDebug(
                            parseException,
                            "Could not parse a DNS request from {RemoteEndPoint}; answering FORMERR",
                            dnsPacket.RemoteEndPoint
                        );

                        var formatError = BuildFormatErrorResponse(dnsPacket.Buffer);

                        if (formatError is not null)
                            await udpUnicastListener.SendAsync(
                                      new ReadOnlyMemory<Byte>(formatError),
                                      dnsPacket.RemoteEndPoint,
                                      CancellationToken
                                  );

                        continue;

                    }

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            "UDP Unicast",
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);
                    if (dnsResponse is not null)
                    {

                        await udpUnicastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(SignIfRequested(SerializeForUDP(dnsResponse, dnsRequest), tsigContext)),
                                  dnsResponse.RemoteSocket.ToIPEndPoint(),
                                  CancellationToken
                              );

                        await LogEvent(
                                  OnDNSResponseSent,
                                  async loggingDelegate => await loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      this,
                                      "UDP Unicast",
                                      dnsResponse,
                                      CancellationToken
                                  ),
                                  nameof(OnDNSResponseSent)
                              );

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error within UDP unicast listener");
                }
            }

        }

        #endregion

        #region (private) ListenUDPMulticastAsync (CancellationToken token)

        private async Task ListenUDPMulticastAsync(CancellationToken CancellationToken)
        {

            var localSocket       = Options.UDPMulticastSocket;

            udpMulticastListener  = new UdpClient {
                                        ExclusiveAddressUse = false
                                    };
            udpMulticastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpMulticastListener.Client.Bind           (localSocket.ToIPEndPoint());

            ActiveUDPMulticastSocket = IPSocket.FromIPEndPoint(udpMulticastListener.Client.LocalEndPoint) ?? localSocket;

            var multicastAddress = System.Net.IPAddress.Parse(Options.MulticastGroupAddress);
            udpMulticastListener.JoinMulticastGroup(multicastAddress);

            await LogEvent(
                      OnDNSUDPMulticastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          ActiveUDPMulticastSocket ?? localSocket,
                          Options.MulticastGroupAddress,
                          CancellationToken
                      ),
                      nameof(OnDNSUDPMulticastListenerStarted)
                  );


            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {

                    var dnsPacket   = await udpMulticastListener.ReceiveAsync(CancellationToken);

                    var dnsRequest  = DNSPacket.Parse(
                                          ActiveUDPMulticastSocket ?? localSocket,
                                          IPSocket.FromIPEndPoint(dnsPacket.RemoteEndPoint),
                                          new MemoryStream(dnsPacket.Buffer)
                                      );

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            "UDP Multicast",
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);
                    if (dnsResponse is not null)
                    {

                        // Multicast response via unicast back to the sender!
                        await udpMulticastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(SerializeForUDP(dnsResponse, dnsRequest)),
                                  dnsResponse.RemoteSocket.ToIPEndPoint(),
                                  CancellationToken
                              );

                        await LogEvent(
                                  OnDNSResponseSent,
                                  async loggingDelegate => await loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      this,
                                      "UDP Multicast",
                                      dnsResponse,
                                      CancellationToken
                                  ),
                                  nameof(OnDNSResponseSent)
                              );

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error within UDP multicast listener");
                }
            }

            try
            {
                udpMulticastListener.DropMulticastGroup(multicastAddress);
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception e)
            {
                logger.LogError(e, "Error dropping multicast group");
            }

        }

        #endregion

        #region (private) ListenTCPUnicastAsync   (CancellationToken token)

        private async Task ListenTCPUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                var localSocket  = Options.TCPUnicastSocket;
                var tcpListener  = new TcpListener(localSocket.ToIPEndPoint());
                tcpUnicastListener = tcpListener;

                try
                {

                    tcpListener.Start(Options.TCPBacklog);
                    ActiveTCPUnicastSocket = IPSocket.FromIPEndPoint(tcpListener.LocalEndpoint) ?? localSocket;

                    await LogEvent(
                          OnDNSTCPUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              ActiveTCPUnicastSocket ?? localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSTCPUnicastListenerStarted)
                      );


                    while (!CancellationToken.IsCancellationRequested)
                    {
                        try
                        {

                            var tcpClient = await tcpListener.AcceptTcpClientAsync(CancellationToken);

                            logger.LogDebug(
                                "New TCP connection from {RemoteEndPoint} accepted on {LocalSocket}",
                                tcpClient.Client.RemoteEndPoint,
                                localSocket
                            );

                            _ = Task.Run(
                                    async () => await HandleTCPClientAsync(
                                                       tcpClient,
                                                       ActiveTCPUnicastSocket ?? localSocket,
                                                       CancellationToken
                                                   ).ConfigureAwait(false),
                                    CancellationToken
                                );

                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error accepting TCP client");
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error within TCP listener");
                }
                finally
                {
                    tcpListener.Stop();
                    if (ReferenceEquals(tcpUnicastListener, tcpListener))
                        tcpUnicastListener = null;
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting TCP listener");
            }

        }

        private async Task HandleTCPClientAsync(TcpClient          TCPClient,
                                                IPSocket           LocalSocket,
                                                CancellationToken  CancellationToken   = default)
        {
            try
            {

                using (TCPClient)
                {

                    var stream        = TCPClient.GetStream();
                    var remoteSocket  = IPSocket.FromIPEndPoint(TCPClient.Client.RemoteEndPoint) ?? IPSocket.Zero;

                    try
                    {

                        logger.LogDebug(
                            "New TCP connection from {RemoteEndPoint}",
                            TCPClient.Client.RemoteEndPoint
                        );

                        await HandleFramedDNSStreamAsync(
                                  stream,
                                  LocalSocket,
                                  remoteSocket,
                                  "TCP Unicast",
                                  CancellationToken
                              ).ConfigureAwait(false);

                    }
                    catch (OperationCanceledException)
                    { }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling TCP connection");
                    }
                    finally
                    {
                        stream.Close();
                    }

                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error handling TCP client");
            }

        }

        #endregion

        #region (private) ListenTLSUnicastAsync   (CancellationToken token)

        private async Task ListenTLSUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                if (Options.TLSServerCertificate is null)
                    throw new InvalidOperationException("A TLS server certificate is required for the DNS TLS listener.");

                var localSocket  = Options.TLSUnicastSocket;
                var tlsListener  = new TcpListener(localSocket.ToIPEndPoint());
                tlsUnicastListener = tlsListener;

                try
                {

                    tlsListener.Start(Options.TCPBacklog);
                    ActiveTLSUnicastSocket = IPSocket.FromIPEndPoint(tlsListener.LocalEndpoint) ?? localSocket;

                    await LogEvent(
                          OnDNSTLSUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              ActiveTLSUnicastSocket ?? localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSTLSUnicastListenerStarted)
                      );

                    while (!CancellationToken.IsCancellationRequested)
                    {
                        try
                        {

                            var tcpClient = await tlsListener.AcceptTcpClientAsync(CancellationToken);

                            logger.LogDebug(
                                "New TLS connection from {RemoteEndPoint} accepted on {LocalSocket}",
                                tcpClient.Client.RemoteEndPoint,
                                localSocket
                            );

                            _ = Task.Run(
                                    async () => await HandleTLSClientAsync(
                                                       tcpClient,
                                                       ActiveTLSUnicastSocket ?? localSocket,
                                                       CancellationToken
                                                   ).ConfigureAwait(false),
                                    CancellationToken
                                );

                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error accepting TLS client");
                        }
                    }

                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error within TLS listener");
                }
                finally
                {
                    tlsListener.Stop();
                    if (ReferenceEquals(tlsUnicastListener, tlsListener))
                        tlsUnicastListener = null;
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting TLS listener");
            }

        }

        private async Task HandleTLSClientAsync(TcpClient          TCPClient,
                                                IPSocket           LocalSocket,
                                                CancellationToken  CancellationToken = default)
        {
            try
            {

                using (TCPClient)
                {

                    var remoteSocket = IPSocket.FromIPEndPoint(TCPClient.Client.RemoteEndPoint) ?? IPSocket.Zero;

                    await using var sslStream = new SslStream(
                                                    TCPClient.GetStream(),
                                                    leaveInnerStreamOpen: false,
                                                    Options.TLSClientCertificateValidator
                                                );

                    var authenticationOptions = new SslServerAuthenticationOptions {
                        ServerCertificate              = Options.TLSServerCertificate,
                        ClientCertificateRequired       = Options.TLSClientCertificateRequired,
                        EnabledSslProtocols             = Options.TLSProtocols,
                        CertificateRevocationCheckMode  = Options.TLSCertificateRevocationCheckMode,
                        EncryptionPolicy                = EncryptionPolicy.RequireEncryption
                    };

                    await sslStream.AuthenticateAsServerAsync(
                                        authenticationOptions,
                                        CancellationToken
                                    ).ConfigureAwait(false);

                    await HandleFramedDNSStreamAsync(
                              sslStream,
                              LocalSocket,
                              remoteSocket,
                              "TLS Unicast",
                              CancellationToken
                          ).ConfigureAwait(false);

                }

            }
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                logger.LogError(e, "Error handling TLS client");
            }

        }

        #endregion

        #region (private) HandleFramedDNSStreamAsync(Stream, LocalSocket, RemoteSocket, ServerType, CancellationToken)

        private async Task HandleFramedDNSStreamAsync(Stream             Stream,
                                                      IPSocket          LocalSocket,
                                                      IPSocket          RemoteSocket,
                                                      String            ServerType,
                                                      CancellationToken CancellationToken)
        {

            var sharedBuffer = ArrayPool<Byte>.Shared.Rent(UInt16.MaxValue);

            try
            {

                while (!CancellationToken.IsCancellationRequested)
                {

                    var lengthBuffer  = new Byte[2];
                    var lengthBytes   = await ReadTCPBytesAsync(Stream, lengthBuffer, CancellationToken).
                                            ConfigureAwait(false);

                    if (lengthBytes == 0)
                        break;

                    if (lengthBytes != 2)
                        throw new EndOfStreamException("Incomplete DNS stream length prefix.");

                    var length        = (UInt16) ((lengthBuffer[0] << 8) | lengthBuffer[1]);
                    logger.LogDebug(
                        "Received {ServerType} DNS request with length {Length}",
                        ServerType,
                        length
                    );

                    if (length == 0)
                        continue;

                    if (length > sharedBuffer.Length)
                        throw new InvalidDataException($"DNS request length {length} exceeds the maximum message size.");

                    var bytesRead     = await ReadTCPBytesAsync(Stream, sharedBuffer.AsMemory(0, length), CancellationToken).
                                            ConfigureAwait(false);

                    if (bytesRead != length)
                        throw new EndOfStreamException("Incomplete DNS stream request payload.");

                    if (!AcceptSignedRequest(sharedBuffer[..bytesRead], out var streamBody, out var tsigContext, out var tsigError))
                    {

                        if (tsigError is not null)
                        {
                            await Stream.WriteAsync(new Byte[] { (Byte) (tsigError.Length >> 8), (Byte) tsigError.Length }, CancellationToken);
                            await Stream.WriteAsync(tsigError, CancellationToken);
                            await Stream.FlushAsync(CancellationToken);
                        }

                        continue;

                    }

                    var dnsRequest = DNSPacket.Parse(
                                         LocalSocket,
                                         RemoteSocket,
                                         new MemoryStream(streamBody)
                                     );

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            ServerType,
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);

                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      Options.UseCompression,
                            CompressionOffsets:  []
                        );

                        var responseBytes  = SignIfRequested(memoryStream.ToArray(), tsigContext);

                        Stream.WriteUInt16BE((UInt16) responseBytes.Length);

                        await Stream.WriteAsync(responseBytes, 0, responseBytes.Length, CancellationToken);
                        await Stream.FlushAsync(CancellationToken);

                        await LogEvent(
                            OnDNSResponseSent,
                            async loggingDelegate => await loggingDelegate.Invoke(
                                Timestamp.Now,
                                this,
                                ServerType,
                                dnsResponse,
                                CancellationToken
                            ),
                            nameof(OnDNSResponseSent)
                        );

                    }

                }

            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(sharedBuffer);
            }

        }

        #endregion

        #region (private) ReadTCPBytesAsync(Stream, Buffer, CancellationToken)

        private async Task<Int32> ReadTCPBytesAsync(Stream             Stream,
                                                    Memory<Byte>       Buffer,
                                                    CancellationToken  CancellationToken)
        {

            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            if (Options.TCPReadTimeout > TimeSpan.Zero)
                timeoutCancellationTokenSource.CancelAfter(Options.TCPReadTimeout);

            var bytesRead = 0;

            while (bytesRead < Buffer.Length)
            {

                var read = await Stream.ReadAsync(
                               Buffer[bytesRead..],
                               timeoutCancellationTokenSource.Token
                           ).ConfigureAwait(false);

                if (read == 0)
                    break;

                bytesRead += read;

            }

            return bytesRead;

        }

        #endregion


        private Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                     CancellationToken  CancellationToken = default)
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



        #region Start()

        public async Task Start()
        {

            if (IsRunning)
                return;

            if (Options.EnableTLSUnicast && Options.TLSServerCertificate is null)
                throw new InvalidOperationException("A TLS server certificate is required for the DNS TLS listener.");

            cancellationTokenSource = new CancellationTokenSource();
            listenerTasks.Clear();

            if (Options.EnableUDPUnicast)
                listenerTasks.Add(ListenUDPUnicastAsync(cancellationTokenSource.Token));

            if (Options.EnableUDPMulticast)
                listenerTasks.Add(ListenUDPMulticastAsync(cancellationTokenSource.Token));

            if (Options.EnableTCPUnicast)
                listenerTasks.Add(ListenTCPUnicastAsync(cancellationTokenSource.Token));

            if (Options.EnableTLSUnicast)
                listenerTasks.Add(ListenTLSUnicastAsync(cancellationTokenSource.Token));

            await LogEvent(
                      OnDNSServerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          cancellationTokenSource?.Token ?? CancellationToken.None
                      ),
                      nameof(OnDNSServerStarted)
                  );

        }

        #endregion

        #region Stop()

        public async Task Stop()
        {

            var cancellationTokenSource = this.cancellationTokenSource;
            if (cancellationTokenSource is null)
                return;

            await LogEvent(
                      OnDNSServerStopped,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          cancellationTokenSource.Token
                      ),
                      nameof(OnDNSServerStopped)
                  );

            cancellationTokenSource?.Cancel();

            udpUnicastListener?.  Dispose();
            udpMulticastListener?.Dispose();
            tcpUnicastListener?.  Stop();
            tlsUnicastListener?.  Stop();

            try
            {
                await Task.WhenAll(listenerTasks).
                           ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            finally
            {
                udpUnicastListener        = null;
                udpMulticastListener      = null;
                tcpUnicastListener        = null;
                tlsUnicastListener        = null;
                ActiveUDPUnicastSocket    = null;
                ActiveUDPMulticastSocket  = null;
                ActiveTCPUnicastSocket    = null;
                ActiveTLSUnicastSocket    = null;
                listenerTasks.Clear();

                cancellationTokenSource?.Dispose();
                this.cancellationTokenSource = null;
            }

        }

        #endregion



        #region (protected) LogEvent     (Module, Logger, LogHandler, ...)

        /// <remarks>
        /// <c>EventName</c> is passed on rather than left to the compiler a
        /// second time: down there the call site is this method, so
        /// <c>CallerArgumentExpression</c> would fill in "Logger" for every
        /// event there is.
        /// </remarks>
        protected Task LogEvent<TDelegate>(String                                             Module,
                                           TDelegate?                                         Logger,
                                           Func<TDelegate, Task>                              LogHandler,
                                           [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                           [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

            => Logger.InvokeAllAsync(
                   LogHandler,
                   (exception, eventName) => HandleErrors(Module, $"{Command}.{eventName}", exception),
                   EventName
               );

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ErrorResponse)

        public virtual Task HandleErrors(String  Module,
                                         String  Caller,
                                         String  ErrorResponse)
        {

            logger.LogError(
                "{Module}.{Caller}: {ErrorResponse}",
                Module,
                Caller,
                ErrorResponse
            );

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ExceptionOccurred)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccurred)
        {

            logger.LogError(
                ExceptionOccurred,
                "{Module}.{Caller}",
                Module,
                Caller
            );

            return Task.CompletedTask;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATCPServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(DNSServer)}: UDP/UC:{Options.UDPUnicastSocket}, UDP/MC:{Options.UDPMulticastSocket}, TCP:{Options.TCPUnicastSocket}, TLS:{Options.TLSUnicastSocket}";

        #endregion


    }

}
