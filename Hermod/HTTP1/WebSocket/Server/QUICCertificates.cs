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

using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// What a WebSocket server shows over QUIC, asked of its selector for
    /// every connection, as TCP asks at every handshake: a certificate that
    /// was replaced is shown to the next client over HTTP/3 without HTTP/3
    /// being started again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The certificate made for QUIC is kept as long as the selector answers
    /// with the same one, and made again once it answers with another. One
    /// that was shown is not let go of before HTTP/3 stops: a connection that
    /// was handed it may still be signing with it, as a connection over TCP
    /// goes on with what it was shown.
    /// </para>
    /// <para>
    /// A selector that cannot answer for a connection leaves it the one shown
    /// before, and says so; only where nothing was shown yet is the
    /// connection refused.
    /// </para>
    /// </remarks>
    internal sealed class QUICCertificates : IDisposable
    {

        #region Data

        private readonly Func<TcpClient, (X509Certificate2 Certificate, X509Certificate2Collection? Intermediates)>  select;
        private readonly RawPublicKeyMode                                                                           rawPublicKeys;
        private readonly Action<Exception>                                                                          cannotAnswer;

        /// <summary>
        /// Asked about by the selectors as a client nothing is known of yet:
        /// QUIC is asked before anything of the client but its address is known.
        /// </summary>
        private readonly TcpClient                nobody      = new();

        private readonly Lock                     lockObject  = new();
        private readonly List<ServerCertificate>  shown       = [];

        private ServerCertificate?  current;
        private String?             currentKey;
        private Boolean             disposed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// What a WebSocket server shows over QUIC.
        /// </summary>
        /// <param name="Select">The certificate the server shows now, with the intermediates that lead from it towards its root, if any.</param>
        /// <param name="RawPublicKeys">Whether the certificate is shown as a raw public key, as over TCP.</param>
        /// <param name="CannotAnswer">Told where the selector cannot answer for a connection that is then shown what was shown before.</param>
        public QUICCertificates(Func<TcpClient, (X509Certificate2 Certificate, X509Certificate2Collection? Intermediates)>  Select,
                                RawPublicKeyMode                                                                           RawPublicKeys,
                                Action<Exception>                                                                          CannotAnswer)
        {
            this.select         = Select;
            this.rawPublicKeys  = RawPublicKeys;
            this.cannotAnswer   = CannotAnswer;
        }

        #endregion


        #region ForTheNextConnection()

        /// <summary>
        /// The certificate the next connection over QUIC is shown: the one the
        /// selector answers with now.
        /// </summary>
        /// <exception cref="Exception">Where the selector cannot answer and nothing was shown before.</exception>
        public ServerCertificate ForTheNextConnection()
        {

            lock (lockObject)
            {

                ObjectDisposedException.ThrowIf(disposed, this);

                try
                {

                    var (certificate, intermediates) = select(nobody);

                    var key = KeyOf(certificate, intermediates);

                    if (current is not null && key == currentKey)
                        return current;

                    var made = ServerCertificate.From(certificate, intermediates);

                    // Shown raw over QUIC where it is shown raw over TCP.
                    made.RawPublicKeys = rawPublicKeys;

                    shown.Add(made);
                    current     = made;
                    currentKey  = key;

                    return made;

                }
                catch (Exception e) when (current is not null)
                {
                    cannotAnswer(e);
                    return current;
                }

            }

        }

        #endregion

        #region (private static) KeyOf(Certificate, Intermediates)

        /// <summary>
        /// What tells one answer of the selector from another: the certificate
        /// and the intermediates sent along with it.
        /// </summary>
        private static String KeyOf(X509Certificate2             Certificate,
                                    X509Certificate2Collection?  Intermediates)

            => Intermediates is { Count: > 0 }
                   ? String.Join("|", [ Certificate.Thumbprint, .. Intermediates.Select(intermediate => intermediate.Thumbprint) ])
                   : Certificate.Thumbprint;

        #endregion


        #region Dispose()

        /// <summary>
        /// Let go of every certificate made for QUIC: once HTTP/3 stopped, and
        /// no connection is signing with any of them any more.
        /// </summary>
        public void Dispose()
        {

            lock (lockObject)
            {

                if (disposed)
                    return;

                disposed = true;

                foreach (var certificate in shown)
                    certificate.Dispose();

                shown.Clear();
                current = null;

                nobody.Dispose();

            }

        }

        #endregion

    }

}
