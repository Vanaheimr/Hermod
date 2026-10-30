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

using System.IO.Pipelines;

// Our own "ExchangeHash" property below would otherwise shadow the ExchangeHash helper type.
using ExchangeHashCalc = org.GraphDefined.Vanaheimr.Hermod.SSH.ExchangeHash;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A stateful SSH transport over an <see cref="IDuplexPipe"/>: it performs the initial key exchange,
    /// then owns the mutable per-direction cipher/MAC and packet-sequence-number state so that packets
    /// can be sent and received with correct framing — and so that a later key <b>re-exchange</b>
    /// (rekeying, RFC 4253 §9) can install fresh keys while preserving the original session id.
    /// </summary>
    /// <remarks>
    /// With strict-KEX (Terrapin mitigation) both sequence numbers reset to zero after <i>every</i>
    /// SSH_MSG_NEWKEYS, initial or subsequent — matching OpenSSH. Nothing of ours interleaves with a key
    /// exchange: what is sent during one is queued and written after our NEWKEYS, and what the peer sent
    /// before it saw our KEXINIT is held back until the exchange is over. With <see cref="RekeyLimits"/>
    /// set, a send or a receive that finds the keys used up starts a rekey by itself, which the receive
    /// loop completes. Instances are single reader; sends serialize themselves.
    /// </remarks>
    public sealed class SshTransport : IDisposable
    {

        #region Data

        private readonly IDuplexPipe             pipe;
        private readonly Boolean                 isServer;
        private readonly Byte[]                  vC;
        private readonly Byte[]                  vS;
        private readonly IReadOnlyList<ISshHostKey>?  hostKeys;   // server side: every key we may present
        private          ISshHostKey?                 hostKey;    // server side: the one the first exchange chose
        private readonly Func<Byte[], Boolean>?  verifyHostKey;   // client side
        private readonly String[]?               ciphers;
        private readonly String[]?               macs;
        private readonly String[]?               keyExchanges;
        private readonly String[]?               hostKeyAlgorithms;
        private readonly String[]                serverSignatureAlgorithms;

        private readonly Dictionary<String, String>  peerExtensions = [];

        private SshTransportCipher   sendCipher;
        private ISshMac?             sendMac;
        private UInt32               sendSequenceNumber;

        private SshTransportCipher   receiveCipher;
        private ISshMac?             receiveMac;
        private UInt32               receiveSequenceNumber;

        private Byte[]               sessionId      = [];
        private Byte[]               exchangeHash   = [];
        private Byte[]               serverHostKey  = [];
        private NegotiatedAlgorithms algorithms     = null!;

        // Whether a key exchange is currently running. Only strict KEX cares: it forbids the otherwise
        // always-legal SSH_MSG_IGNORE / DEBUG / UNIMPLEMENTED while the exchange is in flight.
        private Boolean              keyExchangeInProgress;

        // The class remark above calls this transport single-writer. This field is what makes that
        // true, rather than a request to whoever calls it: every send passes through it, and a key
        // exchange holds it for the whole exchange, so no other packet can interleave with KEX traffic.
        //
        // Two reasons, one older than the other. Framing: SendPacketAsync mutates sendSequenceNumber
        // and the send cipher's state, so two concurrent senders can emit packets whose sequence
        // numbers and MACs do not match what the peer computes — the very failure strict KEX exists to
        // make detectable, arriving by accident instead of by attack. Interop: OpenSSH 10.4 lists under
        // "Potentially incompatible changes" that it now disconnects "if the peer sends non-KEX
        // messages during a post-authentication key re-exchange", so a data packet slipping into a
        // rekey stopped being merely untidy and became a dropped connection.
        //
        // Holding a SEND lock across the exchange's receives is deliberate and cannot deadlock: a peer
        // drives a key exchange to completion without waiting on our channel data — that quiescence is
        // exactly what OpenSSH now enforces.
        private readonly SemaphoreSlim sendLock = new (1, 1);

        // Set under sendLock by CloseOutputAsync: nothing is written into the pipe after it is
        // completed - completing gives its buffers back to the pool every connection rents from.
        private Boolean outputClosed;
        private Int32   closingOutput;

        // What has moved under the current keys, and since when - what an automatic rekey is measured
        // against. Sending is counted under sendLock, receiving by the one reader; both are read from
        // whichever task is sending, hence Interlocked.
        private Int64   bytesSentSinceKex;
        private Int64   bytesReceivedSinceKex;
        private Int64   packetsSentSinceKex;
        private Int64   packetsReceivedSinceKex;
        private Int64   lastKeyExchange;

        // An automatic rekey starts on whichever task sends, but only the reader can finish it. The
        // sender writes our KEXINIT; the reader takes the exchange over when the peer's KEXINIT arrives.
        // kexGate decides the one race between them: the peer's KEXINIT being read while ours is being
        // written.
        //
        //   pendingLocalKexInit    our KEXINIT is on the wire, and the exchange is to use it
        //   peerKexInitSeen        the reader has the peer's KEXINIT; nobody starts another exchange
        //   sendsHeld              from our KEXINIT to our NEWKEYS, what is sent waits in `held`
        //
        // Nothing but the exchange's own packets may go out between our KEXINIT and our NEWKEYS - and
        // OpenSSH 10.4 drops a peer that sends anything else. That used to be kept by holding sendLock
        // for the whole exchange, which could hang the connection: the exchange needs the reader, and
        // the reader sends too - a channel confirmation, a reply to a keepalive - from the very packet
        // it is handling. Waiting for sendLock there, it could never read the KEXINIT that would have
        // freed it. So sendLock is held for one packet at a time, as for any send, and what is sent
        // while sendsHeld is set is queued in order and written after our NEWKEYS, as OpenSSH does -
        // except channel data, which waits for the exchange to end instead. Data is what the
        // application pumps, never the reader, so waiting is safe there; and queued, a bulk sender
        // would pour megabytes into `held` during every exchange, for the reader to write out after it.
        //
        // Whatever the peer sends between our KEXINIT and its own is held back in `deferred` and handed
        // out after the exchange.
        private readonly Object       kexGate = new ();
        private Byte[]?               pendingLocalKexInit;
        private Boolean               peerKexInitSeen;
        private Boolean               sendsHeld;
        private readonly Queue<Byte[]>  held = new ();
        private TaskCompletionSource  sendsReleased = CompletedSignal();
        private readonly Queue<Byte[]>  deferred = new ();
        private Int64                 deferredBytes;

        // A peer that never answers our KEXINIT while it keeps sending is cut off here, instead of being
        // buffered without end.
        private const Int64           MaxDeferredBytes = 32 * 1024 * 1024;

        private static TaskCompletionSource CompletedSignal()
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            signal.SetResult();
            return signal;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The session identifier (the exchange hash of the <i>first</i> key exchange); stable across rekeys.
        /// </summary>
        public Byte[]                SessionId         => sessionId;

        /// <summary>
        /// The exchange hash H of the most recent key exchange.
        /// </summary>
        public Byte[]                ExchangeHash      => exchangeHash;

        /// <summary>
        /// The algorithms negotiated in the most recent key exchange.
        /// </summary>
        public NegotiatedAlgorithms  Algorithms        => algorithms;

        /// <summary>
        /// The peer's host-key blob (K_S) from the most recent key exchange.
        /// </summary>
        public Byte[]                ServerHostKey     => serverHostKey;

        /// <summary>
        /// Whether this end is the server.
        /// </summary>
        public Boolean               IsServer          => isServer;

        /// <summary>
        /// How many key exchanges have completed (1 after the handshake, +1 per rekey).
        /// </summary>
        public Int32                 KeyExchangeCount  { get; private set; }

        /// <summary>
        /// Whether the server has accepted this client's SSH_MSG_SERVICE_REQUEST for "ssh-userauth".
        /// The service is asked for once per connection: a server takes the request once, and a
        /// second one in the middle of authentication is a message it does not expect.
        /// </summary>
        internal Boolean             UserAuthServiceAccepted  { get; set; }

        /// <summary>
        /// What the peer said it is in its identification string - "SSH-2.0-PuTTY_Release_0.83", say -
        /// once the version exchange is done.
        /// </summary>
        public SshIdentificationString?  PeerIdentification  { get; private set; }

        /// <summary>
        /// The largest <c>packet_length</c> this end accepts from its peer — the server's
        /// <see cref="SshServerLimits.MaxPacketSize"/>. Set when the transport is created, so it already
        /// bounds the key exchange and authentication, where an unauthenticated peer is talking.
        /// </summary>
        public Int32                     MaxInboundPacketLength  { get; private set; } = SshPacketFraming.MaxPacketLength;

        /// <summary>
        /// The extensions received from the peer via SSH_MSG_EXT_INFO (RFC 8308), if any.
        /// </summary>
        public IReadOnlyDictionary<String, String>  PeerExtensions  => peerExtensions;

        /// <summary>
        /// When this transport renews its keys by itself, or null (the default) when it only rekeys on
        /// request or when the peer asks. Set by the connection layer once authentication is done:
        /// <see cref="SshChannelMultiplexer.Start"/> arms it, since only a running receive loop can
        /// complete an exchange this end starts while traffic flows.
        /// </summary>
        public SshRekeyLimits?       RekeyLimits       { get; set; }

        /// <summary>
        /// How long the current keys have been in use.
        /// </summary>
        public TimeSpan              TimeSinceKeyExchange
            => TimeProvider.System.GetElapsedTime(Interlocked.Read(ref lastKeyExchange));

        /// <summary>
        /// Whether the current keys have reached <see cref="RekeyLimits"/>, or the packet count after
        /// which every key is renewed (<see cref="SshRekeyLimits.MaxPacketsPerKey"/>).
        /// </summary>
        public Boolean               IsRekeyDue
            => RekeyLimits is { } limits &&
               KeyExchangeCount > 0      &&
               (Interlocked.Read(ref bytesSentSinceKex)       >= limits.DataVolume              ||
                Interlocked.Read(ref bytesReceivedSinceKex)   >= limits.DataVolume              ||
                Interlocked.Read(ref packetsSentSinceKex)     >= SshRekeyLimits.MaxPacketsPerKey ||
                Interlocked.Read(ref packetsReceivedSinceKex) >= SshRekeyLimits.MaxPacketsPerKey ||
                (limits.Interval is { } interval && TimeSinceKeyExchange >= interval));

        /// <summary>
        /// Called on the receiving task after every completed key <i>re</i>-exchange, with the new
        /// <see cref="KeyExchangeCount"/> - the hook for an audit record. Exceptions are swallowed.
        /// </summary>
        public Action<Int32>?        Rekeyed           { get; set; }

        /// <summary>
        /// The peer's "server-sig-algs" list (the signature algorithms a server accepts for public-key
        /// auth), parsed from a received EXT_INFO, or null if the peer never advertised it.
        /// </summary>
        public String[]?             PeerServerSignatureAlgorithms
            => peerExtensions.TryGetValue(SshExtensionNames.ServerSigAlgs, out var value)
                   ? value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   : null;

        #endregion

        #region Constructor(s)

        private SshTransport(IDuplexPipe             Pipe,
                             Boolean                 IsServer,
                             Byte[]                  V_C,
                             Byte[]                  V_S,
                             IReadOnlyList<ISshHostKey>?  HostKeys,
                             Func<Byte[], Boolean>?  VerifyHostKey,
                             String[]?               Ciphers,
                             String[]?               Macs,
                             String[]?               KeyExchanges,
                             String[]?               HostKeyAlgorithms,
                             String[]?               ServerSignatureAlgorithms)
        {

            this.pipe                       = Pipe;
            this.isServer                   = IsServer;
            this.vC                         = V_C;
            this.vS                         = V_S;
            this.hostKeys                   = HostKeys;
            this.verifyHostKey              = VerifyHostKey;
            this.ciphers                    = Ciphers;
            this.macs                       = Macs;
            this.keyExchanges               = KeyExchanges;
            this.hostKeyAlgorithms          = HostKeyAlgorithms;
            this.serverSignatureAlgorithms  = ServerSignatureAlgorithms ?? SshExtensionNames.DefaultServerSignatureAlgorithms;

            // Before the first NEWKEYS everything travels in the clear (cipher "none", no MAC).
            this.sendCipher         = NullTransportCipher.Instance;
            this.receiveCipher      = NullTransportCipher.Instance;
            this.lastKeyExchange    = TimeProvider.System.GetTimestamp();

        }

        #endregion


        #region (static) ClientHandshakeAsync(Pipe, ...)

        /// <summary>
        /// Connect as the client: version exchange, then the initial key exchange, returning an
        /// established transport ready for encrypted traffic and later rekeys.
        /// </summary>
        public static async ValueTask<SshTransport> ClientHandshakeAsync(IDuplexPipe               Pipe,
                                                                         SshIdentificationString?  LocalIdentification  = null,
                                                                         Func<Byte[], Boolean>?    VerifyHostKey        = null,
                                                                         String[]?                 Ciphers              = null,
                                                                         String[]?                 Macs                 = null,
                                                                         String[]?                 KeyExchanges         = null,
                                                                         String[]?                 HostKeyAlgorithms    = null,
                                                                         CancellationToken         CancellationToken    = default)
        {

            var localId  = LocalIdentification ?? SshIdentificationString.Default;
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);

            var transport = new SshTransport(Pipe, IsServer: false, localId.ToWireBytes(), remote.WireBytes,
                                             HostKeys: null, VerifyHostKey, Ciphers, Macs, KeyExchanges, HostKeyAlgorithms,
                                             ServerSignatureAlgorithms: null) {
                                 PeerIdentification = remote.Identification
                             };

            await transport.PerformKeyExchangeAsync(IsInitial: true, PeerKexInit: null, CancellationToken).ConfigureAwait(false);

            return transport;

        }

        #endregion

        #region (static) ServerHandshakeAsync(Pipe, HostKey,  ...)

        /// <summary>
        /// Accept as the server: version exchange, then the initial key exchange, returning an
        /// established transport ready for encrypted traffic and later rekeys.
        /// </summary>
        public static ValueTask<SshTransport> ServerHandshakeAsync(IDuplexPipe               Pipe,
                                                                   ISshHostKey               HostKey,
                                                                   SshIdentificationString?  LocalIdentification        = null,
                                                                   String[]?                 Ciphers                    = null,
                                                                   String[]?                 Macs                       = null,
                                                                   String[]?                 KeyExchanges               = null,
                                                                   String[]?                 ServerSignatureAlgorithms  = null,
                                                                   Int32?                    MaxPacketLength            = null,
                                                                   CancellationToken         CancellationToken          = default)

            => ServerHandshakeAsync(Pipe, [ HostKey ], LocalIdentification, Ciphers, Macs, KeyExchanges,
                                    ServerSignatureAlgorithms, MaxPacketLength, CancellationToken);

        #endregion

        #region (static) ServerHandshakeAsync(Pipe, HostKeys, ...)

        /// <summary>
        /// Accept as the server with several host keys: every key's algorithms are offered, and the one
        /// the client picks decides the key that signs - the first that can, so for two keys of the same
        /// kind the earlier one. That key then stays the host key for the life of the connection.
        /// </summary>
        public static async ValueTask<SshTransport> ServerHandshakeAsync(IDuplexPipe                 Pipe,
                                                                         IReadOnlyList<ISshHostKey>  HostKeys,
                                                                         SshIdentificationString?    LocalIdentification        = null,
                                                                         String[]?                   Ciphers                    = null,
                                                                         String[]?                   Macs                       = null,
                                                                         String[]?                   KeyExchanges               = null,
                                                                         String[]?                   ServerSignatureAlgorithms  = null,
                                                                         Int32?                      MaxPacketLength            = null,
                                                                         CancellationToken           CancellationToken          = default)
        {

            if (HostKeys.Count == 0)
                throw new ArgumentException("A server needs at least one host key!", nameof(HostKeys));

            var localId  = LocalIdentification ?? SshIdentificationString.Default;
            await SshVersionExchange.WriteAsync(Pipe.Output, localId, CancellationToken).ConfigureAwait(false);
            var remote   = await SshVersionExchange.ReadAsync(Pipe.Input, CancellationToken).ConfigureAwait(false);

            var transport = new SshTransport(Pipe, IsServer: true, remote.WireBytes, localId.ToWireBytes(),
                                             HostKeys, VerifyHostKey: null, Ciphers, Macs, KeyExchanges, HostKeyAlgorithms: null,
                                             ServerSignatureAlgorithms) {
                                 PeerIdentification      = remote.Identification,
                                 MaxInboundPacketLength  = MaxPacketLength ?? SshPacketFraming.MaxPacketLength
                             };

            await transport.PerformKeyExchangeAsync(IsInitial: true, PeerKexInit: null, CancellationToken).ConfigureAwait(false);

            return transport;

        }

        #endregion


        #region SendPacketAsync(Payload, CancellationToken)

        /// <summary>
        /// Frame, encrypt and flush one packet under the current send cipher, advancing the send
        /// sequence number. Serialized against every other send and against a key exchange in flight,
        /// so a caller may send from any task without coordinating with the rest.
        /// </summary>
        /// <remarks>
        /// During a key exchange the packet is queued and written after our NEWKEYS, under the new
        /// keys; the call returns once it is queued. Channel data waits for the exchange to end
        /// instead, and a DISCONNECT goes out at once - it may end an exchange.
        /// </remarks>
        public async ValueTask SendPacketAsync(ReadOnlyMemory<Byte> Payload, CancellationToken CancellationToken = default)
        {

            while (true)
            {

                await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);

                Task? exchangeOver = null;

                try
                {

                    lock (kexGate)
                    {
                        if (sendsHeld && !(Payload.Length > 0 && Payload.Span[0] == (Byte) SshMessageNumber.Disconnect))
                        {

                            if (Payload.Length > 0 &&
                                Payload.Span[0] is (Byte) SshMessageNumber.ChannelData or (Byte) SshMessageNumber.ChannelExtendedData)
                                exchangeOver = sendsReleased.Task;

                            else
                            {
                                held.Enqueue(Payload.ToArray());
                                return;
                            }

                        }
                    }

                    if (exchangeOver is null)
                    {

                        await WritePacketAsync(Payload, CancellationToken).ConfigureAwait(false);

                        // Only connection-protocol traffic starts a rekey: not a KEX message, not a
                        // DISCONNECT, and not authentication, which RekeyLimits is not armed for anyway.
                        if (Payload.Length > 0 && Payload.Span[0] >= (Byte) SshMessageNumber.GlobalRequest && IsRekeyDue)
                            await TrySendKexInitAsync(CancellationToken).ConfigureAwait(false);

                        return;

                    }

                }
                finally
                {
                    sendLock.Release();
                }

                await exchangeOver.WaitAsync(CancellationToken).ConfigureAwait(false);

            }

        }

        /// <summary>
        /// The write itself, for callers that hold <c>sendLock</c> already.
        /// </summary>
        private async ValueTask WritePacketAsync(ReadOnlyMemory<Byte> Payload, CancellationToken CancellationToken = default)
        {

            if (outputClosed)
                throw new SshConnectionClosedException("The connection is closed: nothing more is sent on it.");

            SshPacketFraming.WritePacket(pipe.Output, sendCipher, Payload.Span, sendSequenceNumber, sendMac);

            // Counted once it is sealed into the pipe, not once it is flushed: a flush cut off by its
            // token leaves the packet in the pipe, and it goes out with the next flush - which carried
            // it under the same number, and the peer read the next packet with the wrong one.
            unchecked { sendSequenceNumber++; }   // wraps at 2^32 (RFC 4253 §6.4)

            Interlocked.Add      (ref bytesSentSinceKex, Payload.Length);
            Interlocked.Increment(ref packetsSentSinceKex);

            await pipe.Output.FlushAsync(CancellationToken).ConfigureAwait(false);

        }

        /// <summary>
        /// One packet of the key exchange: written past the queue that holds everything else.
        /// </summary>
        private async ValueTask WriteExchangePacketAsync(ReadOnlyMemory<Byte> Payload, CancellationToken CancellationToken)
        {

            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try     { await WritePacketAsync(Payload, CancellationToken).ConfigureAwait(false); }
            finally { sendLock.Release(); }

        }

        #endregion

        #region CloseOutputAsync(Within = null)

        /// <summary>
        /// Close the sending half: once the send in flight is done with the pipe - or, where it is not
        /// within the given time, once the connection is cut off under it - the pipe's writer is
        /// completed, and every send after is refused without touching it.
        /// </summary>
        /// <remarks>
        /// Completing a pipe writer gives its buffers back to the pool, and the pool is shared by every
        /// connection of the process. Completed while a window adjust or a DISCONNECT was still being
        /// written into it, a buffer went back in use - or twice - and a later connection of another
        /// test read a packet whose bytes had changed after they were sealed: a bad Poly1305 tag.
        /// </remarks>
        /// <param name="Within">How long the send in flight is waited for, first, and again after the
        /// connection is cut off; two seconds where nothing is said.</param>
        public async ValueTask CloseOutputAsync(TimeSpan? Within = null)
        {

            if (Interlocked.Exchange(ref closingOutput, 1) == 1)
                return;

            var within  = Within ?? TimeSpan.FromSeconds(2);
            var held    = await TakeSendLock(within).ConfigureAwait(false);

            if (!held && pipe is DuplexPipe { Abort: { } abort })
            {

                try   { abort(); }
                catch { }

                held = await TakeSendLock(within).ConfigureAwait(false);

            }

            // A send that will not end even now keeps its buffers: never given back, rather than
            // given back under it.
            if (!held)
                return;

            try
            {
                outputClosed = true;
                try   { await pipe.Output.CompleteAsync().ConfigureAwait(false); }
                catch { }
            }
            finally
            {
                sendLock.Release();
            }

        }

        private async ValueTask<Boolean> TakeSendLock(TimeSpan Within)
        {
            try
            {
                return await sendLock.WaitAsync(Within).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        #endregion

        #region ReceivePacketAsync(CancellationToken)

        /// <summary>
        /// Read, decrypt and authenticate one packet under the current receive cipher, advancing the
        /// receive sequence number.
        ///
        /// <para>
        /// SSH_MSG_IGNORE, SSH_MSG_DEBUG and SSH_MSG_UNIMPLEMENTED are consumed here rather than handed
        /// on: RFC 4253 §11 lets a peer send them <i>at any time</i>, so every caller would otherwise have
        /// to skip them and any that forgot would break against peers that use them — AsyncSSH, for one,
        /// pads authentication with SSH_MSG_IGNORE to blunt traffic analysis.
        /// </para>
        ///
        /// <para>
        /// The exception is strict KEX: while a key exchange is in flight it requires that no unexpected
        /// packet is tolerated, explicitly including these three, because silently dropping them is what
        /// let Terrapin (CVE-2023-48795) shift the sequence numbers. There they are fatal instead.
        /// </para>
        /// </summary>
        public async ValueTask<Byte[]> ReceivePacketAsync(CancellationToken CancellationToken = default)
        {

            // What arrived while an exchange we started waited for the peer's KEXINIT, in order, once
            // that exchange is over.
            lock (kexGate)
            {
                if (deferred.Count > 0 && pendingLocalKexInit is null && !peerKexInitSeen)
                {
                    var held = deferred.Dequeue();
                    deferredBytes -= held.Length;
                    return held;
                }
            }

            try
            {

                while (true)
                {

                    var payload = await SshPacketFraming.ReadPacketAsync(pipe.Input, receiveCipher, receiveSequenceNumber, receiveMac, CancellationToken, MaxInboundPacketLength).ConfigureAwait(false);

                    unchecked { receiveSequenceNumber++; }

                    Interlocked.Add      (ref bytesReceivedSinceKex, payload.Length);
                    Interlocked.Increment(ref packetsReceivedSinceKex);

                    if (payload.Length < 1)
                        return payload;

                    var messageNumber = (SshMessageNumber) payload[0];

                    if (messageNumber == SshMessageNumber.KexInit)
                    {
                        lock (kexGate)
                            peerKexInitSeen = true;
                        return payload;
                    }

                    if (messageNumber is SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented)
                    {

                        if (keyExchangeInProgress && algorithms is { StrictKex: true })
                            throw new SshWireException($"Strict KEX forbids {messageNumber} ({payload[0]}) during a key exchange.");

                        continue;

                    }

                    // Sent before the peer saw our KEXINIT, so legal (RFC 4253 §7.1) - but not to be
                    // acted on until the exchange is over. A DISCONNECT is the exception: it ends it.
                    if (messageNumber != SshMessageNumber.Disconnect)
                    {
                        lock (kexGate)
                        {
                            if (pendingLocalKexInit is not null && !peerKexInitSeen)
                            {

                                deferred.Enqueue(payload);
                                deferredBytes += payload.Length;

                                if (deferredBytes > MaxDeferredBytes)
                                    throw new SshWireException($"The peer sent {deferredBytes} bytes without answering our KEXINIT.");

                                continue;

                            }
                        }
                    }

                    // A connection that mostly receives sends too little for the check on the send path
                    // to come round in time, so the reader looks as well - as OpenSSH's does.
                    if (payload[0] >= (Byte) SshMessageNumber.GlobalRequest && IsRekeyDue)
                        await TryStartRekeyFromReceiveAsync(CancellationToken).ConfigureAwait(false);

                    return payload;

                }

            }
            catch
            {
                AbandonPendingExchange();
                throw;
            }

        }

        /// <summary>
        /// Start a rekey from the reader - but only if sendLock is free this instant: a busy lock means
        /// a sender is at it, and its own check comes next. The packet that tipped the limit is handed
        /// out as usual; whatever its handling sends is queued behind our KEXINIT.
        /// </summary>
        private async ValueTask TryStartRekeyFromReceiveAsync(CancellationToken CancellationToken)
        {

            if (!sendLock.Wait(0))
                return;

            try     { await TrySendKexInitAsync(CancellationToken).ConfigureAwait(false); }
            finally { sendLock.Release(); }

        }

        /// <summary>
        /// The receiving side is gone while our KEXINIT waited for an answer, so the exchange will never
        /// end: let the sends it held go - they meet a dead connection, rather than a queue that is never
        /// written.
        /// </summary>
        private void AbandonPendingExchange()
        {

            lock (kexGate)
            {
                if (pendingLocalKexInit is not null && !peerKexInitSeen)
                {
                    pendingLocalKexInit = null;
                    ReleaseHeldSends();
                }
            }

        }

        /// <summary>
        /// Under kexGate: from now on, what is sent waits in <c>held</c> until the exchange is over.
        /// </summary>
        private void HoldSends()
        {
            if (!sendsHeld)
            {
                sendsHeld      = true;
                sendsReleased  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>
        /// Under kexGate: drop what was held - the exchange failed, so it can not be sent - and let
        /// sends through again.
        /// </summary>
        private void ReleaseHeldSends()
        {
            held.Clear();
            sendsHeld  = false;
            sendsReleased.TrySetResult();
        }

        /// <summary>
        /// With sendLock held, after our NEWKEYS: write what was held, in order and under the new keys,
        /// then let sends through again. Nothing can come in between: a sender takes sendLock first.
        /// </summary>
        private async ValueTask FlushHeldSendsAsync(CancellationToken CancellationToken)
        {
            while (true)
            {

                Byte[] next;

                lock (kexGate)
                {

                    if (held.Count == 0)
                    {
                        sendsHeld = false;
                        sendsReleased.TrySetResult();
                        return;
                    }

                    next       = held.Dequeue();

                }

                await WritePacketAsync(next, CancellationToken).ConfigureAwait(false);

            }
        }

        #endregion


        #region RekeyAsync(CancellationToken)

        /// <summary>
        /// Initiate a key re-exchange: send a fresh KEXINIT and run the exchange, installing new keys
        /// while keeping the original session id. Both peers may initiate simultaneously (each sends its
        /// KEXINIT and reads the other's) — the standard symmetric case.
        /// </summary>
        public ValueTask RekeyAsync(CancellationToken CancellationToken = default)
            => PerformKeyExchangeAsync(IsInitial: false, PeerKexInit: null, CancellationToken);

        #endregion

        #region RespondToRekeyAsync(PeerKexInit, CancellationToken)

        /// <summary>
        /// Respond to a peer-initiated rekey whose KEXINIT has already been read from the stream (e.g.
        /// by a receive loop): send our own KEXINIT and run the exchange using the peer's KEXINIT.
        /// </summary>
        /// <param name="PeerKexInit">The peer's KEXINIT payload that triggered the rekey.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public ValueTask RespondToRekeyAsync(Byte[] PeerKexInit, CancellationToken CancellationToken = default)
            => PerformKeyExchangeAsync(IsInitial: false, PeerKexInit, CancellationToken);

        #endregion

        #region StartRekeyAsync(CancellationToken)

        /// <summary>
        /// Start a key re-exchange while another task keeps receiving: send our KEXINIT and leave the
        /// rest to the receive loop, which completes the exchange when the peer's KEXINIT arrives (by
        /// passing it to <see cref="RespondToRekeyAsync"/>). Until our NEWKEYS, what is sent is queued.
        /// </summary>
        /// <returns>False when an exchange is already under way, and nothing was sent.</returns>
        public async ValueTask<Boolean> StartRekeyAsync(CancellationToken CancellationToken = default)
        {

            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try     { return await TrySendKexInitAsync(CancellationToken).ConfigureAwait(false); }
            finally { sendLock.Release(); }

        }

        #endregion

        #region (private) TrySendKexInitAsync(CancellationToken)

        /// <summary>
        /// With sendLock held: send our KEXINIT unless an exchange is already under way, and hold what is
        /// sent after it until the exchange is over. Says whether this started the exchange.
        /// </summary>
        private async ValueTask<Boolean> TrySendKexInitAsync(CancellationToken CancellationToken)
        {

            lock (kexGate)
            {

                if (outputClosed || KeyExchangeCount == 0 || peerKexInitSeen || pendingLocalKexInit is not null)
                    return false;

                // Before the write, not after: the reader may take the exchange over the moment the
                // peer's KEXINIT is in, and from our KEXINIT on, nothing else may go out.
                HoldSends();

            }

            var kexInit = LocalKexInit().Encode();

            try
            {
                await WritePacketAsync(kexInit, CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (kexGate)
                    ReleaseHeldSends();
                throw;
            }

            lock (kexGate)
            {

                pendingLocalKexInit = kexInit;

                // The peer's KEXINIT was read while ours was being written: the reader is already waiting
                // for sendLock to run the exchange, and finds ours there once it has it.
                return !peerKexInitSeen;

            }

        }

        #endregion

        #region (private) LocalKexInit()

        // A server offers only what its host keys can sign: at first every key's algorithms, in the order
        // the keys were given, and once a key is chosen only that key's - a rekey must not swap the host
        // key under the client (OpenSSH's client drops the connection if it does).
        private KexInitMessage LocalKexInit()
            => KexInitMessage.CreateLocal(isServer, ciphers, macs, keyExchanges,
                                          isServer ? OfferedHostKeyAlgorithms() : hostKeyAlgorithms,
                                          Initial: KeyExchangeCount == 0);

        private String[] OfferedHostKeyAlgorithms()
            => hostKey is not null
                   ? [.. hostKey.AlgorithmNames]
                   : [.. hostKeys!.SelectMany(key => key.AlgorithmNames).Distinct(StringComparer.Ordinal)];

        #endregion

        #region (private) SelectHostKey(Algorithm)

        // The first key that signs the negotiated algorithm: two keys of the same kind (an old and a new
        // Ed25519 key during a rotation) are told apart by their order, as in sshd_config.
        private ISshHostKey SelectHostKey(String Algorithm)

            => hostKeys!.FirstOrDefault(key => key.AlgorithmNames.Contains(Algorithm, StringComparer.Ordinal))
                   ?? throw new SshWireException($"No host key signs the negotiated algorithm '{Algorithm}'!");

        #endregion


        #region (private) PerformKeyExchangeAsync(IsInitial, PeerKexInit, CancellationToken)

        private async ValueTask PerformKeyExchangeAsync(Boolean            IsInitial,
                                                        Byte[]?            PeerKexInit,
                                                        CancellationToken  CancellationToken)
        {

            // Marks the window in which strict KEX refuses SSH_MSG_IGNORE / DEBUG / UNIMPLEMENTED
            // instead of skipping them (see ReceivePacketAsync).
            keyExchangeInProgress = true;

            var completed = false;

            try
            {

            // 1. Send our KEXINIT - unless an exchange we started has sent it already. Asked under
            //    sendLock: a sender may be writing ours this very moment, and sets it once it has. From
            //    here to our NEWKEYS, what others send is held (see the field's remark).
            KexInitMessage localKexInit;
            Byte[]         iLocal;

            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                Byte[]? alreadySent;

                lock (kexGate)
                {
                    alreadySent = pendingLocalKexInit;
                    HoldSends();
                }

                if (alreadySent is not null)
                {
                    iLocal        = alreadySent;
                    localKexInit  = KexInitMessage.Decode(iLocal);
                }
                else
                {
                    localKexInit  = LocalKexInit();
                    iLocal        = localKexInit.Encode();
                    await WritePacketAsync(iLocal, CancellationToken).ConfigureAwait(false);
                }

            }
            finally
            {
                sendLock.Release();
            }

            // 2. Obtain the peer's KEXINIT (already read on a peer-initiated rekey, else read it now).
            var iRemote = PeerKexInit ?? await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            if (iRemote.Length < 1 || iRemote[0] != (Byte) SshMessageNumber.KexInit)
                throw new SshWireException($"Expected SSH_MSG_KEXINIT (20) to start the key exchange, but found message number {(iRemote.Length > 0 ? iRemote[0] : -1)}!");

            var remoteKexInit = KexInitMessage.Decode(iRemote);

            // I_C is always the client's KEXINIT, I_S the server's.
            var (iC, iS)   = isServer ? (iRemote, iLocal) : (iLocal, iRemote);
            var clientInit = isServer ? remoteKexInit : localKexInit;
            var serverInit = isServer ? localKexInit  : remoteKexInit;

            var negotiated = AlgorithmNegotiation.Negotiate(clientInit, serverInit, WeAreServer: isServer);
            SshKexCore.EnsureSupported(negotiated);

            // Strict KEX is agreed by the first exchange and holds for the connection. OpenSSH's PROTOCOL
            // calls its marker "only valid in the initial SSH2_MSG_KEXINIT", to be ignored after, and
            // OpenSSH sends it only there - so negotiated afresh on a rekey with OpenSSH it came out
            // false: we stopped resetting the sequence numbers after NEWKEYS while OpenSSH went on
            // resetting them, and our first packet under the new keys was "Connection corrupted". Our
            // own two ends never noticed, since we repeat the marker in every KEXINIT.
            if (!IsInitial)
                negotiated = negotiated with { StrictKex = algorithms.StrictKex };

            // RFC 4253 §7.1: a peer may append a *guessed* first key-exchange packet to its KEXINIT. If the
            // guess turns out wrong it must be read and discarded, otherwise the next read would parse a
            // packet meant for a different algorithm. Dropbear guesses on every connection, so without
            // this its clients only ever interoperate on whichever method they happen to prefer.
            if (remoteKexInit.FirstKexPacketFollows &&
                !KexInitMessage.GuessWasCorrect(remoteKexInit, negotiated.KeyExchange, negotiated.HostKey, negotiated.KexGuess2))
            {
                await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            }

            // 3. The method-specific exchange (client sends INIT, server replies) — shared with the
            //    initial handshake. e/f (classic DH) or Q_C/Q_S (ECDH) are carried transparently.
            using var kex = SshKeyExchange.Create(negotiated.KeyExchange);

            Byte[] kS, h, kEncoded;

            if (isServer)
            {

                var initPayload      = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var qC               = SshKexCore.ParseEcdhInit(initPayload);
                var (qS, rawSecret)  = kex.ServerRespond(qC);

                kEncoded        = kex.EncodeSharedSecret(rawSecret);
                // The client picked the algorithm, and with it the key: the first exchange fixes it for
                // the connection, a rekey offered nothing else (LocalKexInit).
                hostKey       ??= SelectHostKey(negotiated.HostKey);

                kS              = hostKey.PublicKeyBlob;
                h               = ExchangeHashCalc.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);

                var signature   = hostKey.Sign(negotiated.HostKey, h);
                await WriteExchangePacketAsync(SshKexCore.BuildEcdhReply(kS, qS, signature), CancellationToken).ConfigureAwait(false);

            }
            else
            {

                var qC          = kex.StartClient();
                await WriteExchangePacketAsync(SshKexCore.BuildEcdhInit(qC), CancellationToken).ConfigureAwait(false);

                var reply                   = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var (kServer, qS, sigBlob)  = SshKexCore.ParseEcdhReply(reply);
                kS                          = kServer;

                var rawSecret   = kex.ClientFinish(qS);
                kEncoded        = kex.EncodeSharedSecret(rawSecret);
                h               = ExchangeHashCalc.Compute(kex.HashAlgorithm, vC, vS, iC, iS, kS, qC, qS, kEncoded);

                if (!SshSignature.Verify(kS, h, sigBlob))
                    throw new SshWireException("The server's host-key signature over the exchange hash is invalid!");

                // The host key was established by the first exchange; a rekey that presents another one
                // is refused rather than put to the policy again - as OpenSSH's client does.
                if (!IsInitial && !kS.AsSpan().SequenceEqual(serverHostKey))
                    throw new SshWireException("The server presented a different host key in a rekey than in the initial key exchange!");

                // The signature above only proves the peer holds the private half of the key it just
                // presented — possession, not identity. Without a verifier there is nothing binding that
                // key to the host we meant to reach, so a missing one must fail closed: silently
                // accepting would leave every connection open to a machine-in-the-middle.
                if (verifyHostKey is null)
                    throw new SshWireException("No host-key verification was supplied, so the server's identity cannot be established. "
                                               + "Pass a verifier (e.g. HostKeyPolicy.ForHost(host, port)), or SshHostKeyVerification.AcceptAnyUnsafe to skip it deliberately.");

                if (!verifyHostKey(kS))
                    throw new SshWireException("The server's host key was rejected by the host-key policy.");

            }

            // 4. The session id is fixed by the FIRST exchange and reused as the KDF salt for rekeys.
            if (IsInitial)
                sessionId = h;

            var derive = SshKexCore.MakeDeriver(kex.HashAlgorithm, kEncoded, h, sessionId);

            // Directional keys: we send in our own direction and receive in the peer's.
            var (newSendCipher, newSendMac) = isServer
                ? SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient)
                : SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer);

            var (newReceiveCipher, newReceiveMac) = isServer
                ? SshKexCore.BuildDirection(negotiated.CipherClientToServer, negotiated.MacClientToServer, derive, Kdf.KeyLetter.EncryptionKeyClientToServer, Kdf.KeyLetter.InitialIVClientToServer, Kdf.KeyLetter.IntegrityKeyClientToServer)
                : SshKexCore.BuildDirection(negotiated.CipherServerToClient, negotiated.MacServerToClient, derive, Kdf.KeyLetter.EncryptionKeyServerToClient, Kdf.KeyLetter.InitialIVServerToClient, Kdf.KeyLetter.IntegrityKeyServerToClient);

            // 5. NEWKEYS: our own is the last packet under the old send keys; the peer's is the last
            //    under the old receive keys. Swap each direction independently, right after the boundary.
            var newKeysMessage = new Byte[] { (Byte) SshMessageNumber.NewKeys };

            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                await WritePacketAsync(newKeysMessage, CancellationToken).ConfigureAwait(false);
                SwapSend(newSendCipher, newSendMac, negotiated.StrictKex);
            }
            finally
            {
                sendLock.Release();
            }

            var newKeys = await ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            if (newKeys.Length < 1 || newKeys[0] != (Byte) SshMessageNumber.NewKeys)
                throw new SshWireException(
                          $"Expected SSH_MSG_NEWKEYS (21) to complete the key exchange, but found " +
                          $"{(newKeys.Length > 0 ? ((SshMessageNumber) newKeys[0]).ToString() : "an empty packet")}. " +
                          $"Negotiated {negotiated.KeyExchange} / {negotiated.HostKey}; the peer listed " +
                          $"{(remoteKexInit.KexAlgorithms.Length > 0 ? remoteKexInit.KexAlgorithms[0] : "-")} / " +
                          $"{(remoteKexInit.ServerHostKeyAlgorithms.Length > 0 ? remoteKexInit.ServerHostKeyAlgorithms[0] : "-")} first " +
                          $"and {(remoteKexInit.FirstKexPacketFollows ? "did" : "did not")} guess. " +
                          $"A peer rejecting our signature here means the two sides computed different exchange hashes.");
            SwapReceive(newReceiveCipher, newReceiveMac, negotiated.StrictKex);

            algorithms     = negotiated;
            exchangeHash   = h;
            serverHostKey  = kS;
            KeyExchangeCount++;

            // Fresh keys: the limits count from here.
            Interlocked.Exchange(ref bytesSentSinceKex,       0);
            Interlocked.Exchange(ref bytesReceivedSinceKex,   0);
            Interlocked.Exchange(ref packetsSentSinceKex,     0);
            Interlocked.Exchange(ref packetsReceivedSinceKex, 0);
            Interlocked.Exchange(ref lastKeyExchange,         TimeProvider.System.GetTimestamp());

            completed = true;

            // 6. What was sent during the exchange goes out now, under the new keys - after
            //    SSH_MSG_EXT_INFO, which RFC 8308 §3.1 puts right after the server's first NEWKEYS
            //    (server-sig-algs; not repeated on rekeys).
            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {

                if (IsInitial && isServer && negotiated.ExtensionInfo)
                {
                    var extInfo = ExtInfoMessage.ForServerSigAlgs(serverSignatureAlgorithms);
                    await WritePacketAsync(extInfo.Encode(), CancellationToken).ConfigureAwait(false);
                }

                await FlushHeldSendsAsync(CancellationToken).ConfigureAwait(false);

            }
            finally
            {
                sendLock.Release();
            }

            }
            finally
            {

                keyExchangeInProgress = false;

                lock (kexGate)
                {

                    pendingLocalKexInit  = null;
                    peerKexInitSeen      = false;

                    // A failed exchange: what it held can not be sent, and nothing may wait for it.
                    if (sendsHeld)
                        ReleaseHeldSends();

                }

            }

            if (completed && !IsInitial && Rekeyed is { } rekeyed)
            {
                try   { rekeyed(KeyExchangeCount); }
                catch { }
            }

        }

        #endregion

        #region TryHandleExtInfo(Payload)

        /// <summary>
        /// If <paramref name="Payload"/> is an SSH_MSG_EXT_INFO message, parse it and record the peer's
        /// extensions (e.g. "server-sig-algs"), returning true; otherwise leave state untouched and
        /// return false. A receive loop calls this on each packet so extension state stays transport-side.
        /// </summary>
        public Boolean TryHandleExtInfo(ReadOnlySpan<Byte> Payload)
        {

            if (Payload.Length < 1 || Payload[0] != (Byte) SshMessageNumber.ExtInfo)
                return false;

            var extInfo = ExtInfoMessage.Decode(Payload);

            foreach (var extension in extInfo.Extensions)
                peerExtensions[extension.Key] = extension.Value;

            return true;

        }

        #endregion

        #region (private) SwapSend / SwapReceive

        private void SwapSend(SshTransportCipher NewCipher, ISshMac? NewMac, Boolean StrictKex)
        {

            var oldCipher = sendCipher;
            var oldMac    = sendMac;

            sendCipher = NewCipher;
            sendMac    = NewMac;

            // Strict-KEX resets the sequence number to zero after every NEWKEYS (initial and subsequent).
            if (StrictKex)
                sendSequenceNumber = 0;

            DisposeCipher(oldCipher);
            oldMac?.Dispose();

        }

        private void SwapReceive(SshTransportCipher NewCipher, ISshMac? NewMac, Boolean StrictKex)
        {

            var oldCipher = receiveCipher;
            var oldMac    = receiveMac;

            receiveCipher = NewCipher;
            receiveMac    = NewMac;

            if (StrictKex)
                receiveSequenceNumber = 0;

            DisposeCipher(oldCipher);
            oldMac?.Dispose();

        }

        // Never dispose the shared "none" singleton (used before the first NEWKEYS).
        private static void DisposeCipher(SshTransportCipher Cipher)
        {
            if (!ReferenceEquals(Cipher, NullTransportCipher.Instance))
                Cipher.Dispose();
        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose the current directional ciphers and MACs, wiping their key material.
        /// </summary>
        public void Dispose()
        {
            DisposeCipher(sendCipher);
            DisposeCipher(receiveCipher);
            sendMac?.Dispose();
            receiveMac?.Dispose();
            sendLock.Dispose();
        }

        #endregion

    }

}
