/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extention methods for API key identifications.
    /// </summary>
    public static class APIKeyIdExtentions
    {

        /// <summary>
        /// Indicates whether this API key identifications is null or empty.
        /// </summary>
        /// <param name="APIKey">An API key identifications.</param>
        public static Boolean IsNullOrEmpty(this APIKey_Id? APIKey)
            => !APIKey.HasValue || APIKey.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this API key identifications is NOT null or empty.
        /// </summary>
        /// <param name="APIKey">An API key identifications.</param>
        public static Boolean IsNotNullOrEmpty(this APIKey_Id? APIKey)
            => APIKey.HasValue && APIKey.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// An API key.
    /// </summary>
    public readonly struct APIKey_Id : IId,
                                       IEquatable<APIKey_Id>,
                                       IComparable<APIKey_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        /// <summary>
        /// Private non-cryptographic random number generator.
        /// </summary>
        private static readonly Random _random = new Random();

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the API key identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalId?.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new API key identification based on the given string.
        /// </summary>
        private APIKey_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a random API key identification.
        /// </summary>
        /// <param name="Length">The expected length of the organization identification.</param>
        public static APIKey_Id Random(UInt16? Length = 64)

            => new APIKey_Id(_random.RandomString(Length ?? 64));

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an API key identification.
        /// </summary>
        /// <param name="Text">A text-representation of an API key identification.</param>
        public static APIKey_Id Parse(String Text)
        {

            if (TryParse(Text, out APIKey_Id apiKeyId))
                return apiKeyId;

            throw new ArgumentException("Invalid text-representation of an API key identification: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an API key identification.
        /// </summary>
        /// <param name="Text">A text-representation of an API key identification.</param>
        public static APIKey_Id? TryParse(String Text)
        {

            if (TryParse(Text, out APIKey_Id apiKeyId))
                return apiKeyId;

            return null;

        }

        #endregion

        #region TryParse(Text, out APIKeyId)

        /// <summary>
        /// Try to parse the given string as an API key identification.
        /// </summary>
        /// <param name="Text">A text-representation of an API key identification.</param>
        /// <param name="APIKeyId">The parsed API key identification.</param>
        public static Boolean TryParse(String Text, out APIKey_Id APIKeyId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    APIKeyId = new APIKey_Id(Text);
                    return true;
                }
                catch
                { }
            }

            APIKeyId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this API key identification.
        /// </summary>
        public APIKey_Id Clone

            => new APIKey_Id(
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (APIKey_Id APIKeyIdId1,
                                           APIKey_Id APIKeyIdId2)

            => APIKeyIdId1.Equals(APIKeyIdId2);

        #endregion

        #region Operator != (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (APIKey_Id APIKeyIdId1,
                                           APIKey_Id APIKeyIdId2)

            => !APIKeyIdId1.Equals(APIKeyIdId2);

        #endregion

        #region Operator <  (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (APIKey_Id APIKeyIdId1,
                                          APIKey_Id APIKeyIdId2)

            => APIKeyIdId1.CompareTo(APIKeyIdId2) < 0;

        #endregion

        #region Operator <= (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (APIKey_Id APIKeyIdId1,
                                           APIKey_Id APIKeyIdId2)

            => APIKeyIdId1.CompareTo(APIKeyIdId2) <= 0;

        #endregion

        #region Operator >  (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (APIKey_Id APIKeyIdId1,
                                          APIKey_Id APIKeyIdId2)

            => APIKeyIdId1.CompareTo(APIKeyIdId2) > 0;

        #endregion

        #region Operator >= (APIKeyIdId1, APIKeyIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyIdId1">An API key identification.</param>
        /// <param name="APIKeyIdId2">Another API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (APIKey_Id APIKeyIdId1,
                                           APIKey_Id APIKeyIdId2)

            => APIKeyIdId1.CompareTo(APIKeyIdId2) >= 0;

        #endregion

        #endregion

        #region IComparable<APIKeyId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is APIKey_Id apiKeyId
                   ? CompareTo(apiKeyId)
                   : throw new ArgumentException("The given object is not an API key identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(APIKeyId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKeyId">An object to compare with.</param>
        public Int32 CompareTo(APIKey_Id APIKeyId)

            => String.Compare(InternalId,
                              APIKeyId.InternalId,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<APIKeyId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is APIKey_Id apiKeyId &&
                   Equals(apiKeyId);

        #endregion

        #region Equals(APIKeyId)

        /// <summary>
        /// Compares two APIKeyIds for equality.
        /// </summary>
        /// <param name="APIKeyId">An API key identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(APIKey_Id APIKeyId)

            => String.Equals(InternalId,
                             APIKeyId.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId?.GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text-representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
