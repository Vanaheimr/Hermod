/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A DNS SRV endpoint, which is used to specify the location and availability of a service.
    /// </summary>
    public class DNSSRVEndpoint : IEquatable<DNSSRVEndpoint>,
                                  IComparable<DNSSRVEndpoint>,
                                  IComparable
    {

        #region Properties

        /// <summary>
        /// The target hostname of this SRV endpoint.
        /// </summary>
        public String                   Target              { get; }

        /// <summary>
        /// The priority of this SRV endpoint.
        /// </summary>
        public UInt16                   Priority            { get; }

        /// <summary>
        /// The weight of this SRV endpoint for load balancing.
        /// </summary>
        public UInt16                   Weight              { get; }

        /// <summary>
        /// The port number of this SRV endpoint.
        /// </summary>
        public IPPort                   Port                { get; }

        /// <summary>
        /// The Time To Live (TTL) for this SRV endpoint. (Int32)
        /// </summary>
        public TimeSpan                 TTL                 { get; }

        /// <summary>
        /// An enumeration of resolved IP addresses (A/AAAA) for this SRV endpoint.
        /// </summary>
        public IEnumerable<IIPAddress>  ResolvedAddresses   { get; }

        /// <summary>
        /// The optional health status for this SRV endpoint.
        /// </summary>
        public Boolean                  IsHealthy           { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS SRV endpoint.
        /// </summary>
        /// <param name="Target">The target hostname of this SRV endpoint.</param>
        /// <param name="Priority">The priority of this SRV endpoint.</param>
        /// <param name="Weight">The weight of this SRV endpoint for load balancing.</param>
        /// <param name="Port">The TCP/IP port number of this SRV endpoint.</param>
        /// <param name="TTL">The Time To Live (TTL) for this SRV endpoint.</param>
        /// <param name="ResolvedAddresses">An enumeration of resolved IP addresses (A/AAAA) for this SRV endpoint.</param>
        /// <param name="IsHealthy">An optional health status for this SRV endpoint.</param>
        public DNSSRVEndpoint(String                    Target,
                              UInt16                    Priority,
                              UInt16                    Weight,
                              IPPort                    Port,
                              TimeSpan                  TTL,
                              IEnumerable<IIPAddress>?  ResolvedAddresses   = null,
                              Boolean                   IsHealthy           = true)
        {

            this.Target             = Target;
            this.Priority           = Priority;
            this.Weight             = Weight;
            this.Port               = Port;
            this.TTL                = TTL;
            this.ResolvedAddresses  = ResolvedAddresses ?? [];
            this.IsHealthy          = IsHealthy;

            unchecked
            {
                hashCode = this.Target.ToLowerInvariant().
                                            GetHashCode()  *  3 ^
                           this.Priority.         GetHashCode()  *  5 ^
                           this.Weight.           GetHashCode()  *  7 ^
                           this.Port.             GetHashCode()  * 11 ^
                           this.TTL.              GetHashCode()  * 13 ^
                           this.ResolvedAddresses.CalcHashCode() * 17 ^
                           this.IsHealthy.        GetHashCode()  * 19;
            }

        }

        #endregion


        #region Operator overloading

        #region Operator == (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DNSSRVEndpoint DNSSRVEndpoint1,
                                           DNSSRVEndpoint DNSSRVEndpoint2)

            => DNSSRVEndpoint1.Equals(DNSSRVEndpoint2);

        #endregion

        #region Operator != (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DNSSRVEndpoint DNSSRVEndpoint1,
                                           DNSSRVEndpoint DNSSRVEndpoint2)

            => !DNSSRVEndpoint1.Equals(DNSSRVEndpoint2);

        #endregion

        #region Operator <  (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DNSSRVEndpoint DNSSRVEndpoint1,
                                          DNSSRVEndpoint DNSSRVEndpoint2)

            => DNSSRVEndpoint1.CompareTo(DNSSRVEndpoint2) < 0;

        #endregion

        #region Operator <= (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DNSSRVEndpoint DNSSRVEndpoint1,
                                           DNSSRVEndpoint DNSSRVEndpoint2)

            => DNSSRVEndpoint1.CompareTo(DNSSRVEndpoint2) <= 0;

        #endregion

        #region Operator >  (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DNSSRVEndpoint DNSSRVEndpoint1,
                                          DNSSRVEndpoint DNSSRVEndpoint2)

            => DNSSRVEndpoint1.CompareTo(DNSSRVEndpoint2) > 0;

        #endregion

        #region Operator >= (DNSSRVEndpoint1, DNSSRVEndpoint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DNSSRVEndpoint1">A DNS SRV Endpoint.</param>
        /// <param name="DNSSRVEndpoint2">Another DNS SRV Endpoint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DNSSRVEndpoint DNSSRVEndpoint1,
                                           DNSSRVEndpoint DNSSRVEndpoint2)

            => DNSSRVEndpoint1.CompareTo(DNSSRVEndpoint2) >= 0;

        #endregion

        #endregion

        #region IComparable<DNSSRVEndpoint> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two DNS SRV Endpoints.
        /// </summary>
        /// <param name="Object">A DNS SRV Endpoint to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DNSSRVEndpoint dnsSRVEndpoint
                   ? CompareTo(dnsSRVEndpoint)
                   : throw new ArgumentException("The given object is not a DNS SRV Endpoint!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DNSSRVEndpoint)

        /// <summary>
        /// Compares two DNS SRV Endpoints.
        /// </summary>
        /// <param name="DNSSRVEndpoint">A DNS SRV Endpoint to compare with.</param>
        public Int32 CompareTo(DNSSRVEndpoint? DNSSRVEndpoint)
        {

            if (DNSSRVEndpoint is null)
                throw new ArgumentNullException(nameof(DNSSRVEndpoint),
                                                "The given DNS SRV Endpoint must not be null!");

            // Priority first and weight second, because that is the order RFC
            // 2782 tries them in: "A client MUST attempt to contact the target
            // host with the lowest-numbered priority it can reach". Sorting a
            // list of endpoints therefore puts them in the order the RFC asks a
            // client to walk, rather than in an order of this type's invention.
            var c = Priority.CompareTo(DNSSRVEndpoint.Priority);

            if (c == 0)
                c = Weight.  CompareTo(DNSSRVEndpoint.Weight);

            // And the rest so that the comparison is total: two endpoints that
            // differ must not compare equal, or every container that orders or
            // de-duplicates them loses one.
            if (c == 0)
                c = String.Compare(Target, DNSSRVEndpoint.Target, StringComparison.OrdinalIgnoreCase);

            if (c == 0)
                c = Port.    CompareTo(DNSSRVEndpoint.Port);

            if (c == 0)
                c = TTL.     CompareTo(DNSSRVEndpoint.TTL);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<DNSSRVEndpoint> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two DNS SRV Endpoints for equality.
        /// </summary>
        /// <param name="Object">A DNS SRV Endpoint to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSSRVEndpoint dnsSRVEndpoint &&
                   Equals(dnsSRVEndpoint);

        #endregion

        #region Equals(DNSSRVEndpoint)

        /// <summary>
        /// Compares two DNS SRV Endpoints for equality.
        /// </summary>
        /// <param name="DNSSRVEndpoint">A DNS SRV Endpoint to compare with.</param>
        public Boolean Equals(DNSSRVEndpoint? DNSSRVEndpoint)

            => DNSSRVEndpoint is not null &&

               // The same seven fields GetHashCode is built from, or two
               // endpoints can share a bucket and still disagree about being
               // the same one.
               String.Equals(Target,
                             DNSSRVEndpoint.Target,
                             StringComparison.OrdinalIgnoreCase) &&

               Priority  == DNSSRVEndpoint.Priority  &&
               Weight    == DNSSRVEndpoint.Weight    &&
               Port      == DNSSRVEndpoint.Port      &&
               TTL       == DNSSRVEndpoint.TTL       &&
               IsHealthy == DNSSRVEndpoint.IsHealthy &&

               ResolvedAddresses.SequenceEqual(DNSSRVEndpoint.ResolvedAddresses);

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   $"Target: {Target}, ",
                   $"Priority: {Priority}, ",
                   $"Weight: {Weight}, ",
                   $"Port: {Port}, ",
                   $"TTL: {TTL.TotalSeconds} seconds, ",
                   $"ResolvedAddresses: [{ResolvedAddresses.AggregateCSV()}], ",
                   $"IsHealthy: {IsHealthy}"
               );

        #endregion

    }

}
