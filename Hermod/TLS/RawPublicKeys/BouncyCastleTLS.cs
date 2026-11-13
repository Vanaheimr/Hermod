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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

using BCCertificate      = Org.BouncyCastle.Tls.Certificate;
using BCCertificateRequest = Org.BouncyCastle.Tls.CertificateRequest;
using BCCertificateType  = Org.BouncyCastle.Tls.CertificateType;
using BCServerName       = Org.BouncyCastle.Tls.ServerName;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    #region TLSHandshakeFailedException

    /// <summary>
    /// A TLS handshake over TCP that failed, with the alert that ended it.
    /// </summary>
    /// <param name="Message">What went wrong.</param>
    /// <param name="AlertDescription">The description of the TLS alert that was sent or received (RFC 8446, section 6), if there was one.</param>
    /// <param name="AlertReceived">Whether the alert came from the peer.</param>
    /// <param name="InnerException">The exception that ended the handshake.</param>
    public sealed class TLSHandshakeFailedException(String      Message,
                                                    Int16?      AlertDescription   = null,
                                                    Boolean     AlertReceived      = false,
                                                    Exception?  InnerException     = null)

        : IOException(Message, InnerException)

    {

        /// <summary>
        /// The description of the TLS alert that ended the handshake
        /// (RFC 8446, section 6), e.g. 42 for bad_certificate.
        /// </summary>
        public Int16?   AlertDescription    { get; } = AlertDescription;

        /// <summary>
        /// Whether the alert came from the peer; else it was raised here.
        /// </summary>
        public Boolean  AlertReceived       { get; } = AlertReceived;

    }

    #endregion

    #region RawPublicKeyTLSClientParameters

    /// <summary>
    /// What a TLS client that may be shown a raw public key (RFC 7250) is
    /// told about its connection.
    /// </summary>
    public sealed class RawPublicKeyTLSClientParameters
    {

        /// <summary>
        /// The key pins of the server, which say as well whether its key may
        /// come raw: a raw public key is taken from a key-pinned server alone.
        /// The server's key - raw, or in a certificate - is checked against
        /// them and against nothing else.
        /// </summary>
        public required KeyPinPolicy                ServerKeyPins              { get; init; }

        /// <summary>
        /// Told the result of the key pin check: a refusal at once, and an
        /// accepted or learned key once the server has proven to hold it.
        /// </summary>
        public Action<KeyPinValidation>?            OnServerKeyPinValidated    { get; init; }

        /// <summary>
        /// The name of the server for the server name indication, or null
        /// to send none, as for an IP address.
        /// </summary>
        public String?                              ServerName                 { get; init; }

        /// <summary>
        /// The application protocols offered (ALPN), by preference.
        /// </summary>
        public IReadOnlyList<String>                ApplicationProtocols       { get; init; } = [];

        /// <summary>
        /// The client's own certificate with its private key, if it has one.
        /// Its key is shown raw where the server asks for a key and takes a
        /// raw one, and in this certificate where it does not.
        /// </summary>
        public X509Certificate2?                    ClientCertificate          { get; init; }

        /// <summary>
        /// The certificates that lead from the client's certificate towards
        /// a root, in that order. Sent along with the certificate.
        /// </summary>
        public IReadOnlyList<X509Certificate2>      ClientCertificateChain     { get; init; } = [];

    }

    #endregion

    #region RawPublicKeyTLSServerParameters

    /// <summary>
    /// What a TLS server that may show or be shown a raw public key
    /// (RFC 7250) is told about its connection.
    /// </summary>
    public sealed class RawPublicKeyTLSServerParameters
    {

        /// <summary>
        /// The server's certificate with its private key. Its key is shown
        /// raw to a client that takes a raw public key, where
        /// <see cref="ServerRawPublicKeys"/> allows it.
        /// </summary>
        public required X509Certificate2            ServerCertificate          { get; init; }

        /// <summary>
        /// The certificates that lead from the server's certificate towards
        /// a root, in that order. Sent along with the certificate.
        /// </summary>
        public IReadOnlyList<X509Certificate2>      ServerCertificateChain     { get; init; } = [];

        /// <summary>
        /// Whether the server's key is shown raw: never, to a client that
        /// takes raw public keys, or to no other.
        /// </summary>
        public RawPublicKeyMode                     ServerRawPublicKeys        { get; init; } = RawPublicKeyMode.Off;

        /// <summary>
        /// The application protocols of the server (ALPN), by preference.
        /// </summary>
        public IReadOnlyList<String>                ApplicationProtocols       { get; init; } = [];

        /// <summary>
        /// Whether the client is asked for a certificate or a key.
        /// </summary>
        public Boolean                              RequestClientCertificate   { get; init; }

        /// <summary>
        /// Whether a client's key may come raw: never, where the client
        /// offers it, or in no other way.
        /// </summary>
        public RawPublicKeyMode                     ClientRawPublicKeys        { get; init; } = RawPublicKeyMode.Off;

        /// <summary>
        /// Asked whether a client's raw public key is believed, during the
        /// handshake and before the client has proven to hold the key: the
        /// handshake fails afterwards where it does not. Without it no raw
        /// client key is taken, whatever <see cref="ClientRawPublicKeys"/> says.
        /// </summary>
        public Func<TLSPeerKey, TLSValidationResult>?  ClientKeyValidator      { get; init; }

        /// <summary>
        /// Asked whether a client's certificate is believed - or, with null
        /// for the certificate, whether a client that showed none is - as
        /// the validation callback of an SslStream is. Without it a
        /// certificate is refused, and a client without one let in.
        /// </summary>
        public RemoteCertificateValidationCallback?    ClientCertificateValidator  { get; init; }

    }

    #endregion


    /// <summary>
    /// TLS 1.3 over TCP with raw public keys (RFC 7250), by BouncyCastle: the
    /// second TLS stack beside SslStream, which knows certificates alone.
    /// It turns a connected stream into an authenticated one and says what
    /// the layers above need: the peer's key, the certificate types that
    /// were negotiated, the application protocol, and an exporter.
    /// </summary>
    /// <remarks>
    /// Used for a connection that is to speak raw public keys, and for no
    /// other: SslStream stays the default. TLS 1.3 only - with TLS 1.2 the
    /// Certificate messages would travel in the clear.
    ///
    /// The private keys stay where they are: CertificateVerify is signed by
    /// the key of the X509Certificate2, which need not be exportable.
    /// </remarks>
    public static class BouncyCastleTLS
    {

        #region Data

        private static readonly Int32[] CipherSuites = [
                                                           CipherSuite.TLS_AES_128_GCM_SHA256,
                                                           CipherSuite.TLS_AES_256_GCM_SHA384,
                                                           CipherSuite.TLS_CHACHA20_POLY1305_SHA256
                                                       ];

        #endregion


        #region AuthenticateAsClientAsync(Stream, Parameters, LeaveInnerStreamOpen = false, CancellationToken = default)

        /// <summary>
        /// Run a TLS 1.3 handshake as a client over the given stream.
        /// </summary>
        /// <param name="Stream">A connected stream.</param>
        /// <param name="Parameters">The key pins of the server, and what else the client is told.</param>
        /// <param name="LeaveInnerStreamOpen">Whether closing the TLS stream leaves the given stream open.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="TLSHandshakeFailedException">Where the handshake fails.</exception>
        public static async Task<TLSSession> AuthenticateAsClientAsync(Stream                           Stream,
                                                                       RawPublicKeyTLSClientParameters  Parameters,
                                                                       Boolean                          LeaveInnerStreamOpen   = false,
                                                                       CancellationToken                CancellationToken      = default)
        {

            var protocol   = new TlsClientProtocol();
            var client     = new Client(new BcTlsCrypto(new SecureRandom()), Parameters);
            var tlsStream  = new BouncyCastleTLSStream(Stream, protocol, LeaveInnerStreamOpen);

            try
            {

                protocol.Connect(client);

                await tlsStream.HandshakeAsync(() => client.HandshakeComplete, CancellationToken).ConfigureAwait(false);

            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw Failed(e, client.AlertRaised, client.AlertReceived, client.Reason);
            }

            // Only now has the server proven to hold its key.
            client.TellValidation();

            var securityParameters = client.Context.SecurityParameters;

            return new TLSSession(
                       TLSProvider.BouncyCastle,
                       tlsStream,
                       securityParameters.ApplicationProtocol?.GetUtf8Decoding(),
                       client.ClientKeyShown ? CertificateTypeOf(securityParameters.ClientCertificateType) : TLSCertificateType.X509,
                       CertificateTypeOf(securityParameters.ServerCertificateType),
                       client.ServerKey,
                       client.Exporter
                   );

        }

        #endregion

        #region AuthenticateAsServerAsync(Stream, Parameters, LeaveInnerStreamOpen = false, CancellationToken = default)

        /// <summary>
        /// Run a TLS 1.3 handshake as a server over the given stream.
        /// </summary>
        /// <param name="Stream">A connected stream.</param>
        /// <param name="Parameters">The server's key, and what else it is told.</param>
        /// <param name="LeaveInnerStreamOpen">Whether closing the TLS stream leaves the given stream open.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="TLSHandshakeFailedException">Where the handshake fails.</exception>
        public static async Task<TLSSession> AuthenticateAsServerAsync(Stream                           Stream,
                                                                       RawPublicKeyTLSServerParameters  Parameters,
                                                                       Boolean                          LeaveInnerStreamOpen   = false,
                                                                       CancellationToken                CancellationToken      = default)
        {

            var protocol   = new TlsServerProtocol();
            var server     = new Server(new BcTlsCrypto(new SecureRandom()), Parameters);
            var tlsStream  = new BouncyCastleTLSStream(Stream, protocol, LeaveInnerStreamOpen);

            try
            {

                protocol.Accept(server);

                await tlsStream.HandshakeAsync(() => server.HandshakeComplete, CancellationToken).ConfigureAwait(false);

            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw Failed(e, server.AlertRaised, server.AlertReceived, server.Reason);
            }

            var securityParameters = server.Context.SecurityParameters;

            return new TLSSession(
                       TLSProvider.BouncyCastle,
                       tlsStream,
                       securityParameters.ApplicationProtocol?.GetUtf8Decoding(),
                       CertificateTypeOf(securityParameters.ServerCertificateType),
                       CertificateTypeOf(securityParameters.ClientCertificateType),
                       server.ClientKey,
                       server.Exporter
                   );

        }

        #endregion


        #region (private) Helpers

        private static TLSCertificateType CertificateTypeOf(Int16 CertificateType)

            => CertificateType == BCCertificateType.RawPublicKey
                   ? TLSCertificateType.RawPublicKey
                   : TLSCertificateType.X509;

        /// <summary>
        /// The exporter of a session (RFC 8446, section 7.5). BouncyCastle
        /// destroys the exporter master secret once the handshake is
        /// complete, so it is taken while the handshake completes, and the
        /// keying material is derived from it here.
        /// </summary>
        private static Func<String, Byte[], Int32, Byte[]>? ExporterOf(Org.BouncyCastle.Tls.TlsContext Context)
        {

            var securityParameters = Context.SecurityParameters;

            if (securityParameters.ExporterMasterSecret is not { } secret ||
               !Enum.IsDefined((Quic.Tls.CipherSuite) securityParameters.CipherSuite))
            {
                return null;
            }

            var exporterMasterSecret  = secret.Extract();
            var keySchedule           = new Quic.Tls.Crypto.KeySchedule((Quic.Tls.CipherSuite) securityParameters.CipherSuite);

            return (label, context, length) => keySchedule.ExportKeyingMaterial(exporterMasterSecret, label, context, length);

        }

        private static TLSHandshakeFailedException Failed(Exception  Exception,
                                                          Int16?     AlertRaised,
                                                          Int16?     AlertReceived,
                                                          String?    Reason)
        {

            var alert  = AlertRaised ?? AlertReceived ?? (Exception as TlsFatalAlert)?.AlertDescription;
            var name   = alert.HasValue ? AlertDescription.GetText(alert.Value) : null;

            return new TLSHandshakeFailedException(
                       String.Concat(
                           "The TLS handshake failed",
                           name is not null
                               ? AlertRaised.HasValue || !AlertReceived.HasValue
                                     ? $" with {name}"
                                     : $": the peer answered {name}"
                               : "",
                           ": ",
                           Reason ?? Exception.Message
                       ),
                       alert,
                       !AlertRaised.HasValue && AlertReceived.HasValue,
                       Exception
                   );

        }

        /// <summary>
        /// The certificate types for a mode, by preference: none for Off,
        /// a raw public key before X.509 for Prefer, a raw public key alone
        /// for Require.
        /// </summary>
        private static Int16[]? CertificateTypesFor(RawPublicKeyMode Mode)

            => Mode switch {
                   RawPublicKeyMode.Prefer   => [ BCCertificateType.RawPublicKey, BCCertificateType.X509 ],
                   RawPublicKeyMode.Require  => [ BCCertificateType.RawPublicKey ],
                   _                         => null
               };

        /// <summary>
        /// The Certificate message for the given certificate: its bare
        /// SubjectPublicKeyInfo where a raw public key was negotiated, else
        /// the certificate and what leads from it towards a root.
        /// </summary>
        private static BCCertificate CertificateMessageOf(BcTlsCrypto                     Crypto,
                                                          Int16                           CertificateType,
                                                          X509Certificate2                Certificate,
                                                          IReadOnlyList<X509Certificate2> Chain,
                                                          Byte[]                          CertificateRequestContext)
        {

            if (CertificateType == BCCertificateType.RawPublicKey)
                return new BCCertificate(
                           BCCertificateType.RawPublicKey,
                           CertificateRequestContext,
                           [ new CertificateEntry(
                                 new BcTlsRawKeyCertificate(Crypto, Certificate.PublicKey.ExportSubjectPublicKeyInfo()),
                                 null
                             ) ]
                       );

            return new BCCertificate(
                       BCCertificateType.X509,
                       CertificateRequestContext,
                       [ .. new[] { Certificate }.
                                Concat(Chain.Where(certificate => !certificate.Equals(Certificate))).
                                Select(certificate => new CertificateEntry(Crypto.CreateCertificate(certificate.RawData), null)) ]
                   );

        }

        /// <summary>
        /// The key and, with it, the pin of what a peer sent as its first
        /// certificate entry.
        /// </summary>
        private static Byte[] FirstEntryOf(BCCertificate Certificate)

            => Certificate.GetCertificateAt(0).GetEncoded();

        #endregion

        #region (private) Signer

        /// <summary>
        /// Signs CertificateVerify with the private key of an
        /// X509Certificate2, where it is: the key is not exported.
        /// </summary>
        private sealed class Signer : TlsCredentialedSigner
        {

            private readonly ECDsa?  ecdsa;
            private readonly RSA?    rsa;

            public BCCertificate              Certificate                 { get; }
            public SignatureAndHashAlgorithm  SignatureAndHashAlgorithm   { get; }

            public Signer(X509Certificate2                 Key,
                          BCCertificate                    Certificate,
                          IList<SignatureAndHashAlgorithm>? PeerSignatureAlgorithms)
            {

                this.Certificate = Certificate;

                if (!Key.HasPrivateKey)
                    throw new TlsFatalAlert(AlertDescription.internal_error,
                                            $"'{Key.Subject}' comes without its private key.");

                if (Key.GetECDsaPrivateKey() is { } ecdsaKey)
                {

                    this.ecdsa  = ecdsaKey;

                    SignatureAndHashAlgorithm = ecdsaKey.KeySize switch {
                                                    256  => SignatureAndHashAlgorithm.GetInstance(Org.BouncyCastle.Tls.HashAlgorithm.sha256, SignatureAlgorithm.ecdsa),
                                                    384  => SignatureAndHashAlgorithm.GetInstance(Org.BouncyCastle.Tls.HashAlgorithm.sha384, SignatureAlgorithm.ecdsa),
                                                    _    => throw new TlsFatalAlert(AlertDescription.internal_error,
                                                                                    $"'{Key.Subject}' has an ECDSA key of {ecdsaKey.KeySize} bits. TLS 1.3 signs here with P-256 or P-384.")
                                                };

                }

                else if (Key.GetRSAPrivateKey() is { } rsaKey)
                {
                    this.rsa                   = rsaKey;
                    SignatureAndHashAlgorithm  = SignatureAndHashAlgorithm.rsa_pss_rsae_sha256;
                }

                else
                    throw new TlsFatalAlert(AlertDescription.internal_error,
                                            $"'{Key.Subject}' has a key of the kind {Key.PublicKey.Oid.FriendlyName ?? Key.PublicKey.Oid.Value}. TLS 1.3 signs here with ECDSA P-256, ECDSA P-384 or RSA.");

                // RFC 8446, section 4.4.3: signed with an algorithm the peer offered.
                if (PeerSignatureAlgorithms is not null &&
                   !PeerSignatureAlgorithms.Contains(SignatureAndHashAlgorithm))
                {
                    throw new TlsFatalAlert(AlertDescription.handshake_failure,
                                            $"The peer takes no signature of the key of '{Key.Subject}'.");
                }

            }

            public Byte[] GenerateRawSignature(Byte[] Hash)

                => ecdsa is not null
                       ? ecdsa.SignHash(Hash, DSASignatureFormat.Rfc3279DerSequence)
                       : rsa!. SignHash(Hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

            // No stream signer: the hash is made by the protocol, and signed here.
            public TlsStreamSigner? GetStreamSigner()

                => null;

        }

        #endregion

        #region (private) Client

        private sealed class Client(BcTlsCrypto                      Crypto,
                                    RawPublicKeyTLSClientParameters  Parameters)

            : DefaultTlsClient(Crypto),
              TlsAuthentication

        {

            private KeyPinValidation? validation;

            public Org.BouncyCastle.Tls.TlsContext Context => m_context;
            public Boolean      HandshakeComplete    { get; private set; }
            public Func<String, Byte[], Int32, Byte[]>? Exporter { get; private set; }
            public TLSPeerKey?  ServerKey            { get; private set; }
            public Boolean      ClientKeyShown       { get; private set; }
            public Int16?       AlertRaised          { get; private set; }
            public Int16?       AlertReceived        { get; private set; }
            public String?      Reason               { get; private set; }

            protected override ProtocolVersion[] GetSupportedVersions()
                => ProtocolVersion.TLSv13.Only();

            protected override Int32[] GetSupportedCipherSuites()
                => CipherSuites;

            protected override IList<ProtocolName>? GetProtocolNames()
                => Parameters.ApplicationProtocols.Count > 0
                       ? [ .. Parameters.ApplicationProtocols.Select(ProtocolName.AsUtf8Encoding) ]
                       : null;

            protected override IList<BCServerName>? GetSniServerNames()
                => Parameters.ServerName is { Length: > 0 } serverName
                       ? [ new BCServerName(NameType.host_name, System.Text.Encoding.ASCII.GetBytes(serverName)) ]
                       : null;

            // The server's key: raw from a key-pinned server whose key pins say so.
            protected override Int16[]? GetAllowedServerCertificateTypes()
                => CertificateTypesFor(Parameters.ServerKeyPins.RawPublicKeys);

            // The client's own key: raw first, where the endpoint speaks raw public keys at all.
            protected override Int16[]? GetAllowedClientCertificateTypes()
                => Parameters.ClientCertificate is not null &&
                   Parameters.ServerKeyPins.RawPublicKeys != RawPublicKeyMode.Off
                       ? [ BCCertificateType.RawPublicKey, BCCertificateType.X509 ]
                       : null;

            // A key-pinned server is its key: nothing is asked about the revocation of a certificate.
            protected override CertificateStatusRequest? GetCertificateStatusRequest()
                => null;

            protected override IList<CertificateStatusRequestItemV2>? GetMultiCertStatusRequest()
                => null;

            public override TlsAuthentication GetAuthentication()
                => this;

            public void NotifyServerCertificate(TlsServerCertificate ServerCertificate)
            {

                var certificate = ServerCertificate.Certificate;

                if (certificate is null || certificate.IsEmpty)
                    throw Refused(AlertDescription.bad_certificate, "The server presented no key.");

                KeyPinValidation  result;
                TLSPeerKey        key;

                if (certificate.CertificateType == BCCertificateType.RawPublicKey)
                {

                    if (certificate.Length != 1)
                        throw Refused(AlertDescription.bad_certificate,
                                      $"The server sent {certificate.Length} entries where a raw public key is a single one.");

                    var subjectPublicKeyInfo = FirstEntryOf(certificate);

                    result  = KeyPinValidator.Validate(Parameters.ServerKeyPins, subjectPublicKeyInfo, TLSCertificateType.RawPublicKey);
                    key     = TLSPeerKey.Raw(subjectPublicKeyInfo);

                }
                else
                {

                    X509Certificate2 x509;

                    try
                    {
                        x509 = X509CertificateLoader.LoadCertificate(FirstEntryOf(certificate));
                    }
                    catch (CryptographicException e)
                    {
                        throw Refused(AlertDescription.bad_certificate, $"The server's certificate cannot be read: {e.Message}");
                    }

                    result  = KeyPinValidator.Validate(Parameters.ServerKeyPins, x509);
                    key     = TLSPeerKey.FromCertificate(x509);

                }

                if (!result.IsAccepted)
                {
                    Parameters.OnServerKeyPinValidated?.Invoke(result);
                    throw Refused(AlertDescription.bad_certificate, result.Reason ?? "The key was refused.");
                }

                // Accepted or learned - once CertificateVerify has shown that the server holds this key.
                validation  = result;
                ServerKey   = key;

            }

            public TlsCredentials? GetClientCredentials(BCCertificateRequest CertificateRequest)
            {

                // Without a key the answer is the empty Certificate.
                if (Parameters.ClientCertificate is not { } clientCertificate)
                    return null;

                var certificateType  = m_context.SecurityParameters.ClientCertificateType;

                var message          = CertificateMessageOf(
                                           (BcTlsCrypto) Crypto,
                                           certificateType,
                                           clientCertificate,
                                           Parameters.ClientCertificateChain,
                                           CertificateRequest.GetCertificateRequestContext() ?? TlsUtilities.EmptyBytes
                                       );

                ClientKeyShown = true;

                return new Signer(clientCertificate, message, CertificateRequest.SupportedSignatureAlgorithms);

            }

            public override void NotifyHandshakeComplete()
            {
                base.NotifyHandshakeComplete();
                Exporter           = ExporterOf(m_context);
                HandshakeComplete  = true;
            }

            /// <summary>
            /// Tell what the key pins said about the server's key, now that
            /// the handshake has shown that the server holds it.
            /// </summary>
            public void TellValidation()
            {
                if (validation is not null)
                    Parameters.OnServerKeyPinValidated?.Invoke(validation);
            }

            public override void NotifyAlertRaised(Int16 AlertLevel, Int16 AlertDescription, String Message, Exception Cause)
            {
                if (AlertLevel == Org.BouncyCastle.Tls.AlertLevel.fatal)
                {
                    AlertRaised = AlertDescription;
                    Reason    ??= Message ?? Cause?.Message;
                }
            }

            public override void NotifyAlertReceived(Int16 AlertLevel, Int16 AlertDescription)
            {
                if (AlertLevel == Org.BouncyCastle.Tls.AlertLevel.fatal)
                    AlertReceived = AlertDescription;
            }

            private TlsFatalAlert Refused(Int16 AlertDescription, String Why)
            {
                Reason = Why;
                return new TlsFatalAlert(AlertDescription, Why);
            }

        }

        #endregion

        #region (private) Server

        private sealed class Server(BcTlsCrypto                      Crypto,
                                    RawPublicKeyTLSServerParameters  Parameters)

            : DefaultTlsServer(Crypto)

        {

            private TlsCredentialedSigner? credentials;

            public Org.BouncyCastle.Tls.TlsContext Context => m_context;
            public Boolean      HandshakeComplete    { get; private set; }
            public Func<String, Byte[], Int32, Byte[]>? Exporter { get; private set; }
            public TLSPeerKey?  ClientKey            { get; private set; }
            public Int16?       AlertRaised          { get; private set; }
            public Int16?       AlertReceived        { get; private set; }
            public String?      Reason               { get; private set; }

            protected override ProtocolVersion[] GetSupportedVersions()
                => ProtocolVersion.TLSv13.Only();

            protected override Int32[] GetSupportedCipherSuites()
                => CipherSuites;

            protected override IList<ProtocolName>? GetProtocolNames()
                => Parameters.ApplicationProtocols.Count > 0
                       ? [ .. Parameters.ApplicationProtocols.Select(ProtocolName.AsUtf8Encoding) ]
                       : null;

            // No stapling: a raw public key has no certificate to be revoked.
            protected override Boolean AllowCertificateStatus()
                => false;

            protected override Boolean AllowMultiCertStatus()
                => false;

            /// <summary>
            /// The server's key, as the client takes it: raw where the key
            /// may be shown raw and the client takes a raw public key, else
            /// in its certificate. A key that is shown raw alone is not
            /// shown to a client that takes no raw public key.
            /// </summary>
            public override TlsCredentials GetCredentials()
            {

                if (credentials is not null)
                    return credentials;

                var taken    = TlsExtensionsUtilities.GetServerCertificateTypeExtensionClient(m_clientExtensions);
                var takesRaw = taken is not null && taken.Contains(BCCertificateType.RawPublicKey);

                if (Parameters.ServerRawPublicKeys == RawPublicKeyMode.Require && !takesRaw)
                    throw Refused(AlertDescription.unsupported_certificate,
                                  "This server shows its key as a raw public key alone, and the client does not take one.");

                var certificateType = Parameters.ServerRawPublicKeys != RawPublicKeyMode.Off && takesRaw
                                          ? BCCertificateType.RawPublicKey
                                          : BCCertificateType.X509;

                credentials = new Signer(
                                  Parameters.ServerCertificate,
                                  CertificateMessageOf(
                                      (BcTlsCrypto) Crypto,
                                      certificateType,
                                      Parameters.ServerCertificate,
                                      Parameters.ServerCertificateChain,
                                      TlsUtilities.EmptyBytes
                                  ),
                                  m_context.SecurityParameters.ClientSigAlgs
                              );

                return credentials;

            }

            // A client's key: raw where the server takes raw client keys and has somebody to ask about one.
            protected override Int16[]? GetAllowedClientCertificateTypes()
                => Parameters.RequestClientCertificate &&
                   Parameters.ClientKeyValidator is not null
                       ? CertificateTypesFor(Parameters.ClientRawPublicKeys)
                       : null;

            // The server's order: a raw public key before a certificate.
            protected override Boolean PreferLocalClientCertificateTypes()
                => true;

            public override BCCertificateRequest? GetCertificateRequest()
                => Parameters.RequestClientCertificate
                       ? new BCCertificateRequest(
                             TlsUtilities.EmptyBytes,
                             TlsUtilities.GetDefaultSupportedSignatureAlgorithms(m_context),
                             null,
                             null
                         )
                       : null;

            public override void NotifyClientCertificate(BCCertificate ClientCertificate)
            {

                #region The client showed nothing

                if (ClientCertificate is null || ClientCertificate.IsEmpty)
                {

                    // Whether a client that shows no certificate may come is the validator's to say, as over SslStream.
                    if (Parameters.ClientCertificateValidator is { } askedAboutNone &&
                       !askedAboutNone(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable))
                    {
                        throw Refused(AlertDescription.certificate_required, "The client presented no key, and one is required.");
                    }

                    return;

                }

                #endregion

                #region The client showed a raw public key

                if (ClientCertificate.CertificateType == BCCertificateType.RawPublicKey)
                {

                    if (ClientCertificate.Length != 1)
                        throw Refused(AlertDescription.bad_certificate,
                                      $"The client sent {ClientCertificate.Length} entries where a raw public key is a single one.");

                    if (Parameters.ClientKeyValidator is not { } keyValidator)
                        throw Refused(AlertDescription.unsupported_certificate, "This server takes no raw public key from a client.");

                    var subjectPublicKeyInfo = FirstEntryOf(ClientCertificate);

                    // A key at all? Parsed strictly before anybody is asked about it.
                    if (!SubjectPublicKeyInfoCheck.IsValid(subjectPublicKeyInfo, TLSCertificateType.RawPublicKey, out var notAKey))
                        throw Refused(AlertDescription.bad_certificate, $"The client's raw public key is {notAKey}.");

                    var key     = TLSPeerKey.Raw(subjectPublicKeyInfo);
                    var result  = keyValidator(key);

                    if (!result.IsValid)
                        throw Refused(AlertDescription.access_denied,
                                      result.Errors.FirstOrDefault()?.ToString() ?? $"The key pin {key.KeyPin} of the client's key was refused.");

                    ClientKey = key;
                    return;

                }

                #endregion

                #region The client showed a certificate

                if (Parameters.ClientRawPublicKeys == RawPublicKeyMode.Require)
                    throw Refused(AlertDescription.unsupported_certificate,
                                  "The client presented a certificate, and this server takes a client's key as a raw public key alone.");

                X509Certificate2 x509;

                try
                {
                    x509 = X509CertificateLoader.LoadCertificate(FirstEntryOf(ClientCertificate));
                }
                catch (CryptographicException e)
                {
                    throw Refused(AlertDescription.bad_certificate, $"The client's certificate cannot be read: {e.Message}");
                }

                if (Parameters.ClientCertificateValidator is not { } certificateValidator)
                    throw Refused(AlertDescription.bad_certificate, "This server has nobody to ask about a client's certificate.");

                // The chain as SslStream would have built it: with what the client sent
                // along, no downloads and no revocation checks.
                using var chain = new X509Chain();

                chain.ChainPolicy.RevocationMode               = X509RevocationMode.NoCheck;
                chain.ChainPolicy.DisableCertificateDownloads  = true;

                var intermediates = new List<X509Certificate2>();
                var policyErrors  = SslPolicyErrors.None;

                try
                {

                    for (var i = 1; i < ClientCertificate.Length; i++)
                    {
                        var intermediate = X509CertificateLoader.LoadCertificate(ClientCertificate.GetCertificateAt(i).GetEncoded());
                        intermediates.Add(intermediate);
                        chain.ChainPolicy.ExtraStore.Add(intermediate);
                    }

                    if (!chain.Build(x509))
                        policyErrors |= SslPolicyErrors.RemoteCertificateChainErrors;

                }
                catch (CryptographicException)
                {
                    policyErrors |= SslPolicyErrors.RemoteCertificateChainErrors;
                }

                try
                {
                    if (!certificateValidator(this, x509, chain, policyErrors))
                        throw Refused(AlertDescription.bad_certificate, $"The certificate of '{x509.Subject}' was refused.");
                }
                finally
                {
                    foreach (var intermediate in intermediates)
                        intermediate.Dispose();
                }

                ClientKey = TLSPeerKey.FromCertificate(x509);

                #endregion

            }

            public override void NotifyHandshakeComplete()
            {
                base.NotifyHandshakeComplete();
                Exporter           = ExporterOf(m_context);
                HandshakeComplete  = true;
            }

            public override void NotifyAlertRaised(Int16 AlertLevel, Int16 AlertDescription, String Message, Exception Cause)
            {
                if (AlertLevel == Org.BouncyCastle.Tls.AlertLevel.fatal)
                {
                    AlertRaised = AlertDescription;
                    Reason    ??= Message ?? Cause?.Message;
                }
            }

            public override void NotifyAlertReceived(Int16 AlertLevel, Int16 AlertDescription)
            {
                if (AlertLevel == Org.BouncyCastle.Tls.AlertLevel.fatal)
                    AlertReceived = AlertDescription;
            }

            private TlsFatalAlert Refused(Int16 AlertDescription, String Why)
            {
                Reason = Why;
                return new TlsFatalAlert(AlertDescription, Why);
            }

        }

        #endregion

    }

}
