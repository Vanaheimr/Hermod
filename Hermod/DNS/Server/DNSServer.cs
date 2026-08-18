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
using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

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

    public delegate Task OnDNSHTTPSUnicastListenerStartedDelegate (DateTimeOffset     Timestamp,
                                                                   DNSServer          Server,
                                                                   IPSocket           LocalSocket,
                                                                   HTTPPath           DNSQueryPath,
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

        private readonly         DNSMessagePipeline        pipeline;
        private readonly         List<Task>                listenerTasks           = [];
        private readonly         ILogger<DNSServer>        logger;
        private readonly         ILoggerFactory            loggerFactory;

        private                  UdpClient?                udpUnicastListener;
        private                  UdpClient?                udpMulticastListener;
        private                  TcpListener?              tcpUnicastListener;
        private                  TcpListener?              tlsUnicastListener;
        private                  DNSOverHTTPSServer?       httpsUnicastListener;

        private                  CancellationTokenSource?  cancellationTokenSource;

        #endregion

        #region Events

        public event OnDNSServerStartedDelegate?                OnDNSServerStarted;
        public event OnDNSUDPUnicastListenerStartedDelegate?    OnDNSUDPUnicastListenerStarted;
        public event OnDNSUDPMulticastListenerStartedDelegate?  OnDNSUDPMulticastListenerStarted;
        public event OnDNSTCPUnicastListenerStartedDelegate?    OnDNSTCPUnicastListenerStarted;
        public event OnDNSTLSUnicastListenerStartedDelegate?    OnDNSTLSUnicastListenerStarted;
        public event OnDNSHTTPSUnicastListenerStartedDelegate?  OnDNSHTTPSUnicastListenerStarted;
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

        public IPSocket?         ActiveHTTPSUnicastSocket { get; private set; }

        /// <summary>
        /// The RFC 8484 listener, while one is running.
        /// </summary>
        public DNSOverHTTPSServer?  HTTPSUnicastListener
            => httpsUnicastListener;

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
            this.loggerFactory  = LoggerFactory  ?? NullLoggerFactory.Instance;
            this.logger         = Logger         ?? this.loggerFactory.CreateLogger<DNSServer>();
            this.pipeline       = new DNSMessagePipeline(
                                      RequestHandler,
                                      this.Options,
                                      this.logger
                                  );

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

                    var dnsPacket = await udpUnicastListener.ReceiveAsync(CancellationToken);

                    if (!pipeline.AcceptSignedRequest(dnsPacket.Buffer, out var udpBody, out var tsigContext, out var tsigError))
                    {

                        if (tsigError is not null)
                            await udpUnicastListener.SendAsync(
                                      new ReadOnlyMemory<Byte>(tsigError),
                                      dnsPacket.RemoteEndPoint,
                                      CancellationToken
                                  );

                        continue;

                    }

                    DNSPacket dnsRequest;

                    try
                    {
                        dnsRequest = DNSPacket.Parse(
                                         ActiveUDPUnicastSocket ?? localSocket,
                                         IPSocket.FromIPEndPoint(dnsPacket.RemoteEndPoint),
                                         new MemoryStream(udpBody)
                                     );
                    }
                    catch (Exception parseException)
                    {

                        logger.LogDebug(
                            parseException,
                            "Could not parse a DNS request from {RemoteEndPoint}; answering FORMERR",
                            dnsPacket.RemoteEndPoint
                        );

                        var formatError = DNSMessagePipeline.BuildFormatErrorResponse(dnsPacket.Buffer);

                        if (formatError is not null)
                            await udpUnicastListener.SendAsync(
                                      new ReadOnlyMemory<Byte>(formatError),
                                      dnsPacket.RemoteEndPoint,
                                      CancellationToken
                                  );

                        continue;

                    }

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

                        await udpUnicastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(DNSMessagePipeline.SignIfRequested(pipeline.SerializeDatagramResponse(dnsResponse, dnsRequest), tsigContext)),
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

                        // Multicast response via unicast back to the sender!
                        await udpMulticastListener.SendAsync(
                                  new ReadOnlyMemory<Byte>(pipeline.SerializeDatagramResponse(dnsResponse, dnsRequest)),
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

                    if (!pipeline.AcceptSignedRequest(sharedBuffer[..bytesRead], out var streamBody, out var tsigContext, out var tsigError))
                    {

                        if (tsigError is not null)
                        {
                            await Stream.WriteAsync(new Byte[] { (Byte) (tsigError.Length >> 8), (Byte) tsigError.Length }, CancellationToken);
                            await Stream.WriteAsync(tsigError, CancellationToken);
                            await Stream.FlushAsync(CancellationToken);
                        }

                        continue;

                    }

                    var dnsRequest = DNSPacket.Parse(
                                         LocalSocket,
                                         RemoteSocket,
                                         new MemoryStream(streamBody)
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

                        var responseBytes  = pipeline.SerializeMessageResponse(dnsResponse, dnsRequest, tsigContext);

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

            => pipeline.ProcessRequest(
                   Request,
                   CancellationToken
               );



        #region (private) StartHTTPSUnicastAsync  (CancellationToken token)

        /// <summary>
        /// Bring up the RFC 8484 listener as a fifth transport on the same zone.
        /// </summary>
        /// <remarks>
        /// Unlike the other four this one is not a loop of its own: an
        /// <see cref="DNSOverHTTPSServer"/> is a TCP server already and runs its
        /// own accept loop, so all there is to do here is start it and forward
        /// what it sees to this server's events, so a listener writing them out
        /// sees DoH exchanges alongside the rest instead of having to subscribe
        /// somewhere else for them.
        /// </remarks>
        private async Task StartHTTPSUnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                var dohServer = new DNSOverHTTPSServer(
                                    DNSServerOptions:  Options,
                                    IPAddress:         Options.HTTPSUnicastSocket.IPAddress,
                                    TCPPort:           Options.HTTPSUnicastSocket.Port,
                                    DNSQueryPath:      Options.HTTPSPath,
                                    Pipeline:          pipeline,
                                    LoggerFactory:     loggerFactory
                                );

                dohServer.OnDoHQueryReceived += (timestamp, server, httpRequest, request, cancellationToken)
                    => LogEvent(
                           OnDNSRequestReceived,
                           loggingDelegate => loggingDelegate.Invoke(
                               timestamp,
                               this,
                               "HTTPS Unicast",
                               request,
                               cancellationToken
                           ),
                           nameof(OnDNSRequestReceived)
                       );

                dohServer.OnDoHResponseSent += (timestamp, server, httpRequest, response, cancellationToken)
                    => LogEvent(
                           OnDNSResponseSent,
                           loggingDelegate => loggingDelegate.Invoke(
                               timestamp,
                               this,
                               "HTTPS Unicast",
                               response,
                               cancellationToken
                           ),
                           nameof(OnDNSResponseSent)
                       );

                httpsUnicastListener = dohServer;

                await dohServer.Start().ConfigureAwait(false);

                var localSocket = new IPSocket(
                                      dohServer.IPAddress,
                                      dohServer.TCPPort
                                  );

                ActiveHTTPSUnicastSocket = localSocket;

                await LogEvent(
                          OnDNSHTTPSUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              localSocket,
                              dohServer.DNSQueryPath,
                              CancellationToken
                          ),
                          nameof(OnDNSHTTPSUnicastListenerStarted)
                      );

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting HTTPS listener");
            }

        }

        #endregion


        #region Start()

        public async Task Start()
        {

            if (IsRunning)
                return;

            if (Options.EnableTLSUnicast && Options.TLSServerCertificate is null)
                throw new InvalidOperationException("A TLS server certificate is required for the DNS TLS listener.");

            // RFC 8484 §5: "This protocol MUST be used with the https URI scheme."
            // A DNSOverHTTPSServer started on its own may run in cleartext, for a
            // TLS-terminating proxy or a test; a listener this server calls HTTPS
            // may not.
            if (Options.EnableHTTPSUnicast && Options.TLSServerCertificate is null)
                throw new InvalidOperationException("A TLS server certificate is required for the DNS HTTPS listener.");

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

            // Awaited rather than added to listenerTasks: this one *returns* once
            // the listener is up, where the other four only return once it is
            // down. Awaiting it also means the caller has a bound port to talk to
            // by the time Start() comes back.
            if (Options.EnableHTTPSUnicast)
                await StartHTTPSUnicastAsync(cancellationTokenSource.Token).ConfigureAwait(false);

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

            if (httpsUnicastListener is not null)
            {
                try
                {
                    await httpsUnicastListener.Stop().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error stopping HTTPS listener");
                }
            }

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
                httpsUnicastListener      = null;
                ActiveUDPUnicastSocket    = null;
                ActiveUDPMulticastSocket  = null;
                ActiveTCPUnicastSocket    = null;
                ActiveTLSUnicastSocket    = null;
                ActiveHTTPSUnicastSocket  = null;
                listenerTasks.Clear();

                cancellationTokenSource?.Dispose();
                this.cancellationTokenSource = null;
            }

        }

        #endregion



        #region (protected) LogEvent     (Module, Logger, LogHandler, ...)

        /// <remarks>
        /// <c>EventName</c> is passed on rather than left to the compiler a
        /// second time: down there the call site is this method, so
        /// <c>CallerArgumentExpression</c> would fill in "Logger" for every
        /// event there is.
        /// </remarks>
        protected Task LogEvent<TDelegate>(String                                             Module,
                                           TDelegate?                                         Logger,
                                           Func<TDelegate, Task>                              LogHandler,
                                           [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                           [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

            => Logger.InvokeAllAsync(
                   LogHandler,
                   (exception, eventName) => HandleErrors(Module, $"{Command}.{eventName}", exception),
                   EventName
               );

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
