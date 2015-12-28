/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
    /// The unique identification of a HTTP hostname.
    /// </summary>
    public class HTTPHostname : IEquatable<HTTPHostname>,
                                IComparable<HTTPHostname>

    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        protected readonly String _Id;

        #endregion

        #region Properties

        #region New

        /// <summary>
        /// Generate a new unique identification of an Electric Vehicle Roaming Network (EVRN Id).
        /// </summary>
        public static HTTPHostname New
        {
            get
            {
                return new HTTPHostname(Guid.NewGuid().ToString());
            }
        }

        #endregion

        #region Length

        /// <summary>
        /// Returns the length of the identificator.
        /// </summary>
        public UInt64 Length
        {
            get
            {
                return (UInt64) _Id.Length;
            }
        }

        #endregion


        #region Any

        /// <summary>
        /// The HTTP 'ANY' host or "*".
        /// </summary>
        public static HTTPHostname Any
        {
            get
            {
                return new HTTPHostname("*");
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Generate a new HTTP hostname based on the given string.
        /// </summary>
        private HTTPHostname(String Text)
        {

            #region Initial checks

            if (Text.IsNullOrEmpty())
                throw new ArgumentException("The parameter must not be null or empty!", "Text");

            #endregion

            _Id = Text.Trim();

        }

        #endregion


        #region Parse(Text)

        /// <summary>
        /// Parse the given string as an Electric Vehicle Roaming Network (EVRN Id).
        /// </summary>
        /// <param name="Text">A text representation of an Electric Vehicle Roaming Network identification.</param>
        public static HTTPHostname Parse(String Text)
        {
            return new HTTPHostname(Text);
        }

        #endregion

        #region TryParse(Text, out RoamingNetworkId)

        /// <summary>
        /// Parse the given string as an Electric Vehicle Roaming Network (EVRN Id).
        /// </summary>
        /// <param name="Text">A text representation of an Electric Vehicle Roaming Network identification.</param>
        /// <param name="RoamingNetworkId">The parsed Electric Vehicle Roaming Network identification.</param>
        public static Boolean TryParse(String Text, out HTTPHostname RoamingNetworkId)
        {

            #region Initial checks

            if (Text.IsNullOrEmpty())
            {
                RoamingNetworkId = null;
                return false;
            }

            #endregion

            try
            {
                RoamingNetworkId = new HTTPHostname(Text);
                return true;
            }
            catch (Exception)
            {
                RoamingNetworkId = null;
                return false;
            }

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this Electric Vehicle Roaming Network identification.
        /// </summary>
        public HTTPHostname Clone
        {
            get
            {
                return new HTTPHostname(new String(_Id.ToCharArray()));
            }
        }

        #endregion


        #region Operator overloading

        #region Operator == (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(RoamingNetworkId1, RoamingNetworkId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) RoamingNetworkId1 == null) || ((Object) RoamingNetworkId2 == null))
                return false;

            return RoamingNetworkId1.Equals(RoamingNetworkId2);

        }

        #endregion

        #region Operator != (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {
            return !(RoamingNetworkId1 == RoamingNetworkId2);
        }

        #endregion

        #region Operator <  (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {

            if ((Object) RoamingNetworkId1 == null)
                throw new ArgumentNullException("The given RoamingNetworkId1 must not be null!");

            return RoamingNetworkId1.CompareTo(RoamingNetworkId2) < 0;

        }

        #endregion

        #region Operator <= (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {
            return !(RoamingNetworkId1 > RoamingNetworkId2);
        }

        #endregion

        #region Operator >  (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {

            if ((Object) RoamingNetworkId1 == null)
                throw new ArgumentNullException("The given RoamingNetworkId1 must not be null!");

            return RoamingNetworkId1.CompareTo(RoamingNetworkId2) > 0;

        }

        #endregion

        #region Operator >= (RoamingNetworkId1, RoamingNetworkId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId1">A HTTPHostname.</param>
        /// <param name="RoamingNetworkId2">Another HTTPHostname.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPHostname RoamingNetworkId1, HTTPHostname RoamingNetworkId2)
        {
            return !(RoamingNetworkId1 < RoamingNetworkId2);
        }

        #endregion

        #endregion

        #region IComparable<HTTPHostname> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an RoamingNetworkId.
            var RoamingNetworkId = Object as HTTPHostname;
            if ((Object) RoamingNetworkId == null)
                throw new ArgumentException("The given object is not a RoamingNetworkId!");

            return CompareTo(RoamingNetworkId);

        }

        #endregion

        #region CompareTo(RoamingNetworkId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="RoamingNetworkId">An object to compare with.</param>
        public Int32 CompareTo(HTTPHostname RoamingNetworkId)
        {

            if ((Object) RoamingNetworkId == null)
                throw new ArgumentNullException("The given RoamingNetworkId must not be null!");

            return _Id.CompareTo(RoamingNetworkId._Id);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPHostname> Members

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

            // Check if the given object is an RoamingNetworkId.
            var RoamingNetworkId = Object as HTTPHostname;
            if ((Object) RoamingNetworkId == null)
                return false;

            return this.Equals(RoamingNetworkId);

        }

        #endregion

        #region Equals(RoamingNetworkId)

        /// <summary>
        /// Compares two RoamingNetworkIds for equality.
        /// </summary>
        /// <param name="RoamingNetworkId">A RoamingNetworkId to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPHostname RoamingNetworkId)
        {

            if ((Object) RoamingNetworkId == null)
                return false;

            return _Id.Equals(RoamingNetworkId._Id);

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
            return _Id.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return _Id.ToString();
        }

        #endregion

    }

}
