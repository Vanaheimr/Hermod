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

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client for a single DNS server over SSH: the byte stream of DNS over TCP in a channel of
    /// an SSH connection, as the subsystem "dns@open.charging.cloud" of the white paper "DNS over SSH".
    /// Keeps the channel open across queries, and pipelines concurrent queries on it
    /// (RFC 7766 §6.2.1.1, RFC 7858 §3.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SSH connection is either this client's own - a host, a port and an SSH login, dialled at the
    /// first query and closed when the client is disposed of - or one it is handed: a connection that
    /// others share (an <see cref="SshSharedConnection"/>, mostly), on which this client opens its
    /// channel and nothing else. It never closes a connection it was handed.
    /// </para>
    /// <para>
    /// What ends is the channel, not the connection (section 3.5 of the white paper): a server closes a
    /// channel that was idle for too long, and the next query opens a new one - on the same connection
    /// while that lasts, on a new one otherwise. A query whose channel broke under it is asked once
    /// more on the next channel, as <see cref="DNSTCPClient"/> and <see cref="DNSTLSClient"/> ask again
    /// on the next connection.
    /// </para>
    /// <para>
    /// It is not an <see cref="IDNSClientWithTransport"/>: that interface speaks of the local and
    /// remote end points of a socket of the client's own, a URL, and connect, send and receive
    /// timeouts. A channel of a connection somebody else made has none of them to tell, and a client
    /// with a connection of its own has them in its <see cref="SshClientOptions"/>.
    /// </para>
    /// </remarks>
    public sealed class DNSSSHClient : IDNSClientWithEDNS
    {

        #region (class) ChannelStream

        /// <summary>
        /// The channel as the stream <see cref="DNSStreamPipeline"/> reads: whatever ends a read or a
        /// write other than a cancellation is an <see cref="IOException"/>.
        /// </summary>
        /// <remarks>
        /// The pipeline, written for sockets, takes an IOException for a connection that broke - the
        /// queries on it may be asked again on the next one - and everything else for the failure of
        /// the query that happened to be reading. A channel whose SSH connection went fails its reads
        /// with whatever ended the connection: an <see cref="SshWireException"/>, mostly.
        /// </remarks>
        private sealed class ChannelStream(SshMuxChannel Channel) : Stream
        {

            private readonly Stream   input = Channel.Input;

            /// <summary>
            /// The end was read. It is told again to whoever reads on: the channel's own stream
            /// tells it once, and fails the read after it.
            /// </summary>
            private volatile Boolean  ended;

            public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                if (ended)
                    return 0;

                try
                {

                    var read = await input.ReadAsync(Buffer, CancellationToken).ConfigureAwait(false);

                    if (read == 0 && Buffer.Length > 0)
                        ended = true;

                    return read;

                }
                catch (Exception e) when (e is not OperationCanceledException and not IOException and not ObjectDisposedException)
                {
                    throw new IOException("The channel of DNS over SSH ended.", e);
                }
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                try
                {
                    await Channel.SendDataAsync(Buffer, CancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException and not IOException)
                {
                    throw new IOException("The channel of DNS over SSH ended.", e);
                }
            }

            public override Task<Int32> ReadAsync (Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)  => ReadAsync (Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();
            public override Task        WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)  => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();
            public override Int32       Read      (Byte[] Buffer, Int32 Offset, Int32 Count)                                       => ReadAsync (Buffer.AsMemory(Offset, Count)).AsTask().GetAwaiter().GetResult();
            public override void        Write     (Byte[] Buffer, Int32 Offset, Int32 Count)                                       => WriteAsync(Buffer.AsMemory(Offset, Count)).AsTask().GetAwaiter().GetResult();

            public override Boolean CanRead   => true;
            public override Boolean CanWrite  => true;
            public override Boolean CanSeek   => false;
            public override Int64   Length    => throw new NotSupportedException();
            public override Int64   Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void  Flush()                                          { }
            public override Task  FlushAsync(CancellationToken CancellationToken)  => Task.CompletedTask;
            public override Int64 Seek(Int64 Offset, SeekOrigin Origin)            => throw new NotSupportedException();
            public override void  SetLength(Int64 Value)                           => throw new NotSupportedException();

        }

        #endregion

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan DefaultQueryTimeout = TimeSpan.FromSeconds(23.5);

        /// <summary>
        /// The name of the SSH subsystem of DNS over SSH.
        /// </summary>
        public const String DefaultSubsystem = DNSServer.SSHSubsystemName;

        /// <summary>
        /// Held to get a connection, to open the channel and to write one query to it - one writer at
        /// a time, as the channel does not keep two writers' octets apart - and no longer.
        /// </summary>
        private readonly SemaphoreSlim streamLock = new(1, 1);

        /// <summary>
        /// RFC 7828 §3.2.2 for this channel: what the server last advertised, and the clock that ends
        /// the channel - not the SSH connection - before that timeout expires.
        /// </summary>
        private readonly DNSKeepalivePolicy keepalive;

        /// <summary>
        /// Set by DisposeAsync, so that a query asked afterwards is refused rather than let in to
        /// open a channel for a client that is going away.
        /// </summary>
        private volatile Boolean disposed;

        private Int32 disposing;

        /// <summary>
        /// Cancelled by DisposeAsync: ends what this client's queries wait for.
        /// </summary>
        private readonly CancellationTokenSource lifetime = new();

        /// <summary>
        /// The queries outstanding on this client's channel, and whose turn it is to read it.
        /// </summary>
        private readonly DNSStreamPipeline pipeline = new();

        /// <summary>
        /// Counts this client's channels, so that a query knows which one it was written to and is
        /// never handed a stream it was not written on. What <see cref="DNSTCPClient"/> counts as its
        /// connections.
        /// </summary>
        private Int32 connection;

        /// <summary>
        /// A channel by its number, the SSH connection it is on, and its stream.
        /// </summary>
        private sealed record Opened(Int32 Number, SshClient Ssh, SshMuxChannel Channel, Stream Stream);

        /// <summary>
        /// The channel last opened, as one object: a reader asks for a stream or a close by number,
        /// and gets that number's own - see <see cref="DNSTCPClient"/>.
        /// </summary>
        private Opened? opened;

        /// <summary>
        /// The login of a connection of this client's own; null where it is handed its connection.
        /// </summary>
        private readonly SshClientOptions? sshOptions;

        /// <summary>
        /// Hands this client its connection; null where it dials one of its own.
        /// </summary>
        private readonly Func<CancellationToken, ValueTask<SshClient>>? connectionOf;

        /// <summary>
        /// The SSH connection last made or handed - which may have ended since.
        /// </summary>
        private volatile SshClient? sshConnection;

        /// <summary>
        /// Where an SSH connection went to.
        /// </summary>
        private sealed record Remote(IIPAddress IPAddress, IPPort Port);

        /// <summary>
        /// Where the SSH connection last made or handed went to, where that could be told. One
        /// object, as it is read by queries that hold no lock.
        /// </summary>
        private volatile Remote? lastRemote;

        private Int64 channelsOpened;
        private Int64 connectionsMade;

        private readonly ILogger<DNSSSHClient> logger;

        #endregion

        #region Properties

        /// <summary>
        /// The host name or address of the SSH server of a connection of this client's own; null where
        /// the client is handed its connection.
        /// </summary>
        public String?           Host                      { get; }

        /// <summary>
        /// The port of the SSH server of a connection of this client's own; null where the client is
        /// handed its connection.
        /// </summary>
        public IPPort?           Port                      { get; }

        /// <summary>
        /// The SSH subsystem asked for: "dns@open.charging.cloud", unless told otherwise.
        /// </summary>
        public String            Subsystem                 { get; }

        /// <summary>
        /// Whether the SSH connection is this client's own: dialled by it, and closed with it. False
        /// for a connection it is handed, of which it opens and closes its channel only.
        /// </summary>
        public Boolean           OwnsConnection
            => connectionOf is null;

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?          RecursionDesired          { get; set; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan          QueryTimeout              { get; set; }

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query.
        /// </summary>
        public List<EDNSOption>  EDNSOptions               { get; } = [];

        /// <inheritdoc />
        public Boolean           DnssecOK                  { get; set; }

        /// <summary>
        /// How this client signs its queries and checks the replies — TSIG (RFC 8945) or SIG(0)
        /// (RFC 2931). Unsigned by default.
        /// </summary>
        /// <remarks>
        /// A transaction signature covers the DNS message, so it is indifferent to what carries it -
        /// see <see cref="DNSTLSClient.TransactionSecurity"/>. Over SSH the server knows the client by
        /// its login already; a signature is for a server that asks for one all the same, e.g. for a
        /// dynamic update.
        /// </remarks>
        public DNSTransactionSecurity  TransactionSecurity { get; set; } = DNSTransactionSecurity.None;

        /// <summary>
        /// The EDNS(0) payload size this client advertises (RFC 6891 §6.2.3). Set to 0 to send no OPT
        /// record at all, which announces no EDNS(0) support and rules out padding in either
        /// direction.
        /// </summary>
        /// <remarks>
        /// The number means nothing in a channel - an answer is limited by its two-octet length only,
        /// as over TCP (section 3.4 of the white paper). What it decides here is whether there is an
        /// OPT record: the only place <see cref="DnssecOK"/>, <see cref="EDNSOptions"/> and the
        /// padding can travel.
        /// </remarks>
        public UInt16            UDPPayloadSize            { get; set; } = DNSPacket.DefaultUDPPayloadSize;

        /// <summary>
        /// The block length queries are padded to, or 0 to send them unpadded.
        /// </summary>
        /// <remarks>
        /// RFC 8467 §4.1: "Clients SHOULD pad queries to the closest multiple of 128 octets", where
        /// "the DNS transport is encrypted". SSH hides the content of a message and not its length
        /// (section 3.4 of the white paper), so the recommendation applies here as it does to DNS over
        /// TLS, and padding is on by default.
        /// </remarks>
        public UInt16            PaddingBlockSize          { get; set; } = DNSPadding.QueryBlockSize;

        /// <summary>
        /// The idle timeout the server advertised in the last EDNS TCP Keepalive response option on
        /// the current channel (RFC 7828). Null if none was received on it.
        /// </summary>
        /// <remarks>
        /// It applies to the channel (section 3.4 of the white paper): this client closes its channel
        /// before the timeout expires, as RFC 7828 §3.2.2 asks, and leaves the SSH connection open.
        /// </remarks>
        public TimeSpan?         ServerKeepaliveTimeout
            => keepalive.ServerTimeout;

        /// <summary>
        /// The SSH connection the channel is on, while that connection lasts; null where none was made
        /// or handed yet, or the last one ended.
        /// </summary>
        /// <remarks>
        /// It stays this client's, or whose it is. Whoever opens a further channel on it closes that
        /// channel, never the connection.
        /// </remarks>
        public SshClient?        Connection
            => sshConnection is SshClient ssh && !ssh.Closed.IsCompleted
                   ? ssh
                   : null;

        /// <summary>
        /// How many channels this client opened so far: one, as long as the first one lasts.
        /// </summary>
        public Int64             ChannelsOpened
            => Interlocked.Read(ref channelsOpened);

        /// <summary>
        /// How many SSH connections this client dialled, or was handed, so far: one, as long as the
        /// first one lasts - however many channels were opened on it.
        /// </summary>
        public Int64             ConnectionsMade
            => Interlocked.Read(ref connectionsMade);

        /// <summary>
        /// Why the last attempt to get a channel failed; null once one was opened, and before the
        /// first attempt.
        /// </summary>
        /// <remarks>
        /// A query that could not be sent comes back as a failed <see cref="DNSInfo"/>, as from every
        /// DNS client - which tells that it failed, and that it did not time out, but not why. This
        /// tells why, by the type of the exception: an <see cref="SshSubsystemRefusedException"/> where
        /// the server answered SSH_MSG_CHANNEL_FAILURE - it does not offer the subsystem to this login,
        /// and asking again would not change it; an <see cref="SshAuthenticationException"/> where it
        /// did not accept the credentials; whatever the handshake threw where the host key was not the
        /// one expected. <see cref="ConnectAsync"/> throws the same exceptions instead of keeping them.
        /// </remarks>
        public Exception?        LastConnectionError       { get; private set; }

        #endregion

        #region Constructor(s)

        #region DNSSSHClient(Host, SSHOptions, Port = null, ...)

        /// <summary>
        /// Create a new DNS client for the given DNS server over an SSH connection of its own, made at
        /// the first query and closed when this client is disposed of.
        /// </summary>
        /// <param name="Host">The host name or address of the SSH server.</param>
        /// <param name="SSHOptions">The SSH login: user name, credentials, and how the server's host key is checked - there is no default for that, the caller pins the key or names the CA.</param>
        /// <param name="Port">The port of the SSH server; 22 by default.</param>
        /// <param name="Subsystem">The SSH subsystem; "dns@open.charging.cloud" by default.</param>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public DNSSSHClient(String            Host,
                            SshClientOptions  SSHOptions,
                            IPPort?           Port               = null,
                            String?           Subsystem          = null,
                            Boolean?          RecursionDesired   = null,
                            TimeSpan?         QueryTimeout       = null,
                            ILoggerFactory?   LoggerFactory      = null)

            : this(Subsystem,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory)

        {

            ArgumentException.ThrowIfNullOrWhiteSpace(Host);
            ArgumentNullException.ThrowIfNull(SSHOptions);

            this.Host        = Host;
            this.Port        = Port ?? IPPort.SSH;
            this.sshOptions  = SSHOptions;

        }

        #endregion

        #region DNSSSHClient(Connection, ...)

        /// <summary>
        /// Create a new DNS client for the DNS server behind an SSH connection it is handed: one that
        /// others share, e.g. the connection of a charging station to its Local Controller, which
        /// carries OCPP in another channel.
        /// </summary>
        /// <remarks>
        /// The client opens its channel on that connection and nothing else. It never closes or
        /// disposes of the connection, and asks the delegate again when the connection it got ended.
        /// </remarks>
        /// <param name="Connection">Hands out the SSH connection: <see cref="SshSharedConnection.GetAsync"/>, mostly. Asked at the first query, and again once the connection it handed out ended.</param>
        /// <param name="Subsystem">The SSH subsystem; "dns@open.charging.cloud" by default.</param>
        /// <param name="RecursionDesired">Whether DNS recursion is desired. Default is true.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout. Default is 23.5 seconds.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public DNSSSHClient(Func<CancellationToken, ValueTask<SshClient>>  Connection,
                            String?                                        Subsystem          = null,
                            Boolean?                                       RecursionDesired   = null,
                            TimeSpan?                                      QueryTimeout       = null,
                            ILoggerFactory?                                LoggerFactory      = null)

            : this(Subsystem,
                   RecursionDesired,
                   QueryTimeout,
                   LoggerFactory)

        {

            ArgumentNullException.ThrowIfNull(Connection);

            this.connectionOf = Connection;

        }

        #endregion

        #region (private) DNSSSHClient(Subsystem, RecursionDesired, QueryTimeout, LoggerFactory)

        private DNSSSHClient(String?          Subsystem,
                             Boolean?         RecursionDesired,
                             TimeSpan?        QueryTimeout,
                             ILoggerFactory?  LoggerFactory)
        {

            this.Subsystem         = Subsystem        ?? DefaultSubsystem;
            this.RecursionDesired  = RecursionDesired ?? true;
            this.QueryTimeout      = QueryTimeout     ?? DefaultQueryTimeout;
            this.logger            = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DNSSSHClient>();
            this.keepalive         = new DNSKeepalivePolicy(streamLock, CloseConnectionOf, () => pipeline.Outstanding == 0);

        }

        #endregion

        #endregion


        #region ConnectAsync(CancellationToken = default)

        /// <summary>
        /// Get the SSH connection and open the channel now, where there is none that lasts - what the
        /// first query does otherwise - and say why where that fails.
        /// </summary>
        /// <remarks>
        /// For whoever has to tell one failure from another before it asks, or after a query failed:
        /// the exceptions are those of <see cref="SshClient.ConnectAsync(String, UInt16, SshClientOptions, CancellationToken)"/>,
        /// of the delegate that hands out the connection, and of <see cref="SshClient.OpenSubsystemAsync"/>.
        /// </remarks>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="SshSubsystemRefusedException">The server answered SSH_MSG_CHANNEL_FAILURE: it does not offer the subsystem to this login. The SSH connection stays open.</exception>
        /// <exception cref="ObjectDisposedException">The client was disposed of.</exception>
        public async Task ConnectAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(disposed, this);

            using var ending = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, CancellationToken);

            await streamLock.WaitAsync(ending.Token).ConfigureAwait(false);

            try
            {
                await ChannelAsync(ending.Token).ConfigureAwait(false);
            }
            finally
            {
                streamLock.Release();
            }

        }

        #endregion

        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   ForceUpdate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = null,
                                         Boolean?                             ForceUpdate         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            #region Initial checks

            var resourceRecordTypes = ResourceRecordTypes.Distinct().ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion

            #region More than one record type is more than one query

            // RFC 9619 §4: "A DNS message with OPCODE = 0 MUST NOT include a
            // QDCOUNT parameter whose value is greater than 1." One query for each
            // type, pipelined on the one channel. See OneQuestionPerQuery.
            if (resourceRecordTypes.Count > 1)
                return await OneQuestionPerQuery.AskEachTypeAsync(
                                 resourceRecordTypes,
                                 resourceRecordType => Query(
                                                           DNSServiceName,
                                                           [ resourceRecordType ],
                                                           Timeout,
                                                           RecursionDesired,
                                                           ForceUpdate,
                                                           CancellationToken
                                                       ),
                                 Timeout
                             ).ConfigureAwait(false);

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               UDPPayloadSize,
                               RecursionDesired ?? this.RecursionDesired ?? true,
                               this.DnssecOK,
                               EDNSOptions.Count > 0 ? EDNSOptions : null,
                               [.. resourceRecordTypes]
                           );

            var signedQuery = SerializeQuery(dnsQuery, out var requestMAC);

            // The length and the message as one array, for one write: what keeps a
            // message whole on a channel (section 3.2 of the white paper), and what
            // RFC 7766 §8 asks for over TCP.
            var data        = new Byte[signedQuery.Length + 2];
            data[0] = (Byte) (signedQuery.Length >> 8);
            data[1] = (Byte) (signedQuery.Length & 0xFF);
            Buffer.BlockCopy(signedQuery, 0, data, 2, signedQuery.Length);

            #region SSH (pipelined on one channel, a new channel when it broke)

            ObjectDisposedException.ThrowIf(disposed, this);

            var effectiveTimeout = Timeout ?? QueryTimeout;
            var asked            = Stopwatch.GetTimestamp();

            // The deadline runs from here, the wait for the connection and the
            // channel included, and the client's lifetime is in it - see
            // DNSTCPClient for what each of the two cost when the deadline began
            // behind the lock.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
                                     lifetime.Token,
                                     CancellationToken
                                 );
            deadline.CancelAfter(effectiveTimeout);

            try
            {

                // Asked at most twice: a channel that breaks under a query - closed by
                // the server between two queries, or gone with its connection - gets
                // one more try on a new one. Section 3.3 of the white paper: "a client
                // re-sends the queries left unanswered on a closed channel over a new
                // one."
                for (var attempt = 1; ; attempt++)
                {

                    DNSStreamPipeline.Exchange exchange;

                    // Held to get the channel and to write this query, and no longer:
                    // the answer is waited for below, with the next query free to be
                    // written meanwhile (RFC 7766 §6.2.1.1).
                    await streamLock.WaitAsync(deadline.Token).
                                     ConfigureAwait(false);

                    try
                    {

                        // RFC 7828 §3.2.2, a TIMEOUT of 0: no more queries on that
                        // channel, and it is closed once its outstanding answers are
                        // in - see DNSTCPClient.
                        if (keepalive.ClosesWhenDrained(Volatile.Read(ref connection)))
                        {
                            await pipeline.Drained.WaitAsync(deadline.Token).ConfigureAwait(false);
                            await CloseConnectionOf(Volatile.Read(ref connection)).ConfigureAwait(false);
                        }

                        var channel  = await ChannelAsync(deadline.Token).ConfigureAwait(false);
                        var sent     = Stopwatch.GetTimestamp();

                        exchange     = pipeline.Register(
                                           dnsQuery.TransactionId,
                                           channel.Number,
                                           message => Accept(message, dnsQuery, signedQuery, requestMAC, effectiveTimeout, sent)
                                       );

                        try
                        {
                            await channel.Stream.WriteAsync(data, deadline.Token).ConfigureAwait(false);
                        }
                        // Not when the client is being disposed - see DNSTCPClient.
                        catch (IOException) when (!disposed && attempt == 1)
                        {
                            pipeline.Withdraw(exchange);
                            await CloseConnectionOf(channel.Number).ConfigureAwait(false);
                            continue;
                        }
                        catch
                        {
                            pipeline.Withdraw(exchange);
                            throw;
                        }

                    }
                    finally
                    {
                        streamLock.Release();
                    }

                    var (ending, answer) = await pipeline.AwaitAsync(
                                                     exchange,
                                                     StreamOf,
                                                     CloseConnectionOf,
                                                     keepalive.ApplyAsync,
                                                     deadline.Token
                                                 ).ConfigureAwait(false);

                    if (ending == DNSStreamPipeline.Ending.Answered)
                        return answer!;

                    if (ending == DNSStreamPipeline.Ending.Broken && attempt == 1 && !disposed)
                        continue;

                    return DNSInfo.Failed(
                               OriginOf(effectiveTimeout),
                               dnsQuery.TransactionId,
                               effectiveTimeout,
                               Stopwatch.GetElapsedTime(asked)
                           );

                }

            }
            catch (Exception e) when (disposed && !CancellationToken.IsCancellationRequested)
            {

                // The client was disposed of under the query, and whatever that
                // came out as, it is one event with one answer - see DNSTCPClient.
                logger.LogWarning(
                    e,
                    "DNS SSH query to {DNSServer} was cancelled by the client - it did not time out",
                    DNSServerLabel
                );

                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout,
                           Stopwatch.GetElapsedTime(asked)
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                logger.LogWarning(
                    "DNS SSH query to {DNSServer} timed out after {Timeout}s",
                    DNSServerLabel,
                    effectiveTimeout.TotalSeconds
                );

                return DNSInfo.TimedOut(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout
                       );

            }
            catch (OperationCanceledException)
            {

                // External cancellation (race-cancel or caller-initiated).
                // Silent return — not a real failure.
                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout,
                           Stopwatch.GetElapsedTime(asked)
                       );

            }
            catch (SshSubsystemRefusedException)
            {

                // An answer, not an outage: the server is there and does not offer
                // the subsystem to this login. Failed, not timed out, and
                // LastConnectionError says which of the two it was.
                logger.LogWarning(
                    "DNS SSH query to {DNSServer} was not sent: the server refused the subsystem '{Subsystem}'",
                    DNSServerLabel,
                    Subsystem
                );

                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout,
                           Stopwatch.GetElapsedTime(asked)
                       );

            }
            catch (Exception ex)
            {

                logger.LogWarning(
                    ex,
                    "DNS SSH query to {DNSServer} failed: [{Exception}] {Message}",
                    DNSServerLabel,
                    ex.GetType().Name,
                    ex.Message
                );

                return DNSInfo.Failed(
                           OriginOf(),
                           dnsQuery.TransactionId,
                           effectiveTimeout,
                           Stopwatch.GetElapsedTime(asked)
                       );

            }

            #endregion

        }

        #endregion


        #region (private) ChannelAsync(CancellationToken)

        /// <summary>
        /// The channel that lasts, or a new one - on the connection that lasts, or on a new one.
        /// Called under the stream lock.
        /// </summary>
        private async Task<Opened> ChannelAsync(CancellationToken CancellationToken)
        {

            ObjectDisposedException.ThrowIf(disposed, this);

            var current = Volatile.Read(ref opened);

            if (current is not null)
            {

                if (!current.Channel.Closed.IsCompleted && !current.Ssh.Closed.IsCompleted)
                    return current;

                // Closed by the server - an idle timeout, mostly - or gone with its
                // connection. What it still holds goes; the connection is not touched.
                await CloseConnectionOf(current.Number).ConfigureAwait(false);

            }

            // Counted before the channel is opened: a query still reading the old
            // one must not take the new one for its own, nor close it on its way
            // out, nor apply to it what the old one's answers say about keepalive -
            // see DNSTCPClient.
            var number = Interlocked.Increment(ref connection);

            keepalive.Connected(number);

            try
            {

                var ssh = await ConnectionAsync(CancellationToken).ConfigureAwait(false);

                SshMuxChannel channel;

                try
                {
                    channel = await ssh.OpenSubsystemAsync(Subsystem, CancellationToken).ConfigureAwait(false);
                }
                // The connection ended between being got and being used: once more, on
                // the next one. A refusal is an answer of a connection that lasts, and
                // is not asked for again.
                catch (Exception e) when (e is not OperationCanceledException       &&
                                          e is not SshSubsystemRefusedException     &&
                                          ssh.Closed.IsCompleted                    &&
                                          !CancellationToken.IsCancellationRequested)
                {
                    ssh      = await ConnectionAsync(CancellationToken).ConfigureAwait(false);
                    channel  = await ssh.OpenSubsystemAsync(Subsystem, CancellationToken).ConfigureAwait(false);
                }

                var made = new Opened(number, ssh, channel, new ChannelStream(channel));

                Volatile.Write(ref opened, made);
                Interlocked.Increment(ref channelsOpened);
                LastConnectionError = null;

                return made;

            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LastConnectionError = e;
                throw;
            }

        }

        #endregion

        #region (private) ConnectionAsync(CancellationToken)

        /// <summary>
        /// The SSH connection that lasts, or a new one: dialled, or asked for from whoever hands it
        /// out. Called under the stream lock.
        /// </summary>
        private async ValueTask<SshClient> ConnectionAsync(CancellationToken CancellationToken)
        {

            var current = sshConnection;

            if (current is not null && !current.Closed.IsCompleted)
                return current;

            SshClient next;

            if (connectionOf is not null)
            {

                // Somebody else's: the one that ended is theirs to dispose of, and the
                // next one theirs to make.
                next = await connectionOf(CancellationToken).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("No SSH connection was handed out.");

            }
            else
            {

                // Its own: what the one that ended still holds goes, before the next one is dialled.
                if (current is not null)
                {
                    sshConnection = null;
                    try { await current.DisposeAsync().ConfigureAwait(false); } catch { }
                }

                next = await SshClient.ConnectAsync(
                                 Host!,
                                 Port!.Value.ToUInt16(),
                                 sshOptions!,
                                 CancellationToken
                             ).ConfigureAwait(false);

            }

            sshConnection  = next;

            if (next.RemoteSocket is IPSocket remoteSocket)
                lastRemote = new Remote(remoteSocket.IPAddress, remoteSocket.Port);

            Interlocked.Increment(ref connectionsMade);

            return next;

        }

        #endregion

        #region (private) SerializeQuery(Query, out RequestMAC)

        /// <summary>
        /// Serialize a query for the wire: padded if this client pads, and signed if it signs - see
        /// <see cref="DNSTLSClient"/>, whose rule this is: the padding is measured after signing, so
        /// that it is the signed message that lands on the block boundary.
        /// </summary>
        /// <param name="Query">The query to put on the wire.</param>
        /// <param name="RequestMAC">The TSIG MAC of the query that actually goes out, which the response's MAC folds in (RFC 8945 §4.3.1).</param>
        private Byte[] SerializeQuery(DNSPacket   Query,
                                      out Byte[]? RequestMAC)
        {

            static Byte[] serialize(DNSPacket message)
            {
                using var stream = new MemoryStream();
                message.Serialize(stream, false, []);
                return stream.ToArray();
            }

            // No block length, or no OPT record to carry the option: send the
            // query as it is. Inventing an OPT record here would announce EDNS(0)
            // support the caller switched off.
            if (PaddingBlockSize == 0 || !DNSPadding.HasEDNS(Query))
                return TransactionSecurity.SignQuery(serialize(Query), out RequestMAC);

            var trial   = TransactionSecurity.SignQuery(serialize(DNSPadding.WithPadding(Query, 0)), out var trialMAC);

            var octets  = DNSPadding.OctetsFor(trial.Length, PaddingBlockSize);

            if (octets == 0)
            {
                RequestMAC = trialMAC;
                return trial;
            }

            return TransactionSecurity.SignQuery(serialize(DNSPadding.WithPadding(Query, octets)), out RequestMAC);

        }

        #endregion

        #region (private) Accept(Message, Query, SignedQuery, RequestMAC, Timeout, Sent)

        /// <summary>
        /// The query's answer, or null when the message is not it: matched by its ID and, where it
        /// has one, by its question (RFC 7858 §3.3, RFC 7766 §7).
        /// </summary>
        private Task<DNSInfo?> Accept(Byte[]     Message,
                                      DNSPacket  Query,
                                      Byte[]     SignedQuery,
                                      Byte[]?    RequestMAC,
                                      TimeSpan   Timeout,
                                      Int64      Sent)
        {

            var body = Message;

            // RFC 8945 §5.4: a message that fails verification is treated as though
            // it had not arrived, and the query goes on waiting - see DNSTLSClient.
            if (!TransactionSecurity.TryAcceptResponse(ref body, RequestMAC, SignedQuery, out var reason))
            {

                logger.LogWarning(
                    "Discarding a DNS over SSH response from {DNSServer} that failed transaction-signature verification: {Reason}",
                    DNSServerLabel,
                    reason
                );

                return Task.FromResult<DNSInfo?>(null);

            }

            var response = DNSInfo.ReadResponse(
                               OriginOf(Timeout),
                               Query.TransactionId,
                               Query.Questions,
                               new MemoryStream(body),
                               Timeout,
                               Stopwatch.GetElapsedTime(Sent)
                           );

            // Not ours - the matching rule said so.
            return Task.FromResult(response.IsValid
                                       ? response
                                       : null);

        }

        #endregion

        #region (private) StreamOf(Connection) / CloseConnectionOf(Connection)

        /// <summary>
        /// The stream of the given channel, or null when that channel is no longer this client's.
        /// </summary>
        /// <remarks>
        /// Also of a channel the server closed already: what it wrote before it closed the channel is
        /// still to be read, and the end behind it is what tells the queries on it that they are to
        /// be asked again.
        /// </remarks>
        private Stream? StreamOf(Int32 Connection)
        {

            var current = Volatile.Read(ref opened);

            return Connection == Volatile.Read(ref connection) && current is not null && current.Number == Connection
                       ? current.Stream
                       : null;

        }

        /// <summary>
        /// Close the given channel, and nothing else: not whatever channel the field holds by now -
        /// see <see cref="DNSTCPClient"/> - and never the SSH connection, which may carry OCPP and
        /// other channels (section 3.5 of the white paper).
        /// </summary>
        private async Task CloseConnectionOf(Int32 Connection)
        {

            var current = Volatile.Read(ref opened);

            if (current is null || current.Number != Connection)
                return;

            Interlocked.CompareExchange(ref opened, null, current);

            try
            {
                // EOF and CLOSE; a channel that is closed already, or whose
                // connection went, has nothing left to be told.
                await current.Channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            { }

        }

        #endregion

        #region (private) DNSServerLabel / OriginOf(QueryTimeout = null)

        /// <summary>
        /// How to name this resolver in a log line.
        /// </summary>
        private String DNSServerLabel
        {
            get
            {

                var login = sshConnection?.Username ?? sshOptions?.Username;

                if (Host is not null)
                    return $"ssh://{login}@{Host}:{Port}";

                return lastRemote is Remote remote
                           ? $"ssh://{login}@{remote.IPAddress.ToIPLiteral()}:{remote.Port}"
                           : "an SSH connection not handed out yet";

            }
        }

        /// <summary>
        /// Where an answer from this client came from.
        /// </summary>
        /// <remarks>
        /// The host this client dials, by address or by name; for a connection it is handed, where
        /// that connection goes - and the unspecified address where none was handed out yet, or it
        /// runs over something that has no address: an origin has to be named, and there is none.
        /// </remarks>
        private DNSServerConfig OriginOf(TimeSpan? QueryTimeout = null)
        {

            if (Host is not null)
                return IPAddress.TryParse(Host, out var ipAddress)

                           ? new DNSServerConfig(
                                 ipAddress,
                                 Port,
                                 DNSTransport.SSH,
                                 QueryTimeout
                             )

                           : new DNSServerConfig(
                                 DomainName.Parse(Host),
                                 Port,
                                 DNSTransport.SSH,
                                 QueryTimeout
                             );

            return lastRemote is Remote remote

                       ? new DNSServerConfig(
                             remote.IPAddress,
                             remote.Port,
                             DNSTransport.SSH,
                             QueryTimeout
                         )

                       : new DNSServerConfig(
                             IPv4Address.Any,
                             IPPort.SSH,
                             DNSTransport.SSH,
                             QueryTimeout
                         );

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"Using DNS server: {DNSServerLabel}, subsystem '{Subsystem}'";

        #endregion

        #region Dispose() / DisposeAsync()

        /// <summary>
        /// Close the channel, and the SSH connection where it is this client's own.
        /// </summary>
        public void Dispose()

            => DisposeAsync().AsTask().GetAwaiter().GetResult();

        /// <summary>
        /// Close the channel, and the SSH connection where it is this client's own. A connection the
        /// client was handed goes on, for whoever else is on it.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposing, 1) != 0)
                return;

            disposed = true;

            keepalive.Dispose();

            // Ends what the queries in flight wait for: each of them then answers
            // "failed", and lets the lock go.
            await lifetime.CancelAsync().ConfigureAwait(false);

            // Under the lock, so that a query that was getting a connection is
            // through with it: a connection dialled meanwhile would otherwise be
            // nobody's to close. Not for ever - whoever hands out the connection may
            // not heed the token.
            var held = await streamLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            try
            {

                if (Volatile.Read(ref opened) is Opened current)
                    await CloseConnectionOf(current.Number).ConfigureAwait(false);

                // A connection of its own; one it was handed goes on.
                if (OwnsConnection && sshConnection is SshClient own)
                {
                    sshConnection = null;
                    try { await own.DisposeAsync().ConfigureAwait(false); } catch { }
                }

            }
            finally
            {
                if (held)
                    streamLock.Release();
            }

            // The lock and the lifetime are not disposed of, and that is deliberate:
            // a query in flight still releases the one and asks the other - see
            // DNSTCPClient.

        }

        #endregion

    }

}
