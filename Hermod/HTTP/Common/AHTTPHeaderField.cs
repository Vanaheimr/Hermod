/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Linq;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    /// <summary>
    /// Defines a field within the HTTP response header
    /// </summary>
    public class AHTTPHeaderField : IEquatable<AHTTPHeaderField>, IComparable<AHTTPHeaderField>, IComparable
    {

        #region Properties

        #region Name

        /// <summary>
        /// The name of this HTTP request field
        /// </summary>
        public String Name { get; private set; }

        #endregion

        #region Description

        /// <summary>
        /// A description of this HTTP request field
        /// </summary>
        public String Description { get; private set; }

        #endregion

        #region Example

        /// <summary>
        /// An usage example for this HTTP request field
        /// </summary>
        public String Example { get; private set; }

        #endregion

        #region SeeAlso

        /// <summary>
        /// An additional source of information, e.g. the defining request-for-comment.
        /// </summary>
        public Uri SeeAlso { get; private set; }

        #endregion

        #region StringParser

        /// <summary>
        /// Parse this HTTPHeaderField from a string.
        /// </summary>
        public Func<String, Boolean> StringParser { get; private set; }

        #endregion

        #region ValueSerializer

        /// <summary>
        /// A delegate to serialize the value of the header field to a string.
        /// </summary>
        public Func<String> ValueSerializer { get; private set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region AHTTPHeaderField(Name, Description = null, Example = null, SeeAlso = null)

        /// <summary>
        /// Creates a new HTTP request header field based on
        /// a name and a description.
        /// </summary>
        /// <param name="Name">The name of the HTTP request header field</param>
        /// <param name="ValueSerializer">A delegate to serialize the value of the header field to a string.</param>
        /// <param name="Description">A description of the HTTP request header field</param>
        /// <param name="Example">An usage example</param>
        /// <param name="SeeAlso">An additional source of information, e.g. the defining request-for-comment</param>
        public AHTTPHeaderField(String Name, Func<String> ValueSerializer, String Description = null, String Example = null, Uri SeeAlso = null)
        {

            #region Initial checks

            if (Name == null && Name == "")
                throw new ArgumentNullException("The given Name must not be null or its length zero!");

            #endregion

            this.Name            = Name;
            this.ValueSerializer = ValueSerializer;
            this.Description     = Description;
            this.Example         = Example;
            this.SeeAlso         = SeeAlso;

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator == (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(AHTTPHeaderField1, AHTTPHeaderField2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) AHTTPHeaderField1 == null) || ((Object) AHTTPHeaderField2 == null))
                return false;

            return AHTTPHeaderField1.Equals(AHTTPHeaderField2);

        }

        #endregion

        #region Operator != (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator != (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 == AHTTPHeaderField2);
        }

        #endregion

        #region Operator <  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator < (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {

            // Check if AHTTPHeaderField1 is null
            if ((Object) AHTTPHeaderField1 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField1 must not be null!");

            // Check if AHTTPHeaderField2 is null
            if ((Object) AHTTPHeaderField2 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) < 0;

        }

        #endregion

        #region Operator >  (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator > (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {

            // Check if AHTTPHeaderField1 is null
            if ((Object) AHTTPHeaderField1 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField1 must not be null!");

            // Check if AHTTPHeaderField2 is null
            if ((Object) AHTTPHeaderField2 == null)
                throw new ArgumentNullException("Parameter AHTTPHeaderField2 must not be null!");

            return AHTTPHeaderField1.CompareTo(AHTTPHeaderField2) > 0;

        }

        #endregion

        #region Operator <= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator <= (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 > AHTTPHeaderField2);
        }

        #endregion

        #region Operator >= (AHTTPHeaderField1, AHTTPHeaderField2)

        public static Boolean operator >= (AHTTPHeaderField AHTTPHeaderField1, AHTTPHeaderField AHTTPHeaderField2)
        {
            return !(AHTTPHeaderField1 < AHTTPHeaderField2);
        }

        #endregion

        #endregion

        #region IComparable<AHTTPHeaderField> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an AHTTPHeaderField.
            var HTTPRequestField = Object as AHTTPHeaderField;
            if ((Object) HTTPRequestField == null)
                throw new ArgumentException("The given object is not a AHTTPHeaderField!");

            return CompareTo(HTTPRequestField);

        }

        #endregion

        #region CompareTo(AHTTPHeaderField)

        public Int32 CompareTo(AHTTPHeaderField AHTTPHeaderField)
        {

            if ((Object) AHTTPHeaderField == null)
                throw new ArgumentNullException("The given AHTTPHeaderField must not be null!");

            return Name.CompareTo(AHTTPHeaderField.Name);

        }

        #endregion

        #endregion

        #region IEquatable<AHTTPHeaderField> Members

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

            // Check if the given object is an AHTTPHeaderField.
            var HTTPRequestField = Object as AHTTPHeaderField;
            if ((Object) HTTPRequestField == null)
                return false;

            return this.Equals(HTTPRequestField);

        }

        #endregion

        #region Equals(AHTTPHeaderField)

        /// <summary>
        /// Compares two AHTTPHeaderFields for equality.
        /// </summary>
        /// <param name="AHTTPHeaderField">An AHTTPHeaderField to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(AHTTPHeaderField AHTTPHeaderField)
        {

            if (AHTTPHeaderField == null)
                return false;

            return Name == AHTTPHeaderField.Name;

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
            return Name.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return Name;
        }

        #endregion

    }

}
