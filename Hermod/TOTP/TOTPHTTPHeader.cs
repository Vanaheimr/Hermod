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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using Org.BouncyCastle.Crypto.Engines;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public enum TOTPHTTPHeaderType
    {

        /// <summary>
        /// Use the raw TOTP value.
        /// </summary>
        RAW                = 0,

        /// <summary>
        /// Use the TOTP value in combination with TLS v1.3 channel binding.
        /// </summary>
        TLSChannelBinding  = 1

    }


    /// <summary>
    /// A HTTP header for Time-based One-Time Passwords (TOTPs).
    /// </summary>
    /// 
    public class TOTPHTTPHeader
    {

        #region Properties

        /// <summary>
        /// The TOTP HTTP header type.
        /// </summary>
        public TOTPHTTPHeaderType  Type     { get; }

        /// <summary>
        /// The TOTP value.
        /// </summary>
        public String              Value    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TOTP HTTP header.
        /// </summary>
        /// <param name="Type">The TOTP HTTP header type.</param>
        /// <param name="Value">The TOTP value.</param>
        public TOTPHTTPHeader(TOTPHTTPHeaderType  Type,
                              String              Value)
        {

            this.Type   = Type;
            this.Value  = Value;

            unchecked
            {
                this.hashCode = this.Type. GetHashCode() * 3 ^
                                this.Value.GetHashCode();
            }

        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a TOTP HTTP header.
        /// </summary>
        /// <param name="Text">The text to parse.</param>
        public static TOTPHTTPHeader Parse(String Text)
        {
            if (TryParse(Text,
                         out var totpHTTPHeader,
                         out var errorResponse))
            {
                return totpHTTPHeader;
            }

            throw new ArgumentException("The given text representation of a TOTP HTTP header is invalid: " + errorResponse,
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text, out TOTPHTTPHeader, out ErrorResponse)

        /// <summary>
        /// Try to parse the given text representation of a TOTP HTTP header.
        /// </summary>
        /// <param name="Text">The text to parse.</param>
        /// <param name="TOTPHTTPHeader">The parsed TOTP HTTP header.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(String                                    Text,
                                       [NotNullWhen(true)]  out TOTPHTTPHeader?  TOTPHTTPHeader,
                                       [NotNullWhen(false)] out String?          ErrorResponse)
        {

            try
            {

                TOTPHTTPHeader = default;

                Text = Text.Trim();

                if (Text.IsNullOrEmpty())
                {
                    ErrorResponse = "The given Text object must not be null or empty!";
                    return false;
                }

                if (Text.Length < 3)
                {
                    ErrorResponse = "The given Text object is too short to be a valid TOTP HTTP header!";
                    return false;
                }

                #region Parse Type        [mandatory]

                if (!Byte.TryParse(Text[0].ToString(), out var typeByte))
                {
                    ErrorResponse = "The given Text representation of a TOTP HTTP header is invalid: The type info is not valid!";
                    return false;
                }

                var type = TOTPHTTPHeaderType.RAW;

                switch (typeByte)
                {

                    case 0: type = TOTPHTTPHeaderType.RAW;                 break;
                    case 1: type = TOTPHTTPHeaderType.TLSChannelBinding;   break;

                    default:
                    {
                        ErrorResponse = $"The given Text representation of a TOTP HTTP header is invalid: The type info '{typeByte}' is not valid!";
                        return false;
                    }

                }

                #endregion

                #region Validate space    [mandatory]

                if (Text[1] != ' ')
                {
                    ErrorResponse = "The given Text representation of a TOTP HTTP header is invalid: Missing space after type info!";
                    return false;
                }

                #endregion

                #region Parse Value       [mandatory]

                var value = Text[2..].Trim();

                if (value.IsNullOrEmpty())
                {
                    ErrorResponse = "The given Text representation of a TOTP HTTP header is invalid: The TOTP value is missing!";
                    return false;
                }

                #endregion


                ErrorResponse   = null;
                TOTPHTTPHeader  = new TOTPHTTPHeader(
                                      type,
                                      value
                                  );

                return true;

            }
            catch (Exception e)
            {
                TOTPHTTPHeader  = default;
                ErrorResponse   = "The given Text representation of a TOTP HTTP header is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this TOTP HTTP header.
        /// </summary>
        public TOTPHTTPHeader Clone()

            => new (
                   Type,
                   Value.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (TOTPHTTPHeader TOTPHTTPHeader1,
                                           TOTPHTTPHeader TOTPHTTPHeader2)
        {

            if (Object.ReferenceEquals(TOTPHTTPHeader1, TOTPHTTPHeader2))
                return true;

            if ((TOTPHTTPHeader1 is null) || (TOTPHTTPHeader2 is null))
                return false;

            return TOTPHTTPHeader1.Equals(TOTPHTTPHeader2);

        }

        #endregion

        #region Operator != (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (TOTPHTTPHeader TOTPHTTPHeader1,
                                           TOTPHTTPHeader TOTPHTTPHeader2)

            => !(TOTPHTTPHeader1 == TOTPHTTPHeader2);

        #endregion

        #region Operator <  (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (TOTPHTTPHeader TOTPHTTPHeader1,
                                          TOTPHTTPHeader TOTPHTTPHeader2)

            => TOTPHTTPHeader1 is null
                   ? throw new ArgumentNullException(nameof(TOTPHTTPHeader1), "The given TOTP HTTP header must not be null!")
                   : TOTPHTTPHeader1.CompareTo(TOTPHTTPHeader2) < 0;

        #endregion

        #region Operator <= (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (TOTPHTTPHeader TOTPHTTPHeader1,
                                           TOTPHTTPHeader TOTPHTTPHeader2)

            => !(TOTPHTTPHeader1 > TOTPHTTPHeader2);

        #endregion

        #region Operator >  (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (TOTPHTTPHeader TOTPHTTPHeader1,
                                          TOTPHTTPHeader TOTPHTTPHeader2)

            => TOTPHTTPHeader1 is null
                   ? throw new ArgumentNullException(nameof(TOTPHTTPHeader1), "The given TOTP HTTP header must not be null!")
                   : TOTPHTTPHeader1.CompareTo(TOTPHTTPHeader2) > 0;

        #endregion

        #region Operator >= (TOTPHTTPHeader1, TOTPHTTPHeader2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPHTTPHeader1">A TOTP HTTP header.</param>
        /// <param name="TOTPHTTPHeader2">Another TOTP HTTP header.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (TOTPHTTPHeader TOTPHTTPHeader1,
                                           TOTPHTTPHeader TOTPHTTPHeader2)

            => !(TOTPHTTPHeader1 < TOTPHTTPHeader2);

        #endregion

        #endregion

        #region IComparable<TOTPHTTPHeader> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two TOTP HTTP header.
        /// </summary>
        /// <param name="Object">A TOTP HTTP header to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is TOTPHTTPHeader totpHTTPHeader
                   ? CompareTo(totpHTTPHeader)
                   : throw new ArgumentException("The given object is not a TOTP HTTP header!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(TOTPHTTPHeader)

        /// <summary>
        /// Compares two TOTP HTTP header.
        /// </summary>
        /// <param name="TOTPHTTPHeader">A TOTP HTTP header to compare with.</param>
        public Int32 CompareTo(TOTPHTTPHeader? TOTPHTTPHeader)
        {

            if (TOTPHTTPHeader is null)
                throw new ArgumentNullException(nameof(TOTPHTTPHeader), "The given TOTP HTTP header must not be null!");

            var c = Type. CompareTo(TOTPHTTPHeader.Type);

            if (c == 0)
                c = Value.CompareTo(TOTPHTTPHeader.Value);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<TOTPHTTPHeader> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two TOTP HTTP header for equality.
        /// </summary>
        /// <param name="Object">A TOTP HTTP header to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is TOTPHTTPHeader totpHTTPHeader &&
                   Equals(totpHTTPHeader);

        #endregion

        #region Equals(TOTPHTTPHeader)

        /// <summary>
        /// Compares two TOTP HTTP header for equality.
        /// </summary>
        /// <param name="TOTPHTTPHeader">A TOTP HTTP header to compare with.</param>
        public Boolean Equals(TOTPHTTPHeader? TOTPHTTPHeader)

            => TOTPHTTPHeader is not null &&

               Type. Equals(TOTPHTTPHeader.Type) &&
               Value.Equals(TOTPHTTPHeader.Value);

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{(Byte) Type} {Value}";

        #endregion

    }

}
