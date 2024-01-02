/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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
    /// Represents a Modbus frame/packet for querying.
    /// The packet can be serialized as Modbus RTU or TCP frame/packet.
    /// </summary>
    public struct ModbusPacket
    {

        #region Data

        /// <summary>
        /// The logical address of the Modbus slave.
        /// </summary>
        public readonly Byte          UnitAddress;

        /// <summary>
        /// The Modbus function code.
        /// </summary>
        public readonly FunctionCode  FunctionCode;

        /// <summary>
        /// The Modbus address of the first register to be queried.
        /// </summary>
        public readonly UInt16        StartingAddress;

        /// <summary>
        /// The number of Modbus registers (16 bit) to be queried.
        /// </summary>
        public readonly UInt16        NumberOfRegisters;

        /// <summary>
        /// An optional transaction identification for Modbus TCP requests.
        /// </summary>
        public readonly Int16         TransactionId;

        #endregion

        #region Properties

        #region RTUFormat

        /// <summary>
        /// The Modbus packet as Modbus RTU frame.
        /// </summary>
        public Byte[] RTUFormat
        {
            get
            {

                var tx_data = new Byte[8];
                tx_data[0] = UnitAddress;
                tx_data[1] = FunctionCode.Value;
                tx_data[2] = (Byte) (StartingAddress   >> 8);
                tx_data[3] = (Byte)  StartingAddress;
                tx_data[4] = (Byte) (NumberOfRegisters >> 8);
                tx_data[5] = (Byte)  NumberOfRegisters;

                // Calculate the CRC value of the Modbus RTU frame
                // (from tx_data[0] to tx_data[5])
                var _CRC = CRC16.GetCRC16(tx_data);
                tx_data[6] = _CRC[0];
                tx_data[7] = _CRC[1];

                return tx_data;

            }
        }

        #endregion

        #region TCPIPFormat

        /// <summary>
        /// The Modbus packet as Modbus TCP packet.
        /// </summary>
        public Byte[] TCPIPFormat
        {
            get
            {

                var tx_data = new Byte[12] { // TransactionId
                                             0x00, 0x01,

                                             // ProtocolId (always zero)
                                             0x00, 0x00,

                                             // Length of frame
                                             0x00, 0x06,

                                             // Unit/device address
                                             0x01,

                                             // Function code
                                             0x03,

                                             // Starting address
                                             0x00, 0x09,

                                             // Number of registers
                                             0x00, 0x04 };

                var _transactionId = BitConverter.GetBytes(TransactionId);
                tx_data[0]  = _transactionId[1];
                tx_data[1]  = _transactionId[0];

                tx_data[6]  = UnitAddress;

                tx_data[7]  = FunctionCode.Value;

                var startingAddress = BitConverter.GetBytes(StartingAddress);
                tx_data[8]  = startingAddress[1];
                tx_data[9]  = startingAddress[0];

                var numberOfRegisters = BitConverter.GetBytes(NumberOfRegisters);
                tx_data[10] = numberOfRegisters[1];
                tx_data[11] = numberOfRegisters[0];

                return tx_data;

            }
        }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new Modbus frame/packet for querying.
        /// The packet can be serialized as Modbus RTU or TCP frame/packet.
        /// </summary>
        /// <param name="NumberOfRegisters">The number of Modbus registers (16 bit) to be queried.</param>
        /// <param name="UnitAddress">The logical address of the Modbus slave.</param>
        /// <param name="FunctionCode">The Modbus function code.</param>
        /// <param name="StartingAddress">The Modbus address of the first register to be queried.</param>
        /// <param name="TransactionId">An optional transaction identification for Modbus TCP requests.</param>
        public ModbusPacket(UInt16        NumberOfRegisters,
                            Byte          UnitAddress,
                            FunctionCode  FunctionCode,
                            UInt16        StartingAddress,
                            Int16         TransactionId = 1)
        {
            this.NumberOfRegisters  = NumberOfRegisters;
            this.UnitAddress        = UnitAddress;
            this.FunctionCode       = FunctionCode;
            this.StartingAddress    = StartingAddress;
            this.TransactionId      = TransactionId;
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return (TransactionId != 1) ? "TransactionId: + "   + TransactionId + ", " : "" +
                                          "NumberOfRegisters: " + NumberOfRegisters +
                                          ", UnitAddress: "    + UnitAddress +
                                          ", FunctionCode: "    + FunctionCode +
                                          ", DataAddress: "     + StartingAddress;
        }

        #endregion

    }

}
