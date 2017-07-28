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
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A unique e-mail message identification.
    /// </summary>
    public class MessageId : IComparable, IComparable<MessageId>, IEquatable<MessageId>
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
        /// Create a new unique e-mail message identification.
        /// </summary>
        /// <param name="MessageId"></param>
        private MessageId(String MessageId)
        {

            var RegExpr = SimpleEMailAddress.SimpleEMail_RegEx.Match(MessageId.Trim());

            if (!RegExpr.Success)
                throw new ArgumentNullException("Invalid e-mail message identification!");

            this._RandomPart  = RegExpr.Groups[1].Value;
            this._DomainPart  = RegExpr.Groups[2].Value;
            this._Value       = _RandomPart + "@" + _DomainPart;

        }

        #endregion


        #region (static) Parse(MessageIdString)

        /// <summary>
        /// Parse an e-mail message identification from a string.
        /// </summary>
        /// <param name="MessageId">A string representation of an e-mail message identification.</param>
        public static MessageId Parse(String MessageIdString)
        {

            return MessageIdString.IsNotNullOrEmpty()
                      ? new MessageId(MessageIdString)
                      : null;

        }

        #endregion

        #region (static) TryParse(MessageIdString, out MessageId)

        /// <summary>
        /// Try to parse an e-mail message identification from a string.
        /// </summary>
        /// <param name="MessageIdString">A string representation of an e-mail message identification.</param>
        /// <param name="MessageId">The parsed e-mail message identification.</param>
        public static Boolean TryParse(String MessageIdString, out MessageId MessageId)
        {

            MessageId = null;

            if (MessageIdString == null)
                return false;

            if (SimpleEMailAddress.SimpleEMail_RegEx.Match(MessageIdString.Trim()).Success)
            {
                MessageId = new MessageId(MessageIdString);
                return true;
            }

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (MessageId MessageId1, MessageId MessageId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(MessageId1, MessageId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) MessageId1 == null) || ((Object) MessageId2 == null))
                return false;

            return MessageId1.Equals(MessageId2);

        }

        #endregion

        #region Operator != (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (MessageId MessageId1, MessageId MessageId2)
        {
            return !(MessageId1 == MessageId2);
        }

        #endregion

        #region Operator <  (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (MessageId MessageId1, MessageId MessageId2)
        {
            return MessageId1._Value.CompareTo(MessageId2._Value) < 0;
        }

        #endregion

        #region Operator <= (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (MessageId MessageId1, MessageId MessageId2)
        {
            return !(MessageId1 > MessageId2);
        }

        #endregion

        #region Operator >  (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(MessageId MessageId1, MessageId MessageId2)
        {
            return MessageId1._Value.CompareTo(MessageId2._Value) > 0;
        }

        #endregion

        #region Operator >= (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A MessageId.</param>
        /// <param name="MessageId2">Another MessageId.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (MessageId MessageId1, MessageId MessageId2)
        {
            return !(MessageId1 < MessageId2);
        }

        #endregion

        #endregion

        #region IComparable<MessageId> Member

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is a MessageId.
            var _MessageId = Object as MessageId;
            if ((Object) _MessageId == null)
                throw new ArgumentException("The given object is not a MessageId!");

            return (this._Value).CompareTo(_MessageId._Value);

        }

        #endregion

        #region CompareTo(MessageId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId">A MessageId to compare with.</param>
        public Int32 CompareTo(MessageId MessageId)
        {

            if ((Object) MessageId == null)
                throw new ArgumentNullException();

            return (this._Value).CompareTo(MessageId._Value);

        }

        #endregion

        #endregion

        #region IEquatable<MessageId> Members

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

            // Check if the given object is a MessageId.
            var _MessageId = Object as MessageId;
            if ((Object) _MessageId == null)
                return false;

            return Equals(_MessageId);

        }

        #endregion

        #region Equals(MessageId)

        /// <summary>
        /// Compares two MessageIds for equality.
        /// </summary>
        /// <param name="MessageId">A MessageId to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(MessageId MessageId)
        {

            if ((Object) MessageId == null)
                return false;

            return this._Value.Equals(MessageId._Value);

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
