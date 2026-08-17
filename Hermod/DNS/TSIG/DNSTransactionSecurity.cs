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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A client's transaction-security configuration — TSIG or SIG(0) — and the
    /// two operations every transport needs: sign the query, check the reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists as one object rather than as a pair of methods on each client
    /// because of finding 19. Signing lived inside <c>DNSUDPClient</c>'s datagram
    /// path; the TCP fallback in the same class built its query separately and
    /// sent it unsigned, and nothing reported it — a server answers unsigned
    /// requests, so there was no error anywhere to notice. Four transports each
    /// keeping their own copy of "and here we sign it" is that mistake with three
    /// more places to make it.
    /// </para>
    /// <para>
    /// So: a transport holds one of these and calls it. Adding a transport now
    /// means deciding not to secure it, rather than forgetting to.
    /// </para>
    /// </remarks>
    public sealed class DNSTransactionSecurity
    {

        #region Data

        /// <summary>
        /// Nothing configured — the common case, and free.
        /// </summary>
        public static readonly DNSTransactionSecurity None = new (null, null, null);

        #endregion

        #region Properties

        /// <summary>
        /// The shared secret to sign queries with (RFC 8945), or null.
        /// </summary>
        public TSIGKey?          TSIGKey          { get; }

        /// <summary>
        /// The key pair to sign queries with (RFC 2931), or null.
        /// </summary>
        public SIG0Key?          SIG0Key          { get; }

        /// <summary>
        /// The KEY records a signed *response* is checked against.
        /// </summary>
        public IEnumerable<KEY>  SIG0ServerKeys   { get; }

        /// <summary>
        /// Whether anything at all is configured.
        /// </summary>
        public Boolean IsActive
            => TSIGKey is not null || SIG0Key is not null || SIG0ServerKeys.Any();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a transaction-security configuration.
        /// </summary>
        /// <param name="TSIGKey">The shared secret to sign with (RFC 8945).</param>
        /// <param name="SIG0Key">The key pair to sign with (RFC 2931).</param>
        /// <param name="SIG0ServerKeys">The KEY records a signed response is checked against.</param>
        public DNSTransactionSecurity(TSIGKey?           TSIGKey          = null,
                                      SIG0Key?           SIG0Key          = null,
                                      IEnumerable<KEY>?  SIG0ServerKeys   = null)
        {

            this.TSIGKey         = TSIGKey;
            this.SIG0Key         = SIG0Key;
            this.SIG0ServerKeys  = SIG0ServerKeys ?? [];

        }

        #endregion


        #region SignQuery(Wire, out RequestMAC)

        /// <summary>
        /// Apply whichever transaction signature is configured.
        /// </summary>
        /// <param name="Wire">The serialized query.</param>
        /// <param name="RequestMAC">The TSIG MAC of the signed query, which the response's MAC folds in (RFC 8945 §4.3.1); null for SIG(0) or an unsigned query.</param>
        /// <returns>The query as it goes on the wire.</returns>
        /// <remarks>
        /// At most one signature is applied. RFC 2931 §3.2 forbids a message
        /// carrying both, and TSIG wins when both are configured — a caller that
        /// set up a shared secret *and* a key pair has said something
        /// contradictory, and quietly choosing the cheaper one is the least
        /// surprising reading.
        /// </remarks>
        public Byte[] SignQuery(Byte[] Wire, out Byte[]? RequestMAC)
        {

            RequestMAC = null;

            if (TSIGKey is not null)
            {
                var signed  = TSIGSigner.Sign(Wire, TSIGKey);
                RequestMAC  = TSIGSigner.Verify(signed, TSIGKey).MAC;
                return signed;
            }

            if (SIG0Key is not null)
                return SIG0Signer.Sign(Wire, SIG0Key);

            return Wire;

        }

        #endregion

        #region TryAcceptResponse(ref Body, RequestMAC, SignedQuery, out Reason)

        /// <summary>
        /// Check a response's transaction signature, and strip it so the rest of
        /// the client sees an ordinary message.
        /// </summary>
        /// <param name="Body">The response as received; replaced by the message without its signature.</param>
        /// <param name="RequestMAC">The MAC of the query, for TSIG.</param>
        /// <param name="SignedQuery">The query exactly as sent — the "full query" a SIG(0) response signature covers (RFC 2931 §3.1).</param>
        /// <param name="Reason">Why the response was rejected, for the caller to log.</param>
        /// <returns>False when the response must not be believed.</returns>
        public Boolean TryAcceptResponse(ref Byte[]    Body,
                                         Byte[]?       RequestMAC,
                                         Byte[]        SignedQuery,
                                         out String?   Reason)
        {

            Reason = null;

            if (TSIGKey is not null)
            {

                var verdict = TSIGSigner.Verify(Body, TSIGKey, RequestMAC: RequestMAC);

                if (!verdict.IsValid)
                {
                    Reason = verdict.Description;
                    return false;
                }

                if (TSIGSigner.TryStripTSIG(Body, out var withoutTSIG, out _) && withoutTSIG is not null)
                    Body = withoutTSIG;

                return true;

            }

            if (SIG0Signer.IsSIG0Signed(Body))
            {

                // RFC 2931 §3.2 makes checking a response SIG(0) a MAY outside
                // TKEY, and tells a party that does not implement it to "ignore
                // them without error". With no key configured there is nothing to
                // decide, so the record is dropped rather than trusted.
                if (SIG0ServerKeys.Any())
                {

                    var verdict = SIG0Signer.Verify(Body, SIG0ServerKeys, Request: SignedQuery);

                    if (!verdict.IsValid)
                    {
                        Reason = verdict.Description;
                        return false;
                    }

                }

                if (SIG0Signer.TryStripSIG0(Body, out var withoutSIG0, out _) && withoutSIG0 is not null)
                    Body = withoutSIG0;

            }

            return true;

        }

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()

            => TSIGKey is not null
                   ? $"TSIG {TSIGKey}"
                   : SIG0Key is not null
                         ? $"SIG(0) {SIG0Key}"
                         : "unsigned";

        #endregion

    }

}
