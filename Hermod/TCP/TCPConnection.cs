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

using System.Net;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.TCP
{

    public static class TCPClientExtensions
    {

        /// <summary>
        /// Poll non-blocking for readability
        /// If poll indicates readable but no data available, it's likely closed (EOF detected)
        /// </summary>
        public static Boolean IsConnection__Closed(this TcpClient TCPClient)
        {

            try
            {

                var socket = TCPClient.GetStream().Socket;

                if (socket is null)
                    return true;

                return socket.Poll(0, SelectMode.SelectRead) &&
                      (socket.Available == 0);

            } catch (Exception)
            {
                return true;
            }

        }

    }


    /// <summary>
    /// A TCP connection.
    /// </summary>
    public class TCPConnection : AReadOnlyLocalRemoteSockets,
                                 IEquatable<TCPConnection>,
                                 IComparable<TCPConnection>,
                                 IComparable,
                                 IDisposable
                               //  IAsyncDisposable
    {

        #region Data

        // For method 'ReadLine(...)'...
        private            Boolean    SkipNextN   = false;
        private readonly   ILogger<TCPConnection> logger;

        protected readonly Stopwatch  stopwatch   = Stopwatch.StartNew();

        #endregion

        #region Properties

        /// <summary>
        /// The associated TCP server.
        /// </summary>
        public ITCPServer                                                TCPServer                     { get; }

        /// <summary>
        /// The associated TCP client.
        /// </summary>
        public TcpClient                                                 TCPClient                     { get; }

        public ServerCertificateSelectorDelegate?                        ServerCertificateSelector     { get; }
        public RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator    { get; }
        public LocalCertificateSelectionHandler?                         LocalCertificateSelector      { get; }
        public SslProtocols?                                             AllowedTLSProtocols           { get; }

        #region ReadTimeout

        private Int32 readTimeoutMS;

        /// <summary>
        /// Gets or sets the amount of time, that a read operation
        /// blocks waiting for data. On default the read operation does not time out.
        /// </summary>
        public TimeSpan ReadTimeout
        {

            get
            {
                return TimeSpan.FromMilliseconds(readTimeoutMS);
            }

            set
            {

                readTimeoutMS               = (Int32) value.TotalMilliseconds;

                NetworkStream?.ReadTimeout  = readTimeoutMS;
                SSLStream?.    ReadTimeout  = readTimeoutMS;

            }

        }

        #endregion

        #region WriteTimeout

        private Int32 writeTimeoutMS;

        /// <summary>
        /// Gets or sets the amount of time, that a write operation
        /// blocks. On default the write operation does not time out.
        /// </summary>
        public TimeSpan WriteTimeout
        {

            get
            {
                return TimeSpan.FromMilliseconds(writeTimeoutMS);
            }

            set
            {

                writeTimeoutMS               = (Int32) value.TotalMilliseconds;

                NetworkStream?.WriteTimeout  = writeTimeoutMS;
                SSLStream?.    WriteTimeout  = writeTimeoutMS;

            }

        }

        #endregion


        /// <summary>
        /// The underlying network stream.
        /// </summary>
        public NetworkStream      NetworkStream        { get; }

        /// <summary>
        /// An optional TLS server certificate.
        /// </summary>
        public X509Certificate2?        ServerCertificate       { get; }

        /// <summary>
        /// The certificate above together with the intermediates sent along
        /// with it, when the server names a whole chain rather than a single
        /// certificate.
        /// </summary>
        public ServerCertificateChain?  ServerCertificateChain  { get; }

        /// <summary>
        /// The optional HTTP client certificate.
        /// </summary>
        public X509Certificate2?  ClientCertificate    { get; private set; }

        /// <summary>
        /// The TLS protocol(s) to use.
        /// </summary>
        public SslProtocols       TLSProtocols         { get; }

        /// <summary>
        /// The underlying TLS stream.
        /// </summary>
        public SslStream?         SSLStream            { get; }

        /// <summary>
        /// The timestamp when the TCP connection has been created.
        /// </summary>
        public DateTimeOffset     Created              { get; }

        /// <summary>
        /// The runtime of this TCP connection.
        /// </summary>
        public TimeSpan           Runtime
            => stopwatch.Elapsed;

        /// <summary>
        /// The TCP connection identification.
        /// </summary>
        public String             ConnectionId         { get;}

        /// <summary>
        /// The connection is keepalive
        /// </summary>
        public Boolean            KeepAlive            { get; set; }

        /// <summary>
        /// Server was requested to stop.
        /// </summary>
        public Boolean            StopRequested        { get; set; }

        /// <summary>
        /// Gets a value that indicates whether data is available
        /// on the System.Net.Sockets.NetworkStream to be read.
        /// </summary>
        public Boolean DataAvailable
            => NetworkStream?.DataAvailable == true;

        #region IsConnected

        /// <summary>
        /// Check if the client is still connected to the server.
        /// </summary>
        public Boolean IsConnected
        {

            get
            {

                if (TCPClient is null)
                    return false;

                try
                {

                    //return TCPClientConnection.Connected;

                    // A better, but not really smart way to check if the
                    // TCP connection is/was closed
                    if (TCPClient.Client.Poll(1, SelectMode.SelectRead) &&
                        TCPClient.Available == 0)
                    {
                        return false;
                    }

                }
                catch (Exception)
                {
                    return false;
                }

                return true;

            }

        }

        #endregion

        #region NoDelay

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive
        /// buffers are not full.
        /// </summary>
        public Boolean NoDelay
        {

            get
            {
                return TCPClient.NoDelay;
            }

            set
            {
                TCPClient.NoDelay = value;
            }

        }

        #endregion

        #region IsClosed

        private volatile Boolean isClosed;

        public Boolean IsClosed
        {
            get          => isClosed;
            internal set => isClosed = value;
        }

        #endregion

        #region ClosedBy

        /// <summary>
        /// A ConnectionClosedBy, or -1 while nobody has said.
        /// </summary>
        private Int32 closedBy = -1;

        /// <summary>
        /// Who closed this connection, once somebody has said: whoever called
        /// Close(), or the server, which looks at the socket when its handler
        /// is done with the connection and nobody has said.
        /// </summary>
        public ConnectionClosedBy? ClosedBy
        {
            get
            {
                var value = Volatile.Read(ref closedBy);
                return value < 0 ? null : (ConnectionClosedBy) value;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP connection.
        /// </summary>
        /// <param name="TCPServer">A TCP server.</param>
        /// <param name="TCPClient">A TCP client.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="SSLStream">An optional TLS stream. If not given, a new TLS stream will be created if a server certificate is selected.</param>
        /// <param name="ReadTimeout">An optional read timeout for the underlying stream.</param>
        /// <param name="WriteTimeout">An optional write timeout for the underlying stream.</param>
        public TCPConnection(ITCPServer                                                TCPServer,
                             TcpClient                                                 TCPClient,
                             ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                             RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                             LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                             SslStream?                                                SSLStream                    = null,
                             TimeSpan?                                                 ReadTimeout                  = null,
                             TimeSpan?                                                 WriteTimeout                 = null,
                             ILogger<TCPConnection>?                                   Logger                       = null)

#pragma warning disable CS8602 // Dereference of a possibly null reference.

            : base(

                  new IPSocket(
                      IPAddress.FromDotNet((         TCPClient.Client.LocalEndPoint  as IPEndPoint).Address),
                      IPPort.   Parse     ((UInt16) (TCPClient.Client.LocalEndPoint  as IPEndPoint).Port)
                  ),

                  new IPSocket(
                      IPAddress.FromDotNet((         TCPClient.Client.RemoteEndPoint as IPEndPoint).Address),
                      IPPort.   Parse     ((UInt16) (TCPClient.Client.RemoteEndPoint as IPEndPoint).Port)
                  )

              )

#pragma warning restore CS8602 // Dereference of a possibly null reference.

        {

            this.TCPServer                   = TCPServer ?? throw new ArgumentNullException(nameof(TCPServer), "The given TCP server must not be null!");
            this.TCPClient                   = TCPClient ?? throw new ArgumentNullException(nameof(TCPClient), "The given TCP client must not be null!");
            this.ServerCertificateSelector   = ServerCertificateSelector;
            this.ClientCertificateValidator  = ClientCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.logger                      = Logger ?? NullLogger<TCPConnection>.Instance;

            if (ReadTimeout. HasValue)
                this.ReadTimeout             = ReadTimeout. Value;

            if (WriteTimeout.HasValue)
                this.WriteTimeout            = WriteTimeout.Value;

            this.Created                     = Timestamp.Now;
            this.ConnectionId                = TCPServer.ConnectionIdBuilder(
                                                   this,
                                                   this.Created,
                                                   base.LocalSocket,
                                                   base.RemoteSocket
                                               );
            this.isClosed                    = false;
            this.NetworkStream               = TCPClient.GetStream();

            this.SSLStream                   = SSLStream;
            // The chain selector says everything the plain one does and names
            // the intermediates besides, so where it is set it is the only one
            // asked - two answers could disagree about which certificate this
            // connection uses.
            this.ServerCertificateChain      = TCPServer.ServerCertificateChainSelector?.Invoke(TCPServer, TCPClient);

            this.ServerCertificate           = this.ServerCertificateChain?.Certificate
                                                   ?? ServerCertificateSelector?.Invoke(TCPServer, TCPClient);


            if (this.SSLStream         is null &&
                this.ServerCertificate is not null)
            {

                try
                {

                    //DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] New TLS connection using server certificate: " + this.ServerCertificate.Subject);

                    this.SSLStream  = new SslStream(
                                          innerStream:                        NetworkStream,
                                          leaveInnerStreamOpen:               true,
                                          userCertificateValidationCallback:  null,
                                          userCertificateSelectionCallback:   LocalCertificateSelector is null
                                                                                  ? null
                                                                                  : (sender,
                                                                                     targetHost,
                                                                                     localCertificates,
                                                                                     remoteCertificate,
                                                                                     acceptableIssuers) => LocalCertificateSelector(
                                                                                                               sender,
                                                                                                               targetHost,
                                                                                                               localCertificates.
                                                                                                                   Cast<X509Certificate>().
                                                                                                                   Select(certificate => new X509Certificate2(certificate)),
                                                                                                               remoteCertificate is not null
                                                                                                                   ? new X509Certificate2(remoteCertificate)
                                                                                                                   : null,
                                                                                                               acceptableIssuers
                                                                                                           ),
                                          encryptionPolicy:                   EncryptionPolicy.RequireEncryption
                                      );

                }
                catch (Exception e)
                {
                    logger.LogError(e,
                                    "TLS stream setup failed for server port {LocalPort} {Description} and client port {RemotePort}.",
                                    LocalPort,
                                    TCPServer.Description.IsNotNullOrEmpty() ? TCPServer.Description : String.Empty,
                                    RemotePort);
                    throw;
                }

            }

        }

        #endregion


        #region GetOrCreateCertificateContext(Certificate)

        /// <summary>
        /// Statischer Cache für SslStreamCertificateContext (Thumbprint als Key → keine starke Referenz auf Cert)
        /// </summary>
        private static readonly ConcurrentDictionary<String, SslStreamCertificateContext> certificateContextCache = new();

        /// <summary>
        /// Liefert einen optimierten CertificateContext (offline + kein Netzwerkzugriff)
        /// </summary>
        /// <param name="Certificate">Das Serverzertifikat.</param>
        /// <param name="Chain">
        /// Das Zertifikat samt seinen Intermediates, falls der Server eine
        /// ganze Kette benennt. Ohne die Intermediates baut .NET die Kette aus
        /// den Zertifikatsspeichern dieser Maschine - und weil sie dort bei
        /// einem ACME-Client wie certbot nicht landen, ginge dann nur das Leaf
        /// hinaus. Zusammen mit offline: true, das AIA-Downloads unterbindet,
        /// bliebe die Kette für strengere Clients unvollständig.
        /// </param>
        private static SslStreamCertificateContext GetOrCreateCertificateContext(X509Certificate2        Certificate,
                                                                                 ServerCertificateChain?  Chain   = null)
        {

            // Der Schlüssel umfasst die Intermediates: ein Kontext, der ohne
            // sie gebaut wurde, darf nicht weiterverwendet werden, sobald sie
            // bekannt sind.
            var cacheKey = Chain?.CacheKey ?? Certificate.Thumbprint;

            if (certificateContextCache.TryGetValue(cacheKey, out var cachedContext))
                return cachedContext;

            // Through the chain, so that the one place that knows why a context
            // cannot be built is the one place that builds it - and so that the
            // answer is the same whether it is asked here, in the middle of a
            // handshake, or in advance by whoever is starting a server.
            var chain = Chain ?? new ServerCertificateChain(Certificate);

            if (!chain.TryCreateContext(out var context, out var error))
                // Not the CryptographicException .NET raises, which says
                // "An unknown chain building error occurred" and names neither
                // the certificate nor what is missing. Thrown rather than
                // swallowed: the caller aborts the connection either way, and
                // the difference is whether anybody can find out why.
                throw new InvalidOperationException(error);

            certificateContextCache.TryAdd(cacheKey, context);

            return context;

        }

        #endregion

        #region (internal static) WarmUpCertificateContexts()

        /// <summary>
        /// How often this process has built the throwaway context - at most
        /// once, which is the whole point of it. Here so that a test can say so
        /// rather than a comment claiming it.
        /// </summary>
        internal static Int32 CertificateContextWarmUps
            => certificateContextWarmUps;

        private static Int32 certificateContextWarmUps;

        /// <summary>
        /// Built on whichever thread asks first, and everybody after that is
        /// given the answer.
        /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> rather
        /// than the default: two servers starting at once must not each spend
        /// the half second, and the cost is the process's, so the second one
        /// would buy nothing with it.
        /// </summary>
        private static readonly Lazy<Boolean>  certificateContextsWarmedUp  = new (
                                                                                 BuildThrowawayCertificateContext,
                                                                                 LazyThreadSafetyMode.ExecutionAndPublication
                                                                             );

        /// <summary>
        /// Pay what the first TLS certificate context of this process costs now,
        /// so that the first TLS connection does not.
        /// </summary>
        /// <returns>
        /// Whether a context was built. False costs nothing but the warmth: the
        /// first connection then pays as it did before.
        /// </returns>
        /// <remarks>
        /// <b>Two costs, both once per process, neither of them this
        /// certificate's.</b> Measured on a Windows 11 box, .NET 10, one process
        /// per sample, building contexts for freshly minted certificates:
        ///
        /// <list type="bullet">
        ///   <item>
        ///     a <em>cold chain engine</em> - paid by the first
        ///     <see cref="SslStreamCertificateContext"/> built in the process
        ///     whatever certificate it is for. Native
        ///     Crypt32.CertGetCertificateChain under X509Chain.Build, plus the
        ///     managed X509 code around it. 62 - 109 ms of CPU.
        ///   </item>
        ///   <item>
        ///     a <em>first unresolvable issuer</em> - paid by the first chain
        ///     whose issuer is in none of this machine's stores, which is every
        ///     certificate from a private authority: all of the ones the tests
        ///     mint, and plenty in the field. Waiting rather than work, and not
        ///     a download whatever the waiting looks like - none of the
        ///     certificates involved carries an authority-information-access
        ///     extension, so there is no URL for the platform to go to. It is
        ///     the machine's own stores being searched, and on Windows opened
        ///     for writing. A further 13 - 20 ms.
        ///   </item>
        /// </list>
        ///
        /// Both are then gone: a second, never-seen-before certificate costs
        /// 1 - 3 ms, and so does a second one from a different unknown root.
        ///
        /// <b>Both figures are a property of this machine as much as of this
        /// code, the second one especially.</b> The same probes a few hours
        /// earlier, against the same code, read 0.55 - 0.73 s of CPU for the
        /// first and 0.35 - 5.2 s of wall clock for the second. What changed in
        /// between is that 3,825 certificates which earlier test runs had left
        /// behind were deleted from CurrentUser\CA, taking it from 3,970 entries
        /// to about 200. Opening a registry certificate store costs time in
        /// proportion to what is in it, and a chain that cannot be completed is
        /// precisely what sends the platform to those stores.
        ///
        /// So the saving is not a constant. On a machine whose stores have grown
        /// - and they grow on their own, which is what
        /// ServerCertificateChainTests.UniqueName is about - it is seconds; on a
        /// tidy one it is the tens of milliseconds above. Worth having either
        /// way, and worth measuring again rather than believing these numbers,
        /// because the next machine will not be this one.
        ///
        /// <b>Why a made-up certificate rather than this server's own.</b> A
        /// server does not know at start which certificate it will present.
        /// <see cref="ITCPServer.ServerCertificateChainSelector"/> and the
        /// narrower ServerCertificateSelector are asked per connection and take
        /// the <see cref="TcpClient"/> as an argument, because an SNI-driven
        /// server may legitimately answer differently for different clients.
        /// There is no client at start, and calling consumer code with a
        /// stand-in for one is a contract this cannot keep. So a server with a
        /// genuinely dynamic selector cannot be warmed <em>for its
        /// certificate</em> - and does not need to be, because what is expensive
        /// belongs to the process and not to the certificate.
        ///
        /// <b>What this leaves on the first connection.</b> Its own
        /// <see cref="SslStreamCertificateContext"/>, at 1 - 3 ms. Nothing is
        /// put into <see cref="certificateContextCache"/> here: that is keyed by
        /// certificate, and this one is thrown away.
        ///
        /// <b>Why the throwaway certificate has an issuer that is nowhere.</b>
        /// Because only that shape warms both costs, which was worth measuring
        /// rather than assuming. A self-signed chain is complete at the leaf, so
        /// it never sends the platform looking for an issuer it has no copy of:
        /// with a self-signed throwaway the CPU moves to the warm-up as
        /// intended and the first real certificate still pays the lookup, at
        /// 15 - 22 ms. With a leaf signed by a root that is neither installed
        /// nor sent along, the warm-up pays both and the first real certificate
        /// costs 1.3 - 2.1 ms.
        ///
        /// That gap was seconds rather than milliseconds before this machine's
        /// CA store was emptied of the certificates earlier test runs had left
        /// in it - the self-signed shape left the first real certificate waiting
        /// 0.14 - 5.2 s. Which is the honest case for this shape: it buys little
        /// on a tidy machine and a great deal on one that has been worked on,
        /// and a server cannot tell in advance which it is running on.
        ///
        /// <b>What that costs a server it does not help.</b> Where the
        /// certificate chains to a root this machine already has - a publicly
        /// trusted one, with its intermediates sent along - the lookup would
        /// never have happened, and the warm-up now makes it happen once at
        /// start. That half of the warm-up is wasted for such a server - tens
        /// of milliseconds here, and seconds on a machine with a grown store -
        /// though the chain-engine half is one it pays for anyway. It is the
        /// default because the servers built on this one are mostly not of that
        /// kind, private authorities and device CAs being the common case here,
        /// and because guessing wrong is asymmetric: a server that needed it and
        /// did not get it makes a client wait, and one that got it and did not
        /// need it waits for nothing on its own time.
        /// </remarks>
        internal static Boolean WarmUpCertificateContexts()

            => certificateContextsWarmedUp.Value;

        #endregion

        #region (private static) BuildThrowawayCertificateContext()

        /// <summary>
        /// Build a context for a certificate invented on the spot and dropped
        /// again, purely so that what is behind it is warm.
        /// </summary>
        private static Boolean BuildThrowawayCertificateContext()
        {

            Interlocked.Increment(ref certificateContextWarmUps);

            // The second shape is a fallback rather than an addition: it warms
            // the chain engine but not the issuer lookup, and it is only reached
            // if a platform refuses to make a context for a chain it cannot
            // complete. This one does not refuse - measured, not assumed - but
            // one that did would otherwise leave the whole cost on the first
            // connection rather than the smaller half of it.
            return TryBuildThrowawayContext(UnreachableIssuer: true) ||
                   TryBuildThrowawayContext(UnreachableIssuer: false);

        }

        /// <summary>
        /// Mint a throwaway certificate and build its context.
        /// </summary>
        /// <param name="UnreachableIssuer">
        /// Whether to sign it with a root that is neither installed on this
        /// machine nor sent along with it, so that building its chain makes the
        /// platform look for an issuer it will not find - the wait that the
        /// first real certificate would otherwise be the one to pay.
        /// </param>
        private static Boolean TryBuildThrowawayContext(Boolean UnreachableIssuer)
        {

            // Nothing beyond basic constraints: this certificate is never shown
            // to anybody, and what is being warmed is the chain machinery
            // rather than anything that inspects a server certificate's fitness.
            //
            // P-256 and not RSA because the key is generated while a server is
            // starting: an ECDSA key costs about a millisecond, a 2048-bit RSA
            // key a good deal more than the build it would be hiding.
            //
            // Names that no certificate has used before, for the reason written
            // up on ServerCertificateChainTests.UniqueName: Windows resolves an
            // issuer by name, and a name reused by certificate after certificate
            // with a different key each time is how a machine's chain engine
            // gets to the point of refusing to build anything. Nothing here is
            // handed to SslStreamCertificateContext as an intermediate, which is
            // what gets installed, so there should be nothing to accumulate -
            // and a unique name costs a Guid, so it is not worth being right
            // about.
            try
            {

                var       now          = Timestamp.Now;
                var       unique       = Guid.NewGuid().ToString("N")[..8];

                using var leafKey      = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var       leafRequest  = new CertificateRequest(
                                             $"CN=Hermod TLS warm-up {unique}",
                                             leafKey,
                                             HashAlgorithmName.SHA256
                                         );

                leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

                if (!UnreachableIssuer)
                {

                    using var selfSigned = leafRequest.CreateSelfSigned(
                                               now.AddHours(-1),
                                               now.AddHours( 1)
                                           );

                    return new ServerCertificateChain(selfSigned).TryCreateContext(out _, out _);

                }

                using var issuerKey      = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var       issuerRequest  = new CertificateRequest(
                                               $"CN=Hermod TLS warm-up issuer {unique}",
                                               issuerKey,
                                               HashAlgorithmName.SHA256
                                           );

                issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

                using var issuer         = issuerRequest.CreateSelfSigned(
                                               now.AddHours(-1),
                                               now.AddHours( 1)
                                           );

                using var signed         = leafRequest.Create(
                                               issuer,
                                               now.AddHours(-1),
                                               now.AddHours( 1),
                                               Guid.NewGuid().ToByteArray()
                                           );

                // The chain is given the leaf alone. Handing it the issuer would
                // complete it, and a chain that completes is the one case that
                // does not warm the lookup.
                using var leaf           = signed.CopyWithPrivateKey(leafKey);

                return new ServerCertificateChain(leaf).TryCreateContext(out _, out _);

            }
            catch
            {
                // Best effort by construction. There is nothing to report and
                // nobody waiting for it: a warm-up that fails leaves the first
                // connection paying what it would have paid anyway.
                return false;
            }

        }

        #endregion

        #region AuthenticateAsServer (Timeout = null, ...)

        /// <summary>
        /// Authenticates the server side of the TLS connection asynchronously.
        /// Will be called async from the server to avoid blocking the main accept loop!
        /// </summary>
        /// <param name="Timeout"></param>
        /// <param name="CancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public async Task AuthenticateAsServer(ITCPServer         TCPServer,
                                               TimeSpan?          Timeout             = null,
                                               CancellationToken  CancellationToken   = default)
        {

            if (SSLStream         is null ||
                ServerCertificate is null ||
                SSLStream.IsAuthenticated)
            {
                return;
            }

            // Create a cachable certificate context
            var certificateContext              = GetOrCreateCertificateContext(ServerCertificate, ServerCertificateChain);

            // Secure TLS server authentication options (no downloads, no revocation checks, ...)
            //     => No denial-of-service attacks possible!
            var chainPolicy                     = new X509ChainPolicy {
                                                      DisableCertificateDownloads     = true,           // ← verhindert AIA-Downloads
                                                      RevocationMode                  = X509RevocationMode.   NoCheck,
                                                      RevocationFlag                  = X509RevocationFlag.   ExcludeRoot,
                                                      VerificationFlags               = X509VerificationFlags.IgnoreRootRevocationUnknown |
                                                                                        X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown |
                                                                                        X509VerificationFlags.IgnoreEndRevocationUnknown
                                                  };

            var tlsServerAuthenticationOptions  = new SslServerAuthenticationOptions {
                                                      ServerCertificateContext        = certificateContext,
                                                      ClientCertificateRequired       = ClientCertificateValidator is not null,
                                                      CertificateRevocationCheckMode  = X509RevocationMode.NoCheck,
                                                      CertificateChainPolicy          = chainPolicy,
                                                      // The server's: this connection's own is never set, and a
                                                      // server told to allow TLS 1.2 only took 1.3 all the same.
                                                      EnabledSslProtocols             = AllowedTLSProtocols ?? TCPServer.AllowedTLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13,
                                                      EncryptionPolicy                = EncryptionPolicy.RequireEncryption
                                                  };

            // RFC 7301: a server that offers nothing negotiates nothing, and a
            // client's ALPN extension goes unanswered — which is what every TLS
            // listener here did. curl, every browser and every HTTP/2-capable
            // client sends the extension; the HTTP/1 server now says "http/1.1"
            // back.
            //
            // The list comes from the server rather than from a constant here,
            // because this class carries the Modbus and DNS-over-TLS frontends
            // too and none of them may claim to speak HTTP.
            var applicationProtocols = TCPServer.TLSApplicationProtocols.ToList();

            if (applicationProtocols.Count > 0)
                tlsServerAuthenticationOptions.ApplicationProtocols = applicationProtocols;


            if (ClientCertificateValidator is not null)
                tlsServerAuthenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) =>
                    ClientCertificateValidator(
                        sender,
                        certificate is not null ? new X509Certificate2(certificate) : null,
                        chain,
                        TCPServer,
                        policyErrors
                    ).IsValid;


            var authenticateTask = SSLStream.AuthenticateAsServerAsync(
                                       tlsServerAuthenticationOptions,
                                       CancellationToken
                                   );

            // Timeout-Schutz
            var timeout = Timeout ?? ReadTimeout;
            if (timeout > TimeSpan.Zero)
            {

                // The delay gets a token of its own, cancelled as soon as the
                // race is decided. A handshake that won used to leave the delay
                // pending, and its timer with it, for the whole timeout: one
                // timer per TLS connection, 30 seconds each at a server's
                // default.
                //
                // That token is linked to the caller's, and made where the delay
                // used to take the caller's: after the handshake has started. A
                // token source runs the callbacks of a cancellation newest first,
                // so a cancellation still ends the delay before the handshake,
                // and still comes out as the TimeoutException below. With
                // Task.WaitAsync(timeout, CancellationToken) it would come out
                // as an OperationCanceledException.
                using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

                var completedTask = await Task.WhenAny(
                                              authenticateTask,
                                              Task.Delay(
                                                  timeout,
                                                  delayCancellation.Token
                                              )
                                          ).ConfigureAwait(false);

                delayCancellation.Cancel();

                if (completedTask != authenticateTask)
                {

                    _ = authenticateTask.ContinueWith(
                            t => t.Exception?.Handle(_ => true),
                            TaskContinuationOptions.OnlyOnFaulted |
                            TaskContinuationOptions.ExecuteSynchronously
                        );

                    try
                    {
                        await SSLStream.DisposeAsync();
                    }
                    catch { }

                    throw new TimeoutException($"TLS server authentication timed out after {timeout.TotalMilliseconds:N0} ms for client {RemoteSocket}!");

                }

            }

            await authenticateTask.ConfigureAwait(false);

            if (SSLStream.RemoteCertificate is not null)
                ClientCertificate = new X509Certificate2(SSLStream.RemoteCertificate);

        }

        #endregion



        /// <summary>
        /// Poll non-blocking for readability
        /// If poll indicates readable but no data available, it's likely closed (EOF detected)
        /// </summary>
        /// <summary>
        /// Whether the peer appears to have gone away.
        ///
        /// **Only meaningful when nothing else is reading this connection.** The
        /// test is <c>Poll(SelectRead) &amp;&amp; Available == 0</c>: the poll is
        /// true when the socket is readable, which means data has arrived *or*
        /// the peer closed, and <c>Available</c> is what separates the two. A
        /// concurrent reader that takes the bytes between the two calls turns a
        /// live connection into a false "closed".
        ///
        /// That is not hypothetical — it is HTTP1ConformanceTests finding H-25,
        /// where this predicate made the TCP server's Warden close connections in
        /// the middle of a transfer. The Warden no longer uses it; it reaps on the
        /// handler task instead. Anything else calling this must be the only
        /// reader, or must treat a true as a hint rather than as an answer.
        /// </summary>
        public Boolean IsConnectionClosed()
        {
            try
            {

                var socket = NetworkStream.Socket;

                if (socket is null)
                    return true;

                return socket.Poll(0, SelectMode.SelectRead) &&
                      (socket.Available == 0);

            }
            catch (Exception)
            {
                return true;
            }
        }


        #region Read(SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a byte value from the TCP connection.
        /// </summary>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The read byte OR 0x00 if nothing could be read.</returns>
        public Byte Read(UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!NetworkStream.CanRead)
                return 0x00;

            var WaitingTimeMS = 0;
            var Value         = -1;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                Value = SSLStream is not null
                            ? SSLStream.    ReadByte()
                            : NetworkStream.ReadByte();
            }

            if (Value == -1)
                return 0x00;

            return (Byte) Value;

        }

        #endregion

        #region TryRead(out Byte, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Try to read a byte value from the TCP connection.
        /// </summary>
        /// <param name="Byte">The byte value OR 0x00 if nothing could be read.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>True, if the byte value is valid; False otherwise.</returns>
        public TCPClientResponse TryRead(out Byte Byte, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 50)
        {

            Byte = 0x00;

            if (!NetworkStream.CanRead)
                return TCPClientResponse.CanNotRead;

            if (SSLStream is not null)
            {

                var value = SSLStream.ReadByte();

                if (value == -1)
                    return TCPClientResponse.CanNotRead;

                Byte = (Byte) value;

                return TCPClientResponse.DataAvailable;

            }

            var WaitingTimeMS  = 0;
            var Value          = -1;


            //if (TCPClientConnection.Client.Poll(1, SelectMode.SelectRead) &&
            //    TCPClientConnection.Available == 0)


            while (TCPClient.Client.Poll(1, SelectMode.SelectRead) == false &&
                   NetworkStream.DataAvailable == false &&
                   (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                Value = SSLStream is not null
                            ? SSLStream.    ReadByte()
                            : NetworkStream.ReadByte();
            }
            else
            {
                if (WaitingTimeMS >= MaxInitialWaitingTimeMS)
                    return TCPClientResponse.Timeout;
                else
                    return TCPClientResponse.ClientClose;
            }

            if (Value == -1)
                return TCPClientResponse.CanNotRead;

            Byte = (Byte) Value;

            return TCPClientResponse.DataAvailable;

        }

        #endregion

        #region Read(Buffer, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read multiple byte values from the TCP connection into the given buffer.
        /// </summary>
        /// <param name="Buffer">An array of byte values.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The number of read bytes.</returns>
        public Int32 Read(Byte[] Buffer, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (Buffer is null || Buffer.Length < 1)
                throw new ArgumentNullException(nameof(Buffer), "The given buffer must not be null or empty!");

            if (!NetworkStream.CanRead)
                return 0;

            var WaitingTimeMS = 0;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                return SSLStream is not null
                           ? SSLStream.    Read(Buffer, 0, Buffer.Length)
                           : NetworkStream.Read(Buffer, 0, Buffer.Length);
            }

            return 0;

        }

        #endregion

        #region ReadString(MaxLength = 1024, Encoding = null, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a string value from the TCP connection.
        /// </summary>
        /// <param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        public String ReadString(Int32 MaxLength = 1024, Encoding? Encoding = null, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!NetworkStream.CanRead)
                return String.Empty;

            var WaitingTimeMS = 0;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {

                Byte ByteValue;
                var ByteArray  = new Byte[MaxLength];
                var Position   = 0U;

                while (NetworkStream.DataAvailable)
                {

                    ByteValue = SSLStream is not null
                                    ? (Byte) SSLStream.    ReadByte()
                                    : (Byte) NetworkStream.ReadByte();

                    ByteArray[Position++] = ByteValue;

                    if (ByteValue == 0x00 ||
                        Position  == MaxLength)
                        break;

                }

                if (Position > 0)
                {

                    Encoding ??= Encoding.UTF8;

                    Array.Resize(ref ByteArray, (Int32) Position - 1);

                    return Encoding.GetString(ByteArray);

                }

            }

            return String.Empty;

        }

        #endregion

        #region ReadLine(MaxLength = 65535, Encoding = null, SleepingTimeMS = null, MaxInitialWaitingTimeMS = null, ReadTimeout = null)

        /// <summary>
        /// Read a line from the TCP connection.
        /// </summary>
        /// <param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTime">When no data is currently available wait at least this amount of time [5ms].</param>
        /// <param name="MaxInitialWaitingTime">When no data is currently available wait at most this amount of time [500ms].</param>
        /// <param name="__ReadTimeout">The read timeout [20sec].</param>
        public String? ReadLine(Int32      MaxLength               = 65535,
                                Encoding?  Encoding                = null,
                                TimeSpan?  SleepingTime            = null,
                                TimeSpan?  MaxInitialWaitingTime   = null,
                                TimeSpan?  __ReadTimeout           = null)
        {

            if (!NetworkStream.CanRead)
                return null;

            if (!SleepingTime.HasValue)
                SleepingTime = TimeSpan.FromMilliseconds(5);

            MaxInitialWaitingTime ??= TimeSpan.FromMilliseconds(500);

            if (__ReadTimeout.HasValue)
            {
                NetworkStream.ReadTimeout  = (Int32) __ReadTimeout.Value.TotalMilliseconds;
                SSLStream?.   ReadTimeout  = (Int32) __ReadTimeout.Value.TotalMilliseconds;
            }

            var sleepingTimeMS           = (Int32) SleepingTime.Value.TotalMilliseconds;
            var maxInitialWaitingTimeMS  = (Int32) MaxInitialWaitingTime.Value.TotalMilliseconds;
            var totalWaitingTime         = 0;

            while (!NetworkStream.DataAvailable && (totalWaitingTime < maxInitialWaitingTimeMS))
            {
                Thread.Sleep(sleepingTimeMS);
                totalWaitingTime += sleepingTimeMS;
            }

            var Started = Timestamp.Now;

            if (NetworkStream.DataAvailable)
            {

                Int32 ByteValue;
                var ByteArray  = new Byte[MaxLength];
                var Position   = 0;

                try
                {

                    // Do not stop (on slow connections)
                    //  before a valid EOL was found!
                    do
                    {

                        ByteValue = SSLStream is not null
                                        ? SSLStream.ReadByte()
                                        : NetworkStream.ReadByte();

                        #region Nothing or a '\r' or a '\n' was read!

                        // Nothing was read!
                        if (ByteValue == -1)
                        {
                            Thread.Sleep(sleepingTimeMS);
                            continue;
                        }

                        // Last time a '\r' was read, thus this might be the '\n' of it!
                        if (SkipNextN && ByteValue == '\n')
                        {
                            SkipNextN = false;
                            continue;
                        }

                        if (ByteValue == '\r')
                        {
                            SkipNextN = true;
                            break;
                        }

                        if (ByteValue == '\n' ||
                            Position == MaxLength)
                            break;

                        #endregion

                        ByteArray[Position++] = (Byte) ByteValue;

                    } while (Timestamp.Now - Started < ReadTimeout);

                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error reading TCP connection {RemoteIPAddress}:{RemotePort}.", RemoteIPAddress, RemotePort);
                }

                if (Position > 0)
                {

                    Encoding ??= Encoding.UTF8;

                    Array.Resize(ref ByteArray, Position);

                    return Encoding.GetString(ByteArray);

                }

                // An empty line was read!
                return "";

            }

            return null;

        }

        #endregion


        #region WriteToResponseStream(UTF8Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="UTF8Text">Some UTF-8 text.</param>
        public void WriteToResponseStream(String UTF8Text)
        {
            WriteToResponseStream(UTF8Text.ToUTF8Bytes());
        }

        #endregion

        #region WriteLineToResponseStream(UTF8Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="UTF8Text">Some UTF-8 text.</param>
        public void WriteLineToResponseStream(String UTF8Text)
        {
            WriteToResponseStream((UTF8Text + "\r\n").ToUTF8Bytes());
        }

        #endregion

        #region WriteToResponseStream(Byte)

        /// <summary>
        /// Writes the given byte to the underlying stream.
        /// </summary>
        /// <param name="Byte">A single byte.</param>
        public void WriteToResponseStream(Byte Byte)
        {
            if (IsConnected)
            {

                if (SSLStream is not null)
                    SSLStream.    WriteByte(Byte);

                else
                    NetworkStream.WriteByte(Byte);

            }
        }

        #endregion

        #region WriteToResponseStream(ByteArray)

        /// <summary>
        /// Writes the given byte array to the underlying stream.
        /// </summary>
        /// <param name="ByteArray">An array of bytes.</param>
        public void WriteToResponseStream(Byte[] ByteArray)
        {

            if (ByteArray is not null && IsConnected)
            {

                if (SSLStream is not null)
                    SSLStream.    Write(ByteArray, 0, ByteArray.Length);

                else if (NetworkStream is not null)
                    NetworkStream.Write(ByteArray, 0, ByteArray.Length);

                else
                    logger.LogWarning("{ConnectionType} SSLStream and NetworkStream are both null.", nameof(TCPConnection));

            }

            else if (!IsConnected)
                logger.LogWarning("{ConnectionType} could not write to response stream: not connected.", nameof(TCPConnection));

            else
                logger.LogWarning("{ConnectionType} could not write to response stream: byte array is null.", nameof(TCPConnection));

        }

        #endregion

        #region WriteToResponseStream(InputStream, ReadTimeout = 1000, BufferSize = 65535)

        /// <summary>
        /// Reads the given input stream and writes its content to the underlying stream.
        /// </summary>
        /// <param name="InputStream">A data source.</param>
        /// <param name="ReadTimeout">A read timeout on the source.</param>
        /// <param name="BufferSize">The buffer size for reading.</param>
        public void WriteToResponseStream(Stream InputStream, Int32 ReadTimeout = 1000, Int32 BufferSize = 65535)
        {

            if (IsConnected)
            {

                var buffer     = new Byte[BufferSize];
                var bytesRead  = 0;

                if (InputStream.CanTimeout && ReadTimeout != 1000)
                    InputStream.ReadTimeout = ReadTimeout;

                if (IsConnected)
                {
                    do
                    {

                        bytesRead = InputStream.Read(buffer, 0, buffer.Length);

                        if (SSLStream is not null)
                            SSLStream.    Write(buffer, 0, bytesRead);

                        else
                            NetworkStream.Write(buffer, 0, bytesRead);

                    } while (bytesRead != 0);
                }

            }

        }

        #endregion


        public virtual JObject ToJSON()
        {

            return JSONObject.Create(

                             new JProperty("socket",                  RemoteSocket.ToString()),
                             new JProperty("connectionId",            ConnectionId),
                             new JProperty("isConnected",             IsConnected),
                             new JProperty("created",                 Created.ToISO8601()),
                             new JProperty("runtime",        (UInt64) Runtime.TotalSeconds),
                             new JProperty("isTLS",                   SSLStream is not null),

                       ClientCertificate is not null
                           ? new JProperty("clientCertificate",       ClientCertificate.Subject)
                           : null

                   );

        }


        #region Flush()

        /// <summary>
        /// Flush all streams.
        /// </summary>
        public void Flush()
        {
            SSLStream?.    Flush();
            NetworkStream?.Flush();
        }

        #endregion

        #region (internal) RecordClosedBy(ClosedBy)

        /// <summary>
        /// Record who closed this connection, unless somebody has already: the
        /// first to say is the one who closed it.
        /// </summary>
        /// <remarks>
        /// Whoever closes the connection says so before closing anything. The
        /// server looks at the socket when nobody has said, and on Linux a
        /// socket that this side has shut down looks just like one whose client
        /// hung up: readable, with nothing to read. (On Windows it does not.)
        /// Looked at first and recorded afterwards, it cannot be mistaken for
        /// one: either the close recorded first, and its answer stands, or it
        /// recorded after the server, and so closed after the server had looked.
        /// </remarks>
        /// <param name="ClosedBy">Who closed this connection.</param>
        /// <returns>Who closed this connection - the given one, or whoever said before.</returns>
        internal ConnectionClosedBy RecordClosedBy(ConnectionClosedBy ClosedBy)
        {

            var before = Interlocked.CompareExchange(ref closedBy, (Int32) ClosedBy, -1);

            return before < 0
                       ? ClosedBy
                       : (ConnectionClosedBy) before;

        }

        #endregion

        #region (internal) ClientHasHungUp()

        /// <summary>
        /// Whether the client has hung up, as far as the socket can tell: closed
        /// its end of the connection, or reset it.
        /// </summary>
        /// <remarks>
        /// Asked when the handler of the connection is done with it, and only
        /// then, because like IsConnectionClosed() it only tells the truth while
        /// nothing else reads from the connection. A socket that is closed
        /// already tells nothing, and neither does one with bytes still unread
        /// in front of the end: both say no.
        /// </remarks>
        internal Boolean ClientHasHungUp()
        {
            try
            {

                // Null once the TcpClient is closed.
                var socket = TCPClient.Client;

                return socket is not null &&
                       socket.Poll(0, SelectMode.SelectRead) &&
                       socket.Available == 0;

            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Close(ClosedBy = ConnectionClosedBy.Server)

        /// <summary>
        /// Close this TCP connection.
        /// </summary>
        /// <remarks>
        /// Reports nothing. The server reports the connection closed once its
        /// handler is done with it, and says who closed it as said here.
        /// </remarks>
        /// <param name="ClosedBy">Who closed the connection: the first to say is the one who did.</param>
        public void Close(ConnectionClosedBy ClosedBy = ConnectionClosedBy.Server)
        {

            // Before anything is closed - see RecordClosedBy().
            RecordClosedBy(ClosedBy);

            if (!isClosed)
            {
                try
                {

                    if (NetworkStream is not null && NetworkStream.DataAvailable)
                    {

                        var buffer = new Byte[1024];

                        do
                        {
                            try
                            {
                                var read = NetworkStream.Read(buffer, 0, buffer.Length);
                            }
                            catch (Exception e)
                            {
                                logger.LogDebug(e, "Error reading before closing TCP connection {ConnectionId}.", ConnectionId);
                            }
                        } while (NetworkStream.DataAvailable);

                        NetworkStream.Close();
                        NetworkStream.Dispose();

                    }

                    if (SSLStream is not null)
                    {
                        SSLStream.Close();
                        SSLStream.Dispose();
                    }

                    if (TCPClient?.Client?.Connected == true)
                    {
                        TCPClient.Client.Shutdown(SocketShutdown.Both);
                    }

                    TCPClient?.Close();
                    TCPClient?.Dispose();

                    isClosed = true;

                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error closing TCP connection {ConnectionId}.", ConnectionId);
                }
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Dispose this TCP connection.
        /// </summary>
        /// <remarks>
        /// Reports nothing, as Close() does not.
        /// </remarks>
        public override void Dispose()
        {
            if (!isClosed)
            {
                try
                {

                    if (SSLStream     is not null)
                    {
                        SSLStream.Close();
                        SSLStream.Dispose();
                    }

                    if (NetworkStream is not null)
                    {
                        NetworkStream.Close();
                        NetworkStream.Dispose();
                    }

                    if (TCPClient     is not null)
                    {
                        TCPClient.Close();
                        TCPClient.Dispose();
                    }

                    isClosed = true;

                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error disposing TCP connection {ConnectionId}.", ConnectionId);
                }
            }
            GC.SuppressFinalize(this);
        }

        #endregion


        #region Operator overloading

        #region Operator == (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two TCP connections for equality.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (TCPConnection? TCPConnection1,
                                           TCPConnection? TCPConnection2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(TCPConnection1, TCPConnection2))
                return true;

            if (TCPConnection1 is null || TCPConnection2 is null)
                return false;

            return TCPConnection1.Equals(TCPConnection2);

        }

        #endregion

        #region Operator != (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two TCP connections for inequality.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (TCPConnection? TCPConnection1,
                                           TCPConnection? TCPConnection2)

            => !(TCPConnection1 == TCPConnection2);

        #endregion

        #region Operator <  (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator < (TCPConnection? TCPConnection1,
                                          TCPConnection? TCPConnection2)
        {

            if (TCPConnection1 is null)
                throw new ArgumentNullException(nameof(TCPConnection1), "The given TCP connection must not be null!");

            return TCPConnection1.CompareTo(TCPConnection2) < 0;

        }

        #endregion

        #region Operator <= (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator <= (TCPConnection? TCPConnection1,
                                           TCPConnection? TCPConnection2)

            => !(TCPConnection1 > TCPConnection2);

        #endregion

        #region Operator >  (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator > (TCPConnection? TCPConnection1,
                                          TCPConnection? TCPConnection2)
        {

            if (TCPConnection1 is null)
                throw new ArgumentNullException(nameof(TCPConnection1), "The given TCP connection must not be null!");

            return TCPConnection1.CompareTo(TCPConnection2) > 0;

        }

        #endregion

        #region Operator >= (TCPConnection1, TCPConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TCPConnection1">A TCP connection.</param>
        /// <param name="TCPConnection2">Another TCP connection.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator >= (TCPConnection? TCPConnection1,
                                           TCPConnection? TCPConnection2)

            => !(TCPConnection1 < TCPConnection2);

        #endregion

        #endregion

        #region IComparable<TCPConnection> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two TCP connections.
        /// </summary>
        /// <param name="Object">A TCP connection to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is TCPConnection tcpConnection
                   ? CompareTo(tcpConnection)
                   : throw new ArgumentException("The given object is not a TCP connection!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(TCPConnection)

        /// <summary>
        /// Compares two TCP connections.
        /// </summary>
        /// <param name="TCPConnection">A TCP connection to compare with.</param>
        public Int32 CompareTo(TCPConnection? TCPConnection)
        {

            if (TCPConnection is null)
                throw new ArgumentNullException(nameof(TCPConnection), "The given TCP connection must not be null!");

            return ConnectionId.CompareTo(TCPConnection.ConnectionId);

        }

        #endregion

        #endregion

        #region IEquatable<TCPConnection> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two TCP connections for equality.
        /// </summary>
        /// <param name="Object">A TCP connection to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is TCPConnection tcpConnection &&
                   Equals(tcpConnection);

        #endregion

        #region Equals(TCPConnection)

        /// <summary>
        /// Compares two TCP connections for equality.
        /// </summary>
        /// <param name="TCPConnection">A TCP connection to compare with.</param>
        public Boolean Equals(TCPConnection? TCPConnection)

            => TCPConnection is not null &&
               ConnectionId.Equals(TCPConnection.ConnectionId);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => ConnectionId.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => ConnectionId;

        #endregion

    }

}
