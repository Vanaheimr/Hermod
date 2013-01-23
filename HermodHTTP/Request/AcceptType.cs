/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP accept type.
    /// </summary>
    public class AcceptType : IEquatable<AcceptType>, IComparable<AcceptType>, IComparable
    {

        #region Data

        private UInt32 _PlaceOfOccurence;

        #endregion

        #region Properties

        /// <summary>
        /// The accepted content type.
        /// </summary>
        public HTTPContentType ContentType { get; set; }

        /// <summary>
        /// A value between 0..1; default is 1.
        /// </summary>
        public Double          Quality     { get; set; }

        #endregion

        #region Constructor(s)

        #region AcceptType(HTTPContentType, Quality)

        /// <summary>
        /// Create a new HTTP accept header field.
        /// </summary>
        /// <param name="HTTPContentType">The accepted content type.</param>
        /// <param name="Quality">The preference of the content type.</param>
        public AcceptType(HTTPContentType HTTPContentType, Double Quality)
        {

            #region Initial checks

            if (HTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            #endregion

            this.ContentType = HTTPContentType;
            this.Quality     = Quality;

        }

        #endregion

        #region AcceptType(AcceptString, Quality)

        public AcceptType(String AcceptString, Double Quality)
        {

            #region Initial checks

            if (AcceptString.IsNullOrEmpty())
                throw new ArgumentNullException("The given Accept string must not be null or empty!");

            #endregion

            this.ContentType = new HTTPContentType(AcceptString);
            this.Quality     = Quality;

        }

        #endregion

        #region AcceptType(AcceptString)

        /// <summary>
        /// Parse the string representation of a HTTP accept header field.
        /// </summary>
        /// <param name="AcceptString"></param>
        public AcceptType(String AcceptString)
        {

            this.Quality = 1;

            var SplittedAcceptString = AcceptString.Split(new Char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            switch (SplittedAcceptString.Length)
            {

                case 1:
                    ContentType = new HTTPContentType(AcceptString);
                    Quality     = 1.0;
                    break;

                case 2:
                    this.ContentType = new HTTPContentType(SplittedAcceptString[0]);
                    this.Quality     = Double.Parse(SplittedAcceptString[1].Substring(2));
                    break;

                default: throw new ArgumentException("Could not parse the given AcceptString!");

            }

        }

        #endregion

        #endregion


        #region IComparable<AcceptType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an AcceptType.
            var AcceptType = Object as AcceptType;
            if ((Object) AcceptType == null)
                throw new ArgumentException("The given object is not a AcceptType!");

            return CompareTo(AcceptType);

        }

        #endregion

        #region CompareTo(AcceptType)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPStatusCode">An object to compare with.</param>
        public Int32 CompareTo(AcceptType AcceptType)
        {

            if ((Object) AcceptType == null)
                throw new ArgumentNullException("The given AcceptType must not be null!");

            if (Quality == AcceptType.Quality)
                return _PlaceOfOccurence.CompareTo(AcceptType._PlaceOfOccurence);
            else
                return Quality.CompareTo(AcceptType.Quality) * -1;

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
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is an AcceptType.
            var AcceptType = Object as AcceptType;
            if ((Object) AcceptType == null)
                return false;

            return this.Equals(AcceptType);

        }

        #endregion

        #region Equals(AcceptType)

        /// <summary>
        /// Compares two AcceptType for equality.
        /// </summary>
        /// <param name="AcceptType">An AcceptType to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(AcceptType AcceptType)
        {
            
            if ((Object) AcceptType == null)
                return false;

            if (ContentType.Equals(AcceptType.ContentType))
                return true;

            if (ContentType.GetMediaSubType() == "*" &&
                ContentType.GetMediaType().Equals(AcceptType.ContentType.GetMediaType()))
                return true;

            return false;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return ContentType.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Concat(ContentType, "; q=", Quality);
        }

        #endregion

    }

}
