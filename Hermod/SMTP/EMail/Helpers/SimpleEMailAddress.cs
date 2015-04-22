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
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A simple e-mail address.
    /// </summary>
    public class SimpleEMailAddress : IComparable, IComparable<SimpleEMailAddress>, IEquatable<SimpleEMailAddress>
    {

        #region Public constants

        /// <summary>
        /// A regular expression to validate a simple e-mail address.
        /// </summary>
        public static readonly Regex EMailRegularExpression = new Regex("^([^@]+)@([^@]+)$");

        #endregion

        #region Properties

        #region User

        private readonly String _User;

        /// <summary>
        /// The user of a simple e-mail address.
        /// </summary>
        public String User
        {
            get
            {
                return _User;
            }
        }

        #endregion

        #region Domain

        private readonly String _Domain;

        /// <summary>
        /// The domain of a simple e-mail address.
        /// </summary>
        public String Domain
        {
            get
            {
                return _Domain;
            }
        }

        #endregion

        #region Value

        private readonly String _Value;

        /// <summary>
        /// The string value of a simple e-mail address.
        /// </summary>
        public String Value
        {
            get
            {
                return _Value;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new simple e-mail address.
        /// </summary>
        /// <param name="EMailAddress">A string representation of a simple e-mail address.</param>
        private SimpleEMailAddress(String EMailAddress)
        {

            var RegExpr = EMailRegularExpression.Match(EMailAddress.Trim());

            if (!RegExpr.Success)
                throw new ArgumentNullException("Invalid e-mail address!");

            this._User    = RegExpr.Groups[1].Value;
            this._Domain  = RegExpr.Groups[2].Value.ToLower();
            this._Value   = _User + "@" + _Domain;

        }

        #endregion


        #region (static) Parse(EMailAddress)

        /// <summary>
        /// Parse a simple e-mail address from a string.
        /// </summary>
        /// <param name="EMailAddress">A string representation of a simple e-mail address.</param>
        public static SimpleEMailAddress Parse(String EMailAddress)
        {
            return new SimpleEMailAddress(EMailAddress);
        }

        #endregion

        #region (static) TryParse(EMailAddress, out EMailAddress)

        /// <summary>
        /// Try to parse a simple e-mail address from a string.
        /// </summary>
        /// <param name="EMailAddressString">A string representation of a simple e-mail address.</param>
        /// <param name="EMailAddress">The parsed e-mail address.</param>
        public static Boolean TryParse(String EMailAddressString, out SimpleEMailAddress EMailAddress)
        {

            if (EMailRegularExpression.Match(EMailAddressString.Trim()).Success)
            {
                EMailAddress = new SimpleEMailAddress(EMailAddressString);
                return true;
            }

            EMailAddress = null;
            return false;

        }

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
        {
            return !(SimpleEMailAddress1 == SimpleEMailAddress2);
        }

        #endregion

        #region Operator <  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
        {
            return SimpleEMailAddress1._Value.CompareTo(SimpleEMailAddress2._Value) < 0;
        }

        #endregion

        #region Operator <= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
        {
            return !(SimpleEMailAddress1 > SimpleEMailAddress2);
        }

        #endregion

        #region Operator >  (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
        {
            return SimpleEMailAddress1._Value.CompareTo(SimpleEMailAddress2._Value) > 0;
        }

        #endregion

        #region Operator >= (SimpleEMailAddress1, SimpleEMailAddress2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SimpleEMailAddress1">A SimpleEMailAddress.</param>
        /// <param name="SimpleEMailAddress2">Another SimpleEMailAddress.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SimpleEMailAddress SimpleEMailAddress1, SimpleEMailAddress SimpleEMailAddress2)
        {
            return !(SimpleEMailAddress1 < SimpleEMailAddress2);
        }

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
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is a SimpleEMailAddress.
            var _SimpleEMailAddress = Object as SimpleEMailAddress;
            if ((Object) _SimpleEMailAddress == null)
                throw new ArgumentException("The given object is not a SimpleEMailAddress!");

            return (this._Value).CompareTo(_SimpleEMailAddress._Value);

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

            return (this._Value).CompareTo(SimpleEMailAddress._Value);

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

            // Check if the given object is a SimpleEMailAddress.
            var _SimpleEMailAddress = Object as SimpleEMailAddress;
            if ((Object) _SimpleEMailAddress == null)
                return false;

            return Equals(_SimpleEMailAddress);

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

            return this._Value.Equals(SimpleEMailAddress._Value);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return _Value.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return _Value;
        }

        #endregion

    }

}
