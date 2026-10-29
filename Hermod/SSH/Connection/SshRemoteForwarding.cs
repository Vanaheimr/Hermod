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
using System.Collections.Concurrent;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// Remote (<c>ssh -R</c>) port forwarding over the connection multiplexer — the feature that needs true
    /// channel multiplexing (a listener plus one <c>forwarded-tcpip</c> channel per accepted connection, all
    /// concurrent with the rest of the session). The client asks the server to listen with a
    /// <c>tcpip-forward</c> global request and relays each resulting channel to a local target; the server
    /// (gated by a <see cref="ForwardingPolicy"/>) listens and opens a <c>forwarded-tcpip</c> channel back per
    /// inbound connection.
    /// </summary>
    public static class SshRemoteForwarding
    {

        private const Int32 RelayBuffer = 32 * 1024;

        #region ServeRemoteForwards(Mux, Policy, CancellationToken)

        /// <summary>
        /// Wire the server side: answer <c>tcpip-forward</c> requests (gated by <paramref name="Policy"/>) by
        /// binding a TCP listener and opening a <c>forwarded-tcpip</c> channel back to the client per inbound
        /// connection, relaying the bytes, and <c>cancel-tcpip-forward</c> by closing that listener again.
        /// Wiring is synchronous; the listeners run in the background until they are cancelled or
        /// <paramref name="CancellationToken"/> ends the connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The bind address is taken the way OpenSSH clients send it, not only as an IP literal: a plain
        /// <c>ssh -R 8080:host:80</c> asks for <c>"localhost"</c>, which binds the IPv4 and IPv6 loopback
        /// addresses, and <c>""</c> or <c>"*"</c> ask for every address (RFC 4254 §7.1), which the policy
        /// then has to permit as the wildcard address. Any other name is refused rather than resolved:
        /// what a name points to is not the client's to decide on the server.
        /// </para>
        /// <para>
        /// Port 0 asks the server to choose. The reply carries the port it bound (RFC 4254 §7.1), and every
        /// <c>forwarded-tcpip</c> channel names that port together with the address exactly as the client
        /// spelled it — OpenSSH accepts only channels that match a forward it asked for.
        /// </para>
        /// <para>
        /// Installed as the multiplexer's <see cref="SshChannelMultiplexer.GlobalRequestResponder"/>, since
        /// the reply to port 0 carries data. Every other request is passed on to the responder that was
        /// installed before, so this composes with the host-key proofs in either order.
        /// </para>
        /// </remarks>
        public static void ServeRemoteForwards(SshChannelMultiplexer Mux, ForwardingPolicy Policy, CancellationToken CancellationToken = default)
        {

            var forwards = new ConcurrentDictionary<(String Address, UInt16 Port), RemoteForwardListener>();
            var previous = Mux.GlobalRequestResponder;

            // A connection that ends takes its listeners with it.
            CancellationToken.Register(() => {
                foreach (var forward in forwards.Values)
                    forward.Stop();
            });

            Mux.GlobalRequestResponder = info => info.Name switch {
                "tcpip-forward"         => ValueTask.FromResult<SshGlobalRequestReply?>(Listen(Mux, Policy, forwards, info.Data, CancellationToken)),
                "cancel-tcpip-forward"  => ValueTask.FromResult<SshGlobalRequestReply?>(Cancel(forwards, info.Data)),
                _                       => previous?.Invoke(info) ?? ValueTask.FromResult<SshGlobalRequestReply?>(null)
            };

        }

        #endregion

        #region (private) Listen / Cancel

        private static SshGlobalRequestReply Listen(SshChannelMultiplexer                                                   Mux,
                                                    ForwardingPolicy                                                        Policy,
                                                    ConcurrentDictionary<(String Address, UInt16 Port), RemoteForwardListener>  Forwards,
                                                    Byte[]                                                                  Data,
                                                    CancellationToken                                                       CancellationToken)
        {

            if (!TryReadAddressAndPort(Data, out var bindAddress, out var requestedPort) || Policy.TcpIpForward is null)
                return SshGlobalRequestReply.Failed;

            var addresses = BindAddresses(bindAddress);
            if (addresses.Count == 0)
                return SshGlobalRequestReply.Failed;

            // A named port is checked before anything is bound; a chosen one can only be checked after.
            if (requestedPort != 0 && !addresses.All(address => Policy.TcpIpForward.Allows(address, requestedPort)))
                return SshGlobalRequestReply.Failed;

            // "localhost" and the wildcard are two addresses. Bind what can be bound, on one port: the first
            // listener decides the port when the client left it to us, the others follow it. One address
            // that cannot be bound (say, IPv6 switched off) does not void the rest, as with sshd.
            var listeners = new List<TcpListener>();
            var port      = requestedPort;

            foreach (var address in addresses)
            {

                var listener = new TcpListener(address, port);

                try
                {
                    listener.Start();
                }
                catch (SocketException)
                {
                    continue;
                }

                listeners.Add(listener);

                if (port == 0)
                    port = (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            }

            var forward = new RemoteForwardListener(listeners, CancellationToken);

            if (listeners.Count == 0                                                                                        ||
                !listeners.All(listener => Policy.TcpIpForward.Allows(((System.Net.IPEndPoint) listener.LocalEndpoint).Address, port)) ||
                !Forwards.TryAdd((bindAddress, port), forward))
            {
                forward.Stop();
                return SshGlobalRequestReply.Failed;
            }

            foreach (var listener in listeners)
                _ = AcceptLoopAsync(Mux, listener, bindAddress, port, forward.Token);

            if (requestedPort != 0)
                return SshGlobalRequestReply.Ok;

            var abw = new ArrayBufferWriter<Byte>();
            new SshPacketWriter(abw).WriteUInt32(port);
            return SshGlobalRequestReply.WithData(abw.WrittenSpan.ToArray());

        }

        private static SshGlobalRequestReply Cancel(ConcurrentDictionary<(String Address, UInt16 Port), RemoteForwardListener>  Forwards,
                                                    Byte[]                                                                  Data)
        {

            if (!TryReadAddressAndPort(Data, out var bindAddress, out var port) ||
                !Forwards.TryRemove((bindAddress, port), out var forward))
                return SshGlobalRequestReply.Failed;

            forward.Stop();
            return SshGlobalRequestReply.Ok;

        }

        private static Boolean TryReadAddressAndPort(Byte[] Data, out String Address, out UInt16 Port)
        {

            Address = "";
            Port    = 0;

            try
            {

                var reader = new SshPacketReader(Data);
                Address    = reader.ReadString();
                var port   = reader.ReadUInt32();

                if (port > UInt16.MaxValue)
                    return false;

                Port = (UInt16) port;
                return true;

            }
            catch (SshWireException)
            {
                return false;
            }

        }

        private static IReadOnlyList<IPAddress> BindAddresses(String BindAddress)
        {

            if (BindAddress.Length == 0 || BindAddress == "*")
                return Socket.OSSupportsIPv6 ? [ IPAddress.Any, IPAddress.IPv6Any ] : [ IPAddress.Any ];

            if (String.Equals(BindAddress, "localhost", StringComparison.OrdinalIgnoreCase))
                return Socket.OSSupportsIPv6 ? [ IPAddress.Loopback, IPAddress.IPv6Loopback ] : [ IPAddress.Loopback ];

            return IPAddress.TryParse(BindAddress, out var address)
                       ? [ address ]
                       : [];

        }

        private static async Task AcceptLoopAsync(SshChannelMultiplexer Mux, TcpListener Listener, String BindAddress, UInt16 Port, CancellationToken CancellationToken)
        {
            try
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    var socket = await Listener.AcceptTcpClientAsync(CancellationToken).ConfigureAwait(false);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var origin = (System.Net.IPEndPoint) socket.Client.RemoteEndPoint!;
                            var ch     = await Mux.OpenChannelAsync("forwarded-tcpip",
                                                                   ForwardedTcpIp(BindAddress, Port, origin.Address.ToString(), (UInt16) origin.Port),
                                                                   CancellationToken).ConfigureAwait(false);
                            await RelayAsync(ch, new NetworkStream(socket.Client, ownsSocket: true), CancellationToken).ConfigureAwait(false);
                        }
                        catch { socket.Dispose(); }
                    }, CancellationToken);
                }
            }
            catch { }
            finally { Listener.Stop(); }
        }

        #endregion

        #region RequestRemoteForwardAsync(Mux, BindHost, BindPort, LocalHost, LocalPort)

        /// <summary>
        /// Request the client side: ask the server to listen on <paramref name="BindHost"/>:
        /// <paramref name="BindPort"/> and relay every resulting <c>forwarded-tcpip</c> channel to
        /// <paramref name="LocalHost"/>:<paramref name="LocalPort"/>. Returns a handle whose disposal stops
        /// accepting.
        /// </summary>
        public static async ValueTask<IAsyncDisposable> RequestRemoteForwardAsync(SshChannelMultiplexer  Mux,
                                                                                  String                 BindHost,
                                                                                  UInt16                 BindPort,
                                                                                  String                 LocalHost,
                                                                                  UInt16                 LocalPort,
                                                                                  CancellationToken      CancellationToken = default)
        {

            Mux.ChannelAcceptor = info => ValueTask.FromResult(info.ChannelType == "forwarded-tcpip");

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(BindHost); w.WriteUInt32(BindPort);
            if (!await Mux.SendGlobalRequestAsync("tcpip-forward", true, abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false))
                throw new SshForwardingException($"The server refused the remote forward on {BindHost}:{BindPort}.");

            var cts  = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var loop = Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        var ch = await Mux.AcceptChannelAsync(cts.Token);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                                await socket.ConnectAsync(LocalHost, LocalPort, cts.Token);
                                await RelayAsync(ch, new NetworkStream(socket, ownsSocket: true), cts.Token);
                            }
                            catch { try { await ch.CloseAsync(); } catch { } }
                        }, cts.Token);
                    }
                }
                catch { }
            }, cts.Token);

            return new RemoteForwardHandle(cts, loop);

        }

        #endregion


        #region (private) relay

        private static async Task RelayAsync(SshMuxChannel Channel, Stream Target, CancellationToken CancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var toTarget  = ChannelToTargetAsync(Channel, Target, cts.Token);
            var toChannel = TargetToChannelAsync(Target, Channel, cts.Token);
            await Task.WhenAny(toTarget, toChannel).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);
            try { await Task.WhenAll(toTarget, toChannel).ConfigureAwait(false); } catch { }
            try { await Channel.CloseAsync().ConfigureAwait(false); } catch { }
            await Target.DisposeAsync().ConfigureAwait(false);
        }

        private static async Task ChannelToTargetAsync(SshMuxChannel Channel, Stream Target, CancellationToken CancellationToken)
        {
            var buffer = new Byte[RelayBuffer];
            var input  = Channel.Input;
            try
            {
                Int32 read;
                while ((read = await input.ReadAsync(buffer, CancellationToken).ConfigureAwait(false)) > 0)
                {
                    await Target.WriteAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                    await Target.FlushAsync(CancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SshChannelClosedException) { }
        }

        private static async Task TargetToChannelAsync(Stream Target, SshMuxChannel Channel, CancellationToken CancellationToken)
        {
            var buffer = new Byte[RelayBuffer];
            try
            {
                Int32 read;
                while ((read = await Target.ReadAsync(buffer, CancellationToken).ConfigureAwait(false)) > 0)
                    await Channel.SendDataAsync(buffer.AsMemory(0, read), CancellationToken).ConfigureAwait(false);
                await Channel.SendEofAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private static Byte[] ForwardedTcpIp(String ConnectedAddress, UInt16 ConnectedPort, String OriginAddress, UInt16 OriginPort)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(ConnectedAddress); w.WriteUInt32(ConnectedPort);
            w.WriteString(OriginAddress);    w.WriteUInt32(OriginPort);
            return abw.WrittenSpan.ToArray();
        }

        private sealed class RemoteForwardListener
        {

            private readonly IReadOnlyList<TcpListener>  listeners;
            private readonly CancellationTokenSource     cts;

            public RemoteForwardListener(IReadOnlyList<TcpListener> Listeners, CancellationToken Connection)
            {
                listeners = Listeners;
                cts       = CancellationTokenSource.CreateLinkedTokenSource(Connection);
            }

            public CancellationToken Token => cts.Token;

            public void Stop()
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                foreach (var listener in listeners)
                    try { listener.Stop(); } catch { }
            }

        }

        private sealed class RemoteForwardHandle : IAsyncDisposable
        {
            private readonly CancellationTokenSource cts;
            private readonly Task loop;
            public RemoteForwardHandle(CancellationTokenSource Cts, Task Loop) { cts = Cts; loop = Loop; }
            public async ValueTask DisposeAsync()
            {
                await cts.CancelAsync().ConfigureAwait(false);
                try { await loop.ConfigureAwait(false); } catch { }
                cts.Dispose();
            }
        }

        #endregion

    }

}
