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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// The Modbus/TCP Read Holding Registers request.
    /// 
    /// This function code is used to read the contents of a contiguous block of holding registers in a
    /// remote device. The Request PDU specifies the starting register address and the number of
    /// registers. In the PDU Registers are addressed starting at zero. Therefore registers numbered
    /// 1-16 are addressed as 0-15.
    /// </summary>
    public class ReadHoldingRegistersRequest : ModbusTCPRequest,
                                               IEquatable<ReadHoldingRegistersRequest>
    {

        #region Properties

        /// <summary>
        /// The starting address.
        /// </summary>
        [Mandatory]
        public UInt16  StartingAddress      { get; }

        /// <summary>
        /// The number of holding registers to read.
        /// </summary>
        [Mandatory]
        public UInt16  NumberOfRegisters    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Read holding registers.
        /// </summary>
        /// <param name="ModbusClient">A Modbus/TCP client.</param>
        /// <param name="TransactionId">A transaction identifier.</param>
        /// <param name="StartingAddress">The starting address.</param>
        /// <param name="NumberOfRegisters">The number of holding registers to read.</param>
        /// <param name="UnitIdentifier">An optional device/unit identifier.</param>
        /// <param name="ProtocolIdentifier">An optional protocol identifier.</param>
        public ReadHoldingRegistersRequest(ModbusTCPClient  ModbusClient,
                                           UInt16           TransactionId,
                                           UInt16           StartingAddress,
                                           UInt16           NumberOfRegisters,
                                           Byte?            UnitIdentifier       = null,
                                           UInt16?          ProtocolIdentifier   = null)

            : base(ModbusClient,
                   TransactionId,
                   FunctionCode.ReadHoldingRegisters,
                   StartingAddress,
                   NumberOfRegisters,
                   UnitIdentifier,
                   ProtocolIdentifier)

        {

            this.StartingAddress    = StartingAddress;
            this.NumberOfRegisters  = NumberOfRegisters;

        }

        #endregion


        #region Operator overloading

        #region Operator == (ReadHoldingRegistersRequest1, ReadHoldingRegistersRequest2)

        /// <summary>
        /// Compares two read holding registers requests for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersRequest1">A read holding registers request.</param>
        /// <param name="ReadHoldingRegistersRequest2">Another read holding registers request.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (ReadHoldingRegistersRequest ReadHoldingRegistersRequest1,
                                           ReadHoldingRegistersRequest ReadHoldingRegistersRequest2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(ReadHoldingRegistersRequest1, ReadHoldingRegistersRequest2))
                return true;

            // If one is null, but not both, return false.
            if (ReadHoldingRegistersRequest1 is null || ReadHoldingRegistersRequest2 is null)
                return false;

            return ReadHoldingRegistersRequest1.Equals(ReadHoldingRegistersRequest2);

        }

        #endregion

        #region Operator != (ReadHoldingRegistersRequest1, ReadHoldingRegistersRequest2)

        /// <summary>
        /// Compares two read holding registers requests for inequality.
        /// </summary>
        /// <param name="ReadHoldingRegistersRequest1">A read holding registers request.</param>
        /// <param name="ReadHoldingRegistersRequest2">Another read holding registers request.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (ReadHoldingRegistersRequest ReadHoldingRegistersRequest1,
                                           ReadHoldingRegistersRequest ReadHoldingRegistersRequest2)

            => !(ReadHoldingRegistersRequest1 == ReadHoldingRegistersRequest2);

        #endregion

        #endregion

        #region IEquatable<ReadHoldingRegistersRequest> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two read holding registers requests for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersRequest">A read holding registers request to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ReadHoldingRegistersRequest readHoldingRegistersRequest &&
                   Equals(readHoldingRegistersRequest);

        #endregion

        #region Equals(ReadHoldingRegistersRequest)

        /// <summary>
        /// Compares two read holding registers requests for equality.
        /// </summary>
        /// <param name="ReadHoldingRegistersRequest">A read holding registers request to compare with.</param>
        public Boolean Equals(ReadHoldingRegistersRequest? ReadHoldingRegistersRequest)

            => ReadHoldingRegistersRequest is not null &&

               StartingAddress.  Equals(ReadHoldingRegistersRequest.StartingAddress)   &&
               NumberOfRegisters.Equals(ReadHoldingRegistersRequest.NumberOfRegisters) &&

               base.Equals(ReadHoldingRegistersRequest);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return StartingAddress.  GetHashCode() * 5 ^
                       NumberOfRegisters.GetHashCode() * 3 ^

                       base.             GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   StartingAddress,
                   ", ",
                   NumberOfRegisters,
                   " register(s)"

               );

        #endregion

    }

}
