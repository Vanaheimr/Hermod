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
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The TLS stack a connection over TCP runs on.
    /// </summary>
    public enum TLSProvider
    {

        /// <summary>
        /// .NET's SslStream: the TLS of the operating system. The default,
        /// and the only one that speaks TLS 1.2. It knows certificates alone.
        /// </summary>
        SslStream,

        /// <summary>
        /// BouncyCastle's TLS 1.3: used where a connection is to speak raw
        /// public keys (RFC 7250), which SslStream cannot.
        /// </summary>
        BouncyCastle

    }


    /// <summary>
    /// What a TLS handshake over TCP came to, whichever TLS stack ran it: the
    /// stream to speak over, and what the layers above need to know about
    /// the peer and about what was negotiated.
    /// </summary>
    public sealed class TLSSession
    {

        #region Data

        private readonly Func<String, Byte[], Int32, Byte[]>? exporter;

        #endregion

        #region Properties

        /// <summary>
        /// The TLS stack this session runs on.
        /// </summary>
        public TLSProvider           Provider                 { get; }

        /// <summary>
        /// The authenticated and encrypted stream.
        /// </summary>
        public Stream                Stream                   { get; }

        /// <summary>
        /// The stream as an SslStream, where that is what it is.
        /// </summary>
        public SslStream?            SslStream
            => Stream as SslStream;

        /// <summary>
        /// The application protocol both sides agreed on (ALPN, RFC 7301),
        /// e.g. "h2" or "http/1.1", or null where none was negotiated.
        /// </summary>
        public String?               ApplicationProtocol      { get; }

        /// <summary>
        /// The certificate type this side authenticated with (RFC 7250).
        /// </summary>
        public TLSCertificateType    LocalCertificateType     { get; }

        /// <summary>
        /// The certificate type the peer authenticated with, or was to
        /// authenticate with (RFC 7250).
        /// </summary>
        public TLSCertificateType    RemoteCertificateType    { get; }

        /// <summary>
        /// The key the peer proved to hold: the key of its certificate, or
        /// its raw public key. Null where the peer showed none, which only
        /// a client may.
        /// </summary>
        public TLSPeerKey?           RemoteKey                { get; }

        /// <summary>
        /// The certificate of the peer, or null where it showed none, or a
        /// raw public key.
        /// </summary>
        public X509Certificate2?     RemoteCertificate
            => RemoteKey?.Certificate;

        /// <summary>
        /// Whether keying material can be exported from this session
        /// (RFC 8446, section 7.5).
        /// </summary>
        public Boolean               CanExportKeyingMaterial
            => exporter is not null;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TLS session.
        /// </summary>
        /// <param name="Provider">The TLS stack this session runs on.</param>
        /// <param name="Stream">The authenticated and encrypted stream.</param>
        /// <param name="ApplicationProtocol">The application protocol both sides agreed on, if any.</param>
        /// <param name="LocalCertificateType">The certificate type this side authenticated with.</param>
        /// <param name="RemoteCertificateType">The certificate type the peer authenticated with.</param>
        /// <param name="RemoteKey">The key the peer proved to hold, if any.</param>
        /// <param name="Exporter">Exports keying material: label, context, length.</param>
        public TLSSession(TLSProvider                           Provider,
                          Stream                                Stream,
                          String?                               ApplicationProtocol     = null,
                          TLSCertificateType                    LocalCertificateType    = TLSCertificateType.X509,
                          TLSCertificateType                    RemoteCertificateType   = TLSCertificateType.X509,
                          TLSPeerKey?                           RemoteKey               = null,
                          Func<String, Byte[], Int32, Byte[]>?  Exporter                = null)
        {

            this.Provider               = Provider;
            this.Stream                 = Stream;
            this.ApplicationProtocol    = ApplicationProtocol;
            this.LocalCertificateType   = LocalCertificateType;
            this.RemoteCertificateType  = RemoteCertificateType;
            this.RemoteKey              = RemoteKey;
            this.exporter               = Exporter;

        }

        #endregion


        #region (static) Of(SslStream)

        /// <summary>
        /// The session of an SslStream whose handshake is done.
        /// </summary>
        /// <param name="SslStream">An authenticated SslStream.</param>
        public static TLSSession Of(SslStream SslStream)
        {

            var remoteCertificate = SslStream.RemoteCertificate switch {
                                        X509Certificate2 certificate  => certificate,
                                        X509Certificate  certificate  => new X509Certificate2(certificate),
                                        _                             => null
                                    };

            var applicationProtocol = SslStream.NegotiatedApplicationProtocol.Protocol.IsEmpty
                                          ? null
                                          : SslStream.NegotiatedApplicationProtocol.ToString();

            return new (TLSProvider.SslStream,
                        SslStream,
                        applicationProtocol,
                        TLSCertificateType.X509,
                        TLSCertificateType.X509,
                        remoteCertificate is not null
                            ? TLSPeerKey.FromCertificate(remoteCertificate)
                            : null);

        }

        #endregion

        #region ExportKeyingMaterial(Label, Context, Length)

        /// <summary>
        /// Export keying material of this session (RFC 8446, section 7.5):
        /// the same on both ends, and on no other connection.
        /// </summary>
        /// <param name="Label">The label of the exporter.</param>
        /// <param name="Context">The context value, which may be empty.</param>
        /// <param name="Length">The number of bytes to export.</param>
        /// <exception cref="NotSupportedException">Where the TLS stack of this session exports none: see <see cref="CanExportKeyingMaterial"/>.</exception>
        public Byte[] ExportKeyingMaterial(String              Label,
                                           ReadOnlySpan<Byte>  Context,
                                           Int32               Length)

            => exporter is not null
                   ? exporter(Label, Context.ToArray(), Length)
                   : throw new NotSupportedException($"The TLS stack {Provider} exports no keying material.");

        #endregion

        #region (override) ToString()

        public override String ToString()

            => String.Concat(
                   Provider,
                   ApplicationProtocol is not null ? $", {ApplicationProtocol}" : "",
                   $", local: {LocalCertificateType}",
                   $", remote: {RemoteCertificateType}",
                   RemoteKey is not null ? $" {RemoteKey.KeyPin}" : ""
               );

        #endregion

    }

}
