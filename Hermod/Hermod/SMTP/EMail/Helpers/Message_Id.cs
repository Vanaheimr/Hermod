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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A unique identification of an e-mail message.
    /// </summary>
    public readonly struct Message_Id : IId,
                                        IEquatable<Message_Id>,
                                        IComparable<Message_Id>
    {

        #region Properties

        /// <summary>
        /// The random part of an e-mail message identification.
        /// </summary>
        public String RandomPart    { get; }

        /// <summary>
        /// The domain part of an e-mail message identification.
        /// </summary>
        public String DomainPart    { get; }

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => RandomPart.IsNullOrEmpty() ||
               DomainPart.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => RandomPart.IsNotNullOrEmpty() &&
               DomainPart.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the e-mail message identifier.
        /// </summary>
        public UInt64 Length
            => (UInt64) (RandomPart.Length + 1 + DomainPart.Length);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique e-mail message identification.
        /// </summary>
        /// <param name="RandomPart">The random part of an e-mail message identification.</param>
        /// <param name="DomainPart">The domain part of an e-mail message identification.</param>
        private Message_Id(String  RandomPart,
                           String  DomainPart)
        {

            this.RandomPart  = RandomPart;
            this.DomainPart  = DomainPart;

        }

        #endregion


        #region (static) Random(DomainPart, Length = 30)

        public static Message_Id Random(String  DomainPart,
                                        Byte    Length = 30)

            => new (RandomExtensions.RandomString(Length),
                    DomainPart);

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail message identification.</param>
        public static Message_Id Parse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of an e-mail message identification must not be null or empty!");

            #endregion

            if (TryParse(Text, out Message_Id MessageId))
                return MessageId;

            throw new ArgumentException(nameof(Text), "The given text representation of an e-mail message identification is invalid!");

        }

        #endregion

        #region (static) Parse   (RandomPart, DomainPart)

        /// <summary>
        /// Parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="RandomPart">The random part of an e-mail message identification.</param>
        /// <param name="DomainPart">The domain part of an e-mail message identification.</param>
        public static Message_Id Parse(String  RandomPart,
                                       String  DomainPart)
        {

            #region Initial checks

            if (RandomPart != null)
                RandomPart = RandomPart.Trim();

            if (RandomPart.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(RandomPart), "The given random part of an e-mail message identification must not be null or empty!");


            if (DomainPart != null)
                DomainPart = DomainPart.Trim();

            if (DomainPart.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(RandomPart), "The given domain part of an e-mail message identification must not be null or empty!");

            #endregion

            if (TryParse(RandomPart, DomainPart, out Message_Id MessageId))
                return MessageId;

            throw new ArgumentException("The given e-mail message identification is invalid!");

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail message identification.</param>
        public static Message_Id? TryParse(String Text)
        {

            if (TryParse(Text, out Message_Id MessageId))
                return MessageId;

            return new Message_Id?();

        }

        #endregion

        #region (static) TryParse(RandomPart, DomainPart)

        /// <summary>
        /// Try to parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="RandomPart">The random part of an e-mail message identification.</param>
        /// <param name="DomainPart">The domain part of an e-mail message identification.</param>
        public static Message_Id? TryParse(String RandomPart, String DomainPart)
        {

            if (TryParse(RandomPart, DomainPart, out Message_Id MessageId))
                return MessageId;

            return new Message_Id?();

        }

        #endregion

        #region (static) TryParse(Text,                   out MessageId)

        /// <summary>
        /// Try to parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="Text">A text representation of an e-mail message identification.</param>
        /// <param name="MessageId">The parsed e-mail message identification.</param>
        public static Boolean TryParse(String Text, out Message_Id MessageId)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                MessageId = default;
                return false;
            }

            #endregion

            try
            {

                var RegExpr = SimpleEMailAddress.SimpleEMail_RegEx.Match(Text);

                if (RegExpr.Success)
                {

                    MessageId = new Message_Id(RegExpr.Groups[1].Value,
                                               RegExpr.Groups[2].Value);

                    return true;

                }

            }
            catch
            { }

            MessageId = default;
            return false;

        }

        #endregion

        #region (static) TryParse(RandomPart, DomainPart, out MessageId)

        /// <summary>
        /// Try to parse the given string as an e-mail message identification.
        /// </summary>
        /// <param name="RandomPart">The random part of an e-mail message identification.</param>
        /// <param name="DomainPart">The domain part of an e-mail message identification.</param>
        /// <param name="MessageId">The parsed e-mail message identification.</param>
        public static Boolean TryParse(String RandomPart, String DomainPart, out Message_Id MessageId)
        {

            #region Initial checks

            if (RandomPart != null)
                RandomPart = RandomPart.Trim();

            if (DomainPart != null)
                DomainPart = DomainPart.Trim();

            if (RandomPart.IsNullOrEmpty() || DomainPart.IsNullOrEmpty())
            {
                MessageId = default;
                return false;
            }

            #endregion

            try
            {

                var RegExpr = SimpleEMailAddress.SimpleEMail_RegEx.Match(RandomPart + "@" + DomainPart);

                if (RegExpr.Success)
                {

                    MessageId = new Message_Id(RandomPart,
                                               DomainPart);

                    return true;

                }

            }
            catch
            { }

            MessageId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this e-mail message identification.
        /// </summary>
        public Message_Id Clone()

            => new (
                   RandomPart.CloneString(),
                   DomainPart.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Message_Id MessageId1, Message_Id MessageId2)
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
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Message_Id MessageId1, Message_Id MessageId2)
            => !(MessageId1 == MessageId2);

        #endregion

        #region Operator <  (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Message_Id MessageId1, Message_Id MessageId2)
        {

            if ((Object) MessageId1 == null)
                throw new ArgumentNullException(nameof(MessageId1), "The given MessageId1 must not be null!");

            return MessageId1.CompareTo(MessageId2) < 0;

        }

        #endregion

        #region Operator <= (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Message_Id MessageId1, Message_Id MessageId2)
            => !(MessageId1 > MessageId2);

        #endregion

        #region Operator >  (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Message_Id MessageId1, Message_Id MessageId2)
        {

            if ((Object) MessageId1 == null)
                throw new ArgumentNullException(nameof(MessageId1), "The given MessageId1 must not be null!");

            return MessageId1.CompareTo(MessageId2) > 0;

        }

        #endregion

        #region Operator >= (MessageId1, MessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId1">A message identification.</param>
        /// <param name="MessageId2">Another message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Message_Id MessageId1, Message_Id MessageId2)
            => !(MessageId1 < MessageId2);

        #endregion

        #endregion

        #region IComparable<MessageId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object is null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is Message_Id MessageId))
                throw new ArgumentException("The given object is not a message identification!",
                                            nameof(Object));

            return CompareTo(MessageId);

        }

        #endregion

        #region CompareTo(MessageId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageId">An object to compare with.</param>
        public Int32 CompareTo(Message_Id MessageId)
        {

            if ((Object) MessageId == null)
                throw new ArgumentNullException(nameof(MessageId),  "The given message identification must not be null!");

            return String.Compare(ToString(), MessageId.ToString(), StringComparison.OrdinalIgnoreCase);

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

            if (!(Object is Message_Id MessageId))
                return false;

            return Equals(MessageId);

        }

        #endregion

        #region Equals(MessageId)

        /// <summary>
        /// Compares two MessageIds for equality.
        /// </summary>
        /// <param name="MessageId">A message identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Message_Id MessageId)
        {

            if (MessageId == null)
                return false;

            return RandomPart.ToLower().Equals(MessageId.RandomPart.ToLower()) &&
                   DomainPart.ToLower().Equals(MessageId.DomainPart.ToLower());

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => RandomPart.GetHashCode() ^
               DomainPart.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a formatted string representation of this object.
        /// </summary>
        /// <returns>A formatted string representation of this object.</returns>
        public override String ToString()

            => String.Concat(RandomPart, "@", DomainPart);

        #endregion

    }

}
