/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Extension methods for simple e-mail addresses.
    /// </summary>
    public static class SimpleEMailAddressExtensions
    {

        /// <summary>
        /// Indicates whether this simple e-mail address is null or empty.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        public static Boolean IsNullOrEmpty(this SimpleEMailAddress? SimpleEMailAddress)
            => !SimpleEMailAddress.HasValue || SimpleEMailAddress.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this simple e-mail address is null or empty.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address.</param>
        public static Boolean IsNotNullOrEmpty(this SimpleEMailAddress? SimpleEMailAddress)
            => SimpleEMailAddress.HasValue && SimpleEMailAddress.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A simple e-mail address.
    /// </summary>
    public readonly struct SimpleEMailAddress : IId,
                                                IComparable<SimpleEMailAddress>,
                                                IEquatable<SimpleEMailAddress>
    {

        #region Data

        /// <summary>
        /// A regular expression to validate a simple e-mail address.
        /// </summary>
        public static readonly Regex SimpleEMail_RegEx = new ("^<?([^@]+)@([^@\\>]+)>?$",
                                                              RegexOptions.IgnorePatternWhitespace);

        #endregion

        #region Properties

        /// <summary>
        /// The user of a simple e-mail address.
        /// </summary>
        public String  User      { get; }

        /// <summary>
        /// The domain of a simple e-mail address.
        /// </summary>
        public String  Domain    { get; }

        /// <summary>
        /// Indicates whether this e-mail address is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => User.IsNullOrEmpty()    || Domain.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this e-mail address is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => User.IsNotNullOrEmpty() && Domain.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the tag identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) (User.Length + 1 + Domain.Length);

        /// <summary>
        /// The string value of a simple e-mail address.
        /// </summary>
        public String  Value
            => $"{User}@{Domain}";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new simple e-mail address.
        /// </summary>
        /// <param name="User">The user part of an email address.</param>
        /// <param name="Domain">The domain part of an emaul address.</param>
        private SimpleEMailAddress(String  User,
                                   String  Domain)
        {

            this.User    = User;
            this.Domain  = Domain;

        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        public static SimpleEMailAddress Parse(String Text)
        {

            if (TryParse(Text, out var simpleEMailAddress))
                return simpleEMailAddress;

            throw new ArgumentException($"Invalid text representation of an e-mail address: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        public static SimpleEMailAddress? TryParse(String Text)
        {

            if (TryParse(Text, out var simpleEMailAddress))
                return simpleEMailAddress;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out EMailAddress)

        /// <summary>
        /// Try to parse the given string as an e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        /// <param name="EMailAddress">The parsed e-mail address.</param>
        public static Boolean TryParse(String Text, out SimpleEMailAddress EMailAddress)
        {

            #region Initial checks

            if (Text.IsNullOrEmpty() || !Text.Contains('@'))
            {
                EMailAddress = default;
                return false;
            }

            #endregion

            try
            {

                var matchCollection = SimpleEMail_RegEx.Matches(Text.Trim());

                if (matchCollection.Count == 1 &&
                    matchCollection[0].Groups[1].Value.IsNotNullOrEmpty() &&
                    matchCollection[0].Groups[2].Value.IsNotNullOrEmpty())
                {

                    EMailAddress = new SimpleEMailAddress(
                                       matchCollection[0].Groups[1].Value,
                                       matchCollection[0].Groups[2].Value
                                   );

                    return true;

                }

            }
            catch
            { }

            EMailAddress = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this simple e-mail address.
        /// </summary>
        public SimpleEMailAddress Clone()

            => new (
                   User.  CloneString(),
                   Domain.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SimpleEMailAddress SimpleEMailAddress1,
                                           SimpleEMailAddress SimpleEMailAddress2)

            => SimpleEMailAddress1.Equals(SimpleEMailAddress2);

        #endregion

        #region Operator != (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SimpleEMailAddress SimpleEMailAddress1,
                                           SimpleEMailAddress SimpleEMailAddress2)

            => !SimpleEMailAddress1.Equals(SimpleEMailAddress2);

        #endregion

        #region Operator <  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SimpleEMailAddress SimpleEMailAddress1,
                                          SimpleEMailAddress SimpleEMailAddress2)

            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) < 0;

        #endregion

        #region Operator <= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SimpleEMailAddress SimpleEMailAddress1,
                                           SimpleEMailAddress SimpleEMailAddress2)

            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) <= 0;

        #endregion

        #region Operator >  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (SimpleEMailAddress SimpleEMailAddress1,
                                          SimpleEMailAddress SimpleEMailAddress2)

            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) > 0;

        #endregion

        #region Operator >= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A simple e-mail address.</param>
        /// <param name="SimpleEMailAddress2">Another simple e-mail address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SimpleEMailAddress SimpleEMailAddress1,
                                           SimpleEMailAddress SimpleEMailAddress2)

            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) >= 0;

        #endregion

        #endregion

        #region IComparable<SimpleEMailAddress> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two simple e-mail addresses.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is SimpleEMailAddress simpleEMailAddress
                   ? CompareTo(simpleEMailAddress)
                   : throw new ArgumentException("The given object is not a simple e-mail address!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(SimpleEMailAddress)

        /// <summary>
        /// Compares two simple e-mail addresses.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address to compare with.</param>
        public Int32 CompareTo(SimpleEMailAddress SimpleEMailAddress)

            => String.Compare(Value,
                              SimpleEMailAddress.Value,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<SimpleEMailAddress> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two simple e-mail addresses for equality.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SimpleEMailAddress simpleEMailAddress &&
                   Equals(simpleEMailAddress);

        #endregion

        #region Equals(SimpleEMailAddress)

        /// <summary>
        /// Compares two simple e-mail addresses for equality.
        /// </summary>
        /// <param name="SimpleEMailAddress">A simple e-mail address to compare with.</param>
        public Boolean Equals(SimpleEMailAddress SimpleEMailAddress)

            => Value?.ToLower().Equals(SimpleEMailAddress.Value?.ToLower() ?? "") ?? false;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Value?.GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => Value ?? "n/a";

        #endregion

    }

}
