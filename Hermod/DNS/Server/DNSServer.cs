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
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;

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

    public delegate Task OnDNSTLSUnicastListenerStartedDelegate   (DateTimeOffset     Timestamp,
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

        private readonly         IDNSRequestHandler        requestHandler;
        private readonly         List<Task>                listenerTasks           = [];
        private readonly         ILogger<DNSServer>        logger;
        private readonly         ILoggerFactory            loggerFactory;

        private                  UdpClient?                udpUnicastListener;
        private                  UdpClient?                udpMulticastListener;
        private                  TcpListener?              tcpUnicastListener;
        private                  TcpListener?              tlsUnicastListener;

        private                  CancellationTokenSource?  cancellationTokenSource;

        #endregion

        #region Events

        public event OnDNSServerStartedDelegate?                OnDNSServerStarted;
        public event OnDNSUDPUnicastListenerStartedDelegate?    OnDNSUDPUnicastListenerStarted;
        public event OnDNSUDPMulticastListenerStartedDelegate?  OnDNSUDPMulticastListenerStarted;
        public event OnDNSTCPUnicastListenerStartedDelegate?    OnDNSTCPUnicastListenerStarted;
        public event OnDNSTLSUnicastListenerStartedDelegate?    OnDNSTLSUnicastListenerStarted;
        public event OnDNSServerStoppedDelegate?                OnDNSServerStopped;

        public event OnDNSRequestReceivedDelegate?              OnDNSRequestReceived;
        public event OnDNSResponseSentDelegate?                 OnDNSResponseSent;

        #endregion

        #region Properties

        public DNSServerOptions  Options                  { get; }

        public ILogger<DNSServer>  Logger                 => logger;

        public ILoggerFactory      LoggerFactory          => loggerFactory;

        public IPSocket?         ActiveUDPUnicastSocket   { get; private set; }

        public IPSocket?         ActiveUDPMulticastSocket { get; private set; }

        public IPSocket?         ActiveTCPUnicastSocket   { get; private set; }

        public IPSocket?         ActiveTLSUnicastSocket   { get; private set; }

        public Boolean           IsRunning
            => cancellationTokenSource is not null &&
              !cancellationTokenSource.IsCancellationRequested;

        #endregion

        #region Constructor(s)

        public DNSServer(IDNSRequestHandler?    RequestHandler   = null,
                         DNSServerOptions?      Options          = null,
                         ILogger<DNSServer>?    Logger           = null,
                         ILoggerFactory?        LoggerFactory    = null)
        {

            this.Options        = Options        ?? new DNSServerOptions();
            this.requestHandler = RequestHandler ?? new AuthoritativeDNSRequestHandler(
                                                        InMemoryDNSZone.CreateDemoZone()
                                                    );
            this.loggerFactory  = LoggerFactory  ?? NullLoggerFactory.Instance;
            this.logger         = Logger         ?? this.loggerFactory.CreateLogger<DNSServer>();

        }

        #endregion


        // ToDo: To well-known problems when listing on localhost IPv4+IPv6,
        //       we might need separate listeners for IPv4 and IPv6!

        #region (private) ListenUDPUnicastAsync   (CancellationToken token)

        private async Task ListenUDPUnicastAsync(CancellationToken CancellationToken)
        {

            var localSocket     = Options.UDPUnicastSocket;
            udpUnicastListener  = new UdpClient(localSocket.ToIPEndPoint());
            ActiveUDPUnicastSocket = IPSocket.FromIPEndPoint(udpUnicastListener.Client.LocalEndPoint) ?? localSocket;

            await LogEvent(
                      OnDNSUDPUnicastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          ActiveUDPUnicastSocket ?? localSocket,
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
                                          ActiveUDPUnicastSocket ?? localSocket,
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

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);
                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      Options.UseCompression,
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
                    logger.LogError(ex, "Error within UDP unicast listener");
                }
            }

        }

        #endregion

        #region (private) ListenUDPMulticastAsync (CancellationToken token)

        private async Task ListenUDPMulticastAsync(CancellationToken CancellationToken)
        {

            var localSocket       = Options.UDPMulticastSocket;

            udpMulticastListener  = new UdpClient {
                                        ExclusiveAddressUse = false
                                    };
            udpMulticastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpMulticastListener.Client.Bind           (localSocket.ToIPEndPoint());

            ActiveUDPMulticastSocket = IPSocket.FromIPEndPoint(udpMulticastListener.Client.LocalEndPoint) ?? localSocket;

            var multicastAddress = System.Net.IPAddress.Parse(Options.MulticastGroupAddress);
            udpMulticastListener.JoinMulticastGroup(multicastAddress);

            await LogEvent(
                      OnDNSUDPMulticastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          ActiveUDPMulticastSocket ?? localSocket,
                          Options.MulticastGroupAddress,
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
                                          ActiveUDPMulticastSocket ?? localSocket,
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

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);
                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      Options.UseCompression,
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
                    logger.LogError(ex, "Error within UDP multicast listener");
                }
            }

            try
            {
                udpMulticastListener.DropMulticastGroup(multicastAddress);
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception e)
            {
                logger.LogError(e, "Error dropping multicast group");
            }

        }

        #endregion

        #region (private) ListenTCPUnicastAsync   (CancellationToken token)

        private async Task ListenTCPUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                var localSocket  = Options.TCPUnicastSocket;
                var tcpListener  = new TcpListener(localSocket.ToIPEndPoint());
                tcpUnicastListener = tcpListener;

                try
                {

                    tcpListener.Start(Options.TCPBacklog);
                    ActiveTCPUnicastSocket = IPSocket.FromIPEndPoint(tcpListener.LocalEndpoint) ?? localSocket;

                    await LogEvent(
                          OnDNSTCPUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              ActiveTCPUnicastSocket ?? localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSTCPUnicastListenerStarted)
                      );


                    while (!CancellationToken.IsCancellationRequested)
                    {
                        try
                        {

                            var tcpClient = await tcpListener.AcceptTcpClientAsync(CancellationToken);

                            logger.LogDebug(
                                "New TCP connection from {RemoteEndPoint} accepted on {LocalSocket}",
                                tcpClient.Client.RemoteEndPoint,
                                localSocket
                            );

                            _ = Task.Run(
                                    async () => await HandleTCPClientAsync(
                                                       tcpClient,
                                                       ActiveTCPUnicastSocket ?? localSocket,
                                                       CancellationToken
                                                   ).ConfigureAwait(false),
                                    CancellationToken
                                );

                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error accepting TCP client");
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error within TCP listener");
                }
                finally
                {
                    tcpListener.Stop();
                    if (ReferenceEquals(tcpUnicastListener, tcpListener))
                        tcpUnicastListener = null;
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting TCP listener");
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
                    var remoteSocket  = IPSocket.FromIPEndPoint(TCPClient.Client.RemoteEndPoint) ?? IPSocket.Zero;

                    try
                    {

                        logger.LogDebug(
                            "New TCP connection from {RemoteEndPoint}",
                            TCPClient.Client.RemoteEndPoint
                        );

                        await HandleFramedDNSStreamAsync(
                                  stream,
                                  LocalSocket,
                                  remoteSocket,
                                  "TCP Unicast",
                                  CancellationToken
                              ).ConfigureAwait(false);

                    }
                    catch (OperationCanceledException)
                    { }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error handling TCP connection");
                    }
                    finally
                    {
                        stream.Close();
                    }

                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error handling TCP client");
            }

        }

        #endregion

        #region (private) ListenTLSUnicastAsync   (CancellationToken token)

        private async Task ListenTLSUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                if (Options.TLSServerCertificate is null)
                    throw new InvalidOperationException("A TLS server certificate is required for the DNS TLS listener.");

                var localSocket  = Options.TLSUnicastSocket;
                var tlsListener  = new TcpListener(localSocket.ToIPEndPoint());
                tlsUnicastListener = tlsListener;

                try
                {

                    tlsListener.Start(Options.TCPBacklog);
                    ActiveTLSUnicastSocket = IPSocket.FromIPEndPoint(tlsListener.LocalEndpoint) ?? localSocket;

                    await LogEvent(
                          OnDNSTLSUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              ActiveTLSUnicastSocket ?? localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSTLSUnicastListenerStarted)
                      );

                    while (!CancellationToken.IsCancellationRequested)
                    {
                        try
                        {

                            var tcpClient = await tlsListener.AcceptTcpClientAsync(CancellationToken);

                            logger.LogDebug(
                                "New TLS connection from {RemoteEndPoint} accepted on {LocalSocket}",
                                tcpClient.Client.RemoteEndPoint,
                                localSocket
                            );

                            _ = Task.Run(
                                    async () => await HandleTLSClientAsync(
                                                       tcpClient,
                                                       ActiveTLSUnicastSocket ?? localSocket,
                                                       CancellationToken
                                                   ).ConfigureAwait(false),
                                    CancellationToken
                                );

                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error accepting TLS client");
                        }
                    }

                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error within TLS listener");
                }
                finally
                {
                    tlsListener.Stop();
                    if (ReferenceEquals(tlsUnicastListener, tlsListener))
                        tlsUnicastListener = null;
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting TLS listener");
            }

        }

        private async Task HandleTLSClientAsync(TcpClient          TCPClient,
                                                IPSocket           LocalSocket,
                                                CancellationToken  CancellationToken = default)
        {
            try
            {

                using (TCPClient)
                {

                    var remoteSocket = IPSocket.FromIPEndPoint(TCPClient.Client.RemoteEndPoint) ?? IPSocket.Zero;

                    await using var sslStream = new SslStream(
                                                    TCPClient.GetStream(),
                                                    leaveInnerStreamOpen: false,
                                                    Options.TLSClientCertificateValidator
                                                );

                    var authenticationOptions = new SslServerAuthenticationOptions {
                        ServerCertificate              = Options.TLSServerCertificate,
                        ClientCertificateRequired       = Options.TLSClientCertificateRequired,
                        EnabledSslProtocols             = Options.TLSProtocols,
                        CertificateRevocationCheckMode  = Options.TLSCertificateRevocationCheckMode,
                        EncryptionPolicy                = EncryptionPolicy.RequireEncryption
                    };

                    await sslStream.AuthenticateAsServerAsync(
                                        authenticationOptions,
                                        CancellationToken
                                    ).ConfigureAwait(false);

                    await HandleFramedDNSStreamAsync(
                              sslStream,
                              LocalSocket,
                              remoteSocket,
                              "TLS Unicast",
                              CancellationToken
                          ).ConfigureAwait(false);

                }

            }
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                logger.LogError(e, "Error handling TLS client");
            }

        }

        #endregion

        #region (private) HandleFramedDNSStreamAsync(Stream, LocalSocket, RemoteSocket, ServerType, CancellationToken)

        private async Task HandleFramedDNSStreamAsync(Stream             Stream,
                                                      IPSocket          LocalSocket,
                                                      IPSocket          RemoteSocket,
                                                      String            ServerType,
                                                      CancellationToken CancellationToken)
        {

            var sharedBuffer = ArrayPool<Byte>.Shared.Rent(UInt16.MaxValue);

            try
            {

                while (!CancellationToken.IsCancellationRequested)
                {

                    var lengthBuffer  = new Byte[2];
                    var lengthBytes   = await ReadTCPBytesAsync(Stream, lengthBuffer, CancellationToken).
                                            ConfigureAwait(false);

                    if (lengthBytes == 0)
                        break;

                    if (lengthBytes != 2)
                        throw new EndOfStreamException("Incomplete DNS stream length prefix.");

                    var length        = (UInt16) ((lengthBuffer[0] << 8) | lengthBuffer[1]);
                    logger.LogDebug(
                        "Received {ServerType} DNS request with length {Length}",
                        ServerType,
                        length
                    );

                    if (length == 0)
                        continue;

                    if (length > sharedBuffer.Length)
                        throw new InvalidDataException($"DNS request length {length} exceeds the maximum message size.");

                    var bytesRead     = await ReadTCPBytesAsync(Stream, sharedBuffer.AsMemory(0, length), CancellationToken).
                                            ConfigureAwait(false);

                    if (bytesRead != length)
                        throw new EndOfStreamException("Incomplete DNS stream request payload.");

                    var dnsRequest = DNSPacket.Parse(
                                         LocalSocket,
                                         RemoteSocket,
                                         new MemoryStream(sharedBuffer, 0, bytesRead)
                                     );

                    await LogEvent(
                        OnDNSRequestReceived,
                        async loggingDelegate => await loggingDelegate.Invoke(
                            Timestamp.Now,
                            this,
                            ServerType,
                            dnsRequest,
                            CancellationToken
                        ),
                        nameof(OnDNSRequestReceived)
                    );

                    var dnsResponse = await ProcessDNSRequest(dnsRequest, CancellationToken).
                                            ConfigureAwait(false);

                    if (dnsResponse is not null)
                    {

                        var memoryStream = new MemoryStream();

                        dnsResponse.Serialize(
                            memoryStream,
                            UseCompression:      Options.UseCompression,
                            CompressionOffsets:  []
                        );

                        var responseBytes  = memoryStream.ToArray();

                        Stream.WriteUInt16BE((UInt16) responseBytes.Length);

                        await Stream.WriteAsync(responseBytes, 0, responseBytes.Length, CancellationToken);
                        await Stream.FlushAsync(CancellationToken);

                        await LogEvent(
                            OnDNSResponseSent,
                            async loggingDelegate => await loggingDelegate.Invoke(
                                Timestamp.Now,
                                this,
                                ServerType,
                                dnsResponse,
                                CancellationToken
                            ),
                            nameof(OnDNSResponseSent)
                        );

                    }

                }

            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(sharedBuffer);
            }

        }

        #endregion

        #region (private) ReadTCPBytesAsync(Stream, Buffer, CancellationToken)

        private async Task<Int32> ReadTCPBytesAsync(Stream             Stream,
                                                    Memory<Byte>       Buffer,
                                                    CancellationToken  CancellationToken)
        {

            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            if (Options.TCPReadTimeout > TimeSpan.Zero)
                timeoutCancellationTokenSource.CancelAfter(Options.TCPReadTimeout);

            var bytesRead = 0;

            while (bytesRead < Buffer.Length)
            {

                var read = await Stream.ReadAsync(
                               Buffer[bytesRead..],
                               timeoutCancellationTokenSource.Token
                           ).ConfigureAwait(false);

                if (read == 0)
                    break;

                bytesRead += read;

            }

            return bytesRead;

        }

        #endregion


        private Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                     CancellationToken  CancellationToken = default)
        {

            Request.Questions.ForEachCounted((question, i) => {
                logger.LogDebug(
                    "Question {QuestionIndex}: Name={DomainName}, Type={QueryType}, Class={QueryClass}",
                    i,
                    question.DomainName,
                    question.QueryType,
                    question.QueryClass
                );
            });

            return requestHandler.ProcessDNSRequest(
                       Request,
                       CancellationToken
                   );

        }



        #region Start()

        public async Task Start()
        {

            if (IsRunning)
                return;

            if (Options.EnableTLSUnicast && Options.TLSServerCertificate is null)
                throw new InvalidOperationException("A TLS server certificate is required for the DNS TLS listener.");

            cancellationTokenSource = new CancellationTokenSource();
            listenerTasks.Clear();

            if (Options.EnableUDPUnicast)
                listenerTasks.Add(ListenUDPUnicastAsync(cancellationTokenSource.Token));

            if (Options.EnableUDPMulticast)
                listenerTasks.Add(ListenUDPMulticastAsync(cancellationTokenSource.Token));

            if (Options.EnableTCPUnicast)
                listenerTasks.Add(ListenTCPUnicastAsync(cancellationTokenSource.Token));

            if (Options.EnableTLSUnicast)
                listenerTasks.Add(ListenTLSUnicastAsync(cancellationTokenSource.Token));

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

            var cancellationTokenSource = this.cancellationTokenSource;
            if (cancellationTokenSource is null)
                return;

            await LogEvent(
                      OnDNSServerStopped,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          cancellationTokenSource.Token
                      ),
                      nameof(OnDNSServerStopped)
                  );

            cancellationTokenSource?.Cancel();

            udpUnicastListener?.  Dispose();
            udpMulticastListener?.Dispose();
            tcpUnicastListener?.  Stop();
            tlsUnicastListener?.  Stop();

            try
            {
                await Task.WhenAll(listenerTasks).
                           ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }
            catch (ObjectDisposedException)
            { }
            finally
            {
                udpUnicastListener        = null;
                udpMulticastListener      = null;
                tcpUnicastListener        = null;
                tlsUnicastListener        = null;
                ActiveUDPUnicastSocket    = null;
                ActiveUDPMulticastSocket  = null;
                ActiveTCPUnicastSocket    = null;
                ActiveTLSUnicastSocket    = null;
                listenerTasks.Clear();

                cancellationTokenSource?.Dispose();
                this.cancellationTokenSource = null;
            }

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

            logger.LogError(
                "{Module}.{Caller}: {ErrorResponse}",
                Module,
                Caller,
                ErrorResponse
            );

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual)   HandleErrors (Module, Caller, ExceptionOccurred)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccurred)
        {

            logger.LogError(
                ExceptionOccurred,
                "{Module}.{Caller}",
                Module,
                Caller
            );

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
                   nameof(ATCPServer),
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

            => $"{nameof(DNSServer)}: UDP/UC:{Options.UDPUnicastSocket}, UDP/MC:{Options.UDPMulticastSocket}, TCP:{Options.TCPUnicastSocket}, TLS:{Options.TLSUnicastSocket}";

        #endregion


    }

}
