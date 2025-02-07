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

using org.GraphDefined.Vanaheimr.Illias;

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
        private CancellationTokenSource?  cts;

        #endregion

        #region Properties

        public IPPort  UDPPort       { get; } = UDPPort ?? IPPort.NTP;

        public UInt32  BufferSize    { get; } = 4096;

        #endregion


        #region Start(CancellationToken = default)

        /// <summary>
        /// Start the NTP server.
        /// </summary>
        public async Task Start(CancellationToken CancellationToken = default)
        {

            if (udpSocket is not null)
                return;

            cts       = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

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

            udpSocket.Close();
            udpSocket = null;

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
