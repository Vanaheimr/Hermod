/*
 * Copyright (c) 2014-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of UsersAPI <https://www.github.com/Vanaheimr/UsersAPI>
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace social.OpenData.UsersAPI
{

    /// <summary>
    /// An URL with its API key identification.
    /// </summary>
    public readonly struct URLWith_APIKeyId : IEquatable<URLWith_APIKeyId>,
                                              IComparable<URLWith_APIKeyId>
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
        /// The API key identification.
        /// </summary>
        public APIKey_Id  APIKeyId    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new API keyrmation.
        /// </summary>
        /// <param name="URL">An URL.</param>
        /// <param name="APIKeyId">An API key identification.</param>
        public URLWith_APIKeyId(URL        URL,
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
        public URLWith_APIKeyId Clone()

            => new URLWith_APIKeyId(URL,
                                    APIKeyId);

        #endregion


        #region Operator overloading

        #region Operator == (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (URLWith_APIKeyId URLWithAPIKeyId1,
                                           URLWith_APIKeyId URLWithAPIKeyId2)

            => URLWithAPIKeyId1.Equals(URLWithAPIKeyId2);

        #endregion

        #region Operator != (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (URLWith_APIKeyId URLWithAPIKeyId1,
                                           URLWith_APIKeyId URLWithAPIKeyId2)

            => !URLWithAPIKeyId1.Equals(URLWithAPIKeyId2);

        #endregion

        #region Operator <  (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (URLWith_APIKeyId URLWithAPIKeyId1,
                                          URLWith_APIKeyId URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) < 0;

        #endregion

        #region Operator <= (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (URLWith_APIKeyId URLWithAPIKeyId1,
                                           URLWith_APIKeyId URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) <= 0;

        #endregion

        #region Operator >  (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (URLWith_APIKeyId URLWithAPIKeyId1,
                                          URLWith_APIKeyId URLWithAPIKeyId2)

            => URLWithAPIKeyId1.CompareTo(URLWithAPIKeyId2) > 0;

        #endregion

        #region Operator >= (URLWithAPIKeyId1, URLWithAPIKeyId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId1">An URL with API key identification.</param>
        /// <param name="URLWithAPIKeyId2">Another URL with API key identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (URLWith_APIKeyId URLWithAPIKeyId1,
                                           URLWith_APIKeyId URLWithAPIKeyId2)

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

            => Object is URLWith_APIKeyId urlWithAPIKeyId
                   ? CompareTo(urlWithAPIKeyId)
                   : throw new ArgumentException("The given object is not an URL with API key information!", nameof(Object));

        #endregion

        #region CompareTo(URLWithAPIKeyId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="URLWithAPIKeyId">An object to compare with.</param>
        public Int32 CompareTo(URLWith_APIKeyId URLWithAPIKeyId)
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

            => Object is URLWith_APIKeyId urlWithAPIKeyId &&
                   Equals(urlWithAPIKeyId);

        #endregion

        #region Equals(URLWithAPIKeyId)

        /// <summary>
        /// Compares two API keys informations for equality.
        /// </summary>
        /// <param name="URLWithAPIKeyId">An API keyrmation to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(URLWith_APIKeyId URLWithAPIKeyId)

            => URL.     Equals(URLWithAPIKeyId.URL) &&
               APIKeyId.Equals(URLWithAPIKeyId.APIKeyId);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => URL.     GetHashCode() ^
               APIKeyId.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(URL, " using '", APIKeyId, "'");

        #endregion

    }

}
