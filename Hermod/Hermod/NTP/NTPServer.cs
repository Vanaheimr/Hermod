/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net;
using System.Net.Sockets;
using System.Text;
using org.GraphDefined.Vanaheimr.Illias;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// A NTP UDP server.
    /// </summary>
    /// <param name="UDPPort">The optional UDP port to listen on (default: 123).</param>
    public class NTPServer(IPPort? UDPPort = null)
    {

        #region Data

        private Socket?                   udpSocket;
        private Socket?                   tcpSocket;
        private CancellationTokenSource?  cts;

        #endregion

        #region Properties

        public IPPort  UDPPort       { get; } = UDPPort ?? IPPort.NTP;

        public IPPort  TCPPort       { get; } = UDPPort ?? IPPort.NTSKE;

        public UInt32  BufferSize    { get; } = 4096;

        #endregion


        #region Start(CancellationToken = default)

        /// <summary>
        /// Start the NTP server.
        /// </summary>
        public async Task Start(CancellationToken CancellationToken = default)
        {

            if (udpSocket is not null || tcpSocket is not null)
                return;

            cts       = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            #region Start UDP server

            udpSocket = new Socket(
                            AddressFamily.InterNetwork,
                            SocketType.Dgram,
                            ProtocolType.Udp
                        );

            udpSocket.Bind(
                new IPEndPoint(
                    System.Net.IPAddress.Any,
                    UDPPort.ToUInt16()
                )
            );

            DebugX.Log($"NTP Server started on port {UDPPort}/UDP");

            // Fire-and-forget task that handles incoming NTP in a loop
            _ = Task.Run(async () => {

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {

                        var buffer       = new Byte[BufferSize];
                        var remoteEP     = new IPEndPoint(System.Net.IPAddress.Any, 0);

                        var result       = await udpSocket.ReceiveFromAsync(
                                                     new ArraySegment<Byte>(buffer),
                                                     SocketFlags.None,
                                                     remoteEP,
                                                     cts.Token
                                                 );

                        // Local copy to pass into the Task
                        var resultLocal  = result;


                        _ = Task.Run(async () => {

                            try
                            {

                                Array.Resize(ref buffer, resultLocal.ReceivedBytes);

                                if (NTPPacket.TryParse(buffer, out var requestPacket, out var errorResponse))
                                {

                                    var responsePacket = BuildResponse(requestPacket);

                                    await udpSocket.SendToAsync(
                                              new ArraySegment<Byte>(responsePacket.ToByteArray()),
                                              SocketFlags.None,
                                              resultLocal.RemoteEndPoint
                                          );

                                }
                                else
                                {
                                    DebugX.Log($"Invalid NTP request from {resultLocal.RemoteEndPoint}: {errorResponse}");
                                }
                            }
                            catch (Exception e)
                            {
                                DebugX.Log($"Exception while processing a NTP request: {e}");
                            }

                        }, cts.Token);

                    }
                }
                catch (ObjectDisposedException)
                {
                    // Will be thrown when the UDP client is closed during shutdown.
                }
                catch (Exception ex)
                {
                    DebugX.Log($"Exception: {ex}");
                }

                try { udpSocket?.Close(); } catch { }
                udpSocket = null;

            }, cts.Token);

            #endregion

            #region Start TCP server

            tcpSocket = new Socket(
                            AddressFamily.InterNetwork,
                            SocketType.Stream,
                            ProtocolType.Tcp
                        );

            tcpSocket.Bind(
                new IPEndPoint(
                    System.Net.IPAddress.Any,
                    TCPPort.ToUInt16()
                )
            );

            tcpSocket.Listen(backlog: 20);

            DebugX.Log($"NTP/NTS-KE Server started on port {TCPPort}/TCP");

            // telnet 127.0.0.1:4460
            // openssl s_client -connect 127.0.0.1:4460
            // openssl s_client -connect 127.0.0.1:4460 -showcerts
            // openssl s_client -connect 127.0.0.1:4460 -verify 0

            // Fire-and-forget loop that Accepts new sockets
            _ = Task.Run(async () => {

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {

                        var clientSocket = await tcpSocket.AcceptAsync(cts.Token);

                        if (clientSocket == null)
                            continue;

                        _ = Task.Run(async () => {

                            try
                            {

                                using var networkStream = new NetworkStream(clientSocket, ownsSocket: false);

                                var tlsServerProtocol = new TlsServerProtocol(networkStream);

                                var tlsServer = new NTSKE_TLSService();
                                tlsServerProtocol.Accept(tlsServer);

                                var c2sKey = tlsServer.NTS_C2S_Key;
                                var s2cKey = tlsServer.NTS_S2C_Key;

                                // Write "Hello World!" to the TLS-encrypted stream
                                using var writer = new StreamWriter(tlsServerProtocol.Stream, Encoding.UTF8, leaveOpen: true);
                                await writer.WriteLineAsync("Hello World!");
                                await writer.FlushAsync();

                                // A friendly close
                                tlsServerProtocol.Close();

                            }
                            catch (Exception ex)
                            {
                                DebugX.Log($"TLS handshake/IO failed: {ex.Message}");
                            }
                            finally
                            {
                                try { clientSocket.Shutdown(SocketShutdown.Both); } catch { }
                                clientSocket.Close();
                            }

                        });

                    }
                }
                catch (ObjectDisposedException)
                {
                    // normal on shutdown
                }
                catch (Exception ex)
                {
                    DebugX.Log($"Exception in TLS Accept loop: {ex}");
                }

                try { tcpSocket?.Close(); } catch { }
                tcpSocket = null;

            }, cts.Token);

            #endregion

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop()
        {
            cts?.Cancel();
        }

        #endregion


        private NTPPacket BuildResponse(NTPPacket requestPacket)
        {

            var response = new NTPPacket(

                               Mode:                4, // 4 (Server)
                               Stratum:             2,
                               Poll:                requestPacket.Poll,
                               Precision:           requestPacket.Precision,
                               TransmitTimestamp:   NTPPacket.GetCurrentNTPTimestamp(),

                               Extensions:          null

                           );

            return response;

        }

    }

}
