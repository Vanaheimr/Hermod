/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Linq;
using System.Diagnostics;

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A e-mail address with owner name and optional cryptographic keys.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public class EMailAddress
    {

        #region Properties

        #region OwnerName

        private readonly String _OwnerName;

        /// <summary>
        /// The name of the owner of the e-mail address.
        /// </summary>
        public String OwnerName
        {
            get
            {
                return _OwnerName;
            }
        }

        #endregion

        #region Address

        private readonly SimpleEMailAddress _Address;

        /// <summary>
        /// The e-mail address.
        /// </summary>
        public SimpleEMailAddress Address
        {
            get
            {
                return _Address;
            }
        }

        #endregion

        #region SecretKeyRing

        private readonly PgpSecretKeyRing _SecretKeyRing;

        /// <summary>
        /// The secret key ring for the given e-mail address.
        /// </summary>
        public PgpSecretKeyRing SecretKeyRing
        {
            get
            {
                return _SecretKeyRing;
            }
        }

        #endregion

        #region PublicKeyRing

        private readonly PgpPublicKeyRing _PublicKeyRing;

        /// <summary>
        /// The public key ring for the given e-mail address.
        /// </summary>
        public PgpPublicKeyRing PublicKeyRing
        {
            get
            {
                return _PublicKeyRing;
            }
        }

        #endregion

        #region (private) DebugView

        private String DebugView
        {
            get
            {

                return this.ToString() +
                       (PublicKeyRing != null ? " publickey: "  + _PublicKeyRing.First().KeyId + " (" + _PublicKeyRing.Count() + ")" : "") +
                       (SecretKeyRing != null ? " privatekey: " + _SecretKeyRing.First().KeyId + " (" + _PublicKeyRing.Count() + ")" : "");

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMailAddress(SimpleEMailAddress, SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(SimpleEMailAddress SimpleEMailAddress,
                            PgpSecretKeyRing    SecretKeyRing  = null,
                            PgpPublicKeyRing    PublicKeyRing  = null)

            : this("", SimpleEMailAddress, SecretKeyRing, PublicKeyRing)

        { }

        #endregion

        #region EMailAddress(SimpleEMailAddressString, SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String SimpleEMailAddressString,
                            PgpSecretKeyRing  SecretKeyRing = null,
                            PgpPublicKeyRing  PublicKeyRing = null)

            : this("", SimpleEMailAddress.Parse(SimpleEMailAddressString), SecretKeyRing, PublicKeyRing)

        { }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddress, SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String              OwnerName,
                            SimpleEMailAddress  SimpleEMailAddress,
                            PgpSecretKeyRing    SecretKeyRing = null,
                            PgpPublicKeyRing    PublicKeyRing = null)

        {

            this._OwnerName      = OwnerName.Trim();
            this._Address        = SimpleEMailAddress;
            this._PublicKeyRing  = PublicKeyRing;
            this._SecretKeyRing  = SecretKeyRing;

        }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddressString, SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="SecretKeyRing">The public key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The secret key ring for an e-mail address.</param>
        public EMailAddress(String            OwnerName,
                            String            SimpleEMailAddressString,
                            PgpSecretKeyRing  SecretKeyRing = null,
                            PgpPublicKeyRing  PublicKeyRing = null)

            : this(OwnerName, SimpleEMailAddress.Parse(SimpleEMailAddressString), SecretKeyRing, PublicKeyRing)

        { }

        #endregion

        #endregion


        #region Implicitly convert SimpleEMailAddress -> EMailAddress

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="EMailAddress">A simple e-mail address.</param>
        /// <returns>A e-mail address.</returns>
        public static implicit operator EMailAddress(SimpleEMailAddress EMailAddress)
        {
            return new EMailAddress(EMailAddress);
        }

        #endregion


        public static EMailAddress Parse(String EMailString)
        {

            var b = EMailString.IndexOf('<');
            var c = EMailString.IndexOf('>');

            if (b > 0 && c > 0 && c > b)
                return new EMailAddress(EMailString.Remove(b, c-b+1).Trim(), SimpleEMailAddress.Parse(EMailString.Substring(b+1, c-b-1).Trim()));

            return new EMailAddress(SimpleEMailAddress.Parse(EMailString));

        }



        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            return _OwnerName.IsNotNullOrEmpty()
                       ? (_OwnerName + " <" + _Address + ">").Trim()
                       : _Address.ToString();

        }

        #endregion

    }

}
