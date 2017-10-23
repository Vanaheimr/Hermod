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
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A WWCP address.
    /// </summary>
    public class Address : ACustomData,
                           IEquatable<Address>,
                           IComparable<Address>,
                           IComparable
    {

        #region Properties

        /// <summary>
        /// The name of the street.
        /// </summary>
        public String      Street           { get; }

        /// <summary>
        /// The house number.
        /// </summary>
        public String      HouseNumber      { get; }

        /// <summary>
        /// The floor level.
        /// </summary>
        public String      FloorLevel       { get; }

        /// <summary>
        /// The postal code.
        /// </summary>
        public String      PostalCode       { get; }

        /// <summary>
        /// The postal code sub.
        /// </summary>
        public String      PostalCodeSub    { get; }

        /// <summary>
        /// The city.
        /// </summary>
        public I18NString  City             { get; }

        /// <summary>
        /// The country.
        /// </summary>
        public Country     Country          { get; }

        /// <summary>
        /// An optional text/comment to describe the address.
        /// </summary>
        public I18NString  Comment          { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new address.
        /// </summary>
        /// <param name="Street">The name of the street.</param>
        /// <param name="HouseNumber">The house number.</param>
        /// <param name="FloorLevel">The floor level.</param>
        /// <param name="PostalCode">The postal code</param>
        /// <param name="PostalCodeSub">The postal code sub</param>
        /// <param name="City">The city.</param>
        /// <param name="Country">The country.</param>
        /// <param name="Comment">An optional text/comment to describe the address.</param>
        /// 
        /// <param name="CustomData">An optional dictionary of customer-specific data.</param>
        private Address(String                               Street,
                        String                               HouseNumber,
                        String                               FloorLevel,
                        String                               PostalCode,
                        String                               PostalCodeSub,
                        I18NString                           City,
                        Country                              Country,
                        I18NString                           Comment      = null,

                        IReadOnlyDictionary<String, Object>  CustomData   = null)

            : base(CustomData)

        {

            this.Street         = Street        ?? "";
            this.HouseNumber    = HouseNumber   ?? "";
            this.FloorLevel     = FloorLevel    ?? "";
            this.PostalCode     = PostalCode    ?? "";
            this.PostalCodeSub  = PostalCodeSub ?? "";
            this.City           = City          ?? I18NString.Empty;
            this.Country        = Country;
            this.Comment        = Comment       ?? I18NString.Empty;

        }

        #endregion


        #region (static) Create(Country, PostalCode, City, Street, HouseNumber, CustomData = null)

        /// <summary>
        /// Create a new minimal address.
        /// </summary>
        /// <param name="Country">The country.</param>
        /// <param name="PostalCode">The postal code</param>
        /// <param name="City">The city.</param>
        /// <param name="Street">The name of the street.</param>
        /// <param name="HouseNumber">The house number.</param>
        /// <param name="FloorLevel">The floor level.</param>
        /// 
        /// <param name="CustomData">An optional dictionary of customer-specific data.</param>
        public static Address Create(Country                              Country,
                                     String                               PostalCode,
                                     I18NString                           City,
                                     String                               Street,
                                     String                               HouseNumber,
                                     String                               FloorLevel   = null,

                                     IReadOnlyDictionary<String, Object>  CustomData   = null)


            => new Address(Street,
                           HouseNumber,
                           FloorLevel,
                           PostalCode,
                           "",
                           City,
                           Country,
                           new I18NString(),
                           CustomData);

        #endregion


        #region Operator overloading

        #region Operator == (Address1, Address2)

        /// <summary>
        /// Compares two addresses for equality.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (Address Address1, Address Address2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(Address1, Address2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) Address1 == null) || ((Object) Address2 == null))
                return false;

            return Address1.Equals(Address2);

        }

        #endregion

        #region Operator != (Address1, Address2)

        /// <summary>
        /// Compares two addresses for inequality.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (Address Address1, Address Address2)

            => !(Address1 == Address2);

        #endregion

        #region Operator <  (Address1, Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Address Address1, Address Address2)
        {

            if ((Object) Address1 == null)
                throw new ArgumentNullException(nameof(Address1), "The given Address1 must not be null!");

            return Address1.CompareTo(Address2) < 0;

        }

        #endregion

        #region Operator <= (Address1, Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Address Address1, Address Address2)
            => !(Address1 > Address2);

        #endregion

        #region Operator >  (Address1, Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Address Address1, Address Address2)
        {

            if ((Object)Address1 == null)
                throw new ArgumentNullException(nameof(Address1), "The given Address1 must not be null!");

            return Address1.CompareTo(Address2) > 0;

        }

        #endregion

        #region Operator >= (Address1, Address2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Address1">An address.</param>
        /// <param name="Address2">Another address.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Address Address1, Address Address2)
            => !(Address1 < Address2);

        #endregion

        #endregion

        #region IComparable<Address> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var Address = Object as Address;
            if ((Object)Address == null)
                throw new ArgumentException("The given object is not an address identification!", nameof(Object));

            return CompareTo(Address);

        }

        #endregion

        #region CompareTo(Address)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Address">An object to compare with.</param>
        public Int32 CompareTo(Address Address)
        {

            if ((Object) Address == null)
                throw new ArgumentNullException(nameof(Address), "The given address must not be null!");

            var c = Country.     CompareTo(Address.Country);
            if (c != 0)
                return c;

            c = PostalCode.      CompareTo(Address.PostalCode);
            if (c != 0)
                return c;

            c = City.FirstText().CompareTo(Address.City.FirstText());
            if (c != 0)
                return c;

            return Street.       CompareTo(Address.Street);

        }

        #endregion

        #endregion

        #region IEquatable<Address> Members

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

            // Check if the given object is an Address.
            var Address = Object as Address;
            if ((Object) Address == null)
                return false;

            return this.Equals(Address);

        }

        #endregion

        #region Equals(Address)

        /// <summary>
        /// Compares two addresses for equality.
        /// </summary>
        /// <param name="Address">An address to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Address Address)
        {

            if ((Object) Address == null)
                return false;

            try
            {

            return Street.        Equals(Address.Street)        &&
                   HouseNumber.   Equals(Address.HouseNumber)   &&
                   FloorLevel.    Equals(Address.FloorLevel)    &&
                   PostalCode.    Equals(Address.PostalCode)    &&
                   PostalCodeSub. Equals(Address.PostalCodeSub) &&
                   City.          Equals(Address.City)          &&
                   Country.       Equals(Address.Country);

            }
            catch (Exception e)
            {
                return false;
            }

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
            unchecked
            {

                return Street.        GetHashCode() * 41 ^
                       HouseNumber.   GetHashCode() * 37 ^
                       FloorLevel.    GetHashCode() * 31 ^
                       PostalCode.    GetHashCode() * 23 ^
                       PostalCodeSub. GetHashCode() * 17 ^
                       City.          GetHashCode() * 11 ^
                       Country.       GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()

            => Street                          + " " +
               HouseNumber                     + " " +
               FloorLevel                      + ", " +
               PostalCode                      + " " +
               PostalCodeSub                   + " " +
               City                            + ", " +
               Country.CountryName.FirstText() + " / " +
               Comment;

        #endregion

    }

}
