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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for DNS Service Specification identifications.
    /// </summary>
    public static class SRVSpecExtensions
    {

        /// <summary>
        /// Indicates whether this DNS Service Specification identifications is null or empty.
        /// </summary>
        /// <param name="APIKey">An DNS Service Specification identifications.</param>
        public static Boolean IsNullOrEmpty(this SRV_Spec? APIKey)
            => !APIKey.HasValue || APIKey.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this DNS Service Specification identifications is NOT null or empty.
        /// </summary>
        /// <param name="APIKey">An DNS Service Specification identifications.</param>
        public static Boolean IsNotNullOrEmpty(this SRV_Spec? APIKey)
            => APIKey.HasValue && APIKey.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// An DNS Service Specification.
    /// </summary>
    public readonly struct SRV_Spec : IEquatable<SRV_Spec>,
                                      IComparable<SRV_Spec>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId1;

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId2;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId1.IsNullOrEmpty() || InternalId2.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId1.IsNotNullOrEmpty() && InternalId2.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the DNS Service Specification identifier.
        /// </summary>
        public UInt64 Length
            => (UInt64) ((InternalId1?.Length ?? 0) + (InternalId2?.Length ?? 0));

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNS Service Specification identification based on the given string.
        /// </summary>
        private SRV_Spec(String  Service,
                         String  Protocol)
        {

            InternalId1  = Service;
            InternalId2  = Protocol;

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Text">A text representation of an DNS Service Specification identification.</param>
        public static SRV_Spec Parse(String  Service,
                                     String  Protocol)
        {

            if (TryParse(Service, Protocol, out SRV_Spec apiKeyId))
                return apiKeyId;

            throw new ArgumentException($"Invalid text representation of an DNS Service Specification identification: '{Service}'.'{Protocol}'!");

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Text">A text representation of an DNS Service Specification identification.</param>
        public static SRV_Spec? TryParse(String  Service,
                                         String  Protocol)
        {

            if (TryParse(Service, Protocol, out SRV_Spec apiKeyId))
                return apiKeyId;

            return null;

        }

        #endregion

        #region TryParse(Text, out SRVSpec)

        /// <summary>
        /// Try to parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Text">A text representation of an DNS Service Specification identification.</param>
        /// <param name="SRVSpec">The parsed DNS Service Specification identification.</param>
        public static Boolean TryParse(String        Service,
                                       String        Protocol,
                                       out SRV_Spec  SRVSpec)
        {

            Service   = Service. Trim().TrimStart('_');
            Protocol  = Protocol.Trim().TrimStart('_');

            if (Service.IsNotNullOrEmpty() && Protocol.IsNotNullOrEmpty())
            {
                try
                {
                    SRVSpec = new SRV_Spec(Service, Protocol);
                    return true;
                }
                catch
                { }
            }

            SRVSpec = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this DNS Service Specification identification.
        /// </summary>
        public SRV_Spec Clone

            => new (
                   InternalId1.CloneString(),
                   InternalId2.CloneString()
               );

        #endregion



        #region UDP   (Service)

        /// <summary>
        /// Parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Service">A text representation of an DNS Service Specification identification.</param>
        public static SRV_Spec UDP(String Service)
        {

            if (TryParse(Service, "udp", out var apiKeyId))
                return apiKeyId;

            throw new ArgumentException($"Invalid text representation of an DNS Service Specification identification: '{Service}'!");

        }

        #endregion

        #region TCP   (Service)

        /// <summary>
        /// Parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Service">A text representation of an DNS Service Specification identification.</param>
        public static SRV_Spec TCP(String Service)
        {

            if (TryParse(Service, "tcp", out var apiKeyId))
                return apiKeyId;

            throw new ArgumentException($"Invalid text representation of an DNS Service Specification identification: '{Service}'!");

        }

        #endregion

        #region TLS   (Service)

        /// <summary>
        /// Parse the given string as an DNS Service Specification identification.
        /// </summary>
        /// <param name="Service">A text representation of an DNS Service Specification identification.</param>
        public static SRV_Spec TLS(String Service)
        {

            if (TryParse(Service, "tls", out var apiKeyId))
                return apiKeyId;

            throw new ArgumentException($"Invalid text representation of an DNS Service Specification identification: '{Service}'!");

        }

        #endregion









        #region Operator overloading

        #region Operator == (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SRV_Spec SRVSpec1,
                                           SRV_Spec SRVSpec2)

            => SRVSpec1.Equals(SRVSpec2);

        #endregion

        #region Operator != (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SRV_Spec SRVSpec1,
                                           SRV_Spec SRVSpec2)

            => !SRVSpec1.Equals(SRVSpec2);

        #endregion

        #region Operator <  (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SRV_Spec SRVSpec1,
                                          SRV_Spec SRVSpec2)

            => SRVSpec1.CompareTo(SRVSpec2) < 0;

        #endregion

        #region Operator <= (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SRV_Spec SRVSpec1,
                                           SRV_Spec SRVSpec2)

            => SRVSpec1.CompareTo(SRVSpec2) <= 0;

        #endregion

        #region Operator >  (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (SRV_Spec SRVSpec1,
                                          SRV_Spec SRVSpec2)

            => SRVSpec1.CompareTo(SRVSpec2) > 0;

        #endregion

        #region Operator >= (SRVSpec1, SRVSpec2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec1">An DNS Service Specification identification.</param>
        /// <param name="SRVSpec2">Another DNS Service Specification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SRV_Spec SRVSpec1,
                                           SRV_Spec SRVSpec2)

            => SRVSpec1.CompareTo(SRVSpec2) >= 0;

        #endregion

        #endregion

        #region IComparable<SRV_Spec> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is SRV_Spec apiKeyId
                   ? CompareTo(apiKeyId)
                   : throw new ArgumentException("The given object is not an DNS Service Specification identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(SRVSpec)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SRVSpec">An object to compare with.</param>
        public Int32 CompareTo(SRV_Spec SRVSpec)
        {

            var c = String.Compare(InternalId1,
                                   SRVSpec.InternalId1,
                                   StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(InternalId2,
                                   SRVSpec.InternalId2,
                                   StringComparison.Ordinal);

            return c;

        }

        #endregion

            #endregion

        #region IEquatable<SRV_Spec> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is SRV_Spec apiKeyId &&
                   Equals(apiKeyId);

        #endregion

        #region Equals(SRVSpec)

        /// <summary>
        /// Compares two DNS Service Specification identifications for equality.
        /// </summary>
        /// <param name="SRVSpec">An DNS Service Specification identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(SRV_Spec SRVSpec)

            => String.Equals(InternalId1,
                             SRVSpec.InternalId1,
                             StringComparison.Ordinal) &&

               String.Equals(InternalId2,
                             SRVSpec.InternalId2,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => (InternalId1?.GetHashCode() ?? 0) ^
               (InternalId2?.GetHashCode() ?? 0) * 3;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"_{InternalId1 ?? ""}._{InternalId2 ?? ""}";

        #endregion

    }

}
