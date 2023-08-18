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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for network network service node identifications.
    /// </summary>
    public static class ServiceNodeIdExtensions
    {

        /// <summary>
        /// Indicates whether this network network service node identification is null or empty.
        /// </summary>
        /// <param name="NodeId">A network network service node identification.</param>
        public static Boolean IsNullOrEmpty(this NetworkServiceNode_Id? NodeId)
            => !NodeId.HasValue || NodeId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this network network service node identification is null or empty.
        /// </summary>
        /// <param name="NodeId">A network network service node identification.</param>
        public static Boolean IsNotNullOrEmpty(this NetworkServiceNode_Id? NodeId)
            => NodeId.HasValue && NodeId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a network network service node.
    /// </summary>
    public readonly struct NetworkServiceNode_Id : IId,
                                                   IEquatable <NetworkServiceNode_Id>,
                                                   IComparable<NetworkServiceNode_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the node identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique network service node identification based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a network network service node identification.</param>
        private NetworkServiceNode_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) NewRandom(Length = 50)

        /// <summary>
        /// Create a new random network service node identification.
        /// </summary>
        /// <param name="Length">The expected length of the network service node identification.</param>
        public static NetworkServiceNode_Id NewRandom(Byte Length = 50)

            => new(RandomExtensions.RandomString(Length));

        #endregion

        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a network network service node identification.
        /// </summary>
        /// <param name="Text">A text representation of a network network service node identification.</param>
        public static NetworkServiceNode_Id Parse(String Text)
        {

            if (TryParse(Text, out var networkServiceNodeId))
                return networkServiceNodeId;

            throw new ArgumentException($"Invalid text representation of a network network service node identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a network network service node identification.
        /// </summary>
        /// <param name="Text">A text representation of a network network service node identification.</param>
        public static NetworkServiceNode_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var networkServiceNodeId))
                return networkServiceNodeId;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out NodeId)

        /// <summary>
        /// Parse the given string as a network network service node identification.
        /// </summary>
        /// <param name="Text">A text representation of a network network service node identification.</param>
        /// <param name="NodeId">The parsed network service node identification.</param>
        public static Boolean TryParse(String Text, out NetworkServiceNode_Id NodeId)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    NodeId = new NetworkServiceNode_Id(Text);
                    return true;
                }
                catch
                { }
            }

            NodeId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this network service node identification.
        /// </summary>
        public NetworkServiceNode_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications for equality.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NetworkServiceNode_Id NetworkServiceNodeId1,
                                           NetworkServiceNode_Id NetworkServiceNodeId2)

            => NetworkServiceNodeId1.Equals(NetworkServiceNodeId2);

        #endregion

        #region Operator != (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications for inequality.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NetworkServiceNode_Id NetworkServiceNodeId1,
                                           NetworkServiceNode_Id NetworkServiceNodeId2)

            => !NetworkServiceNodeId1.Equals(NetworkServiceNodeId2);

        #endregion

        #region Operator <  (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NetworkServiceNode_Id NetworkServiceNodeId1,
                                          NetworkServiceNode_Id NetworkServiceNodeId2)

            => NetworkServiceNodeId1.CompareTo(NetworkServiceNodeId2) < 0;

        #endregion

        #region Operator <= (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NetworkServiceNode_Id NetworkServiceNodeId1,
                                           NetworkServiceNode_Id NetworkServiceNodeId2)

            => NetworkServiceNodeId1.CompareTo(NetworkServiceNodeId2) <= 0;

        #endregion

        #region Operator >  (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NetworkServiceNode_Id NetworkServiceNodeId1,
                                          NetworkServiceNode_Id NetworkServiceNodeId2)

            => NetworkServiceNodeId1.CompareTo(NetworkServiceNodeId2) > 0;

        #endregion

        #region Operator >= (NetworkServiceNodeId1, NetworkServiceNodeId2)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="NetworkServiceNodeId1">A network service node identification.</param>
        /// <param name="NetworkServiceNodeId2">Another network service node identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NetworkServiceNode_Id NetworkServiceNodeId1,
                                           NetworkServiceNode_Id NetworkServiceNodeId2)

            => NetworkServiceNodeId1.CompareTo(NetworkServiceNodeId2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="Object">A network service node identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is NetworkServiceNode_Id networkServiceNodeId
                   ? CompareTo(networkServiceNodeId)
                   : throw new ArgumentException("The given object is not a network network service node identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(NodeId)

        /// <summary>
        /// Compares two network service node identifications.
        /// </summary>
        /// <param name="NodeId">A network service node identification to compare with.</param>
        public Int32 CompareTo(NetworkServiceNode_Id NodeId)

            => String.Compare(InternalId,
                              NodeId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two network service node identifications for equality.
        /// </summary>
        /// <param name="Object">A network service node identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is NetworkServiceNode_Id networkServiceNodeId &&
                   Equals(networkServiceNodeId);

        #endregion

        #region Equals(NodeId)

        /// <summary>
        /// Compares two network service node identifications for equality.
        /// </summary>
        /// <param name="NodeId">A network service node identification to compare with.</param>
        public Boolean Equals(NetworkServiceNode_Id NodeId)

            => String.Equals(InternalId,
                             NodeId.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
