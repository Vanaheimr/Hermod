/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// an API key.
    /// </summary>
    public struct APIKey : IId,
                           IEquatable<APIKey>,
                           IComparable<APIKey>
    {

        #region Data

        private static readonly Random _random = new Random(DateTime.Now.Millisecond);

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// The length of the API key.
        /// </summary>
        public UInt64 Length

            => (UInt64) InternalId.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique API key based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the API key.</param>
        private APIKey(String  String)
        {

            this.InternalId  = String;

        }

        #endregion


        #region (static) Random   (Size)

        /// <summary>
        /// Create a random API key.
        /// </summary>
        /// <param name="Size">The expected size of the API key.</param>
        public static APIKey Random(UInt16? Size = 64)

            => new APIKey(_random.RandomString(Size ?? 64));

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an API key.
        /// </summary>
        /// <param name="Text">A text representation of an API key.</param>
        public static APIKey Parse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of an API key must not be null or empty!");

            #endregion

            return new APIKey(Text);

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an API key.
        /// </summary>
        /// <param name="Text">A text representation of an API key.</param>
        public static APIKey? TryParse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of an API key must not be null or empty!");

            #endregion

            if (TryParse(Text, out APIKey _APIKey))
                return _APIKey;

            return new APIKey?();

        }

        #endregion

        #region (static) TryParse(Text, out APIKey)

        /// <summary>
        /// Try to parse the given string as an API key.
        /// </summary>
        /// <param name="Text">A text representation of an API key.</param>
        /// <param name="APIKey">The parsed API key.</param>
        public static Boolean TryParse(String Text, out APIKey APIKey)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of an API key must not be null or empty!");

            #endregion

            try
            {
                APIKey = new APIKey(Text);
                return true;
            }
            catch (Exception)
            {
                APIKey = default(APIKey);
                return false;
            }

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this API key.
        /// </summary>
        public APIKey Clone

            => new APIKey(
                   new String(InternalId.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (APIKey APIKey1, APIKey APIKey2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(APIKey1, APIKey2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) APIKey1 == null) || ((Object) APIKey2 == null))
                return false;

            return APIKey1.Equals(APIKey2);

        }

        #endregion

        #region Operator != (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 == APIKey2);

        #endregion

        #region Operator <  (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (APIKey APIKey1, APIKey APIKey2)
        {

            if ((Object) APIKey1 == null)
                throw new ArgumentNullException(nameof(APIKey1), "The given APIKey1 must not be null!");

            return APIKey1.CompareTo(APIKey2) < 0;

        }

        #endregion

        #region Operator <= (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 > APIKey2);

        #endregion

        #region Operator >  (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (APIKey APIKey1, APIKey APIKey2)
        {

            if ((Object) APIKey1 == null)
                throw new ArgumentNullException(nameof(APIKey1), "The given APIKey1 must not be null!");

            return APIKey1.CompareTo(APIKey2) > 0;

        }

        #endregion

        #region Operator >= (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">an API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 < APIKey2);

        #endregion

        #endregion

        #region IComparable<APIKey> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is APIKey))
                throw new ArgumentException("The given object is not an API key!",
                                            nameof(Object));

            return CompareTo((APIKey) Object);

        }

        #endregion

        #region CompareTo(APIKey)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey">An object to compare with.</param>
        public Int32 CompareTo(APIKey APIKey)
        {

            if ((Object) APIKey == null)
                throw new ArgumentNullException(nameof(APIKey),  "The given API key must not be null!");

            return String.Compare(InternalId, APIKey.InternalId, StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<APIKey> Members

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

            if (!(Object is APIKey))
                return false;

            return Equals((APIKey) Object);

        }

        #endregion

        #region Equals(APIKey)

        /// <summary>
        /// Compares two API keys for equality.
        /// </summary>
        /// <param name="APIKey">an API key to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(APIKey APIKey)
        {

            if ((Object) APIKey == null)
                return false;

            return InternalId.Equals(APIKey.InternalId);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => InternalId;

        #endregion

    }

}
