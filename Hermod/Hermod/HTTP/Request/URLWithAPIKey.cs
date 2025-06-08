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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An URL with an API key, e.g. for accessing remote APIs.
    /// </summary>
    public readonly struct URLWithAPIKey : IEquatable<URLWithAPIKey>,
                                           IComparable<URLWithAPIKey>
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of users.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/URLWithAPIKeyId");

        #endregion

        #region Properties

        /// <summary>
        /// The URL.
        /// </summary>
        public URL        URL         { get; }

        /// <summary>
        /// The API key.
        /// </summary>
        public APIKey_Id  APIKeyId    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new URL with an API key.
        /// </summary>
        /// <param name="URL">An URL.</param>
        /// <param name="APIKeyId">An API key.</param>
        public URLWithAPIKey(URL        URL,
                                APIKey_Id  APIKeyId)
        {
            this.URL       = URL;
            this.APIKeyId  = APIKeyId;
        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this object.
        /// </summary>
        public URLWithAPIKey Clone()

            => new (URL,
                    APIKeyId);

        #endregion


        #region Operator overloading

        #region Operator == (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (URLWithAPIKey URLWithAPIKeyId1,
                                           URLWithAPIKey URLWithAPIKeyId2)

            => URLWithAPIKeyId1.Equals(URLWithAPIKeyId2);

        #endregion

        #region Operator != (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (URLWithAPIKey URLWithAPIKeyId1,
                                           URLWithAPIKey URLWithAPIKeyId2)

            => !URLWithAPIKeyId1.Equals(URLWithAPIKeyId2);

        #endregion

        #region Operator <  (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (URLWithAPIKey URLWithAPIKeyId1,
                                          URLWithAPIKey URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) < 0;

        #endregion

        #region Operator <= (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (URLWithAPIKey URLWithAPIKeyId1,
                                           URLWithAPIKey URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) <= 0;

        #endregion

        #region Operator >  (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (URLWithAPIKey URLWithAPIKeyId1,
                                          URLWithAPIKey URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) > 0;

        #endregion

        #region Operator >= (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with an API key.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with an API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (URLWithAPIKey URLWithAPIKeyId1,
                                           URLWithAPIKey URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) >= 0;

        #endregion

        #endregion

        #region IComparable<URLWith_APIKeyId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is URLWithAPIKey urlWithAPIKey
                   ? CompareTo(urlWithAPIKey)
                   : throw new ArgumentException("The given object is not an URL with an API key!", nameof(Object));

        #endregion

        #region CompareTo(URLWithAPIKeyId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId">An object to compare with.</param>
        public Int32 CompareTo(URLWithAPIKey URLWithAPIKeyId)
        {

            var c = URL.CompareTo(URLWithAPIKeyId.URL);

            if (c == 0)
                return APIKeyId.CompareTo(URLWithAPIKeyId.APIKeyId);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<URLWithAPIKeyId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is URLWithAPIKey urlWithAPIKey &&
                   Equals(urlWithAPIKey);

        #endregion

        #region Equals(URLWithAPIKeyId)

        /// <summary>
        /// Compares two API keys informations for equality.
        /// </summary>
        /// <param name="URLWithAPIKeyId">An API keyrmation to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(URLWithAPIKey URLWithAPIKeyId)

            => URL.     Equals(URLWithAPIKeyId.URL) &&
               APIKeyId.Equals(URLWithAPIKeyId.APIKeyId);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => URL.     GetHashCode() ^
               APIKeyId.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{URL} using '{APIKeyId}'";

        #endregion

    }

}
