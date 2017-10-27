/*
 * Copyright (c) 2010-2017, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A simple e-mail address.
    /// </summary>
    public struct SimpleEMailAddress : IId,
                                       IComparable<SimpleEMailAddress>,
                                       IEquatable<SimpleEMailAddress>
    {

        #region Data

        /// <summary>
        /// A regular expression to validate a simple e-mail address.
        /// </summary>
        public static readonly Regex SimpleEMail_RegEx = new Regex("^<?([^@]+)@([^@\\>]+)>?$",
                                                                   RegexOptions.IgnorePatternWhitespace);

        #endregion

        #region Properties

        /// <summary>
        /// The user of a simple e-mail address.
        /// </summary>
        public String  User     { get; }

        /// <summary>
        /// The domain of a simple e-mail address.
        /// </summary>
        public String  Domain   { get; }

        /// <summary>
        /// The string value of a simple e-mail address.
        /// </summary>
        public String  Value
            => User + "@" + Domain;

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

            #region Initial checks

            if (User.IsNullOrEmpty() || User.Trim().IsNullOrEmpty())
                throw new ArgumentNullException(nameof(User),    "The given user part of an email address must not be null or empty!");

            if (Domain.IsNullOrEmpty() || Domain.Trim().IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Domain),  "The given domain part of an email address must not be null or empty!");

            #endregion

            this.User    = User;
            this.Domain  = Domain;

        }

        #endregion


        #region (static) Parse(Text)

        /// <summary>
        /// Parse the given string as an e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        public static SimpleEMailAddress Parse(String Text)
        {

            #region Initial checks

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of an email address must not be null or empty!");

            #endregion

            var MatchCollection = SimpleEMail_RegEx.Matches(Text.Trim());

            if (MatchCollection.Count != 1)
                throw new ArgumentException("Illegal email address '" + Text + "'!",
                                            nameof(Text));

            return new SimpleEMailAddress(MatchCollection[0].Groups[1].Value,
                                          MatchCollection[0].Groups[2].Value);

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        public static SimpleEMailAddress? TryParse(String Text)
        {

            SimpleEMailAddress _SimpleEMailAddress;

            if (TryParse(Text, out _SimpleEMailAddress))
                return _SimpleEMailAddress;

            return new SimpleEMailAddress?();

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

            if (Text.IsNullOrEmpty())
            {
                EMailAddress = default(SimpleEMailAddress);
                return false;
            }

            #endregion

            try
            {

                var MatchCollection = SimpleEMail_RegEx.Matches(Text.Trim().ToUpper());

                if (MatchCollection.Count == 1)
                {

                    EMailAddress = new SimpleEMailAddress(MatchCollection[0].Groups[0].Value,
                                                          MatchCollection[0].Groups[1].Value);

                    return true;

                }

            }
#pragma warning disable RCS1075  // Avoid empty catch clause that catches System.Exception.
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
            { }
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning restore RCS1075  // Avoid empty catch clause that catches System.Exception.

            EMailAddress = default(SimpleEMailAddress);
            return false;

        }

        #endregion

        #region (static) IsValid(Text)

        /// <summary>
        /// Checks if the given string is a valid e-mail address.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail address.</param>
        public static Boolean IsValid(String Text)

            => SimpleEMail_RegEx.
                   Match(Text.Trim()).
                   Success;

        #endregion


        #region Operator overloading

        #region Operator == (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(SimpleEMailAddress1, SimpleEMailAddress2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) SimpleEMailAddress1 == null) || ((Object) SimpleEMailAddress2 == null))
                return false;

            return SimpleEMailAddress1.Equals(SimpleEMailAddress2);

        }

        #endregion

        #region Operator != (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
            => !(SimpleEMailAddress1 == SimpleEMailAddress2);

        #endregion

        #region Operator <  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) < 0;

        #endregion

        #region Operator <= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
            => !(SimpleEMailAddress1 > SimpleEMailAddress2);

        #endregion

        #region Operator >  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
            => SimpleEMailAddress1.Value.CompareTo(SimpleEMailAddress2.Value) > 0;

        #endregion

        #region Operator >= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
            => !(SimpleEMailAddress1 < SimpleEMailAddress2);

        #endregion

        #endregion

        #region IComparable<SimpleEMailAddress> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is SimpleEMailAddress))
                throw new ArgumentException("The given object is not a SimpleEMailAddress!", nameof(Object));

            return CompareTo((SimpleEMailAddress) Object);

        }

        #endregion

        #region CompareTo(SimpleEMailAddress)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress">A SimpleEMailAddress to compare with.</param>
        public Int32 CompareTo(SimpleEMailAddress SimpleEMailAddress)
        {

            if ((Object) SimpleEMailAddress == null)
                throw new ArgumentNullException();

            return String.Compare(Value, SimpleEMailAddress.Value, StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<SimpleEMailAddress> Members

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

            if (!(Object is SimpleEMailAddress))
                return false;

            return Equals((SimpleEMailAddress) Object);

        }

        #endregion

        #region Equals(SimpleEMailAddress)

        /// <summary>
        /// Compares two SimpleEMailAddresss for equality.
        /// </summary>
        /// <param name="SimpleEMailAddress">A SimpleEMailAddress to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(SimpleEMailAddress SimpleEMailAddress)
        {

            if ((Object) SimpleEMailAddress == null)
                return false;

            return Value.Equals(SimpleEMailAddress.Value);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => Value.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
            => Value;

        #endregion

    }

}
