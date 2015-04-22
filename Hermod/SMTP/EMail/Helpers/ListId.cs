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
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A unique mailinglist identification.
    /// </summary>
    public class ListId : IComparable, IComparable<ListId>, IEquatable<ListId>
    {

        #region Properties

        #region RandomPart

        private readonly String _RandomPart;

        /// <summary>
        /// The random part of an e-mail message identification.
        /// </summary>
        public String RandomPart
        {
            get
            {
                return _RandomPart;
            }
        }

        #endregion

        #region DomainPart

        private readonly String _DomainPart;

        /// <summary>
        /// The domain part of an e-mail message identification.
        /// </summary>
        public String DomainPart
        {
            get
            {
                return _DomainPart;
            }
        }

        #endregion

        #region Value

        private readonly String _Value;

        /// <summary>
        /// The string value of an e-mail message identification.
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
        /// Create a new unique mailinglist identification.
        /// </summary>
        /// <param name="ListId"></param>
        private ListId(String ListId)
        {

            var RegExpr = SimpleEMailAddress.EMailRegularExpression.Match(ListId.Trim());

            if (!RegExpr.Success)
                throw new ArgumentNullException("Invalid e-mail message identification!");

            this._RandomPart  = RegExpr.Groups[1].Value;
            this._DomainPart  = RegExpr.Groups[2].Value;
            this._Value       = _RandomPart + "@" + _DomainPart;

        }

        #endregion


        #region (static) Parse(EMailAddress)

        /// <summary>
        /// Parse an e-mail message identification from a string.
        /// </summary>
        /// <param name="EMailAddress">A string representation of a simple e-mail address.</param>
        public static ListId Parse(String ListId)
        {
            return new ListId(ListId);
        }

        #endregion

        #region (static) TryParse(ListIdString, out ListId)

        /// <summary>
        /// Try to parse an e-mail message identification from a string.
        /// </summary>
        /// <param name="ListIdString">A string representation of a simple e-mail address.</param>
        /// <param name="ListId">The parsed e-mail message identification.</param>
        public static Boolean TryParse(String ListIdString, out ListId ListId)
        {

            if (SimpleEMailAddress.EMailRegularExpression.Match(ListIdString.Trim()).Success)
            {
                ListId = new ListId(ListIdString);
                return true;
            }

            ListId = null;
            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ListId ListId1, ListId ListId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(ListId1, ListId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) ListId1 == null) || ((Object) ListId2 == null))
                return false;

            return ListId1.Equals(ListId2);

        }

        #endregion

        #region Operator != (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 == ListId2);
        }

        #endregion

        #region Operator <  (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ListId ListId1, ListId ListId2)
        {
            return ListId1._Value.CompareTo(ListId2._Value) < 0;
        }

        #endregion

        #region Operator <= (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 > ListId2);
        }

        #endregion

        #region Operator >  (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(ListId ListId1, ListId ListId2)
        {
            return ListId1._Value.CompareTo(ListId2._Value) > 0;
        }

        #endregion

        #region Operator >= (ListId1, ListId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId1">A ListId.</param>
        /// <param name="ListId2">Another ListId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ListId ListId1, ListId ListId2)
        {
            return !(ListId1 < ListId2);
        }

        #endregion

        #endregion

        #region IComparable<ListId> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is a ListId.
            var _ListId = Object as ListId;
            if ((Object) _ListId == null)
                throw new ArgumentException("The given object is not a ListId!");

            return (this._Value).CompareTo(_ListId._Value);

        }

        #endregion

        #region CompareTo(ListId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ListId">A ListId to compare with.</param>
        public Int32 CompareTo(ListId ListId)
        {

            if ((Object) ListId == null)
                throw new ArgumentNullException();

            return (this._Value).CompareTo(ListId._Value);

        }

        #endregion

        #endregion

        #region IEquatable<ListId> Members

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

            // Check if the given object is a ListId.
            var _ListId = Object as ListId;
            if ((Object) _ListId == null)
                return false;

            return Equals(_ListId);

        }

        #endregion

        #region Equals(ListId)

        /// <summary>
        /// Compares two ListIds for equality.
        /// </summary>
        /// <param name="ListId">A ListId to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(ListId ListId)
        {

            if ((Object) ListId == null)
                return false;

            return this._Value.Equals(ListId._Value);

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
        /// Returns a formated string representation of this object.
        /// </summary>
        /// <returns>A formated string representation of this object.</returns>
        public override String ToString()
        {
            return _RandomPart + "@" + _DomainPart;
        }

        #endregion

    }

}
