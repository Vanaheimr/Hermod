/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Diagnostics;

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;
using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A e-mail address with owner name and optional cryptographic keys.
    /// </summary>
    public class EMailAddress
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/UsersAPI+json/EMailAddress";

        #endregion

        #region Properties

        /// <summary>
        /// The name of the owner of the e-mail address.
        /// </summary>
        public String              OwnerName        { get; }

        /// <summary>
        /// The e-mail address.
        /// </summary>
        public SimpleEMailAddress  Address          { get; }

        /// <summary>
        /// The secret key ring for the given e-mail address.
        /// </summary>
        public PgpSecretKeyRing    SecretKeyRing    { get; }

        /// <summary>
        /// The public key ring for the given e-mail address.
        /// </summary>
        public PgpPublicKeyRing    PublicKeyRing    { get; }


        #region (private) DebugView

        private String DebugView
        {
            get
            {

                return this +
                       (PublicKeyRing != null ? " publickey: "  + PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().First().KeyId + " (" + PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count() + ")" : "") +
                       (SecretKeyRing != null ? " privatekey: " + SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().First().KeyId + " (" + SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().Count() + ")" : "");

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMailAddress(           SimpleEMailAddress,       SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(SimpleEMailAddress  SimpleEMailAddress,
                            PgpSecretKeyRing    SecretKeyRing,
                            PgpPublicKeyRing    PublicKeyRing   = null)
        {

            this.OwnerName      = "";
            this.Address        = SimpleEMailAddress;
            this.PublicKeyRing  = PublicKeyRing;
            this.SecretKeyRing  = SecretKeyRing;

        }

        #endregion

        #region EMailAddress(           SimpleEMailAddressString, SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String            SimpleEMailAddressString,
                            PgpSecretKeyRing  SecretKeyRing,
                            PgpPublicKeyRing  PublicKeyRing   = null)
        {

            this.OwnerName      = "";
            this.Address        = SimpleEMailAddress.Parse(SimpleEMailAddressString);
            this.PublicKeyRing  = PublicKeyRing;
            this.SecretKeyRing  = SecretKeyRing;

        }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddress,       SecretKeyRing = null, PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="SecretKeyRing">The secret key ring for an e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String              OwnerName,
                            SimpleEMailAddress  SimpleEMailAddress,
                            PgpSecretKeyRing    SecretKeyRing,
                            PgpPublicKeyRing    PublicKeyRing   = null)

        {

            #region Initial checks

            if (OwnerName.IsNotNullOrEmpty())
                OwnerName = OwnerName.Trim();

            if (OwnerName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(OwnerName),  "The given OwnerName must not be null or empty!");

            #endregion

            this.OwnerName      = OwnerName.Trim();
            this.Address        = SimpleEMailAddress;
            this.PublicKeyRing  = PublicKeyRing;
            this.SecretKeyRing  = SecretKeyRing;

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
                            PgpSecretKeyRing  SecretKeyRing,
                            PgpPublicKeyRing  PublicKeyRing   = null)

            : this(OwnerName,
                   SimpleEMailAddress.Parse(SimpleEMailAddressString),
                   SecretKeyRing,
                   PublicKeyRing)

        { }

        #endregion


        #region EMailAddress(           SimpleEMailAddress,                             PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(SimpleEMailAddress  SimpleEMailAddress,
                            PgpPublicKeyRing    PublicKeyRing  = null)

            : this(SimpleEMailAddress:  SimpleEMailAddress,
                   SecretKeyRing:       null,
                   PublicKeyRing:       PublicKeyRing)

        { }

        #endregion

        #region EMailAddress(           SimpleEMailAddressString,                       PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String            SimpleEMailAddressString,
                            PgpPublicKeyRing  PublicKeyRing  = null)

            : this(SimpleEMailAddressString:  SimpleEMailAddressString,
                   SecretKeyRing:             null,
                   PublicKeyRing:             PublicKeyRing)

        { }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddress,                             PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        /// <param name="PublicKeyRing">The public key ring for an e-mail address.</param>
        public EMailAddress(String              OwnerName,
                            SimpleEMailAddress  SimpleEMailAddress,
                            PgpPublicKeyRing    PublicKeyRing  = null)

            : this(OwnerName:           OwnerName,
                   SimpleEMailAddress:  SimpleEMailAddress,
                   SecretKeyRing:       null,
                   PublicKeyRing:       PublicKeyRing)

        { }

        #endregion

        #region EMailAddress(OwnerName, SimpleEMailAddressString,                       PublicKeyRing = null)

        /// <summary>
        /// Create a new e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="SimpleEMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="PublicKeyRing">The secret key ring for an e-mail address.</param>
        public EMailAddress(String            OwnerName,
                            String            SimpleEMailAddressString,
                            PgpPublicKeyRing  PublicKeyRing  = null)

            : this(OwnerName:                 OwnerName,
                   SimpleEMailAddressString:  SimpleEMailAddressString,
                   SecretKeyRing:             null,
                   PublicKeyRing:             PublicKeyRing)

        { }

        #endregion

        #endregion


        #region Implicitly convert SimpleEMailAddress -> EMailAddress

        /// <summary>
        /// Implicitly convert a SimpleEMailAddress into an EMailAddress.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        public static implicit operator EMailAddress(SimpleEMailAddress SimpleEMailAddress)
            => new EMailAddress(SimpleEMailAddress:  SimpleEMailAddress,
                                SecretKeyRing:       null,
                                PublicKeyRing:       null);

        #endregion


        #region (static) Parse(EMailAddressString)

        /// <summary>
        /// Parse the given e-mail address.
        /// </summary>
        /// <param name="EMailAddressString">A text representation of an e-mail address.</param>
        public static EMailAddress Parse(String EMailAddressString)
        {

            EMailAddressString = EMailAddressString?.Trim();

            if (EMailAddressString.IsNullOrEmpty())
                return null;

            var b      = EMailAddressString.IndexOf('<');
            var c      = EMailAddressString.IndexOf('>');
            var name   = (b >= 0 && c > b)
                             ? EMailAddressString.Remove(b, c - b + 1).Trim()
                             : null;
            var email  = (b >= 0 && c > b)
                             ? EMailAddressString.Substring(b + 1, c - b - 1).Trim()
                             : EMailAddressString.Trim();

            if (name.IsNeitherNullNorEmpty())
                return new EMailAddress(OwnerName:                 name,
                                        SimpleEMailAddressString:  email,
                                        SecretKeyRing:             null,
                                        PublicKeyRing:             null);

            return new EMailAddress(SimpleEMailAddressString:  email,
                                    SecretKeyRing:             null,
                                    PublicKeyRing:             null);

        }

        #endregion

        #region (static) Parse(OwnerName, EMailAddress)

        /// <summary>
        /// Parse the given e-mail address.
        /// </summary>
        /// <param name="OwnerName">The name of the owner of the e-mail address.</param>
        /// <param name="EMailAddress">The text representation of an e-mail address.</param>
        public static EMailAddress Parse(String OwnerName,
                                         String EMailAddress)
        {

            if (OwnerName != null)
                OwnerName = OwnerName.Trim();

            if (EMailAddress != null)
                EMailAddress = EMailAddress.Trim();

            if (OwnerName.IsNullOrEmpty() || EMailAddress.IsNullOrEmpty())
                return null;


            return new EMailAddress(OwnerName:                 OwnerName,
                                    SimpleEMailAddressString:  EMailAddress,
                                    SecretKeyRing:             null,
                                    PublicKeyRing:             null);

        }

        #endregion


        #region ToJSON(Embedded = true)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        public JObject ToJSON(Boolean Embedded = true)

            => JSONObject.Create(

                   !Embedded
                       ? new JProperty("@context", JSONLDContext.ToString())
                       : null,

                   new JProperty("ownerName",  OwnerName),
                   new JProperty("address",    Address.ToString()),

                   SecretKeyRing != null
                       ? new JProperty("secretKeyRing",  null)
                       : null,

                   PublicKeyRing != null
                       ? new JProperty("publicKeyRing",  null)
                       : null

               );

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out EMailAddress, out ErrorResponse)

        public static Boolean TryParseJSON(JObject           JSONObject,
                                           out EMailAddress  EMailAddress,
                                           out String        ErrorResponse,
                                           Boolean           IgnoreContext = false)
        {

            try
            {

                EMailAddress = null;

                #region Parse Context      [mandatory]

                if (IgnoreContext)
                {

                    if (JSONObject.ParseOptional("@context",
                                                 "JSON-LinkedData context information",
                                                 out String Context1,
                                                 out ErrorResponse))
                    {
                        if (Context1 != JSONLDContext)
                        {
                            ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context1 + "' is not supported!";
                            return false;
                        }
                    }

                }

                else
                {
                    if (!JSONObject.ParseMandatoryText("@context",
                                                       "JSON-LinkedData context information",
                                                       out String Context,
                                                       out ErrorResponse))
                    {
                        ErrorResponse = @"The JSON-LD ""@context"" information is missing!";
                        return false;
                    }

                    if (Context != JSONLDContext)
                    {
                        ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context + "' is not supported!";
                        return false;
                    }
                }

                #endregion

                #region Parse OwnerName    [mandatory]

                if (!JSONObject.ParseMandatoryText("ownerName",
                                                   "owner name",
                                                   out String OwnerName,
                                                   out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Address      [mandatory]

                if (!JSONObject.ParseMandatory("address",
                                               "e-mail address",
                                               SimpleEMailAddress.TryParse,
                                               out SimpleEMailAddress Address,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                EMailAddress = new EMailAddress(OwnerName,
                                                Address,
                                                null,
                                                null);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                EMailAddress   = null;
                return false;
            }

        }

        #endregion


        #region Operator overloading

        #region Operator == (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (EMailAddress EMailAddress1, EMailAddress EMailAddress2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(EMailAddress1, EMailAddress2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) EMailAddress1 == null) || ((Object) EMailAddress2 == null))
                return false;

            return EMailAddress1.Equals(EMailAddress2);

        }

        #endregion

        #region Operator != (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (EMailAddress EMailAddress1, EMailAddress EMailAddress2)
            => !(EMailAddress1 == EMailAddress2);

        #endregion

        #region Operator <  (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (EMailAddress EMailAddress1, EMailAddress EMailAddress2)
            => EMailAddress1.CompareTo(EMailAddress2) < 0;

        #endregion

        #region Operator <= (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (EMailAddress EMailAddress1, EMailAddress EMailAddress2)
            => !(EMailAddress1 > EMailAddress2);

        #endregion

        #region Operator >  (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(EMailAddress EMailAddress1, EMailAddress EMailAddress2)
            => EMailAddress1.CompareTo(EMailAddress2) > 0;

        #endregion

        #region Operator >= (EMailAddress1, EMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress1">A EMailAddress.</param>
        /// <param name="EMailAddress2">Another EMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (EMailAddress EMailAddress1, EMailAddress EMailAddress2)
            => !(EMailAddress1 < EMailAddress2);

        #endregion

        #endregion

        #region IComparable<EMailAddress> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is EMailAddress))
                throw new ArgumentException("The given object is not a EMailAddress!", nameof(Object));

            return CompareTo((EMailAddress) Object);

        }

        #endregion

        #region CompareTo(EMailAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="EMailAddress">A EMailAddress to compare with.</param>
        public Int32 CompareTo(EMailAddress EMailAddress)
        {

            if ((Object) EMailAddress == null)
                throw new ArgumentNullException();

            return String.Compare(ToString(), EMailAddress.ToString(), StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<EMailAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            if (!(Object is EMailAddress))
                return false;

            return Equals((EMailAddress) Object);

        }

        #endregion

        #region Equals(EMailAddress)

        /// <summary>
        /// Compares two EMailAddresss for equality.
        /// </summary>
        /// <param name="EMailAddress">A EMailAddress to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(EMailAddress EMailAddress)
        {

            if ((Object) EMailAddress == null)
                return false;

            return ToString().Equals(EMailAddress.ToString());

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => ToString().GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => OwnerName.IsNotNullOrEmpty()
                   ? (OwnerName + " <" + Address + ">").Trim()
                   : Address.ToString();

        #endregion

    }

}
