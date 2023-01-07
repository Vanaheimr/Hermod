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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// The Modbus/TCP Read Holding Registers response.
    /// 
    /// The register data in the response message are packed as two bytes per register, with the
    /// binary contents right justified within each byte. For each register, the first byte contains the
    /// high order bits and the second contains the low order bits.
    /// </summary>
    public class ReadHoldingRegistersResponse : ModbusTCPResponse<ReadHoldingRegistersRequest>,
                                                IEquatable<ReadHoldingRegistersResponse>
    {

        #region Properties

        /// <summary>
        /// The enumeration of holding registers.
        /// </summary>
        [Mandatory]
        public IEnumerable<UInt16>  HoldingRegisters    { get; }

        #endregion

        #region Constructor(s)

        #region ReadHoldingRegistersResponse(ResponseBytes, ResponseTimestamp = null)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers response.
        /// </summary>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadHoldingRegistersResponse(Byte[]     ResponseBytes,
                                            DateTime?  ResponseTimestamp   = null)

            : this(null,
                   ResponseBytes,
                   ResponseTimestamp)

        { }

        #endregion

        #region ReadHoldingRegistersResponse(Request, ResponseBytes, ResponseTimestamp = null)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadHoldingRegistersResponse(ReadHoldingRegistersRequest?  Request,
                                            Byte[]                        ResponseBytes,
                                            DateTime?                     ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   ResponseBytes)

        {

            #region Initial checks

            if (ResponseBytes[7] != 3)
                throw new ArgumentException("The given Modbus function code is invalid!", nameof(ResponseBytes));

            #endregion

            var holdingRegisters   = new List<UInt16>(NumberOfBytes / 2);

            for (var i = 0; i < NumberOfBytes / 2; i++)
                holdingRegisters.Add((UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(ResponseBytes, i * 2 + 9)));

            this.HoldingRegisters  = holdingRegisters;

        }

        #endregion

        #region ReadHoldingRegistersResponse(Request, TransactionId, HoldingRegisters, ProtocolId = null, UnitIdentifier = null, ResponseTimestamp = null)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="HoldingRegisters">An enumeration of holding registers.</param>
        public ReadHoldingRegistersResponse(ReadHoldingRegistersRequest  Request,
                                            UInt16                       TransactionId,
                                            IEnumerable<UInt16>          HoldingRegisters,
                                            UInt16?                      ProtocolId          = null,
                                            Byte?                        UnitIdentifier      = null,
                                            DateTime?                    ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   TransactionId,
                   ProtocolId     ?? 0,
                   (UInt16) (2 * HoldingRegisters.Count() + 3),
                   FunctionCode.ReadHoldingRegisters,
                   UnitIdentifier ?? 0,
                   (Byte)   (2 * HoldingRegisters.Count()),
                   new Byte[2 * HoldingRegisters.Count()])

        {

            this.HoldingRegisters  = HoldingRegisters;

            var i = 0;

            foreach (var holdingRegister in HoldingRegisters)
                Array.Copy(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) holdingRegister)),
                           0,
                           EntirePDU,
                           (i++) * 2 + 9,
                           2);

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (ReadHoldingRegistersResponse1, ReadHoldingRegistersResponse2)

        /// <summary>
        /// Compares two read holding registers responses for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersResponse1">A read holding registers response.</param>
        /// <param name="ReadHoldingRegistersResponse2">Another read holding registers response.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (ReadHoldingRegistersResponse ReadHoldingRegistersResponse1,
                                           ReadHoldingRegistersResponse ReadHoldingRegistersResponse2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(ReadHoldingRegistersResponse1, ReadHoldingRegistersResponse2))
                return true;

            // If one is null, but not both, return false.
            if (ReadHoldingRegistersResponse1 is null || ReadHoldingRegistersResponse2 is null)
                return false;

            return ReadHoldingRegistersResponse1.Equals(ReadHoldingRegistersResponse2);

        }

        #endregion

        #region Operator != (ReadHoldingRegistersResponse1, ReadHoldingRegistersResponse2)

        /// <summary>
        /// Compares two read holding registers responses for inequality.
        /// </summary>
        /// <param name="ReadHoldingRegistersResponse1">A read holding registers response.</param>
        /// <param name="ReadHoldingRegistersResponse2">Another read holding registers response.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (ReadHoldingRegistersResponse ReadHoldingRegistersResponse1,
                                           ReadHoldingRegistersResponse ReadHoldingRegistersResponse2)

            => !(ReadHoldingRegistersResponse1 == ReadHoldingRegistersResponse2);

        #endregion

        #endregion

        #region IEquatable<ReadHoldingRegistersResponse> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two read holding registers responses for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersResponse">A read holding registers response to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ReadHoldingRegistersResponse readHoldingRegistersResponse &&
                   Equals(readHoldingRegistersResponse);

        #endregion

        #region Equals(ReadHoldingRegistersResponse)

        /// <summary>
        /// Compares two read holding registers responses for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersResponse">A read holding registers response to compare with.</param>
        public Boolean Equals(ReadHoldingRegistersResponse? ReadHoldingRegistersResponse)

            => ReadHoldingRegistersResponse is not null &&

               HoldingRegisters.Count().Equals(ReadHoldingRegistersResponse.HoldingRegisters.Count()) &&
               HoldingRegisters.Any(holdingRegister => ReadHoldingRegistersResponse.HoldingRegisters.Contains(holdingRegister)) &&

               base.Equals(ReadHoldingRegistersResponse);

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

                return HoldingRegisters.CalcHashCode() * 3 ^
                       base.            GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   HoldingRegisters.Count(),
                   " register(s)"

               );

        #endregion

    }

}
