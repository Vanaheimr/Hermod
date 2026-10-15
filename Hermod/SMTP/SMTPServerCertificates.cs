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

using System.Collections.Concurrent;
using System.Net.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// The certificates an SMTP server presents, asked for at every handshake:
    /// what <see cref="SMTPServerConfig.ServerCertificateSelector"/> answers,
    /// or the one certificate read from <see cref="SMTPServerConfig.CertificatePath"/>.
    /// </summary>
    /// <remarks>
    /// A handshake presents a context, not a certificate: the certificate
    /// together with its intermediates, which SMTP clients need more than
    /// browsers do - an MTA does not fetch a missing intermediate the way a
    /// browser does, it fails the handshake or, worse, falls back to plain
    /// text. A context is built once per chain (by its
    /// <see cref="ServerCertificateChain.CacheKey"/>) and offline, so that no
    /// handshake ever waits for the network.
    /// </remarks>
    public sealed class SMTPServerCertificates(Func<String?, ServerCertificateChain?>  Selector,
                                               ILogger?                                Logger   = null)
    {

        private readonly ConcurrentDictionary<String, SslStreamCertificateContext> contexts = new ();

        /// <summary>
        /// The same certificate for every name.
        /// </summary>
        public static SMTPServerCertificates Of(ServerCertificateChain  Chain,
                                                ILogger?                Logger   = null)

            => new (_ => Chain, Logger);


        /// <summary>
        /// Whether there is a certificate to present now - what decides whether
        /// STARTTLS is offered.
        /// </summary>
        public Boolean Available
            => Chain(null) is not null;

        /// <summary>
        /// The chain for the given name, or null; a selector that throws is a
        /// selector without an answer.
        /// </summary>
        public ServerCertificateChain? Chain(String? ServerName)
        {
            try
            {
                return Selector(ServerName);
            }
            catch (Exception e)
            {
                Logger?.Log(LogLevel.Error, $"The server certificate selector failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The TLS context for the given name, or null when there is no
        /// certificate for it.
        /// </summary>
        public SslStreamCertificateContext? ContextFor(String? ServerName)
        {

            var chain = Chain(ServerName);

            if (chain is null)
                return null;

            return contexts.GetOrAdd(
                       chain.CacheKey,
                       _ => {

                           Logger?.Log(LogLevel.Info, $"TLS certificate: {chain}, valid until {chain.Certificate.NotAfter.ToUniversalTime():u}");

                           return SslStreamCertificateContext.Create(
                                      target:                  chain.Certificate,
                                      additionalCertificates:  chain.HasIntermediates ? chain.Intermediates : null,
                                      offline:                 true
                                  );

                       }
                   );

        }

    }

}
