/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
using System.Globalization;
using System.Linq;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP accept type.
    /// </summary>
    public class AcceptType : IEquatable<AcceptType>,
                              IComparable<AcceptType>,
                              IComparable
    {

        #region Data

        private UInt32 _PlaceOfOccurence;

        #endregion

        #region Properties

        /// <summary>
        /// The accepted content type.
        /// </summary>
        public HTTPContentType  ContentType    { get; set; }

        /// <summary>
        /// A value between 0..1; default is 1.
        /// </summary>
        public Double           Quality        { get; set; }

        #endregion

        #region Constructor(s)

        #region AcceptType(HTTPContentType, Quality = 1.0)

        /// <summary>
        /// Create a new HTTP accept header field.
        /// </summary>
        /// <param name="HTTPContentType">The accepted content type.</param>
        /// <param name="Quality">The preference of the content type.</param>
        public AcceptType(HTTPContentType  HTTPContentType,
                          Double           Quality   = 1.0)
        {

            this.ContentType  = HTTPContentType;
            this.Quality      = Quality;

        }

        #endregion

        #region AcceptType(AcceptString, Quality)

        public AcceptType(String AcceptString, Double Quality)
        {

            var acceptString  = AcceptString.Split('/');

            this.ContentType  = HTTPContentType.ForMediaType(AcceptString, () => new HTTPContentType(acceptString[0], acceptString[1], "utf-8", null, null));
            this.Quality      = Quality;

        }

        #endregion

        #region AcceptType(AcceptString)

        /// <summary>
        /// Parse the string representation of a HTTP accept header field.
        /// </summary>
        /// <param name="AcceptString"></param>
        public AcceptType(String AcceptString)
        {

            // text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8

            // text/html
            // application/xhtml+xml
            // application/xml;q=0.9
            // */*;q=0.8

            // text/html,application/xhtml+xml,application/xml
            // q=0.9,*/*
            // q=0.8

            // text/html
            // application/xhtml+xml
            // application/xml
            // q=0.9
            // */*
            // q=0.8

            var splittedAcceptString  = AcceptString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()).ToArray();
            var mediaTypes            = splittedAcceptString[0].Split('/');
            var charset               = Array.Find(splittedAcceptString, part => part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase));
            var quality               = Array.Find(splittedAcceptString, part => part.StartsWith("q=",       StringComparison.OrdinalIgnoreCase));

            if (mediaTypes.Length == 1)
            {
                this.ContentType      = HTTPContentType.ALL;
            }
            else if(mediaTypes.Length == 2)
            {
                this.ContentType      = HTTPContentType.ForMediaType(AcceptString, () => new HTTPContentType(mediaTypes[0],
                                                                                                             mediaTypes[1],
                                                                                                             charset.IsNotNullOrEmpty() ? charset.Substring(8) : null,
                                                                                                             null,
                                                                                                             null));
            }

            this.Quality              = quality is not null && quality.IsNotNullOrEmpty()
                                            ? Double.Parse(quality[2..], CultureInfo.InvariantCulture)
                                            : 1.0;

        }

        #endregion

        #endregion


        public static Boolean TryParse(String AcceptString, out AcceptType? AcceptType)
        {

            // text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8

            // text/html
            // application/xhtml+xml
            // application/xml;q=0.9
            // */*;q=0.8

            // text/html,application/xhtml+xml,application/xml
            // q=0.9,*/*
            // q=0.8

            // text/html
            // application/xhtml+xml
            // application/xml
            // q=0.9
            // */*
            // q=0.8

            var splittedAcceptString  = AcceptString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()).ToArray();
            var mediaTypes            = splittedAcceptString[0].Split('/');
            var charset               = Array.Find(splittedAcceptString, part => part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase));
            var quality               = Array.Find(splittedAcceptString, part => part.StartsWith("q=",       StringComparison.OrdinalIgnoreCase));

            var quality2              = quality is not null && quality.IsNotNullOrEmpty()
                                            ? Double.Parse(quality[2..], CultureInfo.InvariantCulture)
                                            : 1.0;

            if (mediaTypes.Length == 1)
            {

                AcceptType = new AcceptType(HTTPContentType.ALL);
                return true;

            }
            else if(mediaTypes.Length == 2)
            {

                AcceptType = new AcceptType(
                                 HTTPContentType.ForMediaType(AcceptString, () => new HTTPContentType(mediaTypes[0],
                                                                                                      mediaTypes[1],
                                                                                                      charset.IsNotNullOrEmpty() ? charset.Substring(8) : null,
                                                                                                      null,
                                                                                                      null)),
                                 quality2
                             );

                return true;

            }

            AcceptType = null;
            return false;

        }

        #region Clone()

        public AcceptType Clone()

            => new (ContentType,
                    Quality);

        #endregion


        #region IComparable<AcceptType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two accept types.
        /// </summary>
        /// <param name="Object">An accept type to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is AcceptType acceptType
                   ? CompareTo(acceptType)
                   : throw new ArgumentException("The given object is not an accept type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AcceptType)

        /// <summary>
        /// Compares two accept types.
        /// </summary>
        /// <param name="AcceptType">An accept type to compare with.</param>
        public Int32 CompareTo(AcceptType? AcceptType)
        {

            if (AcceptType is null)
                throw new ArgumentNullException(nameof(AcceptType),
                                                "The given accept type must not be null!");

            return Quality == AcceptType.Quality
                       ? _PlaceOfOccurence.CompareTo(AcceptType._PlaceOfOccurence)
                       : Quality.CompareTo(AcceptType.Quality) * -1;

        }

        #endregion

        #endregion

        #region IEquatable<AcceptType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is AcceptType acceptType &&
                   Equals(acceptType);

        #endregion

        #region Equals(AcceptType)

        /// <summary>
        /// Compares two AcceptType for equality.
        /// </summary>
        /// <param name="AcceptType">An AcceptType to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(AcceptType? AcceptType)
        {

            if (AcceptType is null)
                return false;

            if (ContentType.Equals(AcceptType.ContentType))
                return true;

            if (ContentType.MediaSubType == "*" &&
                ContentType.MediaMainType.Equals(AcceptType.ContentType.MediaMainType))
                return true;

            return false;

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
            return ContentType.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Concat(ContentType, "; q=", Quality);
        }

        #endregion

    }

}
