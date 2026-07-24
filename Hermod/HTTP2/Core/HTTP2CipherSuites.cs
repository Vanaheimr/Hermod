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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Collections.Concurrent;
    using System.Net.Security;


    /// <summary>
    /// RFC 9113, Section 9.2.2: the TLS 1.2 cipher-suite restrictions for HTTP/2.
    ///
    /// A deployment of HTTP/2 over TLS 1.2 MUST NOT use any of the cipher suites
    /// listed in RFC 9113, Appendix A, and an endpoint MAY treat the negotiation
    /// of such a suite as a connection error of type INADEQUATE_SECURITY.
    ///
    /// Appendix A is a ~300-entry enumeration, but it is not arbitrary: it is
    /// exactly the set of TLS 1.2 cipher suites that lack an *ephemeral* key
    /// exchange (no forward secrecy) or lack an *AEAD* cipher (RFC 9113, Section
    /// 9.2.2 states both properties as the requirement). This class therefore
    /// tests those two structural properties instead of transcribing the table —
    /// same verdict for every suite in the table, and, unlike a transcribed
    /// table, it cannot silently go stale.
    ///
    /// Note the direction of the two failure modes: a suite the runtime does not
    /// even know (no name for the value) is reported as *permitted*, because
    /// Appendix A is a closed list — a suite registered after RFC 9113 is by
    /// definition not on it, and refusing the unknown would be a stricter rule
    /// than the RFC states.
    ///
    /// TLS 1.3 needs no filtering at all: every TLS 1.3 cipher suite is AEAD, and
    /// the key exchange is always ephemeral. Its suite names carry no key-exchange
    /// component (no "_WITH_"), which is how they are recognized here.
    /// </summary>
    public static class HTTP2CipherSuites
    {

        #region Data

        /// <summary>
        /// Verdicts are memoized: a connection resolves exactly one cipher suite,
        /// and a server sees the same handful of suites over and over.
        /// </summary>
        private static readonly ConcurrentDictionary<TlsCipherSuite, Boolean> blocklistCache = new();

        #endregion


        #region IsBlocklisted (CipherSuite)

        /// <summary>
        /// Whether the given cipher suite appears in RFC 9113, Appendix A, and
        /// must therefore not be used to carry HTTP/2 over TLS 1.2.
        /// </summary>
        /// <param name="CipherSuite">The cipher suite negotiated by the TLS handshake.</param>
        public static Boolean IsBlocklisted(TlsCipherSuite CipherSuite)

            => blocklistCache.GetOrAdd(
                   CipherSuite,
                   static suite => !IsPermitted(Enum.GetName(suite))
               );

        #endregion

        #region IsPermitted (CipherSuiteName)

        /// <summary>
        /// The structural test behind <see cref="IsBlocklisted(TlsCipherSuite)"/>,
        /// working on the IANA cipher-suite name (which is what the
        /// <see cref="TlsCipherSuite"/> enum members are named after).
        /// </summary>
        /// <param name="CipherSuiteName">An IANA cipher-suite name, or null for a suite this runtime has no name for.</param>
        public static Boolean IsPermitted(String? CipherSuiteName)
        {

            // A value the runtime cannot name is not in the (closed) Appendix A
            // list — see the class remarks on the direction of this decision.
            if (String.IsNullOrEmpty(CipherSuiteName))
                return true;

            // TLS 1.3 suites name only the cipher, never the key exchange
            // ("TLS_AES_128_GCM_SHA256"). All of them are AEAD + ephemeral.
            if (!CipherSuiteName.Contains("_WITH_", StringComparison.Ordinal))
                return true;

            // Forward secrecy: an *ephemeral* Diffie-Hellman key exchange.
            // Deliberately not "TLS_DH_"/"TLS_ECDH_" — those are the static
            // variants, and they are on the list.
            var isEphemeral = CipherSuiteName.StartsWith("TLS_DHE_",   StringComparison.Ordinal) ||
                              CipherSuiteName.StartsWith("TLS_ECDHE_", StringComparison.Ordinal);

            if (!isEphemeral)
                return false;

            // Authenticated encryption with associated data: GCM, CCM (including
            // the truncated-tag CCM_8) or ChaCha20-Poly1305. Everything else —
            // CBC with a MAC, RC4, NULL — is on the list.
            return CipherSuiteName.Contains("_GCM",                StringComparison.Ordinal) ||
                   CipherSuiteName.Contains("_CCM",                StringComparison.Ordinal) ||
                   CipherSuiteName.Contains("CHACHA20_POLY1305",   StringComparison.Ordinal);

        }

        #endregion

    }

}
