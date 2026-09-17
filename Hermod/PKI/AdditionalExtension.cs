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

using Org.BouncyCastle.Asn1;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    /// <summary>
    /// One X.509 extension this factory knows nothing about, to be put into a
    /// certificate as it is.
    /// </summary>
    /// <remarks>
    /// There are more certificate profiles than any one library will ever have
    /// a parameter for. SunSpec's Modbus-TLS puts the role a client may play
    /// into an extension under a private arc; a charging network may require
    /// its own; and both are perfectly ordinary X.509. So rather than growing
    /// a parameter each time, a caller that knows its profile brings the
    /// extension already encoded.
    ///
    /// <b>Whether it is critical is a decision, not a detail.</b> A critical
    /// extension obliges anybody who does not understand it to reject the
    /// certificate - which is what makes a role extension enforceable, and
    /// also what makes a certificate useless to every other program if the
    /// critical flag is set carelessly. It has no default here for that
    /// reason.
    /// </remarks>
    /// <param name="OID">Which extension, by object identifier.</param>
    /// <param name="Critical">Whether a reader that does not understand it must refuse the certificate.</param>
    /// <param name="Value">Its contents, already encoded.</param>
    public sealed class AdditionalExtension(DerObjectIdentifier  OID,
                                            Boolean              Critical,
                                            Asn1Encodable        Value)
    {

        #region Properties

        /// <summary>
        /// Which extension, by object identifier.
        /// </summary>
        public DerObjectIdentifier  OID       { get; } = OID;

        /// <summary>
        /// Whether a reader that does not understand this extension must refuse
        /// the certificate.
        /// </summary>
        public Boolean              Critical  { get; } = Critical;

        /// <summary>
        /// Its contents, already encoded.
        /// </summary>
        public Asn1Encodable        Value     { get; } = Value;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// One extension, its identifier written the way everybody writes one.
        /// </summary>
        /// <param name="OID">Which extension, e.g. "1.3.6.1.4.1.50316.802.1".</param>
        /// <param name="Critical">Whether a reader that does not understand it must refuse the certificate.</param>
        /// <param name="Value">Its contents, already encoded.</param>
        public AdditionalExtension(String         OID,
                                   Boolean        Critical,
                                   Asn1Encodable  Value)

            : this(new DerObjectIdentifier(OID),
                   Critical,
                   Value)

        { }

        #endregion

        #region (override) ToString()

        public override String ToString()

            => $"{OID}{(Critical ? " (critical)" : "")}";

        #endregion

    }

}
