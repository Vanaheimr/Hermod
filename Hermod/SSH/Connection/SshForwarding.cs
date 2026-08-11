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
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>Resolves a forwarding target host name to the addresses that will actually be dialed.</summary>
    public delegate ValueTask<IReadOnlyList<IPAddress>> SshAddressResolver(String Host, CancellationToken CancellationToken);


    /// <summary>
    /// TCP/IP port forwarding over the SSH connection protocol (RFC 4254 §7). The client opens a
    /// <c>direct-tcpip</c> channel (the <c>ssh -L</c> / in-process tunnel primitive) as a plain
    /// <see cref="Stream"/>; the server accepts such channels, gates them against a
    /// <see cref="ForwardingPolicy"/> (DNS-rebinding safe — resolve once, dial exactly the checked
    /// addresses), and relays the bytes to the real destination. Denials answer
    /// <c>SSH_MSG_CHANNEL_OPEN_FAILURE (ADMINISTRATIVELY_PROHIBITED)</c> and leave the session healthy.
    /// </summary>
    public static class SshForwarding
    {

        #region Constants

        private const UInt32 InitialWindow  = 2 * 1024 * 1024;
        private const UInt32 MaxPacket      = 32 * 1024;

        private const UInt32 AdministrativelyProhibited = 1;
        private const UInt32 ConnectFailed              = 2;
        private const UInt32 UnknownChannelType         = 3;

        #endregion


        #region OpenTcpStreamAsync(Transport, Host, Port, CancellationToken)

        /// <summary>
        /// Open a <c>direct-tcpip</c> channel to <paramref name="Host"/>:<paramref name="Port"/> through the
        /// peer and return it as a plain <see cref="Stream"/> — an in-process tunnel with no local listener.
        /// </summary>
        public static async ValueTask<SshChannelStream> OpenTcpStreamAsync(SshTransport       Transport,
                                                                           String             Host,
                                                                           UInt16             Port,
                                                                           CancellationToken  CancellationToken = default)
        {

            const UInt32 localChannel = 0;

            await Transport.SendPacketAsync(BuildDirectTcpIpOpen(localChannel, Host, Port), CancellationToken).ConfigureAwait(false);

            while (true)
            {
                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                if (message == SshMessageNumber.ChannelOpenConfirmation)
                {
                    var reader = new SshPacketReader(payload);
                    reader.ReadByte();
                    reader.ReadUInt32();                      // our channel
                    var remoteChannel = reader.ReadUInt32();  // peer's channel
                    var remoteWindow  = reader.ReadUInt32();  // peer's initial window
                    return new SshChannelStream(new SshChannelDuplex(Transport, remoteChannel, remoteWindow));
                }

                if (message == SshMessageNumber.ChannelOpenFailure)
                {
                    var reader = new SshPacketReader(payload);
                    reader.ReadByte(); reader.ReadUInt32();
                    var reason = reader.ReadUInt32();
                    var text   = reader.ReadString();
                    throw new SshForwardingException($"The peer refused the direct-tcpip channel (reason {reason}): {text}");
                }
            }

        }

        #endregion

        #region ServeDirectTcpIpAsync(Transport, Policy, Resolver, CancellationToken)

        /// <summary>
        /// Serve <c>direct-tcpip</c> requests on this connection: accept a channel, gate its destination
        /// against <paramref name="Policy"/>, connect to the (already-resolved) address and relay the bytes.
        /// A denied destination is refused with ADMINISTRATIVELY_PROHIBITED and the loop keeps serving. The
        /// call returns once a permitted forward has been relayed to completion (single-forward per call).
        /// </summary>
        public static async ValueTask ServeDirectTcpIpAsync(SshTransport         Transport,
                                                            ForwardingPolicy     Policy,
                                                            SshAddressResolver?  Resolver           = null,
                                                            CancellationToken    CancellationToken   = default)
        {

            const UInt32 localChannel = 0;
            Resolver ??= DefaultResolver;

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = (SshMessageNumber) payload[0];

                if (message != SshMessageNumber.ChannelOpen)
                {
                    await HandleTransportTrafficAsync(Transport, payload, CancellationToken).ConfigureAwait(false);
                    continue;
                }

                var open = ParseChannelOpen(payload);
                if (open.ChannelType != "direct-tcpip")
                {
                    await Transport.SendPacketAsync(BuildChannelOpenFailure(open.SenderChannel, UnknownChannelType, "unknown channel type"), CancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Resolve once, gate every resolved address, then dial exactly those (no re-resolve).
                IReadOnlyList<IPAddress> addresses;
                try   { addresses = await Resolver(open.Host, CancellationToken).ConfigureAwait(false); }
                catch { addresses = []; }

                if (Policy.DirectTcpIp is null || !Policy.DirectTcpIp.AllowsAll(addresses, open.Port, open.Host))
                {
                    await Transport.SendPacketAsync(BuildChannelOpenFailure(open.SenderChannel, AdministrativelyProhibited, "administratively prohibited"), CancellationToken).ConfigureAwait(false);
                    continue;
                }

                Socket socket;
                try
                {
                    socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(new System.Net.IPEndPoint(addresses[0], open.Port), CancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await Transport.SendPacketAsync(BuildChannelOpenFailure(open.SenderChannel, ConnectFailed, "connect failed"), CancellationToken).ConfigureAwait(false);
                    continue;
                }

                await Transport.SendPacketAsync(BuildChannelOpenConfirmation(open.SenderChannel, localChannel, InitialWindow, MaxPacket), CancellationToken).ConfigureAwait(false);

                var channel       = new SshChannelDuplex(Transport, open.SenderChannel, open.InitialWindow);
                await using var channelStream = new SshChannelStream(channel);
                await using var targetStream  = new NetworkStream(socket, ownsSocket: true);

                await RelayAsync(channelStream, targetStream, CancellationToken).ConfigureAwait(false);
                return;

            }

        }

        #endregion


        #region (private) relay

        private static async Task RelayAsync(Stream A, Stream B, CancellationToken CancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var one = PumpAsync(A, B, cts.Token);
            var two = PumpAsync(B, A, cts.Token);
            await Task.WhenAny(one, two).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(one, two).ConfigureAwait(false); } catch { }
        }

        private static async Task PumpAsync(Stream From, Stream To, CancellationToken CancellationToken)
        {
            var buffer = new Byte[MaxPacket];
            try
            {
                while (true)
                {
                    var read = await From.ReadAsync(buffer, CancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        return;
                    await To.WriteAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                    await To.FlushAsync(CancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private static ValueTask<IReadOnlyList<IPAddress>> DefaultResolver(String Host, CancellationToken CancellationToken)
        {
            if (IPAddress.TryParse(Host, out var literal))
                return ValueTask.FromResult<IReadOnlyList<IPAddress>>([literal]);
            return ResolveViaDnsAsync(Host, CancellationToken);
        }

        private static async ValueTask<IReadOnlyList<IPAddress>> ResolveViaDnsAsync(String Host, CancellationToken CancellationToken)
            => await System.Net.Dns.GetHostAddressesAsync(Host, CancellationToken).ConfigureAwait(false);

        #endregion

        #region (private) transport traffic (PING/PONG, global requests)

        private static async ValueTask HandleTransportTrafficAsync(SshTransport Transport, Byte[] Payload, CancellationToken CancellationToken)
        {
            var message = (SshMessageNumber) Payload[0];

            if (message == SshMessageNumber.Ping)
            {
                var reader = new SshPacketReader(Payload);
                reader.ReadByte();
                var data = reader.ReadBinaryString();
                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteByte((Byte) SshMessageNumber.Pong);
                w.WriteBinaryString(data);
                await Transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
            }
            else if (message == SshMessageNumber.GlobalRequest)
            {
                var reader = new SshPacketReader(Payload);
                reader.ReadByte(); reader.ReadString();
                if (reader.ReadBoolean())
                {
                    var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                    w.WriteByte((Byte) SshMessageNumber.RequestFailure);
                    await Transport.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region (private) message builders / parsers

        private static Byte[] BuildDirectTcpIpOpen(UInt32 Sender, String Host, UInt16 Port)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpen);
            w.WriteString("direct-tcpip");
            w.WriteUInt32(Sender); w.WriteUInt32(InitialWindow); w.WriteUInt32(MaxPacket);
            w.WriteString(Host); w.WriteUInt32(Port);
            w.WriteString("127.0.0.1"); w.WriteUInt32(0);       // originator
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelOpenConfirmation(UInt32 Recipient, UInt32 Sender, UInt32 Window, UInt32 MaxPacketSize)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpenConfirmation);
            w.WriteUInt32(Recipient); w.WriteUInt32(Sender); w.WriteUInt32(Window); w.WriteUInt32(MaxPacketSize);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildChannelOpenFailure(UInt32 Recipient, UInt32 Reason, String Description)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpenFailure);
            w.WriteUInt32(Recipient); w.WriteUInt32(Reason); w.WriteString(Description); w.WriteString("");
            return abw.WrittenSpan.ToArray();
        }

        private readonly record struct DirectTcpIpOpen(String ChannelType, UInt32 SenderChannel, UInt32 InitialWindow, UInt32 MaxPacketSize, String Host, UInt16 Port);

        private static DirectTcpIpOpen ParseChannelOpen(ReadOnlySpan<Byte> Payload)
        {
            var reader = new SshPacketReader(Payload);
            reader.ReadByte();
            var type   = reader.ReadString();
            var sender = reader.ReadUInt32();
            var window = reader.ReadUInt32();
            var maxPkt = reader.ReadUInt32();
            var host   = "";
            UInt16 port = 0;
            if (type == "direct-tcpip")
            {
                host = reader.ReadString();
                port = (UInt16) reader.ReadUInt32();
            }
            return new DirectTcpIpOpen(type, sender, window, maxPkt, host, port);
        }

        #endregion

    }


    /// <summary>Thrown when a port-forwarding channel cannot be established.</summary>
    public sealed class SshForwardingException : Exception
    {
        /// <summary>Create a new forwarding exception.</summary>
        public SshForwardingException(String Message) : base(Message) { }
    }

}
