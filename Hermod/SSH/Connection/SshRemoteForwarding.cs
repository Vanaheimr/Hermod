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

        #region ServeRemoteForwardsAsync(Mux, Policy)

        /// <summary>
        /// Wire the server side: handle <c>tcpip-forward</c> requests (gated by <paramref name="Policy"/>) by
        /// binding a TCP listener and opening a <c>forwarded-tcpip</c> channel back to the client per inbound
        /// connection, relaying the bytes. Wiring is synchronous; the listeners run in the background.
        /// </summary>
        public static void ServeRemoteForwards(SshChannelMultiplexer Mux, ForwardingPolicy Policy, CancellationToken CancellationToken = default)
        {
            Mux.GlobalRequestHandler = info =>
            {
                if (info.Name != "tcpip-forward")
                    return ValueTask.FromResult(false);

                var reader   = new SshPacketReader(info.Data);
                var bindAddr = reader.ReadString();
                var bindPort = (UInt16) reader.ReadUInt32();

                if (Policy.TcpIpForward is null || !IPAddress.TryParse(bindAddr, out var ip) || !Policy.TcpIpForward.Allows(ip, bindPort))
                    return ValueTask.FromResult(false);

                TcpListener listener;
                try
                {
                    listener = new TcpListener(ip, bindPort);
                    listener.Start();
                }
                catch { return ValueTask.FromResult(false); }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!CancellationToken.IsCancellationRequested)
                        {
                            var socket = await listener.AcceptTcpClientAsync(CancellationToken);
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var origin = (System.Net.IPEndPoint) socket.Client.RemoteEndPoint!;
                                    var ch     = await Mux.OpenChannelAsync("forwarded-tcpip",
                                                                           ForwardedTcpIp(bindAddr, bindPort, origin.Address.ToString(), (UInt16) origin.Port),
                                                                           CancellationToken);
                                    await RelayAsync(ch, new NetworkStream(socket.Client, ownsSocket: true), CancellationToken);
                                }
                                catch { }
                            }, CancellationToken);
                        }
                    }
                    catch { }
                    finally { listener.Stop(); }
                }, CancellationToken);

                return ValueTask.FromResult(true);
            };
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
