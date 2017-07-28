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
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP Keep-Alive response header property.
    /// </summary>
    public class KeepAliveType : IEquatable<KeepAliveType>, IComparable<KeepAliveType>, IComparable
    {

        #region Properties

        /// <summary>
        /// The timeout till the next request.
        /// </summary>
        public TimeSpan? Timeout                { get; private set; }

        /// <summary>
        /// The maximum number of requests within this connection.
        /// </summary>
        public UInt32?   MaxNumberOfRequests    { get; private set; }

        #endregion

        #region Constructor(s)

        #region KeepAliveType(Timeout = null, MaxNumberOfRequests = null)

        public KeepAliveType(TimeSpan? Timeout              = null,
                             UInt32?   MaxNumberOfRequests  = null)
        {
            this.Timeout              = Timeout;
            this.MaxNumberOfRequests  = MaxNumberOfRequests;
        }

        #endregion

        #region KeepAliveType(KeepAliveString)

        /// <summary>
        /// Parse the string representation of a HTTP accept header field.
        /// </summary>
        /// <param name="AcceptString"></param>
        public KeepAliveType(String KeepAliveString)
        {

            UInt32 _Value;
            Char[] _Equals  = new Char[1] { '=' };

            // "timeout=10, max=5"
            foreach (var Token in KeepAliveString.Split(new Char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()))
            {

                var Property = Token.Split(_Equals, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToArray();

                if (Property.Length == 2)
                    if (Property[0] == "timeout")
                        if (UInt32.TryParse(Property[1], out _Value))
                        {
                            this.Timeout = TimeSpan.FromSeconds(_Value);
                            continue;
                        }

                if (Property.Length == 2)
                    if (Property[0] == "max")
                        if (UInt32.TryParse(Property[1], out _Value))
                        {
                            this.MaxNumberOfRequests = _Value;
                            continue;
                        }

                throw new ArgumentException("Invalid KeepAliv string!", "KeepAliveString");

            }

        }

        #endregion

        #endregion


        #region IComparable<KeepAliveType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an KeepAliveType.
            var KeepAliveType = Object as KeepAliveType;
            if ((Object) KeepAliveType == null)
                throw new ArgumentException("The given object is not a KeepAliveType!");

            return CompareTo(KeepAliveType);

        }

        #endregion

        #region CompareTo(KeepAliveType)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPStatusCode">An object to compare with.</param>
        public Int32 CompareTo(KeepAliveType KeepAliveType)
        {

            if ((Object) KeepAliveType == null)
                throw new ArgumentNullException("The given KeepAliveType must not be null!");

            if (Timeout == KeepAliveType.Timeout)
                return MaxNumberOfRequests.Value.CompareTo(KeepAliveType.MaxNumberOfRequests.Value);
            else
                return Timeout.Value.CompareTo(KeepAliveType.Timeout.Value);

        }

        #endregion

        #endregion

        #region IEquatable<KeepAliveType> Members

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

            // Check if the given object is an KeepAliveType.
            var KeepAliveType = Object as KeepAliveType;
            if ((Object) KeepAliveType == null)
                return false;

            return this.Equals(KeepAliveType);

        }

        #endregion

        #region Equals(KeepAliveType)

        /// <summary>
        /// Compares two KeepAliveType for equality.
        /// </summary>
        /// <param name="KeepAliveType">An KeepAliveType to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(KeepAliveType KeepAliveType)
        {
            
            if ((Object) KeepAliveType == null)
                return false;

            if (!Timeout.Equals(KeepAliveType.Timeout))
                return false;

            if (!MaxNumberOfRequests.Equals(KeepAliveType.MaxNumberOfRequests))
                return false;

            return true;

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
            return Timeout.GetHashCode() ^ MaxNumberOfRequests.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            var Response = new List<String>();

            if (Timeout.HasValue)
                Response.Add("timeout=" + ((UInt32) Timeout.Value.TotalSeconds));

            if (MaxNumberOfRequests.HasValue)
                Response.Add("max=" + MaxNumberOfRequests.Value);

            return Response.AggregateOrDefault((a, b) => a + ", " + b, "");

        }

        #endregion

    }

}
