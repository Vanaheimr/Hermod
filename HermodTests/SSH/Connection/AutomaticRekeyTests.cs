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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Automatic rekeying (RFC 4253 §9): with <see cref="SshRekeyLimits"/> set, a connection renews its
    /// keys by itself once a direction has carried the data volume or the interval is up - while its
    /// channels keep moving data, and without either side losing or reordering a byte.
    ///
    /// <para>
    /// Before this, the keys of a connection were the ones of its handshake for as long as it lived:
    /// <see cref="SshTransport.RekeyAsync"/> existed, but only for a transport nobody else was reading,
    /// and nothing ever called it.
    /// </para>
    /// </summary>
    [TestFixture]
    public class AutomaticRekeyTests
    {

        private const Int32 Megabyte = 1024 * 1024;

        private static SshRekeyLimits Every64KiB { get; } = new () { DataVolume = 64 * 1024, Interval = null };


        #region (private) Connected(ClientLimits, ServerLimits, CancellationToken)

        private sealed record Connected(SshTransport           ClientTransport,
                                        SshTransport           ServerTransport,
                                        SshChannelMultiplexer  ClientMux,
                                        SshChannelMultiplexer  ServerMux,
                                        SshMuxChannel          ClientChannel,
                                        SshMuxChannel          ServerChannel) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await ClientMux.DisposeAsync();
                await ServerMux.DisposeAsync();
                ClientTransport.Dispose();
                ServerTransport.Dispose();
            }
        }

        /// <summary>
        /// Our client and server over an in-memory pipe, each with its own rekey limits, and one session
        /// channel open between them.
        /// </summary>
        private static async Task<Connected> Connect(SshRekeyLimits?    ClientLimits,
                                                     SshRekeyLimits?    ServerLimits,
                                                     CancellationToken  CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            var clientTransport = await clientTask;
            var serverTransport = await serverTask;

            var clientMux = new SshChannelMultiplexer(clientTransport) { RekeyLimits = ClientLimits }.Start();
            var serverMux = new SshChannelMultiplexer(serverTransport) { RekeyLimits = ServerLimits }.Start();

            var accepting     = serverMux.AcceptChannelAsync(CancellationToken);
            var clientChannel = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var serverChannel = await accepting;

            return new Connected(clientTransport, serverTransport, clientMux, serverMux, clientChannel, serverChannel);

        }

        #endregion

        #region (private) Send / Receive

        /// <summary>
        /// Send the bytes and the channel's EOF.
        /// </summary>
        private static async Task Send(SshMuxChannel Channel, Byte[] Data, CancellationToken CancellationToken)
        {
            await Channel.SendDataAsync(Data, CancellationToken);
            await Channel.SendEofAsync(CancellationToken);
        }

        /// <summary>
        /// Everything up to the peer's EOF.
        /// </summary>
        private static async Task<Byte[]> Receive(SshMuxChannel Channel, CancellationToken CancellationToken)
        {
            using var received = new MemoryStream();
            await Channel.Input.CopyToAsync(received, CancellationToken);
            return received.ToArray();
        }

        #endregion


        #region TheSendingSide_RenewsItsKeys_AfterTheDataVolume

        /// <summary>
        /// A client limited to 64 KiB that sends a megabyte rekeys again and again on its own sends - and
        /// the server, which has no limit, follows every one of them.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheSendingSide_RenewsItsKeys_AfterTheDataVolume(CancellationToken CancellationToken)
        {

            await using var connected = await Connect(Every64KiB, null, CancellationToken);

            var data      = RandomNumberGenerator.GetBytes(Megabyte);
            var receiving = Receive(connected.ServerChannel, CancellationToken);

            await Send(connected.ClientChannel, data, CancellationToken);

            await receiving;

            Assert.Multiple(() => {
                Assert.That(receiving.Result,                                 Is.EqualTo(data), "every byte, in order, across the rekeys");
                Assert.That(connected.ClientTransport.KeyExchangeCount,     Is.GreaterThanOrEqualTo(8), "a megabyte at 64 KiB a key is many rekeys");
                Assert.That(connected.ServerTransport.KeyExchangeCount,     Is.EqualTo(connected.ClientTransport.KeyExchangeCount));
            });

        }

        #endregion

        #region TheReceivingSide_RenewsItsKeys_WhileThePeerKeepsSending

        /// <summary>
        /// The server is the one limited, and receives the megabyte: it starts every rekey from its window
        /// adjusts while the client is still sending - so what the client sent before it saw our KEXINIT
        /// arrives in the middle of the exchange, and must be delivered after it, not dropped or refused.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task TheReceivingSide_RenewsItsKeys_WhileThePeerKeepsSending(CancellationToken CancellationToken)
        {

            await using var connected = await Connect(null, Every64KiB, CancellationToken);

            var data      = RandomNumberGenerator.GetBytes(Megabyte);
            var receiving = Receive(connected.ServerChannel, CancellationToken);

            await Send(connected.ClientChannel, data, CancellationToken);

            await receiving;

            Assert.Multiple(() => {
                Assert.That(receiving.Result,                                 Is.EqualTo(data), "every byte, in order, across the rekeys");
                Assert.That(connected.ServerTransport.KeyExchangeCount,     Is.GreaterThanOrEqualTo(8));
                Assert.That(connected.ClientTransport.KeyExchangeCount,     Is.EqualTo(connected.ServerTransport.KeyExchangeCount));
            });

        }

        #endregion

        #region BothSides_RenewTheirKeys_WhileDataFlowsBothWays

        /// <summary>
        /// Both limited, both sending at once: the two sides start rekeys of their own, and often at the
        /// same moment - each with its KEXINIT already on the wire when the other's arrives. That is one
        /// exchange, not two, and the data both ways survives it.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task BothSides_RenewTheirKeys_WhileDataFlowsBothWays(CancellationToken CancellationToken)
        {

            await using var connected = await Connect(Every64KiB, Every64KiB, CancellationToken);

            var up          = RandomNumberGenerator.GetBytes(Megabyte);
            var down        = RandomNumberGenerator.GetBytes(Megabyte);

            var serverGets  = Receive(connected.ServerChannel, CancellationToken);
            var clientGets  = Receive(connected.ClientChannel, CancellationToken);

            await Task.WhenAll(Send(connected.ClientChannel, up,   CancellationToken),
                               Send(connected.ServerChannel, down, CancellationToken));

            await Task.WhenAll(serverGets, clientGets);

            Assert.Multiple(() => {
                Assert.That(serverGets.Result,                                Is.EqualTo(up));
                Assert.That(clientGets.Result,                                Is.EqualTo(down));
                Assert.That(connected.ClientTransport.KeyExchangeCount,     Is.GreaterThanOrEqualTo(8));
                Assert.That(connected.ServerTransport.KeyExchangeCount,     Is.EqualTo(connected.ClientTransport.KeyExchangeCount));
            });

        }

        #endregion

        #region WhatThePeerSendsBeforeItSeesOurKexInit_IsAnsweredAfterTheExchange

        /// <summary>
        /// The order that would deadlock: our server has sent its KEXINIT, and the client - not having read
        /// it yet - opens a channel. Answered at once, the confirmation would wait for the send lock the
        /// exchange holds, on the very task that has to read the client's KEXINIT to end the exchange. The
        /// open is held back instead, and confirmed under the new keys.
        ///
        /// <para>
        /// The client here is a bare transport, driven by hand, so the order is the test's and not the
        /// scheduler's.
        /// </para>
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task WhatThePeerSendsBeforeItSeesOurKexInit_IsAnsweredAfterTheExchange(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            if (client.Algorithms.ExtensionInfo)
                Assert.That(client.TryHandleExtInfo(await client.ReceivePacketAsync(CancellationToken)), Is.True);

            await using var serverMux = new SshChannelMultiplexer(server) { RekeyLimits = null }.Start();

            Assert.That(await server.StartRekeyAsync(CancellationToken), Is.True, "the server starts a rekey");

            // Sent before the client reads anything - so before it has seen the server's KEXINIT.
            await client.SendPacketAsync(SshChannelWire.ChannelOpen("session", 7, 65536, 32768, []), CancellationToken);

            var serverKexInit = await client.ReceivePacketAsync(CancellationToken);
            Assert.That(serverKexInit[0], Is.EqualTo((Byte) SshMessageNumber.KexInit));

            await client.RespondToRekeyAsync(serverKexInit, CancellationToken);

            var confirmation = await client.ReceivePacketAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(confirmation[0],         Is.EqualTo((Byte) SshMessageNumber.ChannelOpenConfirmation), "the open is answered - after the exchange");
                Assert.That(client.KeyExchangeCount, Is.EqualTo(2));
                Assert.That(server.KeyExchangeCount, Is.EqualTo(2));
            });

        }

        #endregion

        #region AQuietConnection_RenewsItsKeys_AfterTheInterval

        /// <summary>
        /// Time is a limit of its own: a connection on which nothing is sent still gets new keys once the
        /// interval is up - and the channel works afterwards.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task AQuietConnection_RenewsItsKeys_AfterTheInterval(CancellationToken CancellationToken)
        {

            await using var connected = await Connect(new SshRekeyLimits { Interval = TimeSpan.FromMilliseconds(300) }, null, CancellationToken);

            var sessionId = (Byte[]) connected.ClientTransport.SessionId.Clone();

            while (connected.ServerTransport.KeyExchangeCount < 3)
                await Task.Delay(50, CancellationToken);

            var data      = RandomNumberGenerator.GetBytes(10_000);
            var receiving = Receive(connected.ServerChannel, CancellationToken);
            await Send(connected.ClientChannel, data, CancellationToken);

            await receiving;

            Assert.Multiple(() => {
                Assert.That(receiving.Result,                         Is.EqualTo(data), "the channel still works after the rekeys");
                Assert.That(connected.ClientTransport.SessionId,     Is.EqualTo(sessionId), "the session id is the first exchange's for good");
            });

        }

        #endregion

        #region WithoutLimits_TheKeysOfTheHandshakeStay

        /// <summary>
        /// The control: the same megabyte with no limits on either side leaves the handshake's keys in
        /// place - so the rekeys above are the limits' doing.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task WithoutLimits_TheKeysOfTheHandshakeStay(CancellationToken CancellationToken)
        {

            await using var connected = await Connect(null, null, CancellationToken);

            var data      = RandomNumberGenerator.GetBytes(Megabyte);
            var receiving = Receive(connected.ServerChannel, CancellationToken);

            await Send(connected.ClientChannel, data, CancellationToken);

            await receiving;

            Assert.Multiple(() => {
                Assert.That(receiving.Result,                             Is.EqualTo(data));
                Assert.That(connected.ClientTransport.KeyExchangeCount, Is.EqualTo(1));
                Assert.That(connected.ServerTransport.KeyExchangeCount, Is.EqualTo(1));
            });

        }

        #endregion

        #region TheLimits_RefuseNonsense

        [Test]
        public void TheLimits_RefuseNonsense()
        {
            Assert.Multiple(() => {
                Assert.That(() => new SshRekeyLimits { DataVolume = 0 },                Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => new SshRekeyLimits { Interval   = TimeSpan.Zero },    Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(SshRekeyLimits.Default.DataVolume,                          Is.EqualTo(1L << 30));
                Assert.That(SshRekeyLimits.Default.Interval,                            Is.EqualTo(TimeSpan.FromHours(1)));
            });
        }

        #endregion

    }

}
