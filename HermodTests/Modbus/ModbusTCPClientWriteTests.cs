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

using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.Modbus;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus;

/// <summary>
/// What the Modbus/TCP client puts on the wire when it writes, byte for byte:
/// a peer on a loopback port writes down every frame it is sent, and answers
/// it the way a device does that did what it was asked.
/// </summary>
/// <remarks>
/// The register writes are tested end to end as well, against the SunSpec
/// meter in <see cref="SunSpecModbusTLSTests"/>. That meter has no coils and
/// does not know function code 23, and it could not tell a wrong coil bit from
/// a right one anyway - which is what this peer is for.
/// </remarks>
public class ModbusTCPClientWriteTests
{

    #region (helpers)

    /// <summary>
    /// A client of the given peer for unit 7, whose StartingAddressOffset is
    /// 100: register or coil N is address N + 99 on the wire - numbered from
    /// 1 as the Modbus data model does, then offset. So 1 is address 100
    /// (0x0064), and 101 is address 200 (0x00C8).
    /// </summary>
    private static ModbusTCPClient NewClient(ModbusFrameRecorder Peer)

        => new (IPv4Address.Localhost,
                IPPort.Parse(Peer.Port),
                UnitAddress:            7,
                StartingAddressOffset:  100,
                PreferIPv4:             true,
                RequestTimeout:         TimeSpan.FromSeconds(2),
                MaxNumberOfRetries:     1);

    /// <summary>
    /// A frame without its transaction identifier, the one part of it the
    /// client chooses at random.
    /// </summary>
    private static Byte[] WithoutTransactionId(Byte[] Frame)
        => Frame[2..];

    #endregion


    #region WriteSingleCoil_Test()

    [Test]
    public async Task WriteSingleCoil_Test()
    {

        await using var peer    = new ModbusFrameRecorder();
        using       var client  = NewClient(peer);

        var on         = await client.WriteSingleCoils(1, true);
        var off        = await client.WriteSingleCoils(1, false);

        var exchanges  = peer.Exchanges.ToArray();

        Assert.That(exchanges, Has.Length.EqualTo(2));

        Assert.Multiple(() => {

            // On is 0xFF00 and off is 0x0000: two bytes either way.
            Assert.That(WithoutTransactionId(exchanges[0].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,     // protocol identifier
                            0x00, 0x06,     // length
                            0x07,           // unit identifier
                            0x05,           // write single coil
                            0x00, 0x64,     // address
                            0xFF, 0x00      // on
                        }));

            Assert.That(WithoutTransactionId(exchanges[1].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,
                            0x00, 0x06,
                            0x07,
                            0x05,
                            0x00, 0x64,
                            0x00, 0x00      // off
                        }));

            Assert.That(on,   Is.EqualTo(exchanges[0].Response));
            Assert.That(off,  Is.EqualTo(exchanges[1].Response));

        });

    }

    #endregion

    #region WriteMultipleCoils_Test()

    /// <summary>
    /// Ten coils, once packed into bytes by the caller and once as booleans
    /// packed by the client: the first coil is the lowest bit of the first
    /// byte, and ten coils take two bytes.
    /// </summary>
    [Test]
    public async Task WriteMultipleCoils_Test()
    {

        await using var peer    = new ModbusFrameRecorder();
        using       var client  = NewClient(peer);

        var asBytes    = await client.WriteMultipleCoils(1, 10, [ 0b0000_1101, 0b0000_0011 ]);
        var asBooleans = await client.WriteMultipleCoils(1, true,  false, true,  true,  false,
                                                            false, false, false, true,  true);

        var exchanges  = peer.Exchanges.ToArray();

        Assert.That(exchanges, Has.Length.EqualTo(2));

        var expected   = new Byte[] {
                             0x00, 0x00,    // protocol identifier
                             0x00, 0x09,    // length
                             0x07,          // unit identifier
                             0x0F,          // write multiple coils
                             0x00, 0x64,    // address
                             0x00, 0x0A,    // ten coils
                             0x02,          // in two bytes
                             0x0D, 0x03
                         };

        Assert.Multiple(() => {

            Assert.That(WithoutTransactionId(exchanges[0].Request), Is.EqualTo(expected));
            Assert.That(WithoutTransactionId(exchanges[1].Request), Is.EqualTo(expected));

            Assert.That(asBytes,     Is.EqualTo(exchanges[0].Response));
            Assert.That(asBooleans,  Is.EqualTo(exchanges[1].Response));

        });

    }

    #endregion

    #region WriteSingleRegister_Test()

    [Test]
    public async Task WriteSingleRegister_Test()
    {

        await using var peer    = new ModbusFrameRecorder();
        using       var client  = NewClient(peer);

        var response   = await client.WriteSingleRegister(1, [ 0x12, 0x34 ]);

        var exchange   = peer.Exchanges.Single();

        Assert.Multiple(() => {

            Assert.That(WithoutTransactionId(exchange.Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,     // protocol identifier
                            0x00, 0x06,     // length
                            0x07,           // unit identifier
                            0x06,           // write single register
                            0x00, 0x64,     // address
                            0x12, 0x34      // value
                        }));

            Assert.That(response, Is.EqualTo(exchange.Response));

        });

        // A register is two bytes, and a frame that says so but carries a
        // different number of them is not sent.
        Assert.ThrowsAsync<ArgumentException>(() => client.WriteSingleRegister(1, [ 0x12 ]));
        Assert.ThrowsAsync<ArgumentException>(() => client.WriteSingleRegister(1, [ 0x12, 0x34, 0x56 ]));
        Assert.That(peer.Exchanges, Has.Count.EqualTo(1));

    }

    #endregion

    #region WriteMultipleRegister_Test()

    /// <summary>
    /// An odd number of bytes is padded with a zero byte to whole registers,
    /// which is what the byte count in the frame already said.
    /// </summary>
    [Test]
    public async Task WriteMultipleRegister_Test()
    {

        await using var peer    = new ModbusFrameRecorder();
        using       var client  = NewClient(peer);

        var even       = await client.WriteMultipleRegister(101, [ 0x12, 0x34, 0x56, 0x78 ]);
        var odd        = await client.WriteMultipleRegister(101, [ 0x12, 0x34, 0x56 ]);

        var exchanges  = peer.Exchanges.ToArray();

        Assert.That(exchanges, Has.Length.EqualTo(2));

        Assert.Multiple(() => {

            Assert.That(WithoutTransactionId(exchanges[0].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,     // protocol identifier
                            0x00, 0x0B,     // length
                            0x07,           // unit identifier
                            0x10,           // write multiple registers
                            0x00, 0xC8,     // address
                            0x00, 0x02,     // two registers
                            0x04,           // in four bytes
                            0x12, 0x34, 0x56, 0x78
                        }));

            Assert.That(WithoutTransactionId(exchanges[1].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,
                            0x00, 0x0B,
                            0x07,
                            0x10,
                            0x00, 0xC8,
                            0x00, 0x02,
                            0x04,
                            0x12, 0x34, 0x56, 0x00
                        }));

            Assert.That(even,  Is.EqualTo(exchanges[0].Response));
            Assert.That(odd,   Is.EqualTo(exchanges[1].Response));

        });

    }

    #endregion

    #region ReadWriteMultipleRegister_Test()

    /// <summary>
    /// Function code 23, whose write part is padded to whole registers just
    /// like function code 16's.
    /// </summary>
    [Test]
    public async Task ReadWriteMultipleRegister_Test()
    {

        await using var peer    = new ModbusFrameRecorder();
        using       var client  = NewClient(peer);

        var even       = await client.ReadWriteMultipleRegister(1, 3, 101, [ 0x12, 0x34, 0x56, 0x78 ]);
        var odd        = await client.ReadWriteMultipleRegister(1, 3, 101, [ 0x12, 0x34, 0x56 ]);

        var exchanges  = peer.Exchanges.ToArray();

        Assert.That(exchanges, Has.Length.EqualTo(2));

        Assert.Multiple(() => {

            Assert.That(WithoutTransactionId(exchanges[0].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,     // protocol identifier
                            0x00, 0x0F,     // length
                            0x07,           // unit identifier
                            0x17,           // read/write multiple registers
                            0x00, 0x64,     // read from address 100...
                            0x00, 0x03,     // ...three registers
                            0x00, 0xC8,     // write to address 200...
                            0x00, 0x02,     // ...two registers
                            0x04,           // in four bytes
                            0x12, 0x34, 0x56, 0x78
                        }));

            Assert.That(WithoutTransactionId(exchanges[1].Request), Is.EqualTo(new Byte[] {
                            0x00, 0x00,
                            0x00, 0x0F,
                            0x07,
                            0x17,
                            0x00, 0x64,
                            0x00, 0x03,
                            0x00, 0xC8,
                            0x00, 0x02,
                            0x04,
                            0x12, 0x34, 0x56, 0x00
                        }));

            // The three registers read, which this peer numbers 1, 2 and 3
            Assert.That(even,                          Is.EqualTo(exchanges[0].Response));
            Assert.That(odd,                           Is.EqualTo(exchanges[1].Response));
            Assert.That(WithoutTransactionId(even),    Is.EqualTo(new Byte[] {
                                                           0x00, 0x00,
                                                           0x00, 0x09,
                                                           0x07,
                                                           0x17,
                                                           0x06,
                                                           0x00, 0x01, 0x00, 0x02, 0x00, 0x03
                                                       }));

        });

    }

    #endregion


    #region (class) ModbusFrameRecorder

    /// <summary>
    /// A Modbus/TCP peer on a loopback port. It takes one connection, reads
    /// whole frames by the length in their MBAP header, and writes each one
    /// down together with the answer it gave.
    /// </summary>
    private sealed class ModbusFrameRecorder : IAsyncDisposable
    {

        private readonly TcpListener              listener;
        private readonly CancellationTokenSource  cancellationTokenSource  = new (TimeSpan.FromSeconds(30));
        private readonly Task                     peerTask;

        /// <summary>
        /// The TCP port this peer listens on.
        /// </summary>
        public Int32                                               Port         { get; }

        /// <summary>
        /// Every request frame, whole, and the frame that answered it.
        /// </summary>
        public ConcurrentQueue<(Byte[] Request, Byte[] Response)>  Exchanges    { get; } = new();

        public ModbusFrameRecorder()
        {

            listener  = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            Port      = ((IPEndPoint) listener.LocalEndpoint).Port;
            peerTask  = RunAsync(cancellationTokenSource.Token);

        }

        private async Task RunAsync(CancellationToken CancellationToken)
        {

            try
            {

                using var tcpClient  = await listener.AcceptTcpClientAsync(CancellationToken);
                var       stream     = tcpClient.GetStream();

                while (true)
                {

                    var header  = new Byte[7];
                    await stream.ReadExactlyAsync(header, CancellationToken);

                    // The length counts everything after itself: the unit identifier and the PDU
                    var length  = (header[4] << 8) | header[5];

                    if (length < 2)
                    {
                        Exchanges.Enqueue((header, []));
                        return;
                    }

                    var request = new Byte[6 + length];
                    header.CopyTo(request, 0);
                    await stream.ReadExactlyAsync(request.AsMemory(7), CancellationToken);

                    var response = AnswerTo(request);
                    Exchanges.Enqueue((request, response));

                    await stream.WriteAsync(response, CancellationToken);

                }

            }
            catch (Exception e) when (e is OperationCanceledException or EndOfStreamException or IOException or SocketException)
            { }

        }

        /// <summary>
        /// What a device answers that did what it was asked.
        /// </summary>
        private static Byte[] AnswerTo(Byte[] Request)

            => Request[7] switch {

                   // Write single coil, write single register: the request itself
                   0x05 or 0x06  => Request,

                   // Write multiple coils or registers: where, and how many
                   0x0F or 0x10  => [ Request[0], Request[1], 0x00, 0x00, 0x00, 0x06, Request[6], Request[7],
                                      Request[8], Request[9], Request[10], Request[11] ],

                   // Read/write multiple registers: the registers read
                   0x17          => ReadRegistersAnswer(Request, (Request[10] << 8) | Request[11]),

                   // Anything else: exception 01, illegal function
                   _             => [ Request[0], Request[1], 0x00, 0x00, 0x00, 0x03, Request[6], (Byte) (Request[7] | 0x80), 0x01 ]

               };

        /// <summary>
        /// The given number of registers, numbered from 1.
        /// </summary>
        private static Byte[] ReadRegistersAnswer(Byte[] Request, Int32 NumberOfRegisters)
        {

            var answer = new Byte[9 + 2 * NumberOfRegisters];

            answer[0] = Request[0];
            answer[1] = Request[1];
            answer[4] = (Byte) ((3 + 2 * NumberOfRegisters) >> 8);
            answer[5] = (Byte)  (3 + 2 * NumberOfRegisters);
            answer[6] = Request[6];
            answer[7] = Request[7];
            answer[8] = (Byte)  (2 * NumberOfRegisters);

            for (var i = 0; i < NumberOfRegisters; i++)
                answer[10 + 2 * i] = (Byte) (i + 1);

            return answer;

        }

        public async ValueTask DisposeAsync()
        {

            await cancellationTokenSource.CancelAsync();
            listener.Stop();

            // Whatever happened in there, a test that failed has already said
            // why, and this must not say something else over it.
            try
            {
                await peerTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            { }

            cancellationTokenSource.Dispose();

        }

    }

    #endregion

}
