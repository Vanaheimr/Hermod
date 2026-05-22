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

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.PKI;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A Transport Layer Security (TLS) client.
    /// </summary>
    public abstract class ATLSClient : ATCPClient
    {

        #region Data

        /// <summary>
        /// The TLS stream.
        /// </summary>
        protected SslStream? tlsStream;

        #endregion

        #region Properties

        /// <summary>
        /// Use this hostname for TLS SNI (Server Name Indication) and remote certificate validation.
        /// If null, the RemoteURL's hostname or the RemoteIPAddress will be used.
        /// </summary>
        public String?                                                   TLSHostname                      { get; }

        /// <summary>
        /// The remote TLS server certificate validation handler.
        /// </summary>
        public RemoteTLSServerCertificateValidationHandler<ATLSClient>?  RemoteCertificateValidator       { get; }

        /// <summary>
        /// The local TLS client certificate selection handler.
        /// </summary>
        public LocalCertificateSelectionHandler?                         LocalCertificateSelector         { get; }

        /// <summary>
        /// The TLS client certificates to use for authentication. Ignored if ClientCertificateContext is provided.
        /// </summary>
        public IEnumerable<X509Certificate2>                             ClientCertificates               { get; private set; } = [];

        /// <summary>
        /// The TLS client certificate context, including the client certificate and any intermediate CAs.
        /// If provided, this takes precedence over ClientCertificates.
        /// </summary>
        public SslStreamCertificateContext?                              ClientCertificateContext         { get; private set; }

        /// <summary>
        /// The TLS client certificate chain, including the client certificate and any intermediate CAs.
        /// </summary>
        public IEnumerable<X509Certificate2>                             ClientCertificateChain           { get; } = [];

        /// <summary>
        /// The TLS protocols to use. Defaults to TLS 1.3 if not specified.
        /// </summary>
        public SslProtocols                                              TLSProtocols                     { get; } = SslProtocols.Tls13;

        /// <summary>
        /// The TLS cipher suites policy to use.
        /// If null, the system defaults will be used.
        /// </summary>
        public CipherSuitesPolicy?                                       CipherSuitesPolicy               { get; }

        /// <summary>
        /// The TLS certificate chain policy to use for validating the server's certificate chain.
        /// </summary>
        public X509ChainPolicy?                                          CertificateChainPolicy           { get; }

        /// <summary>
        /// The TLS certificate revocation check mode to use for validating the server's certificate.
        /// </summary>
        public X509RevocationMode?                                       CertificateRevocationCheckMode   { get; }

        /// <summary>
        /// The TLS application protocols to use for ALPN (Application-Layer Protocol Negotiation).
        /// If empty, ALPN will be disabled.
        /// </summary>
        public IEnumerable<SslApplicationProtocol>                       ApplicationProtocols             { get; } = [];

        /// <summary>
        /// Whether to enforce TLS. If true, the client will attempt to establish a TLS connection immediately after connecting,
        /// </summary>
        public Boolean                                                   EnforceTLS                       { get; }

        /// <summary>
        /// Whether to allow TLS renegotiation.
        /// Defaults to true if not specified.
        /// </summary>
        public Boolean?                                                  AllowRenegotiation               { get; }

        /// <summary>
        /// Whether to allow TLS session resumption.
        /// Defaults to false if not specified.
        /// </summary>
        public Boolean?                                                  AllowTLSResume                   { get; }

        protected Stream? ActiveStream

            => tlsStream is not null
                   ? tlsStream
                   : tcpClient?.GetStream();

        public Boolean IsTLSActive

            => tlsStream is not null;

        #endregion

        #region Constructor(s)

        #region (protected) ATLSClient(IPAddress,  TCPPort, ...)

        protected ATLSClient(IIPAddress                                                IPAddress,
                             IPPort                                                    TCPPort,
                             I18NString?                                               Description                      = null,

                             String?                                                   TLSHostname                      = null,
                             RemoteTLSServerCertificateValidationHandler<ATLSClient>?  RemoteCertificateValidator       = null,
                             LocalCertificateSelectionHandler?                         LocalCertificateSelector         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificates               = null,
                             SslStreamCertificateContext?                              ClientCertificateContext         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificateChain           = null,
                             SslProtocols?                                             TLSProtocols                     = null,
                             CipherSuitesPolicy?                                       CipherSuitesPolicy               = null,
                             X509ChainPolicy?                                          CertificateChainPolicy           = null,
                             X509RevocationMode?                                       CertificateRevocationCheckMode   = null,
                             Boolean?                                                  EnforceTLS                       = null,
                             IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols             = null,
                             Boolean?                                                  AllowRenegotiation               = null,
                             Boolean?                                                  AllowTLSResume                   = null,

                             IPVersionPreference?                                      IPVersionPreference              = null,
                             TimeSpan?                                                 ConnectTimeout                   = null,
                             TimeSpan?                                                 ReceiveTimeout                   = null,
                             TimeSpan?                                                 SendTimeout                      = null,
                             TransmissionRetryDelayDelegate?                           TransmissionRetryDelay           = null,
                             UInt16?                                                   MaxNumberOfRetries               = null,
                             UInt32?                                                   InternalBufferSize               = null,

                             Boolean?                                                  DisableLogging                   = null,
                             ILogger<ATLSClient>?                                      Logger                           = null,
                             ILoggerFactory?                                           LoggerFactory                    = null)

            : base(IPAddress,
                   TCPPort,
                   Description,
                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   Logger,
                   LoggerFactory)

        {

            this.RemoteCertificateValidator      = RemoteCertificateValidator;
            this.LocalCertificateSelector        = LocalCertificateSelector;
            this.TLSHostname                     = TLSHostname;
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

        #region (protected) ATLSClient(URL, ...)

        protected ATLSClient(URL                                                       URL,
                             I18NString?                                               Description                      = null,

                             String?                                                   TLSHostname                      = null,
                             RemoteTLSServerCertificateValidationHandler<ATLSClient>?  RemoteCertificateValidator       = null,
                             LocalCertificateSelectionHandler?                         LocalCertificateSelector         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificates               = null,
                             SslStreamCertificateContext?                              ClientCertificateContext         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificateChain           = null,
                             SslProtocols?                                             TLSProtocols                     = null,
                             CipherSuitesPolicy?                                       CipherSuitesPolicy               = null,
                             X509ChainPolicy?                                          CertificateChainPolicy           = null,
                             X509RevocationMode?                                       CertificateRevocationCheckMode   = null,
                             Boolean?                                                  EnforceTLS                       = null,
                             IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols             = null,
                             Boolean?                                                  AllowRenegotiation               = null,
                             Boolean?                                                  AllowTLSResume                   = null,

                             IPVersionPreference?                                      IPVersionPreference              = null,
                             TimeSpan?                                                 ConnectTimeout                   = null,
                             TimeSpan?                                                 ReceiveTimeout                   = null,
                             TimeSpan?                                                 SendTimeout                      = null,
                             TransmissionRetryDelayDelegate?                           TransmissionRetryDelay           = null,
                             UInt16?                                                   MaxNumberOfRetries               = null,
                             UInt32?                                                   InternalBufferSize               = null,

                             Boolean?                                                  DisableLogging                   = null,
                             IDNSClient?                                               DNSClient                        = null,
                             ILogger<ATLSClient>?                                      Logger                           = null,
                             ILoggerFactory?                                           LoggerFactory                    = null)

            : base(URL,
                   Description,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.TLSHostname                     = TLSHostname;
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

        #region (protected) ATLSClient(DomainName, DNSService, ...)

        protected ATLSClient(DomainName                                                DomainName,
                             SRV_Spec                                                  DNSService,
                             I18NString?                                               Description                      = null,

                             String?                                                   TLSHostname                      = null,
                             RemoteTLSServerCertificateValidationHandler<ATLSClient>?  RemoteCertificateValidator       = null,
                             LocalCertificateSelectionHandler?                         LocalCertificateSelector         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificates               = null,
                             SslStreamCertificateContext?                              ClientCertificateContext         = null,
                             IEnumerable<X509Certificate2>?                            ClientCertificateChain           = null,
                             SslProtocols?                                             TLSProtocols                     = null,
                             CipherSuitesPolicy?                                       CipherSuitesPolicy               = null,
                             X509ChainPolicy?                                          CertificateChainPolicy           = null,
                             X509RevocationMode?                                       CertificateRevocationCheckMode   = null,
                             Boolean?                                                  EnforceTLS                       = null,
                             IEnumerable<SslApplicationProtocol>?                      ApplicationProtocols             = null,
                             Boolean?                                                  AllowRenegotiation               = null,
                             Boolean?                                                  AllowTLSResume                   = null,

                             IPVersionPreference?                                      IPVersionPreference              = null,
                             TimeSpan?                                                 ConnectTimeout                   = null,
                             TimeSpan?                                                 ReceiveTimeout                   = null,
                             TimeSpan?                                                 SendTimeout                      = null,
                             TransmissionRetryDelayDelegate?                           TransmissionRetryDelay           = null,
                             UInt16?                                                   MaxNumberOfRetries               = null,
                             UInt32?                                                   InternalBufferSize               = null,

                             Boolean?                                                  DisableLogging                   = null,
                             IDNSClient?                                               DNSClient                        = null,
                             ILogger<ATLSClient>?                                      Logger                           = null,
                             ILoggerFactory?                                           LoggerFactory                    = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        {

            this.TLSHostname                     = TLSHostname;
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

        public override async Task<TCPConnectionResult>

            ReconnectAsync(CancellationToken CancellationToken = default)

        {

            try
            {

                try { tlsStream?.Dispose(); } catch { }
                tlsStream = null;

                var tcpConnectionResult = await base.ReconnectAsync(CancellationToken);

                // recreates _cts and tcpClient
                return await AfterSuccessfulConnect(
                                 tcpConnectionResult,
                                 CancellationToken
                             );

            }
            catch (Exception e)
            {

                await Log(e.Message);

                if (e.StackTrace is not null)
                    await Log(e.StackTrace);

            }

            return TCPConnectionResult.Failed($"{nameof(ATLSClient)}.{nameof(ReconnectAsync)} TCP reconnect failed!");

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task<TCPConnectionResult>

            ConnectAsync(CancellationToken CancellationToken = default)

        {

            var response = await base.ConnectAsync(CancellationToken);

            if (!response.IsSuccess)
                return response;

            return await AfterSuccessfulConnect(response, CancellationToken);

        }

        #endregion

        #region (protected) StartTLS(CancellationToken = default)

        protected async Task<TCPConnectionResult>

            StartTLS(CancellationToken CancellationToken = default)

        {

            if (tcpClient is null)
                return TCPConnectionResult.Failed($"{nameof(ATLSClient)}.{nameof(StartTLS)}.{nameof(tcpClient)} is null!");

            var remoteCertificateValidationErrors = new List<Error>();

            try
            {

                var tcpStream = tcpClient.GetStream();

                if (tcpStream is null)
                    return TCPConnectionResult.Failed($"{nameof(ATLSClient)}.{nameof(StartTLS)}.{nameof(tcpStream)} is null!");

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
                                                AllowTlsResume                        = AllowTLSResume     ?? false,
                                                TargetHost                            = TLSHostname ??
                                                                                        (!RemoteURL.IsNullOrEmpty
                                                                                             ? RemoteURL.Hostname.ToString() // SNI!
                                                                                             : DomainName?.      ToString() ??
                                                                                               RemoteIPAddress?. ToString()),
                                                ClientCertificates                    = ClientCertificateContext is null &&
                                                                                       ClientCertificates.IsNeitherNullNorEmpty()
                                                                                             ? [.. ClientCertificates]
                                                                                             : null,
                                                ClientCertificateContext              = ClientCertificateContext,
                                                CertificateRevocationCheckMode        = CertificateRevocationCheckMode ?? X509RevocationMode.NoCheck,
                                                EncryptionPolicy                      = EncryptionPolicy.RequireEncryption,
                                                EnabledSslProtocols                   = TLSProtocols,
                                                CipherSuitesPolicy                    = CipherSuitesPolicy,
                                                CertificateChainPolicy                = CertificateChainPolicy,
                                                RemoteCertificateValidationCallback   = RemoteCertificateValidator is not null
                                                                                            ? (sender, certificate, chain, policyErrors) => {

                                                                                                  var result = RemoteCertificateValidator(
                                                                                                                   sender,
                                                                                                                   certificate is not null
                                                                                                                       ? new X509Certificate2(certificate)
                                                                                                                       : null,
                                                                                                                   chain,
                                                                                                                   this,
                                                                                                                   policyErrors
                                                                                                               );

                                                                                                  remoteCertificateValidationErrors = [.. result.Errors];

                                                                                                  return result.IsValid;

                                                                                              }
                                                                                            : null

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
                                  $"{nameof(ATLSClient)}.{nameof(StartTLS)}.tlsStream.{nameof(tlsStream.AuthenticateAsClientAsync)}: {e.Message}"
                              };

                while (e.InnerException is not null)
                {
                    e = e.InnerException;
                    errors.Add($"-- Inner Exception: {e.Message}");
                }

                if (remoteCertificateValidationErrors.Count > 0)
                    errors.AddRange($"Remote Certificate Validation Errors: {remoteCertificateValidationErrors.AggregateWith(", ")}");

                return TCPConnectionResult.Failed(errors);

            }

            return TCPConnectionResult.Success();

        }

        #endregion

        #region (private)   AfterSuccessfulConnect(TCPConnectionResult, CancellationToken = default)

        private async Task<TCPConnectionResult> AfterSuccessfulConnect(TCPConnectionResult  TCPConnectionResult,
                                                                       CancellationToken    CancellationToken   = default)
        {

            if (EnforceTLS ||
                RemoteURL.Protocol == URLProtocols.tls   ||
                RemoteURL.Protocol == URLProtocols.https ||
                RemoteURL.Protocol == URLProtocols.wss)
            {

                var startTLSResult = await StartTLS(CancellationToken);

                if (startTLSResult.IsSuccess == false)
                {
                    ResolvedIPAddress = null;
                    ResolvedIPAddresses.Clear();
                    await Log("StartTLS failed, closing the entire TCP connection!");
                    await Close();
                }

                return startTLSResult;

            }

            return TCPConnectionResult;

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(ATLSClient),
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

            => $"{nameof(ATLSClient)}: {LocalSocket} -> {RemoteSocket} (Connected: {IsConnected})";

        #endregion

    }

}
