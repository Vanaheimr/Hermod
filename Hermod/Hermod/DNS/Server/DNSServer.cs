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

using org.GraphDefined.Vanaheimr.Illias;
using System.Net;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public class DNSServer
    {

        private const Int32   UDPUnicastPort         = 63;
        private const Int32   TCPUnicastPort         = 63;
        private const Int32   MulticastPort          = 6363;
        private const String  MulticastGroupAddress  = "224.0.0.251";

        private UdpClient? _unicastListener;
        private UdpClient? _multicastListener;
        private CancellationTokenSource _cts;

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();

            // Starte Unicast-Listener
            var unicastTask = ListenUDPUnicastAsync(_cts.Token);

            // Starte Multicast-Listener
            var multicastTask = ListenUDPMulticastAsync(_cts.Token);

            // Warte auf beide Tasks (oder Abbruch)
            await Task.WhenAll(unicastTask, multicastTask);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _unicastListener?.Dispose();
            _multicastListener?.Dispose();
        }

        private async Task ListenUDPUnicastAsync(CancellationToken token)
        {

            _unicastListener = new UdpClient(UDPUnicastPort);
            Console.WriteLine($"Unicast-Listener gestartet auf Port {UDPUnicastPort}.");

            while (!token.IsCancellationRequested)
            {
                try
                {

                    var receiveResult  = await _unicastListener.ReceiveAsync(token);

                    var remoteEndPoint = receiveResult.RemoteEndPoint;
                    var receivedBytes  = receiveResult.Buffer;

                    Console.WriteLine($"Unicast-Anfrage empfangen von {remoteEndPoint}: {receivedBytes.Length} Bytes.");

                    var responseBytes = ProcessDNSQuery(receivedBytes);
                    if (responseBytes is not null)
                    {
                        await _unicastListener.SendAsync(new ReadOnlyMemory<Byte>(responseBytes), remoteEndPoint, token);
                        Console.WriteLine($"Unicast-Antwort gesendet an {remoteEndPoint}.");
                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler im Unicast-Listener: {ex.Message}");
                }
            }
        }

        private async Task ListenUDPMulticastAsync(CancellationToken token)
        {

            _multicastListener = new UdpClient();
            _multicastListener.ExclusiveAddressUse = false;
            _multicastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _multicastListener.Client.Bind(new IPEndPoint(System.Net.IPAddress.Any, MulticastPort));

            var multicastAddress = System.Net.IPAddress.Parse(MulticastGroupAddress);
            _multicastListener.JoinMulticastGroup(multicastAddress);

            Console.WriteLine($"Multicast-Listener gestartet auf Port {MulticastPort}, Gruppe {MulticastGroupAddress}.");

            while (!token.IsCancellationRequested)
            {
                try
                {

                    var receiveResult  = await _multicastListener.ReceiveAsync(token);

                    var remoteEndPoint = receiveResult.RemoteEndPoint;
                    var receivedBytes  = receiveResult.Buffer;

                    Console.WriteLine($"Multicast-Anfrage empfangen von {remoteEndPoint}: {receivedBytes.Length} Bytes.");

                    var responseBytes = ProcessDNSQuery(receivedBytes);
                    if (responseBytes is not null)
                    {
                        // Für Multicast: Antwort als Unicast zurücksenden (üblich für mDNS)
                        await _multicastListener.SendAsync(new ReadOnlyMemory<byte>(responseBytes), remoteEndPoint, token);
                        Console.WriteLine($"Multicast-Antwort gesendet an {remoteEndPoint}.");
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler im Multicast-Listener: {ex.Message}");
                }
            }

            // Cleanup: Multicast-Gruppe verlassen
            _multicastListener.DropMulticastGroup(multicastAddress);

        }





        private Byte[] ProcessDNSQuery(Byte[] packet)
        {

            var dnsPacket = DNSPacket.Parse(new MemoryStream(packet));

            dnsPacket.Questions.ForEachCounted((question, i) => {
                Console.WriteLine($"Question {i}: Name={question.DomainName}, Type={question.QueryType}, Class={question.QueryClass}");
            });


            var response = dnsPacket.CreateResponse(
                               Opcode:                0,
                               AuthoritativeAnswer:   false,
                               Truncation:            false,
                               RecursionDesired:      false,
                               RecursionAvailable:    true,
                               ResponseCode:          DNSResponseCodes.NoError,
                               AnswerRRs:             new IDNSResourceRecord[] {
                                                          new A(
                                                              DomainName.Parse("api1.example.org."),
                                                              DNSQueryClasses.IN,
                                                              TimeSpan.FromDays(30),
                                                              IPv4Address.Parse("141.24.12.2")
                                                          ),
                                                          new AAAA(
                                                              DomainName.Parse("api2.example.org."),
                                                              DNSQueryClasses.IN,
                                                              TimeSpan.FromDays(30),
                                                              IPv6Address.Parse("::2")
                                                          ),
                                                          new SRV(
                                                              DNSServiceName.Parse("_ocpp._tls.api2.example.org."),
                                                              DNSQueryClasses.IN,
                                                              TimeSpan.FromDays(30),
                                                              10,
                                                              20,
                                                              IPPort.Parse(443),
                                                              DomainName.Parse("api2.example.org.")
                                                          ),
                                                          new SSHFP(
                                                              DomainName.Parse("api2.example.org."),
                                                              DNSQueryClasses.IN,
                                                              TimeSpan.FromDays(30),
                                                              SSHFP_Algorithm.ECDSA,
                                                              SSHFP_FingerprintType.SHA256,
                                                              "0095d7637f456888505741e952a1e7ff635e018f9a95c9b3b38af4bb9fdb0c36".FromHEX()
                                                          ),
                                                          new TXT(
                                                              DomainName.Parse("api2.example.org."),
                                                              DNSQueryClasses.IN,
                                                              TimeSpan.FromDays(30),
                                                              "Hello world!"
                                                          )

                                                      },
                               AuthorityRRs:          new IDNSResourceRecord[] { },
                               AdditionalRRs:         new IDNSResourceRecord[] { }
                           );

            var ms = new MemoryStream();
            response.Serialize(ms, false, []);

            return ms.ToArray();

            ////// Kopiere Anfrage-ID (erste 2 Bytes)
            //var response = new byte[packet.Length];
            //Array.Copy(packet, response, packet.Length);

            //// Setze QR-Flag auf 1 (Response), und AA auf 1 (Authoritative)
            //response[2] = (byte)(response[2] | 0x80); // QR = 1
            //response[3] = (byte)(response[3] | 0x04); // AA = 1 (Beispiel)

            // Für Demo: Einfach echo zurück
            //return response;

        }

    }

}
