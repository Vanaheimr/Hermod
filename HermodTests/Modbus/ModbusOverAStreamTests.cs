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

using System.IO.Pipelines;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

using NetIPAddress = System.Net.IPAddress;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus;

/// <summary>
/// The Modbus/TLS frontend serves Modbus over any stream it is handed - an SSH
/// channel, a WebSocket - with the same policy, deciding every request by the
/// role it is told at that request.
/// </summary>
[TestFixture]
public class ModbusOverAStreamTests
{

    #region (private sealed class) DuplexStream

    /// <summary>
    /// One end of a connection of two pipes: what it writes is read at the
    /// other end, and what the other end writes is read here.
    /// </summary>
    private sealed class DuplexStream(PipeReader Reader, PipeWriter Writer) : Stream
    {

        private readonly Stream reader = Reader.AsStream();
        private readonly Stream writer = Writer.AsStream();

        public override Boolean CanRead  => true;
        public override Boolean CanWrite => true;
        public override Boolean CanSeek  => false;
        public override Int64   Length   => throw new NotSupportedException();
        public override Int64   Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default) => reader.ReadAsync(Buffer, CancellationToken);
        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)                                           => reader.Read(Buffer, Offset, Count);
        public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default) => writer.WriteAsync(Buffer, CancellationToken);
        public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)                                           => writer.Write(Buffer, Offset, Count);
        public override Task FlushAsync(CancellationToken CancellationToken)                                           => writer.FlushAsync(CancellationToken);
        public override void Flush()                                                                                   => writer.Flush();
        public override Int64 Seek(Int64 Offset, SeekOrigin Origin) => throw new NotSupportedException();
        public override void SetLength(Int64 Value)                 => throw new NotSupportedException();

        protected override void Dispose(Boolean Disposing)
        {
            if (Disposing)
                Writer.Complete();
            base.Dispose(Disposing);
        }

    }

    #endregion


    #region ServeAsync_DecidesEveryRequest_ByTheRoleItIsToldThen()

    /// <summary>
    /// A login whose role changes while it is connected is decided by its new
    /// role at its next request: ReadOnly may not write the meter mode, which is
    /// commanded, and GridService may. The connection is said to be open and to
    /// be closed, with its transport and its login.
    /// </summary>
    [Test]
    public async Task ServeAsync_DecidesEveryRequest_ByTheRoleItIsToldThen()
    {

        using var meter     = new SunSpecMeterDevice("meter-over-a-stream", SunSpecMeterMode.ImportOnly, runSimulation: false);

        using var frontend  = new ModbusTlsFrontend(
                                  new ModbusTlsFrontendOptions(
                                      NetIPAddress.Loopback,
                                      IPPort.Zero,
                                      null,
                                      null,
                                      null,
                                      TimeSpan.FromSeconds(5),
                                      TimeSpan.FromSeconds(5),
                                      TimeSpan.FromSeconds(5),
                                      ServerCertificateSelector:  serverName => throw new InvalidOperationException("Nothing listens here."),
                                      ClientTrustAnchors:         () => []
                                  ),
                                  new SunSpecBackendFactory(meter),
                                  new AuthorizationPolicy(meter)
                              );

        var opened    = new ConcurrentQueue<ModbusConnectionInfo>();
        var closed    = new ConcurrentQueue<ModbusConnectionInfo>();
        var requests  = new ConcurrentQueue<ModbusRequestInfo>();

        frontend.OnConnectionOpened += opened.  Enqueue;
        frontend.OnConnectionClosed += closed.  Enqueue;
        frontend.OnModbusRequest    += requests.Enqueue;

        var toServer  = new Pipe();
        var toClient  = new Pipe();

        var role      = SunSpecRoles.ReadOnly;

        var serving   = frontend.ServeAsync(
                            new DuplexStream(toServer.Reader, toClient.Writer),
                            () => role,
                            "127.0.0.1:50000",
                            "SSH",
                            "cs001"
                        );

        var client    = ModbusTCPClient.Over(new DuplexStream(toClient.Reader, toServer.Writer));

        var mode      = (await client.ReadHoldingRegistersAsync(1, 40094, 1))[0];

        ModbusException? refused = null;

        try
        {
            await client.WriteSingleRegisterAsync(1, 40094, mode);
        }
        catch (ModbusException e)
        {
            refused = e;
        }

        role = SunSpecRoles.GridService;

        await client.WriteSingleRegisterAsync(1, 40094, mode);

        await client.DisposeAsync();

        await serving.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() => {

            Assert.That(refused?.Code,                       Is.EqualTo(ModbusExceptionCode.IllegalFunction), "ReadOnly wrote the meter mode.");

            Assert.That(requests.Select(request => request.Role),
                        Is.EqualTo(new[] { SunSpecRoles.ReadOnly, SunSpecRoles.ReadOnly, SunSpecRoles.GridService }),
                        "The requests were not decided by the role of their moment.");

            Assert.That(requests.Select(request => request.Allowed), Is.EqualTo(new[] { true, false, true }));

            Assert.That(opened,                              Has.Count.EqualTo(1));
            Assert.That(opened.First().Transport,            Is.EqualTo("SSH"));
            Assert.That(opened.First().Login,                Is.EqualTo("cs001"));
            Assert.That(opened.First().Role,                 Is.EqualTo(SunSpecRoles.ReadOnly));
            Assert.That(closed,                              Has.Count.EqualTo(1), "The connection was not said to be over when its stream ended.");

        });

    }

    #endregion

}
