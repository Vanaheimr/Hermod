/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Tunnels;

/// <summary>
/// Backpressure on an HTTP/3 tunnel, both ways, at <see cref="Http3Tunnel.HighWatermark"/>.
///
/// The tunnel queued what it received without a bound, as the pump took everything off the QUIC
/// stream whether the consumer read or not, and <see cref="Http3Tunnel.WriteAsync"/> completed at
/// once, into a queue without a bound either. A consumer that did not read, or a peer that did not,
/// let a tunnel grow for as long as the other side kept writing. Now the connection leaves what a
/// saturated tunnel would receive on the QUIC stream, so its window stays shut, and a write waits
/// while a watermark of bytes waits to go out.
/// </summary>
[TestFixture]
public class Http3TunnelBackpressureTests
{

    #region (helpers)

    private static readonly CancellationToken None = CancellationToken.None;

    public enum Direction { ClientToServer, ServerToClient }

    public enum Closer { Client, Server }

    private const int ChunkSize = 16 * 1024;

    /// <summary>
    /// A client and a server with a raw tunnel between them: the server accepts every Extended
    /// CONNECT and hands its end of the tunnel out, never reading it itself.
    /// </summary>
    private sealed class Pair : IDisposable
    {

        public required ServerCertificate      Certificate  { get; init; }
        public required Http3ClientConnection  Client       { get; init; }
        public required Http3ServerConnection  Server       { get; init; }
        public required Http3Tunnel            ClientTunnel { get; init; }
        public required Http3Tunnel            ServerTunnel { get; init; }

        /// <summary>
        /// One round of both connections, their timers included: a pump wakes on a timer as well
        /// as on traffic, and only a timer notices that a consumer has read and made room.
        /// </summary>
        public void Pump()
        {
            Client.CheckTimeouts();
            Server.CheckTimeouts();
            foreach (byte[] datagram in Client.GetDatagramsToSend())
                Server.ProcessDatagram(datagram);
            foreach (byte[] datagram in Server.GetDatagramsToSend())
                Client.ProcessDatagram(datagram);
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
            Certificate.Dispose();
        }

    }

    private static Pair OpenPair()
    {

        var          cert          = ServerCertificate.CreateSelfSigned("localhost");
        Http3Tunnel? serverTunnel  = null;

        var server = new Http3ServerConnection(cert,
                                               _ => new Http3Response { Status = 200, Body = [] },
                                               connectHandler: _ => new Http3ConnectResult { Status = 200, OnTunnel = tunnel => serverTunnel = tunnel });

        var client = new Http3ClientConnection("localhost",
                                               certificateValidation: new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] });

        void Pump()
        {
            client.CheckTimeouts();
            server.CheckTimeouts();
            foreach (byte[] datagram in client.GetDatagramsToSend())
                server.ProcessDatagram(datagram);
            foreach (byte[] datagram in server.GetDatagramsToSend())
                client.ProcessDatagram(datagram);
        }

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump();
        client.InitializeHttp3();
        for (int round = 0; round < 5; round++)
            Pump();
        Assert.That(client.ServerEnablesConnectProtocol, Is.True, "the server's SETTINGS");

        ulong        streamId      = client.SendExtendedConnect("localhost", "/bytes", "bytes", []);
        Http3Tunnel? clientTunnel  = null;
        for (int round = 0; round < 20 && clientTunnel is null; round++)
        {
            Pump();
            client.TryGetConnectResponse(streamId, out _, out _, out clientTunnel);
        }
        Assert.That(clientTunnel, Is.Not.Null, "the client's tunnel");
        Assert.That(serverTunnel, Is.Not.Null, "the server's tunnel");

        return new Pair { Certificate = cert, Client = client, Server = server, ClientTunnel = clientTunnel!, ServerTunnel = serverTunnel! };

    }

    private static byte[] Chunk(int index)
    {
        var chunk = new byte[ChunkSize];
        for (int i = 0; i < chunk.Length; i++)
            chunk[i] = (byte) (index * 31 + i);
        return chunk;
    }

    /// <summary>
    /// Write chunks, pumping while a write waits, until one still waits after 50 rounds; the
    /// number of chunks written by then, and the write that waits, if one does.
    /// </summary>
    private static (int Written, Task? Waiting) WriteUntilHeldBack(Pair pair, Http3Tunnel writer, int maxChunks)
    {
        for (int index = 0; index < maxChunks; index++)
        {
            Task write = writer.WriteAsync(Chunk(index), None);
            for (int round = 0; round < 50 && !write.IsCompleted; round++)
                pair.Pump();
            if (!write.IsCompleted)
                return (index, write);
            pair.Pump();
        }
        return (maxChunks, null);
    }

    #endregion


    #region ATunnelNotRead_HoldsTheWriterBack_AndLetsItOnOnceRead(Direction)

    /// <summary>
    /// The reading end does not read: the writer is held back after a few hundred KiB — the QUIC
    /// window, the reader's watermark and its own — rather than writing all 4 MiB into memory, and
    /// the reader holds no more than its watermark and a chunk. Once the reader reads, every write
    /// goes through, and every byte arrives, in order.
    /// </summary>
    [Test]
    public void ATunnelNotRead_HoldsTheWriterBack_AndLetsItOnOnceRead([Values] Direction direction)
    {

        using Pair pair = OpenPair();

        Http3Tunnel writer   = direction == Direction.ClientToServer ? pair.ClientTunnel : pair.ServerTunnel;
        Http3Tunnel reader   = direction == Direction.ClientToServer ? pair.ServerTunnel : pair.ClientTunnel;
        const int   chunks   = 256;   // 4 MiB

        (int written, Task? waiting) = WriteUntilHeldBack(pair, writer, chunks);
        int bufferedWhileHeldBack    = reader.Buffered;

        // Now read, and write the rest as there is room.
        var       received   = new List<byte>();
        Task<byte[]?> read   = reader.ReadAsync(None);
        int       next       = written + 1;
        Task      write      = waiting ?? Task.CompletedTask;

        for (int round = 0; round < 20_000 && received.Count < chunks * ChunkSize; round++)
        {
            while (read.IsCompleted)
            {
                received.AddRange(read.Result!);
                read = reader.ReadAsync(None);
            }
            while (write.IsCompleted && next < chunks)
                write = writer.WriteAsync(Chunk(next++), None);
            pair.Pump();
        }

        Assert.Multiple(() =>
        {

            Assert.That(waiting,                        Is.Not.Null,                                         "a write held back");
            Assert.That(written * ChunkSize,            Is.LessThan(1024 * 1024),                            "the bytes written before one was held back");
            Assert.That(bufferedWhileHeldBack,          Is.LessThan(Http3Tunnel.HighWatermark + ChunkSize),  "the bytes the reader held unread");

            Assert.That(received.Count,                 Is.EqualTo(chunks * ChunkSize),                      "the bytes received once read");
            Assert.That(received.SequenceEqual(Enumerable.Range(0, chunks).SelectMany(Chunk)), Is.True,      "the bytes received, byte for byte");

        });

    }

    #endregion

    #region AWaitingWrite_Fails_WhenTheConnectionEnds(Closer)

    /// <summary>
    /// A write held back when the connection ends would wait for ever: nothing will make room for
    /// it any more. It fails with an <see cref="OperationCanceledException"/>, and so does a write
    /// after it.
    /// </summary>
    [Test]
    public void AWaitingWrite_Fails_WhenTheConnectionEnds([Values] Closer closer)
    {

        using Pair pair = OpenPair();

        (_, Task? waiting) = WriteUntilHeldBack(pair, pair.ClientTunnel, 256);
        Assert.That(waiting, Is.Not.Null, "a write held back");

        if (closer == Closer.Client)
            pair.Client.CloseGracefully();
        else
            pair.Server.CloseGracefully();

        for (int round = 0; round < 50 && !waiting!.IsCompleted; round++)
            pair.Pump();

        Task after = pair.ClientTunnel.WriteAsync([1, 2, 3], None);

        Assert.Multiple(() =>
        {
            Assert.That(waiting!.IsCompleted,                     Is.True,                                         "the waiting write, once the connection ended");
            Assert.That(waiting.Exception?.InnerException,        Is.InstanceOf<OperationCanceledException>(),     "how the waiting write failed");
            Assert.That(after.Exception?.InnerException,          Is.InstanceOf<OperationCanceledException>(),     "how a write after the end failed");
        });

    }

    #endregion


    #region The tunnel on its own, over a bare QUIC stream

    private static (Http3Tunnel Tunnel, QuicStream Stream) Bare()
    {
        var stream = new QuicStream(new StreamId(0));
        return (new Http3Tunnel(stream), stream);
    }

    /// <summary>
    /// Fill the stream to the watermark: one write of a watermark's worth, put on the stream.
    /// </summary>
    private static void Fill(Http3Tunnel tunnel)
    {
        Assert.That(tunnel.WriteAsync(new byte[Http3Tunnel.HighWatermark], None).IsCompleted, Is.True, "the write that fills the stream");
        tunnel.PumpOutbound();
    }

    /// <summary>
    /// Send what is on the stream, as the connection would once the peer gives credit.
    /// </summary>
    private static List<byte> Send(QuicStream stream)
    {
        stream.Send.MaxData = ulong.MaxValue;
        var sent = new List<byte>();
        while (stream.Send.NextFrame(1200) is { } frame)
            sent.AddRange(frame.Data.ToArray());
        return sent;
    }

    [Test]
    public void ReceivedBytesNotRead_SaturateTheTunnel_AtTheWatermark()
    {

        (Http3Tunnel tunnel, _) = Bare();

        tunnel.Deliver(new byte[Http3Tunnel.HighWatermark - 1]);
        bool below = tunnel.IsSaturated;

        tunnel.Deliver([0]);
        bool at    = tunnel.IsSaturated;

        tunnel.ReadAsync(None);
        bool read  = tunnel.IsSaturated;

        Assert.Multiple(() =>
        {
            Assert.That(below,  Is.False, "a byte below the watermark");
            Assert.That(at,     Is.True,  "at the watermark");
            Assert.That(read,   Is.False, "once a chunk is read");
        });

    }

    [Test]
    public void AWrite_Waits_WhileAWatermarkWaitsToGoOut_AndGoesOnceSent()
    {

        (Http3Tunnel tunnel, QuicStream stream) = Bare();

        Fill(tunnel);
        Task write          = tunnel.WriteAsync([1, 2, 3], None);
        bool waitedOnPump   = !write.IsCompleted;

        tunnel.PumpOutbound();                 // nothing was sent: still no room
        bool waitedUnsent   = !write.IsCompleted;

        Send(stream);
        tunnel.PumpOutbound();                 // sent: room

        Assert.Multiple(() =>
        {
            Assert.That(waitedOnPump,        Is.True,  "the write, while a watermark waits");
            Assert.That(waitedUnsent,        Is.True,  "the write, while that watermark is not sent");
            Assert.That(write.IsCompleted,   Is.True,  "the write, once it is sent");
            Assert.That(Send(stream),        Is.EqualTo(new byte[] { 0x00, 3, 1, 2, 3 }), "what went out after it: the write's DATA frame");
        });

    }

    [Test]
    public void AWaitingWrite_CancelledByItsToken_IsNeverSent()
    {

        (Http3Tunnel tunnel, QuicStream stream) = Bare();
        using var cancellation = new CancellationTokenSource();

        Fill(tunnel);
        Task cancelled = tunnel.WriteAsync([1, 1, 1], cancellation.Token);
        Task kept      = tunnel.WriteAsync([2, 2], None);

        cancellation.Cancel();
        Send(stream);
        tunnel.PumpOutbound();

        Assert.Multiple(() =>
        {
            Assert.That(cancelled.IsCanceled,  Is.True,                                  "the write whose token was cancelled");
            Assert.That(kept.IsCompleted,      Is.True,                                  "the write behind it");
            Assert.That(Send(stream),          Is.EqualTo(new byte[] { 0x00, 2, 2, 2 }), "what went out: the kept write alone");
        });

    }

    [Test]
    public void AWaitingWrite_Fails_WhenThePeerResets_AndWhenAborted([Values] bool aborted)
    {

        (Http3Tunnel tunnel, _) = Bare();

        Fill(tunnel);
        Task waiting = tunnel.WriteAsync([1, 2, 3], None);

        if (aborted)
            tunnel.Abort();
        else
            tunnel.EndAbruptly();

        Task after = tunnel.WriteAsync([4], None);

        Assert.Multiple(() =>
        {
            Assert.That(waiting.Exception?.InnerException,  Is.InstanceOf<OperationCanceledException>(), "the waiting write");
            Assert.That(after.  Exception?.InnerException,  Is.InstanceOf<OperationCanceledException>(), "a write after it");
        });

    }

    [Test]
    public void AFinFromThePeer_LeavesWritingOpen()
    {

        (Http3Tunnel tunnel, QuicStream stream) = Bare();

        tunnel.End();
        Task write = tunnel.WriteAsync([7], None);
        tunnel.PumpOutbound();

        Assert.Multiple(() =>
        {
            Assert.That(write.IsCompletedSuccessfully,  Is.True,                              "the write after the peer's FIN");
            Assert.That(Send(stream),                   Is.EqualTo(new byte[] { 0x00, 1, 7 }), "what went out");
        });

    }

    #endregion

}
