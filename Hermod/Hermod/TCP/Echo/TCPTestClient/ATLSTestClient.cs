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

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TLS echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class ATLSTestClient : ATCPTestClient
    {

        #region Data

        /// <summary>
        /// The TLS stream.
        /// </summary>
        protected SslStream? tlsStream;

        #endregion

        #region Properties

        /// <summary>
        /// The remote TLS server certificate validation handler.
        /// </summary>
        public RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidationHandler    { get; }
        public LocalCertificateSelectionHandler?                             LocalCertificateSelector              { get; }
        public IEnumerable<X509Certificate>                                  ClientCertificateChain                { get; } = [];
        public SslProtocols?                                                 TLSProtocols                          { get; }
        public CipherSuitesPolicy?                                           CipherSuitesPolicy                    { get; }
        public X509ChainPolicy?                                              CertificateChainPolicy                { get; }
        public IEnumerable<SslApplicationProtocol>                           ApplicationProtocols                  { get; } = [];
        public Boolean                                                       EnforceTLS                            { get; }
        public Boolean?                                                      AllowRenegotiation                    { get; }
        public Boolean?                                                      AllowTLSResume                        { get; }

        #endregion

        #region Constructor(s)

        #region (protected) ATLSTestClient(IPAddress, TCPPort, ...)

        protected ATLSTestClient(IIPAddress                                                    IPAddress,
                                 IPPort                                                        TCPPort,
                                 I18NString?                                                   Description                  = null,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator   = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector     = null,
                                 IEnumerable<X509Certificate>?                                 ClientCertificateChain       = null,
                                 SslProtocols?                                                 TLSProtocols                 = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy           = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy       = null,
                                 Boolean?                                                      EnforceTLS                   = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols         = null,
                                 Boolean?                                                      AllowRenegotiation           = null,
                                 Boolean?                                                      AllowTLSResume               = null,
                                 TimeSpan?                                                     ConnectTimeout               = null,
                                 TimeSpan?                                                     ReceiveTimeout               = null,
                                 TimeSpan?                                                     SendTimeout                  = null,
                                 UInt32?                                                       BufferSize                   = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler               = null)

            : base(IPAddress,
                   TCPPort,
                   Description,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        {

            this.RemoteCertificateValidationHandler  = RemoteCertificateValidator;
            this.LocalCertificateSelector            = LocalCertificateSelector;
            this.ClientCertificateChain              = ClientCertificateChain           ?? [];
            this.TLSProtocols                        = TLSProtocols                     ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.EnforceTLS                          = EnforceTLS                       ?? false;
            this.ApplicationProtocols                = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation                  = AllowRenegotiation;
            this.AllowTLSResume                      = AllowTLSResume;

        }

        #endregion

        #region (protected) ATLSTestClient(URL,        DNSService = null, ..., DNSClient = null)

        protected ATLSTestClient(URL                                                           URL,
                                 SRV_Spec?                                                     DNSService                   = null,
                                 I18NString?                                                   Description                  = null,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator   = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector     = null,
                                 IEnumerable<X509Certificate>?                                 ClientCertificateChain       = null,
                                 SslProtocols?                                                 TLSProtocols                 = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy           = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy       = null,
                                 Boolean?                                                      EnforceTLS                   = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols         = null,
                                 Boolean?                                                      AllowRenegotiation           = null,
                                 Boolean?                                                      AllowTLSResume               = null,
                                 TimeSpan?                                                     ConnectTimeout               = null,
                                 TimeSpan?                                                     ReceiveTimeout               = null,
                                 TimeSpan?                                                     SendTimeout                  = null,
                                 UInt32?                                                       BufferSize                   = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler               = null,
                                 DNSClient?                                                    DNSClient                    = null)

            : base(URL,
                   DNSService,
                   Description,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.RemoteCertificateValidationHandler  = RemoteCertificateValidator;
            this.LocalCertificateSelector            = LocalCertificateSelector;
            this.ClientCertificateChain              = ClientCertificateChain           ?? [];
            this.TLSProtocols                        = TLSProtocols                     ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.EnforceTLS                          = EnforceTLS                       ?? false;
            this.ApplicationProtocols                = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation                  = AllowRenegotiation;
            this.AllowTLSResume                      = AllowTLSResume;

        }

        #endregion

        #region (protected) ATLSTestClient(DomainName, DNSService,        ..., DNSClient = null)

        protected ATLSTestClient(DomainName                                                    DomainName,
                                 SRV_Spec                                                      DNSService,
                                 I18NString?                                                   Description                  = null,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator   = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector     = null,
                                 IEnumerable<X509Certificate>?                                 ClientCertificateChain       = null,
                                 SslProtocols?                                                 TLSProtocols                 = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy           = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy       = null,
                                 Boolean?                                                      EnforceTLS                   = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols         = null,
                                 Boolean?                                                      AllowRenegotiation           = null,
                                 Boolean?                                                      AllowTLSResume               = null,
                                 TimeSpan?                                                     ConnectTimeout               = null,
                                 TimeSpan?                                                     ReceiveTimeout               = null,
                                 TimeSpan?                                                     SendTimeout                  = null,
                                 UInt32?                                                       BufferSize                   = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler               = null,
                                 DNSClient?                                                    DNSClient                    = null)

            : base(DomainName,
                   DNSService,
                   Description,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.RemoteCertificateValidationHandler  = RemoteCertificateValidator;
            this.LocalCertificateSelector            = LocalCertificateSelector;
            this.ClientCertificateChain              = ClientCertificateChain           ?? [];
            this.TLSProtocols                        = TLSProtocols                     ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.CipherSuitesPolicy                  = CipherSuitesPolicy;
            this.CertificateChainPolicy              = CertificateChainPolicy;
            this.EnforceTLS                          = EnforceTLS                       ?? false;
            this.ApplicationProtocols                = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation                  = AllowRenegotiation;
            this.AllowTLSResume                      = AllowTLSResume;

        }

        #endregion

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public override async Task ReconnectAsync(CancellationToken CancellationToken = default)
        {

            await base.ReconnectAsync(CancellationToken);

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task ConnectAsync(CancellationToken CancellationToken = default)
        {

            await base.ConnectAsync(CancellationToken);

            if (EnforceTLS ||
                RemoteURL?.Protocol == URLProtocols.tls   ||
                RemoteURL?.Protocol == URLProtocols.https ||
                RemoteURL?.Protocol == URLProtocols.wss)
            {
                await StartTLS(CancellationToken);
            }

        }

        #endregion

        #region (protected) StartTLS(CancellationToken = default)

        protected async Task StartTLS(CancellationToken CancellationToken = default)
        {

            if (tcpClient is not null)
            {

                var tcpStream = tcpClient.GetStream();

                if (tcpStream is not null)
                {

                    tlsStream = new SslStream(
                                    tcpStream,
                                    leaveInnerStreamOpen: false
                                );

                    var authenticationOptions  = new SslClientAuthenticationOptions {
                                                     //ApplicationProtocols             = new List<SslApplicationProtocol> {
                                                     //                                       SslApplicationProtocol.Http2,  // Example: Add HTTP/2   protocol
                                                     //                                       SslApplicationProtocol.Http11  // Example: Add HTTP/1.1 protocol
                                                     //                                   },
                                                     AllowRenegotiation               = AllowRenegotiation ?? true,
                                                     AllowTlsResume                   = AllowTLSResume     ?? true,
                                                     TargetHost                       = RemoteURL?.Hostname.ToString() ?? DomainName?.ToString() ?? RemoteIPAddress?.ToString(), //SNI!
                                                     ClientCertificates               = null,
                                                     ClientCertificateContext         = null,
                                                     CertificateRevocationCheckMode   = X509RevocationMode.NoCheck,
                                                     EncryptionPolicy                 = EncryptionPolicy.RequireEncryption,
                                                     EnabledSslProtocols              = TLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13,
                                                     CipherSuitesPolicy               = null, // new CipherSuitesPolicy(TlsCipherSuite.),
                                                     CertificateChainPolicy           = null, // new X509ChainPolicy()
                                                 };

                    if (RemoteCertificateValidationHandler is not null)
                    {
                        authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {

                            var result = RemoteCertificateValidationHandler(
                                             sender,
                                             certificate is not null
                                                 ? new X509Certificate2(certificate)
                                                 : null,
                                             chain,
                                             this,
                                             policyErrors
                                         );

                            return result.Item1;

                        };
                    }
                    else
                    {
                        authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {
                            return true;
                        };
                    }


                    if (LocalCertificateSelector is not null)
                    {
                        authenticationOptions.LocalCertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => {
                            return LocalCertificateSelector(
                                       sender,
                                       targetHost,
                                       localCertificates.Cast<X509Certificate2>(),
                                       remoteCertificate is not null
                                           ? new X509Certificate2(remoteCertificate)
                                           : null,
                                       acceptableIssuers
                                   );
                        };
                    }


                    try
                    {
                        await tlsStream.AuthenticateAsClientAsync(
                                  authenticationOptions,
                                  CancellationToken
                              );
                    }
                    catch (Exception e)
                    {
                        DebugX.Log($"Error during TLS authentication: {e.Message}");
                    }

                }

            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(ATLSTestClient)}: {RemoteIPAddress}:{RemotePort} (Connected: {IsConnected})";

        #endregion


        #region Dispose / IAsyncDisposable

        //public override async ValueTask DisposeAsync()
        //{
        //    await Close();
        //    cts?.Dispose();
        //    GC.SuppressFinalize(this);
        //}

        //public override void Dispose()
        //{
        //    DisposeAsync().AsTask().GetAwaiter().GetResult();
        //    GC.SuppressFinalize(this);
        //}

        #endregion

    }

}
