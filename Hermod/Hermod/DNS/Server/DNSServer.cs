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
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public delegate Task OnDNSServerStartedDelegate               (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSUDPUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSUDPMulticastListenerStartedDelegate (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   String             MCAddr,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSTCPUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSServerStoppedDelegate               (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   CancellationToken  CancellationToken);


    public delegate Task OnDNSRequestReceivedDelegate             (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   String             ServerType,
                                                                   DNSPacket          Request,
                                                                   CancellationToken  CancellationToken);

    public delegate Task OnDNSResponseSentDelegate                (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   String             ServerType,
                                                                   DNSPacket          Response,
                                                                   CancellationToken  CancellationToken);


    public class DNSServer
    {

        #region Data

        private readonly static  IPPort                    UDPUnicastPort          = IPPort.Parse(63);
        private readonly static  IPPort                    TCPUnicastPort          = IPPort.Parse(63);
        private readonly static  IPPort                    MulticastPort           = IPPort.Parse(6363);
        private const            String                    MulticastGroupAddress   = "224.0.0.251";

        private                  UdpClient?                udpUnicastListener;
        private                  UdpClient?                udpMulticastListener;
        private                  TcpClient?                tcpUnicastListener;

        private                  CancellationTokenSource?  cancellationTokenSource;

        #endregion

        #region Events

        public event OnDNSServerStartedDelegate?                OnDNSServerStarted;
        public event OnDNSUDPUnicastListenerStartedDelegate?    OnDNSUDPUnicastListenerStarted;
        public event OnDNSUDPMulticastListenerStartedDelegate?  OnDNSUDPMulticastListenerStarted;
        public event OnDNSTCPUnicastListenerStartedDelegate?    OnDNSTCPUnicastListenerStarted;
        public event OnDNSServerStoppedDelegate?                OnDNSServerStopped;

        public event OnDNSRequestReceivedDelegate?              OnDNSRequestReceived;
        public event OnDNSResponseSentDelegate?                 OnDNSResponseSent;

        #endregion


        // ToDo: To well-known problems when listing on localhost IPv4+IPv6,
        //       we might need separate listeners for IPv4 and IPv6!

        #region (private) ListenUDPUnicastAsync   (CancellationToken token)

        private async Task ListenUDPUnicastAsync(CancellationToken CancellationToken)
        {

            var localSocket     = new IPSocket(IPvXAddress.Any, UDPUnicastPort);
            //udpUnicastListener  = new UdpClient(localSocket.ToIPEndPoint());
            udpUnicastListener  = new UdpClient(localSocket.Port.ToUInt16());

            await LogEvent(
                      OnDNSUDPUnicastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          localSocket,
                          CancellationToken
                      ),
                      nameof(OnDNSUDPUnicastListenerStarted)
                  );


            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {

                    var dnsPacket   = await udpUnicastListener.ReceiveAsync(CancellationToken);

                    var dnsRequest  = DNSPacket.Parse(
                                          localSocket,
                                          IPSocket.FromIPEndPoint(dnsPacket.RemoteEndPoint),
                                          new MemoryStream(dnsPacket.Buffer)
                                      );

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            "UDP Unicast",
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = ProcessDNSRequest(dnsRequest);
                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      false,
                            CompressionOffsets:  []
                        );

                        await udpUnicastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(memoryStream.ToArray()),
                                  dnsResponse.RemoteSocket.ToIPEndPoint(),
                                  CancellationToken
                              );

                        await LogEvent(
                                  OnDNSResponseSent,
                                  async loggingDelegate => await loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      this,
                                      "UDP Unicast",
                                      dnsResponse,
                                      CancellationToken
                                  ),
                                  nameof(OnDNSResponseSent)
                              );

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    DebugX.Log($"Fehler im Unicast-Listener: {ex.Message}");
                }
            }

        }

        #endregion

        #region (private) ListenUDPMulticastAsync (CancellationToken token)

        private async Task ListenUDPMulticastAsync(CancellationToken CancellationToken)
        {

            var localSocket       = new IPSocket(IPvXAddress.Any, MulticastPort);

            udpMulticastListener  = new UdpClient {
                                        ExclusiveAddressUse = false
                                    };
            udpMulticastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpMulticastListener.Client.Bind           (localSocket.ToIPEndPoint());

            var multicastAddress = System.Net.IPAddress.Parse(MulticastGroupAddress);
            udpMulticastListener.JoinMulticastGroup(multicastAddress);

            await LogEvent(
                      OnDNSUDPMulticastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          localSocket,
                          MulticastGroupAddress,
                          CancellationToken
                      ),
                      nameof(OnDNSUDPMulticastListenerStarted)
                  );


            while (!CancellationToken.IsCancellationRequested)
            {
                try
                {

                    var dnsPacket   = await udpMulticastListener.ReceiveAsync(CancellationToken);

                    var dnsRequest  = DNSPacket.Parse(
                                          localSocket,
                                          IPSocket.FromIPEndPoint(dnsPacket.RemoteEndPoint),
                                          new MemoryStream(dnsPacket.Buffer)
                                      );

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            "UDP Multicast",
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = ProcessDNSRequest(dnsRequest);
                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      false,
                            CompressionOffsets:  []
                        );

                        // Multicast response via unicast back to the sender!
                        await udpMulticastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(memoryStream.ToArray()),
                                  dnsResponse.RemoteSocket.ToIPEndPoint(),
                                  CancellationToken
                              );

                        await LogEvent(
                                  OnDNSResponseSent,
                                  async loggingDelegate => await loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      this,
                                      "UDP Multicast",
                                      dnsResponse,
                                      CancellationToken
                                  ),
                                  nameof(OnDNSResponseSent)
                              );

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    DebugX.Log($"Fehler im Multicast-Listener: {ex.Message}");
                }
            }

            try
            {
                udpMulticastListener.DropMulticastGroup(multicastAddress);
            }
            catch (Exception e)
            {
                DebugX.Log(e, "Error dropping multicast group: " + e.Message);
            }

        }

        #endregion

        #region (private) ListenTCPUnicastAsync   (CancellationToken token)

        private async Task ListenTCPUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                var localSocket  = new IPSocket(IPvXAddress.Any, TCPUnicastPort);
                var tcpListener  = new TcpListener(localSocket.ToIPEndPoint());

                try
                {

                    tcpListener.Start();

                    await LogEvent(
                          OnDNSTCPUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSTCPUnicastListenerStarted)
                      );


                    while (!CancellationToken.IsCancellationRequested)
                    {
                        try
                        {

                            var tcpClient = await tcpListener.AcceptTcpClientAsync(CancellationToken);

                            DebugX.Log($"New TCP connection from {tcpClient.Client.RemoteEndPoint} accepted on {localSocket}!");

                            _ = Task.Run(async () => await HandleTCPClientAsync(tcpClient, localSocket, CancellationToken), CancellationToken);

                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            DebugX.Log($"Fehler beim Akzeptieren des TCP-Clients: {ex.Message}");
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugX.Log(e, "Error within TCP listener: " + e.Message);
                }
                finally
                {
                    tcpListener.Stop();
                }

            }
            catch (Exception e)
            {
                DebugX.Log(e, "Error starting TCP listener: " + e.Message);
            }

        }

        private async Task HandleTCPClientAsync(TcpClient          TCPClient,
                                                IPSocket           LocalSocket,
                                                CancellationToken  CancellationToken   = default)
        {
            try
            {

                using (TCPClient)
                {

                    var stream        = TCPClient.GetStream();

                    // Rent a shared buffer from ArrayPool
                    var sharedBuffer  = ArrayPool<Byte>.Shared.Rent(UInt16.MaxValue);

                    try
                    {

                        DebugX.Log($"New TCP connection from {TCPClient.Client.RemoteEndPoint}");

                        while (!CancellationToken.IsCancellationRequested)
                        {

                            if (stream.DataAvailable)
                            {

                                // Read the 2-byte length prefix (DNS over TCP)
                                var length     = stream.ReadUInt16BE();
                                DebugX.Log($"Received TCP DNS request with length: {length}");

                                var bytesRead  = await stream.ReadAsync(sharedBuffer.AsMemory(0, length), CancellationToken);
                                if (bytesRead != length)
                                    break;

                                var dnsRequest = DNSPacket.Parse(
                                                     LocalSocket,
                                                     IPSocket.FromIPEndPoint(TCPClient.Client.RemoteEndPoint) ?? IPSocket.Zero,
                                                     new MemoryStream(sharedBuffer, 0, bytesRead)
                                                 );

                                await LogEvent(
                                    OnDNSRequestReceived,
                                    async loggingDelegate => await loggingDelegate.Invoke(
                                        Timestamp.Now,
                                        this,
                                        "TCP Unicast",
                                        dnsRequest,
                                        CancellationToken
                                    ),
                                    nameof(OnDNSRequestReceived)
                                );

                                var dnsResponse = ProcessDNSRequest(dnsRequest);
                                if (dnsResponse is not null)
                                {

                                    var memoryStream = new MemoryStream();

                                    dnsResponse.Serialize(
                                        memoryStream,
                                        UseCompression:      false,
                                        CompressionOffsets:  []
                                    );

                                    var responseBytes  = memoryStream.ToArray();

                                          // Write length prefix
                                          stream.WriteUInt16BE((UInt16) responseBytes.Length);

                                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, CancellationToken);
                                    await stream.FlushAsync(CancellationToken);

                                    await LogEvent(
                                        OnDNSResponseSent,
                                        async loggingDelegate => await loggingDelegate.Invoke(
                                            Timestamp.Now,
                                            this,
                                            "TCP Unicast",
                                            dnsResponse,
                                            CancellationToken
                                        ),
                                        nameof(OnDNSResponseSent)
                                    );

                                }

                            }
                            else
                            {

                                await Task.Delay(10, CancellationToken);

                                var sock         = TCPClient.Client;
                                var clientClose  = sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0;

                                if (clientClose)
                                {
                                    DebugX.Log($"TCP client {TCPClient.Client.RemoteEndPoint} closed the connection.");
                                    break;
                                }

                            }

                        }
                    }
                    catch (OperationCanceledException)
                    { }
                    catch (Exception ex)
                    {
                        DebugX.Log($"Fehler beim Verarbeiten der TCP-Verbindung: {ex.Message}");
                    }
                    finally
                    {
                        stream.Close();
                    }

                }

            }
            catch (Exception e)
            {
                DebugX.Log(e, "Error handling TCP client: " + e.Message);
            }

        }

        #endregion


        private DNSResponse ProcessDNSRequest(DNSPacket Request)
        {

            Request.Questions.ForEachCounted((question, i) => {
                DebugX.Log($"Question {i}: Name={question.DomainName}, Type={question.QueryType}, Class={question.QueryClass}");
            });


            var response = Request.CreateResponse(
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

            return response;

        }



        #region Start()

        public async Task Start()
        {

            cancellationTokenSource = new CancellationTokenSource();

            _ = Task.WhenAll(
                    ListenUDPUnicastAsync  (cancellationTokenSource.Token),
                    ListenUDPMulticastAsync(cancellationTokenSource.Token),
                    ListenTCPUnicastAsync  (cancellationTokenSource.Token)
                );

            await LogEvent(
                      OnDNSServerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          cancellationTokenSource?.Token ?? CancellationToken.None
                      ),
                      nameof(OnDNSServerStarted)
                  );

        }

        #endregion

        #region Stop()

        public async Task Stop()
        {

            await LogEvent(
                      OnDNSServerStopped,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          cancellationTokenSource?.Token ?? CancellationToken.None
                      ),
                      nameof(OnDNSServerStopped)
                  );

            cancellationTokenSource?.Cancel();

            udpUnicastListener?.  Dispose();
            udpMulticastListener?.Dispose();
            tcpUnicastListener?.  Dispose();

        }

        #endregion



        #region (protected) LogEvent     (Module, Logger, LogHandler, ...)

        protected async Task LogEvent<TDelegate>(String                                             Module,
                                                 TDelegate?                                         Logger,
                                                 Func<TDelegate, Task>                              LogHandler,
                                                 [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                                 [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

        {
            if (Logger is not null)
            {
                try
                {

                    await Task.WhenAll(
                              Logger.GetInvocationList().
                                     OfType<TDelegate>().
                                     Select(LogHandler)
                          ).ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    await HandleErrors(Module, $"{Command}.{EventName}", e);
                }
            }
        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ErrorResponse)

        public virtual Task HandleErrors(String  Module,
                                         String  Caller,
                                         String  ErrorResponse)
        {

            DebugX.Log($"{Module}.{Caller}: {ErrorResponse}");

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ExceptionOccurred)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccurred)
        {

            DebugX.LogException(ExceptionOccurred, $"{Module}.{Caller}");

            return Task.CompletedTask;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATCPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(DNSServer)}: UDP/UC:{UDPUnicastPort}, UDP/MC:{MulticastPort}, TCP:{TCPUnicastPort}";

        #endregion


    }

}
