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

    public delegate Task OnDNSHTTP2UnicastListenerStartedDelegate (DateTimeOffset     Timestamp,
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

        /// <summary>
        /// Every UDP unicast listener, which is two when the bind address is the
        /// wildcard and one otherwise. <see cref="udpUnicastListener"/> is the
        /// first of them and stays for the callers that only ever wanted a port.
        /// </summary>
        private readonly         List<UdpClient>           udpUnicastListeners = [];

        private readonly         List<TcpListener>         tcpUnicastListeners = [];
        private                  UdpClient?                udpMulticastListener;
        private                  TcpListener?              tcpUnicastListener;
        private                  TcpListener?              tlsUnicastListener;
        private                  DNSOverHTTPSServer?       httpsUnicastListener;
        private                  DNSOverHTTP2Server?       http2UnicastListener;

        private                  CancellationTokenSource?  cancellationTokenSource;

        #endregion

        #region Events

        public event OnDNSServerStartedDelegate?                OnDNSServerStarted;
        public event OnDNSUDPUnicastListenerStartedDelegate?    OnDNSUDPUnicastListenerStarted;
        public event OnDNSUDPMulticastListenerStartedDelegate?  OnDNSUDPMulticastListenerStarted;
        public event OnDNSTCPUnicastListenerStartedDelegate?    OnDNSTCPUnicastListenerStarted;
        public event OnDNSTLSUnicastListenerStartedDelegate?    OnDNSTLSUnicastListenerStarted;
        public event OnDNSHTTPSUnicastListenerStartedDelegate?  OnDNSHTTPSUnicastListenerStarted;
        public event OnDNSHTTP2UnicastListenerStartedDelegate?  OnDNSHTTP2UnicastListenerStarted;
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

        public IPSocket?         ActiveHTTP2UnicastSocket { get; private set; }

        /// <summary>
        /// The RFC 8484 listener speaking HTTP/1.1, while one is running.
        /// </summary>
        public DNSOverHTTPSServer?  HTTPSUnicastListener
            => httpsUnicastListener;

        /// <summary>
        /// The RFC 8484 listener speaking HTTP/2, while one is running.
        /// </summary>
        public DNSOverHTTP2Server?  HTTP2UnicastListener
            => http2UnicastListener;

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


        #region (private static) BindEndpointsOf(Socket)

        /// <summary>
        /// The endpoints a configured unicast socket actually has to be bound to:
        /// two when the address is the wildcard, one otherwise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A socket bound to <c>[::]</c> does not answer on 127.0.0.1, and one
        /// bound to <c>0.0.0.0</c> does not answer on ::1 — measured, on Windows,
        /// as a full query timeout in each direction rather than a refusal. The
        /// default bind address is the wildcard and resolves to <c>[::]</c>, so a
        /// server started with default options was reachable over IPv6 only.
        /// </para>
        /// <para>
        /// The other way to fix that is one socket with <c>DualMode</c> enabled,
        /// and it is one line. It is not what this does, because a dual-mode
        /// socket hands every IPv4 client to the application as
        /// <c>::ffff:a.b.c.d</c> — and RFC 7873 §5.2.1 derives a server cookie
        /// from the client's IP address. Changing what that address looks like
        /// would silently invalidate every cookie an IPv4 client holds, and would
        /// do it inside a mechanism whose whole job is to be hard to forge. Two
        /// listeners keep each request in the family it arrived on.
        /// </para>
        /// <para>
        /// IPv6 is bound first and IPv4 second, so that an ephemeral port chosen
        /// by the first can be reused by the second: both families have to answer
        /// on the same port or a client that fell back from UDP to TCP would find
        /// nothing there.
        /// </para>
        /// </remarks>
        private static List<System.Net.IPEndPoint> BindEndpointsOf(IPSocket Socket)
        {

            if (!Socket.IPAddress.IsAny)
                return [ Socket.ToIPEndPoint() ];

            return [
                       new (System.Net.IPAddress.IPv6Any, Socket.Port.ToUInt16()),
                       new (System.Net.IPAddress.Any,     Socket.Port.ToUInt16())
                   ];

        }

        #endregion

        #region (private) BindBothFamilies(LocalSocket, Bind, Release, EndPointOf, What)

        /// <summary>
        /// Bind every endpoint a configured socket means, on one port, retrying
        /// with a different port when a system-chosen one turns out to be taken
        /// in the other family.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The retry is the part that is easy to leave out and expensive to
        /// leave out. An ephemeral port the operating system hands out for
        /// <c>[::]</c> says nothing about whether the same number is free for
        /// <c>0.0.0.0</c>: they are separate spaces, and an outgoing connection
        /// from this machine may already hold it. Without the retry, a server
        /// asking for "any free port" occasionally gets one it cannot bind in
        /// both families, and the natural thing to write there — log a warning
        /// and carry on — puts it back to serving a single family, which is the
        /// defect this whole routine exists to remove.
        /// </para>
        /// <para>
        /// Both listeners are released before each retry. Holding the one that
        /// worked would stop the operating system from offering that number
        /// again, so the next attempt would walk up the ephemeral range one
        /// socket at a time. <c>ATCPServer</c> met all of this first; the
        /// sixteen attempts are its number.
        /// </para>
        /// <para>
        /// A family that is simply not on this host — <c>AddressFamilyNotSupported</c>,
        /// <c>AddressNotAvailable</c> — is not an error: a machine with IPv6
        /// switched off is an ordinary machine, and serving the other family is
        /// the right answer. A port that is *taken* on a fixed port number is a
        /// different thing, and throws, because quietly answering on half of
        /// what was asked for is how this started.
        /// </para>
        /// </remarks>
        private List<T> BindBothFamilies<T>(IPSocket                              LocalSocket,
                                            Func<System.Net.IPEndPoint, T>        Bind,
                                            Action<T>                             Release,
                                            Func<T, System.Net.IPEndPoint?>       EndPointOf,
                                            String                                What)
        {

            const Int32 attempts   = 16;

            var endPoints          = BindEndpointsOf(LocalSocket);
            var portChosenBySystem = LocalSocket.Port.ToUInt16() == 0;

            for (var attempt = 1; ; attempt++)
            {

                var bound  = new List<T>();
                var port   = LocalSocket.Port.ToUInt16();
                var retry  = false;

                foreach (var endPoint in endPoints)
                {

                    var target = new System.Net.IPEndPoint(endPoint.Address, port);

                    try
                    {

                        var listener = Bind(target);
                        bound.Add(listener);

                        // Whatever the first one actually got is what the rest
                        // have to use: two families on two ports would be two
                        // servers, and a client falling back from UDP to TCP
                        // (RFC 7766 §5) would find nothing at the second.
                        if (port == 0)
                            port = (UInt16) (EndPointOf(listener)?.Port ?? 0);

                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressFamilyNotSupported ||
                                                    e.SocketErrorCode == SocketError.AddressNotAvailable)
                    {

                        logger.LogDebug(
                            "The {What} listener has no {Family} on this host ({Error}); serving the other family only",
                            What,
                            target.AddressFamily,
                            e.SocketErrorCode
                        );

                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse &&
                                                    portChosenBySystem &&
                                                    attempt < attempts)
                    {

                        logger.LogDebug(
                            "Port {Port} was free for one family and taken for {Family}; asking for another",
                            port,
                            target.AddressFamily
                        );

                        retry = true;
                        break;

                    }
                    catch (SocketException e)
                    {

                        // A fixed port that is taken in one family, or a
                        // system-chosen one still taken after every attempt.
                        // Whatever was bound so far has to go: leaving it would
                        // hold a port for a server that is not going to run, and
                        // the caller would be told nothing — this method runs
                        // inside a background listener task, where an escaping
                        // exception is nobody's.
                        foreach (var listener in bound)
                        {
                            try { Release(listener); } catch { }
                        }

                        throw new InvalidOperationException(
                                  $"The {What} listener could not be bound to {target}: {e.SocketErrorCode}. " +
                                   "Serving only the other address family would answer half the clients and " +
                                   "look healthy doing it.",
                                  e
                              );

                    }

                }

                if (!retry)
                {

                    if (bound.Count == 0)
                        throw new InvalidOperationException(
                                  $"The {What} listener could not be bound to any of {endPoints.Count} endpoint(s)!");

                    if (bound.Count < endPoints.Count && !portChosenBySystem)
                        logger.LogWarning(
                            "The {What} listener was asked for {Wanted} endpoint(s) and got {Got}",
                            What, endPoints.Count, bound.Count
                        );

                    return bound;

                }

                foreach (var listener in bound)
                {
                    try { Release(listener); } catch { }
                }

            }

        }

        #endregion

        #region (private) ListenUDPUnicastAsync   (CancellationToken token)

        private async Task ListenUDPUnicastAsync(CancellationToken CancellationToken)
        {

            var localSocket  = Options.UDPUnicastSocket;
            var loops        = new List<Task>();

            udpUnicastListeners.AddRange(
                BindBothFamilies(
                    localSocket,
                    endPoint => {

                        // Constructed for the family and then bound, rather than
                        // built around a socket assigned afterwards: a UdpClient
                        // tracks its own address family, and handing it a socket
                        // of a family it does not believe it has makes
                        // ReceiveAsync wait for a datagram that can never arrive.
                        var listener = new UdpClient(endPoint.AddressFamily);

                        // Belt beside braces, and honestly labelled as such:
                        // with the IPv4 listener below actually bound, this flag
                        // is never consulted — IPv4 datagrams go to the IPv4
                        // socket, and a mutation turning it back on changes
                        // nothing any test can see. It states the intent and
                        // guards the degraded case where the second bind is ever
                        // removed; it is not what keeps a client's address
                        // unmapped. The second listener is.
                        if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                            listener.Client.DualMode = false;

                        listener.Client.Bind(endPoint);

                        return listener;

                    },
                    listener => listener.Dispose(),
                    listener => listener.Client.LocalEndPoint as System.Net.IPEndPoint,
                    "UDP unicast"
                )
            );

            udpUnicastListener      = udpUnicastListeners[0];
            ActiveUDPUnicastSocket  = IPSocket.FromIPEndPoint(udpUnicastListener.Client.LocalEndPoint) ?? localSocket;

            // Every bind happens above, before the first await. Start() does not
            // wait for this method — it returns the moment this method yields —
            // so anything bound after an await would not be there yet when the
            // caller asks for a port and starts sending to it.
            foreach (var listener in udpUnicastListeners)
            {

                await LogEvent(
                          OnDNSUDPUnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              IPSocket.FromIPEndPoint(listener.Client.LocalEndPoint) ?? localSocket,
                              CancellationToken
                          ),
                          nameof(OnDNSUDPUnicastListenerStarted)
                      );

                loops.Add(ReceiveUDPUnicastAsync(listener, localSocket, CancellationToken));

            }

            await Task.WhenAll(loops);

        }

        #endregion

        #region (private) ReceiveUDPUnicastAsync  (Listener, LocalSocket, CancellationToken)

        /// <summary>
        /// One receive loop, for one listener. Everything it needs is a parameter
        /// rather than a field, because there is now more than one of them.
        /// </summary>
        private async Task ReceiveUDPUnicastAsync(UdpClient          udpUnicastListener,
                                                  IPSocket           localSocket,
                                                  CancellationToken  CancellationToken)
        {

            var boundSocket = IPSocket.FromIPEndPoint(udpUnicastListener.Client.LocalEndPoint) ?? localSocket;

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
                                         boundSocket,
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

            var localSocket  = Options.TCPUnicastSocket;
            var loops        = new List<Task>();

            // Every bind before the first await, for the same reason as UDP:
            // Start() returns as soon as this method yields, and a caller that
            // has a port is entitled to expect something listening on it.
            try
            {

                tcpUnicastListeners.AddRange(
                    BindBothFamilies(
                        localSocket,
                        endPoint => {

                            var tcpListener = new TcpListener(endPoint);

                            // As on the UDP side: unobservable while the IPv4
                            // listener exists, kept for intent and for the
                            // degraded case.
                            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                                tcpListener.Server.DualMode = false;

                            tcpListener.Start(Options.TCPBacklog);

                            return tcpListener;

                        },
                        tcpListener => tcpListener.Stop(),
                        tcpListener => tcpListener.LocalEndpoint as System.Net.IPEndPoint,
                        "TCP unicast"
                    )
                );

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting TCP listener");
                return;
            }

            tcpUnicastListener      = tcpUnicastListeners[0];
            ActiveTCPUnicastSocket  = IPSocket.FromIPEndPoint(tcpUnicastListener.LocalEndpoint) ?? localSocket;

            foreach (var tcpListener in tcpUnicastListeners)
            {

                await LogEvent(
                      OnDNSTCPUnicastListenerStarted,
                      async loggingDelegate => await loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          IPSocket.FromIPEndPoint(tcpListener.LocalEndpoint) ?? localSocket,
                          CancellationToken
                      ),
                      nameof(OnDNSTCPUnicastListenerStarted)
                  );

                loops.Add(AcceptTCPUnicastAsync(tcpListener, localSocket, CancellationToken));

            }

            await Task.WhenAll(loops);

        }

        /// <summary>
        /// One accept loop, for one listener.
        /// </summary>
        /// <remarks>
        /// The local socket handed to each connection is this listener's own
        /// rather than whichever one happened to be bound first. A DNS cookie is
        /// derived from the client's address (RFC 7873 §5.2.1), and a connection
        /// reported against the wrong local endpoint is the same class of mistake
        /// as reporting the wrong remote one.
        /// </remarks>
        private async Task AcceptTCPUnicastAsync(TcpListener        tcpListener,
                                                 IPSocket           localSocket,
                                                 CancellationToken  CancellationToken)
        {

            var boundSocket = IPSocket.FromIPEndPoint(tcpListener.LocalEndpoint) ?? localSocket;

            try
            {

                while (!CancellationToken.IsCancellationRequested)
                {
                    try
                    {

                        var tcpClient = await tcpListener.AcceptTcpClientAsync(CancellationToken);

                        logger.LogDebug(
                            "New TCP connection from {RemoteEndPoint} accepted on {LocalSocket}",
                            tcpClient.Client.RemoteEndPoint,
                            boundSocket
                        );

                        _ = Task.Run(
                                async () => await HandleTCPClientAsync(
                                                   tcpClient,
                                                   boundSocket,
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

                dohServer.OnDoHQueryReceived += (timestamp, server, request, cancellationToken)
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

                dohServer.OnDoHResponseSent += (timestamp, server, response, cancellationToken)
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

        #region (private) StartHTTP2UnicastAsync  (CancellationToken token)

        /// <summary>
        /// Bring up the RFC 8484 listener again, this time over the version §5.2
        /// recommends.
        /// </summary>
        /// <remarks>
        /// The same resource, the same pipeline and the same zone as the HTTP/1.1
        /// listener beside it — only the framing differs, which is exactly what
        /// §5.2 is about: "Earlier versions of HTTP are capable of conveying the
        /// semantic requirements of DoH but may result in very poor performance."
        /// </remarks>
        private async Task StartHTTP2UnicastAsync(CancellationToken CancellationToken)
        {

            try
            {

                var doh2Server = new DNSOverHTTP2Server(
                                     DNSServerOptions:  Options,
                                     IPAddress:         Options.HTTP2UnicastSocket.IPAddress,
                                     TCPPort:           Options.HTTP2UnicastSocket.Port,
                                     DNSQueryPath:      Options.HTTPSPath,
                                     Pipeline:          pipeline,
                                     LoggerFactory:     loggerFactory
                                 );

                doh2Server.OnDoHQueryReceived += (timestamp, server, request, cancellationToken)
                    => LogEvent(
                           OnDNSRequestReceived,
                           loggingDelegate => loggingDelegate.Invoke(
                               timestamp,
                               this,
                               "HTTP/2 Unicast",
                               request,
                               cancellationToken
                           ),
                           nameof(OnDNSRequestReceived)
                       );

                doh2Server.OnDoHResponseSent += (timestamp, server, response, cancellationToken)
                    => LogEvent(
                           OnDNSResponseSent,
                           loggingDelegate => loggingDelegate.Invoke(
                               timestamp,
                               this,
                               "HTTP/2 Unicast",
                               response,
                               cancellationToken
                           ),
                           nameof(OnDNSResponseSent)
                       );

                http2UnicastListener = doh2Server;

                await doh2Server.Start().ConfigureAwait(false);

                var localSocket = new IPSocket(
                                      doh2Server.IPAddress,
                                      doh2Server.TCPPort
                                  );

                ActiveHTTP2UnicastSocket = localSocket;

                await LogEvent(
                          OnDNSHTTP2UnicastListenerStarted,
                          async loggingDelegate => await loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              localSocket,
                              doh2Server.DNSQueryPath,
                              CancellationToken
                          ),
                          nameof(OnDNSHTTP2UnicastListenerStarted)
                      );

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error starting HTTP/2 listener");
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

            if (Options.EnableHTTP2Unicast && Options.TLSServerCertificate is null)
                throw new InvalidOperationException("A TLS server certificate is required for the DNS HTTP/2 listener.");

            // Both DoH listeners default to 443, and two listeners cannot have it.
            // ALPN is what lets one port carry both versions, and this server
            // cannot do that yet — so say which port the second one should take
            // rather than letting the bind fail somewhere inside a listener task.
            if (Options.EnableHTTPSUnicast &&
                Options.EnableHTTP2Unicast &&
                Options.HTTPSUnicastSocket.Port == Options.HTTP2UnicastSocket.Port &&
                Options.HTTPSUnicastSocket.Port != IPPort.Zero)
            {
                throw new InvalidOperationException(
                          $"The HTTP/1.1 and HTTP/2 DoH listeners cannot share port {Options.HTTPSUnicastSocket.Port}. " +
                           "Give HTTP2UnicastSocket a port of its own, or enable only one of them."
                      );
            }

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

            if (Options.EnableHTTP2Unicast)
                await StartHTTP2UnicastAsync(cancellationTokenSource.Token).ConfigureAwait(false);

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

            foreach (var listener in udpUnicastListeners)
                listener.Dispose();

            udpUnicastListener?.  Dispose();
            udpMulticastListener?.Dispose();

            foreach (var listener in tcpUnicastListeners)
                listener.Stop();

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

            if (http2UnicastListener is not null)
            {
                try
                {
                    await http2UnicastListener.Stop().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error stopping HTTP/2 listener");
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
                http2UnicastListener      = null;
                ActiveUDPUnicastSocket    = null;
                ActiveUDPMulticastSocket  = null;
                ActiveTCPUnicastSocket    = null;
                ActiveTLSUnicastSocket    = null;
                ActiveHTTPSUnicastSocket  = null;
                ActiveHTTP2UnicastSocket  = null;
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
