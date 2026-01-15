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

using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A Transport Layer Security (TLS) client.
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
        public RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator       { get; }
        public LocalCertificateSelectionHandler?                             LocalCertificateSelector         { get; }
        public IEnumerable<X509Certificate2>                                 ClientCertificates               { get; private set; } = [];
        public SslStreamCertificateContext?                                  ClientCertificateContext         { get; private set; }
        public IEnumerable<X509Certificate2>                                 ClientCertificateChain           { get; } = [];
        public SslProtocols                                                  TLSProtocols                     { get; } = SslProtocols.Tls13;
        public CipherSuitesPolicy?                                           CipherSuitesPolicy               { get; }
        public X509ChainPolicy?                                              CertificateChainPolicy           { get; }
        public X509RevocationMode?                                           CertificateRevocationCheckMode   { get; }
        public IEnumerable<SslApplicationProtocol>                           ApplicationProtocols             { get; } = [];
        public Boolean                                                       EnforceTLS                       { get; }
        public Boolean?                                                      AllowRenegotiation               { get; }
        public Boolean?                                                      AllowTLSResume                   { get; }

        #endregion

        #region Constructor(s)

        #region (protected) ATLSTestClient(IPAddress,  TCPPort,           ...)

        protected ATLSTestClient(IIPAddress                                                    IPAddress,
                                 IPPort                                                        TCPPort,
                                 I18NString?                                                   Description                      = null,

                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator       = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificates               = null,
                                 SslStreamCertificateContext?                                  ClientCertificateContext         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificateChain           = null,
                                 SslProtocols?                                                 TLSProtocols                     = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy               = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy           = null,
                                 X509RevocationMode?                                           CertificateRevocationCheckMode   = null,
                                 Boolean?                                                      EnforceTLS                       = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols             = null,
                                 Boolean?                                                      AllowRenegotiation               = null,
                                 Boolean?                                                      AllowTLSResume                   = null,

                                 Boolean?                                                      PreferIPv4                       = null,
                                 TimeSpan?                                                     ConnectTimeout                   = null,
                                 TimeSpan?                                                     ReceiveTimeout                   = null,
                                 TimeSpan?                                                     SendTimeout                      = null,
                                 TransmissionRetryDelayDelegate?                               TransmissionRetryDelay           = null,
                                 UInt16?                                                       MaxNumberOfRetries               = null,
                                 UInt32?                                                       BufferSize                       = null)

            : base(IPAddress,
                   TCPPort,
                   Description,
                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize)

        {

            this.RemoteCertificateValidator      = RemoteCertificateValidator;
            this.LocalCertificateSelector        = LocalCertificateSelector;
            this.ClientCertificates              = ClientCertificates               ?? [];
            this.ClientCertificateContext        = ClientCertificateContext;
            this.ClientCertificateChain          = ClientCertificateChain           ?? [];
            this.TLSProtocols                    = TLSProtocols                     ?? SslProtocols.Tls13;
            this.CipherSuitesPolicy              = CipherSuitesPolicy;
            this.CertificateChainPolicy          = CertificateChainPolicy;
            this.CertificateRevocationCheckMode  = CertificateRevocationCheckMode;
            this.EnforceTLS                      = EnforceTLS                       ?? false;
            this.ApplicationProtocols            = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation              = AllowRenegotiation;
            this.AllowTLSResume                  = AllowTLSResume;

        }

        #endregion

        #region (protected) ATLSTestClient(URL,        DNSService = null, ..., DNSClient = null)

        protected ATLSTestClient(URL                                                           URL,
                                 SRV_Spec?                                                     DNSService                       = null,
                                 I18NString?                                                   Description                      = null,

                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator       = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificates               = null,
                                 SslStreamCertificateContext?                                  ClientCertificateContext         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificateChain           = null,
                                 SslProtocols?                                                 TLSProtocols                     = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy               = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy           = null,
                                 X509RevocationMode?                                           CertificateRevocationCheckMode   = null,
                                 Boolean?                                                      EnforceTLS                       = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols             = null,
                                 Boolean?                                                      AllowRenegotiation               = null,
                                 Boolean?                                                      AllowTLSResume                   = null,

                                 Boolean?                                                      PreferIPv4                       = null,
                                 TimeSpan?                                                     ConnectTimeout                   = null,
                                 TimeSpan?                                                     ReceiveTimeout                   = null,
                                 TimeSpan?                                                     SendTimeout                      = null,
                                 TransmissionRetryDelayDelegate?                               TransmissionRetryDelay           = null,
                                 UInt16?                                                       MaxNumberOfRetries               = null,
                                 UInt32?                                                       BufferSize                       = null,
                                 IDNSClient?                                                   DNSClient                        = null)

            : base(URL,
                   DNSService,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   DNSClient)

        {

            this.RemoteCertificateValidator      = RemoteCertificateValidator;
            this.LocalCertificateSelector        = LocalCertificateSelector;
            this.ClientCertificates              = ClientCertificates               ?? [];
            this.ClientCertificateContext        = ClientCertificateContext;
            this.ClientCertificateChain          = ClientCertificateChain           ?? [];
            this.TLSProtocols                    = TLSProtocols                     ?? SslProtocols.Tls13;
            this.CipherSuitesPolicy              = CipherSuitesPolicy;
            this.CertificateChainPolicy          = CertificateChainPolicy;
            this.CertificateRevocationCheckMode  = CertificateRevocationCheckMode;
            this.EnforceTLS                      = EnforceTLS                       ?? false;
            this.ApplicationProtocols            = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation              = AllowRenegotiation;
            this.AllowTLSResume                  = AllowTLSResume;

        }

        #endregion

        #region (protected) ATLSTestClient(DomainName, DNSService,        ..., DNSClient = null)

        protected ATLSTestClient(DomainName                                                    DomainName,
                                 SRV_Spec                                                      DNSService,
                                 I18NString?                                                   Description                      = null,

                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidator       = null,
                                 LocalCertificateSelectionHandler?                             LocalCertificateSelector         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificates               = null,
                                 SslStreamCertificateContext?                                  ClientCertificateContext         = null,
                                 IEnumerable<X509Certificate2>?                                ClientCertificateChain           = null,
                                 SslProtocols?                                                 TLSProtocols                     = null,
                                 CipherSuitesPolicy?                                           CipherSuitesPolicy               = null,
                                 X509ChainPolicy?                                              CertificateChainPolicy           = null,
                                 X509RevocationMode?                                           CertificateRevocationCheckMode   = null,
                                 Boolean?                                                      EnforceTLS                       = null,
                                 IEnumerable<SslApplicationProtocol>?                          ApplicationProtocols             = null,
                                 Boolean?                                                      AllowRenegotiation               = null,
                                 Boolean?                                                      AllowTLSResume                   = null,

                                 Boolean?                                                      PreferIPv4                       = null,
                                 TimeSpan?                                                     ConnectTimeout                   = null,
                                 TimeSpan?                                                     ReceiveTimeout                   = null,
                                 TimeSpan?                                                     SendTimeout                      = null,
                                 TransmissionRetryDelayDelegate?                               TransmissionRetryDelay           = null,
                                 UInt16?                                                       MaxNumberOfRetries               = null,
                                 UInt32?                                                       BufferSize                       = null,
                                 IDNSClient?                                                   DNSClient                        = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   DNSClient)

        {

            this.RemoteCertificateValidator      = RemoteCertificateValidator;
            this.LocalCertificateSelector        = LocalCertificateSelector;
            this.ClientCertificates              = ClientCertificates               ?? [];
            this.ClientCertificateContext        = ClientCertificateContext;
            this.ClientCertificateChain          = ClientCertificateChain           ?? [];
            this.TLSProtocols                    = TLSProtocols                     ?? SslProtocols.Tls13;
            this.CipherSuitesPolicy              = CipherSuitesPolicy;
            this.CertificateChainPolicy          = CertificateChainPolicy;
            this.CertificateRevocationCheckMode  = CertificateRevocationCheckMode;
            this.EnforceTLS                      = EnforceTLS                       ?? false;
            this.ApplicationProtocols            = ApplicationProtocols?.Distinct() ?? [];
            this.AllowRenegotiation              = AllowRenegotiation;
            this.AllowTLSResume                  = AllowTLSResume;

        }

        #endregion

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public override async Task<(Boolean, List<String>)>

            ReconnectAsync(CancellationToken CancellationToken = default)

        {

            return await base.ReconnectAsync(CancellationToken);

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task<(Boolean, List<String>)>

            ConnectAsync(CancellationToken CancellationToken = default)

        {

            var response = await base.ConnectAsync(CancellationToken);

            if (!response.Item1)
                return response;

            if (EnforceTLS ||
                RemoteURL.Protocol == URLProtocols.tls   ||
                RemoteURL.Protocol == URLProtocols.https ||
                RemoteURL.Protocol == URLProtocols.wss)
            {

                var startTLSResult = await StartTLS(CancellationToken);

                if (startTLSResult.Item1 == false)
                {
                    await Log("StartTLS failed, closing the entire TCP connection!");
                    await Close();
                }

                return startTLSResult;

            }

            return response;

        }

        #endregion

        #region (protected) StartTLS(CancellationToken = default)

        protected async Task<(Boolean, List<String>)>

            StartTLS(CancellationToken CancellationToken = default)

        {

            if (tcpClient is null)
                return (false, new List<String>() { $"{nameof(ATLSTestClient)}.{nameof(StartTLS)}.{nameof(tcpClient)} is null!" });

            if (RemoteCertificateValidator is null)
                return (false, new List<String>() { $"{nameof(ATLSTestClient)}.{nameof(StartTLS)}.{nameof(RemoteCertificateValidator)} is null!" });

            var remoteCertificateValidationErrors = new List<String>();

            try
            {

                var tcpStream = tcpClient.GetStream();

                if (tcpStream is null)
                    return (false, new List<String>() { $"{nameof(ATLSTestClient)}.{nameof(StartTLS)}.{nameof(tcpStream)} is null!" });

                tlsStream = new SslStream(
                                tcpStream,
                                leaveInnerStreamOpen: false
                            );


                #region A Client Certificate Chain was provided (while ClientCertificates and ClientCertificateContext are empty!)

                if (!ClientCertificates.Any() &&
                     ClientCertificateContext is null &&
                     ClientCertificateChain.Any())
                {

                    var clientCertificate          = ClientCertificateChain.First(cert => !cert.IsCertificateAuthority());
                    var intermediateCAs            = ClientCertificateChain.Where(cert =>  cert.IsCertificateAuthority()).ToArray();

                    this.ClientCertificates        = [ ClientCertificateChain.First() ];
                    this.ClientCertificateContext  = SslStreamCertificateContext.Create(
                                                         clientCertificate,
                                                         [.. intermediateCAs]
                                                     );

                }

                #endregion

                var authenticationOptions = new SslClientAuthenticationOptions {

                                                ApplicationProtocols                  = ApplicationProtocols.IsNeitherNullNorEmpty()
                                                                                            ? ApplicationProtocols?.ToList()
                                                                                            : null,
                                                AllowRenegotiation                    = AllowRenegotiation ?? true,
                                                AllowTlsResume                        = AllowTLSResume     ?? true,
                                                TargetHost                            = RemoteURL.Hostname.ToString() ?? //SNI!
                                                                                        DomainName?.       ToString() ??
                                                                                        RemoteIPAddress?.  ToString(),
                                                ClientCertificates                    = ClientCertificates.IsNeitherNullNorEmpty()
                                                                                            ? [.. ClientCertificates.ToArray()]
                                                                                            : null,
                                                ClientCertificateContext              = ClientCertificateContext,
                                                CertificateRevocationCheckMode        = X509RevocationMode.NoCheck,
                                                EncryptionPolicy                      = EncryptionPolicy.RequireEncryption,
                                                EnabledSslProtocols                   = TLSProtocols,
                                                CipherSuitesPolicy                    = CipherSuitesPolicy,
                                                CertificateChainPolicy                = CertificateChainPolicy,
                                                RemoteCertificateValidationCallback   = (sender, certificate, chain, policyErrors) => {

                                                                                            var result = RemoteCertificateValidator(
                                                                                                             sender,
                                                                                                             certificate is not null
                                                                                                                 ? new X509Certificate2(certificate)
                                                                                                                 : null,
                                                                                                             chain,
                                                                                                             this,
                                                                                                             policyErrors
                                                                                                         );

                                                                                            remoteCertificateValidationErrors = [.. result.Item2];

                                                                                            return result.Item1;

                                                                                        }

                                            };

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

                await tlsStream.AuthenticateAsClientAsync(
                          authenticationOptions,
                          CancellationToken
                      );

            }
            catch (Exception e)
            {

                var errors  = new List<String>() {
                                  $"{nameof(ATLSTestClient)}.{nameof(StartTLS)}.tlsStream.{nameof(tlsStream.AuthenticateAsClientAsync)}: {e.Message}"
                              };

                while (e.InnerException is not null)
                {
                    e = e.InnerException;
                    errors.Add($"-- Inner Exception: {e.Message}");
                }

                if (remoteCertificateValidationErrors.Count > 0)
                    errors.AddRange($"Remote Certificate Validation Errors: {remoteCertificateValidationErrors.AggregateWith(", ")}");

                return (false, errors);

            }

            return (true, []);

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATLSTestClient),
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

            => $"{nameof(ATLSTestClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

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
