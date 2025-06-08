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

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// A single ping result.
    /// </summary>
    public readonly struct PingResult : IEquatable<PingResult>,
                                        IComparable<PingResult>,
                                        IComparable
    {

        #region Properties

        /// <summary>
        /// The runtime of the ping.
        /// </summary>
        public TimeSpan    Runtime    { get; }

        /// <summary>
        /// The result or error of the ping.
        /// </summary>
        public ICMPErrors  Error      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new single ping result.
        /// </summary>
        /// <param name="Runtime">The runtime of the ping.</param>
        /// <param name="Error">The result or error of the ping.</param>
        public PingResult(TimeSpan    Runtime,
                          ICMPErrors  Error)
        {
            this.Runtime  = Runtime;
            this.Error    = Error;
        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this ping result.
        /// </summary>
        public PingResult Clone()

            => new (
                   Runtime,
                   Error
               );

        #endregion


        #region Operator overloading

        #region Operator == (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (PingResult pingResult1,
                                            PingResult pingResult2)

            => pingResult1.Equals(pingResult2);

        #endregion

        #region Operator != (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (PingResult pingResult1,
                                            PingResult pingResult2)

            => !pingResult1.Equals(pingResult2);

        #endregion

        #region Operator <  (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (PingResult pingResult1,
                                            PingResult pingResult2)

            => pingResult1.CompareTo(pingResult2) < 0;

        #endregion

        #region Operator <= (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (PingResult pingResult1,
                                            PingResult pingResult2)

            => pingResult1.CompareTo(pingResult2) <= 0;

        #endregion

        #region Operator >  (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (PingResult pingResult1,
                                            PingResult pingResult2)

            => pingResult1.CompareTo(pingResult2) > 0;

        #endregion

        #region Operator >= (pingResult1, pingResult2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult1">A ping result.</param>
        /// <param name="pingResult2">Another ping result.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (PingResult pingResult1,
                                            PingResult pingResult2)

            => pingResult1.CompareTo(pingResult2) >= 0;

        #endregion

        #endregion

        #region IComparable<PingResult> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is PingResult pingResult
                    ? CompareTo(pingResult)
                    : throw new ArgumentException("The given object is not a ping result!",
                                                    nameof(Object));

        #endregion

        #region CompareTo(pingResult)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="pingResult">An object to compare with.</param>
        public Int32 CompareTo(PingResult pingResult)
        {

            var c = Error.CompareTo(pingResult.Error);

            if (c == 0)
                c = Runtime.CompareTo(pingResult.Runtime);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<PingResult> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is PingResult pingResult &&
                    Equals(pingResult);

        #endregion

        #region Equals(pingResult)

        /// <summary>
        /// Compares two ping results for equality.
        /// </summary>
        /// <param name="pingResult">A ping result to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(PingResult pingResult)

            => Runtime.Equals(pingResult.Runtime) &&
                Error.  Equals(pingResult.Error);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Runtime.GetHashCode() ^
                Error.  GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(Math.Round(Runtime.TotalMilliseconds, 2), " ms, ", Error);

        #endregion

    }

}
