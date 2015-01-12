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
using System.Diagnostics;

using Org.BouncyCastle.Bcpg.OpenPgp;

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

        #region SecretKey

        private readonly PgpSecretKey _SecretKey;

        /// <summary>
        /// The secret key for the given e-mail address.
        /// </summary>
        public PgpSecretKey SecretKey
        {
            get
            {
                return _SecretKey;
            }
        }

        #endregion

        #region PublicKey

        private readonly PgpPublicKey _PublicKey;

        /// <summary>
        /// The public key for the given e-mail address.
        /// </summary>
        public PgpPublicKey PublicKey
        {
            get
            {
                return _PublicKey;
            }
        }

        #endregion

        #region (private) DebugView

        private String DebugView
        {
            get
            {

                return this.ToString() +
                       (PublicKey  != null ? " publickey: "  + _PublicKey. KeyId : "") +
                       (SecretKey != null ? " privatekey: " + _SecretKey.KeyId : "");

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMailAddress(SimpleEMailAddress, SecretKey = null, PublicKey = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="PublicKey">The public key for an e-mail address.</param>
        /// <param name="SecretKey">The secret key for an e-mail address.</param>
        public EMailAddress(SimpleEMailAddress  SimpleEMailAddress,
                            PgpSecretKey        SecretKey   = null,
                            PgpPublicKey        PublicKey   = null)

            : this("", SimpleEMailAddress, SecretKey, PublicKey)

        { }

        #endregion

        #region EMailAddress(SimpleEMailAddressString, SecretKey = null, PublicKey = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="PublicKey">The public key for an e-mail address.</param>
        /// <param name="SecretKey">The secret key for an e-mail address.</param>
        public EMailAddress(String        SimpleEMailAddressString,
                            PgpSecretKey  SecretKey   = null,
                            PgpPublicKey  PublicKey   = null)

            : this("", SimpleEMailAddress.Parse(SimpleEMailAddressString), SecretKey, PublicKey)

        { }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddress, SecretKey = null, PublicKey = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="SecretKey">The secret key for an e-mail address.</param>
        /// <param name="PublicKey">The public key for an e-mail address.</param>
        public EMailAddress(String OwnerName,
                            SimpleEMailAddress  SimpleEMailAddress,
                            PgpSecretKey        SecretKey   = null,
                            PgpPublicKey        PublicKey   = null)

        {

            this._OwnerName   = OwnerName.Trim();
            this._Address     = SimpleEMailAddress;
            this._PublicKey   = PublicKey;
            this._SecretKey   = SecretKey;

        }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddressString, SecretKey = null, PublicKey = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="PublicKey">The public key for an e-mail address.</param>
        /// <param name="SecretKey">The secret key for an e-mail address.</param>
        public EMailAddress(String         OwnerName,
                            String         SimpleEMailAddressString,
                            PgpSecretKey   SecretKey   = null,
                            PgpPublicKey   PublicKey   = null)

            : this(OwnerName, SimpleEMailAddress.Parse(SimpleEMailAddressString), SecretKey, PublicKey)

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


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return (_OwnerName + " <" + _Address + ">").Trim();
        }

        #endregion

    }

}
