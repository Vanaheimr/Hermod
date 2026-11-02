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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// A DNS server over TCP that hands every framed query to a script, with the
    /// number of the connection it arrived on: 1 for the first one accepted.
    /// </summary>
    internal sealed class ScriptedDNSTCPServer : IAsyncDisposable
    {

        private readonly TcpListener                                  listener  = new (System.Net.IPAddress.Loopback, 0);
        private readonly CancellationTokenSource                      stop      = new();
        private readonly Func<Int32, NetworkStream, Byte[], Task>     onQuery;
        private readonly List<Task>                                   serving   = [];
        private readonly Task                                         accepting;
        private          Int32                                        connections;

        public IPPort  Port           { get; }

        /// <summary>
        /// The number of connections accepted so far.
        /// </summary>
        public Int32   Connections
            => Volatile.Read(ref connections);

        /// <param name="OnQuery">What to do with a query: the connection's number, its stream, and the query without its length.</param>
        public ScriptedDNSTCPServer(Func<Int32, NetworkStream, Byte[], Task> OnQuery)
        {
            onQuery    = OnQuery;
            listener.Start();
            Port       = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);
            accepting  = Accept();
        }

        private async Task Accept()
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    var number = Interlocked.Increment(ref connections);
                    lock (serving)
                        serving.Add(Serve(client, number));
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException)            { }
            catch (ObjectDisposedException)    { }
        }

        private async Task Serve(TcpClient Client, Int32 Number)
        {
            using (Client)
            {
                try
                {
                    var stream = Client.GetStream();
                    while (true)
                        await onQuery(Number, stream, await ReadFrame(stream, stop.Token));
                }
                catch (OperationCanceledException) { }
                catch (IOException)                { }
                catch (ObjectDisposedException)    { }
            }
        }

        private static async Task<Byte[]> ReadFrame(NetworkStream Stream, CancellationToken Token)
        {
            var length = new Byte[2];
            await Stream.ReadExactlyAsync(length, Token);
            var message = new Byte[(length[0] << 8) | length[1]];
            await Stream.ReadExactlyAsync(message, Token);
            return message;
        }

        /// <summary>
        /// Write a message with its two-octet length (RFC 1035 §4.2.2).
        /// </summary>
        public static async Task WriteFrame(NetworkStream Stream, Byte[] Message)
        {
            await Stream.WriteAsync(new[] { (Byte) (Message.Length >> 8), (Byte) Message.Length });
            await Stream.WriteAsync(Message);
            await Stream.FlushAsync();
        }

        /// <summary>
        /// The query turned round, with one A record pointing back at its question
        /// name, 192.0.2.1 - and, when asked for, an OPT record carrying the
        /// edns-tcp-keepalive option with the given TIMEOUT (RFC 7828 §3.1).
        /// </summary>
        public static Byte[] Answer(Byte[] Query, UInt16? KeepaliveTimeout = null)
        {

            // The question ends after the name, its type and its class; an OPT
            // record after it is left out of the answer.
            var end = 12;
            while (Query[end] != 0)
                end += 1 + Query[end];
            end += 5;

            var answer = new List<Byte>(Query[..end]);
            answer[2]  = (Byte) (answer[2] | 0x80);   // QR
            answer[3]  = 0x80;                        // RA, NOERROR
            answer[6]  = 0; answer[7]  = 1;           // ANCOUNT 1
            answer[8]  = 0; answer[9]  = 0;
            answer[10] = 0; answer[11] = (Byte) (KeepaliveTimeout is null ? 0 : 1);

            answer.AddRange([ 0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x01, 0x2C, 0x00, 0x04, 192, 0, 2, 1 ]);

            // Root name, OPT, a UDP size of 1232, no extended RCODE or flags, and
            // RDATA of one option: code 11, length 2, the TIMEOUT.
            if (KeepaliveTimeout is UInt16 timeout)
                answer.AddRange([ 0x00, 0x00, 0x29, 0x04, 0xD0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06,
                                  0x00, 0x0B, 0x00, 0x02, (Byte) (timeout >> 8), (Byte) timeout ]);

            return [.. answer];

        }

        public async ValueTask DisposeAsync()
        {

            stop.Cancel();
            listener.Stop();

            await accepting;

            Task[] all;
            lock (serving)
                all = [.. serving];

            await Task.WhenAll(all);

            stop.Dispose();

        }

    }

}
