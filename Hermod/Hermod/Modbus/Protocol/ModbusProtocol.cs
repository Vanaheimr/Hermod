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

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    public static class ModbusProtocol
    {

        // An exception response from a MODBUS slave (server) will have
        // the high-bit (0x80) set on it's function code.
        const Byte EXCEPTION_BIT = 1 << 7;


        #region CreateGenericHeader  (InvocationId, MessageSize, FunctionCode, UnitIdentifier = 0)

        /// <summary>
        /// Create a new generic Modbus header.
        /// </summary>
        /// <param name="InvocationId">An invocation/transaction identifier.</param>
        /// <param name="MessageSize">The length of the field/the message size.</param>
        /// <param name="FunctionCode">A function code.</param>
        /// <param name="UnitIdentifier">An unit identifier/slave address (255 if not used).</param>
        /// <param name="ProtocolIdentifier">An optional protocol identifier (0 for Modbus/TCP).</param>
        public static MemoryStream CreateGenericHeader(UInt16        InvocationId,
                                                       UInt16        MessageSize,
                                                       FunctionCode  FunctionCode,
                                                       Byte          UnitIdentifier       = 0,
                                                       Byte          ProtocolIdentifier   = 0)
        {

            var memoryStream = new MemoryStream(20);

            memoryStream.WriteWord(InvocationId,       ByteOrder.HostToNetwork);
            memoryStream.WriteWord(ProtocolIdentifier, ByteOrder.HostToNetwork);
            memoryStream.WriteWord(MessageSize,        ByteOrder.HostToNetwork);
            memoryStream.WriteByte(UnitIdentifier);
            memoryStream.WriteByte(FunctionCode.Value);

            return memoryStream;

        }

        #endregion

        #region CreateReadHeader     (TransactionId, UnitIdentifier, FunctionCode, StartAddress, Length)

        /// <summary>
        /// Create a new modbus header for reading data.
        /// </summary>
        /// <param name="TransactionId">An invocation/transaction identifier.</param>
        /// <param name="UnitIdentifier">An device/unit identifier.</param>
        /// <param name="FunctionCode">The function code.</param>
        /// <param name="StartAddress">A start address for reading data.</param>
        /// <param name="Length">The length of the data to read.</param>
        public static Byte[] CreateReadHeader(UInt16        TransactionId,
                                              Byte          UnitIdentifier,
                                              FunctionCode  FunctionCode,
                                              UInt16        StartAddress,
                                              UInt16        Length)
        {

            // The start address within the application starts at 1, but on the network at 0!
            if (StartAddress == 0)
                StartAddress = 1;

            var invocationId        = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) TransactionId));
            var protocolIdentifier  = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) 0));
            var messageSize         = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) 6));
            var startAddress        = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) StartAddress - 1));
            var length              = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) Length));

            var header = new Byte[12];

            header[0]  = invocationId[0];        // high byte
            header[1]  = invocationId[1];        // low  byte

            header[2]  = protocolIdentifier[0];  // high byte
            header[3]  = protocolIdentifier[1];  // low  byte

            header[4]  = messageSize[0];         // high byte
            header[5]  = messageSize[1];         // low  byte

            header[6]  = UnitIdentifier;

            header[7]  = FunctionCode.Value;

            header[8]  = startAddress[0];        // high byte
            header[9]  = startAddress[1];        // low  byte

            header[10] = length[0];             // high byte
            header[11] = length[1];             // low  byte

            return header;

        }

        #endregion

        #region CreateWriteHeader    (InvocationId, StartAddress, numData, numBytes, FunctionCode)

        /// <summary>
        /// Create a new modbus header for writing data.
        /// </summary>
        /// <param name="InvocationId">An invocation/transaction identifier.</param>
        /// <param name="StartAddress">A start address for reading data.</param>
        /// <param name="numData"></param>
        /// <param name="numBytes"></param>
        /// <param name="FunctionCode">The function code.</param>
        public static MemoryStream CreateWriteHeader(UInt16        InvocationId,
                                                     UInt16        StartAddress,
                                                     UInt16        numData,
                                                     Byte          numBytes,
                                                     FunctionCode  FunctionCode)
        {

            var header = CreateGenericHeader(InvocationId,
                                             (UInt16) (numBytes + 5),
                                             FunctionCode);

            header.WriteWord(StartAddress, ByteOrder.HostToNetwork);

            if (FunctionCode.Value >= 15) // >= FunctionCode.WriteMultipleCoils
            {
                header.WriteWord(numData, ByteOrder.HostToNetwork);
                header.WriteByte((Byte) (numBytes - 2));
            }

            return header;

        }

        #endregion

        #region CreateReadWriteHeader(InvocationId, ReadStartAddress, ReadLength, WriteStartAddress, WriteLength)

        /// <summary>
        /// Create a new modbus header for reading and writing data.
        /// </summary>
        /// <param name="InvocationId"></param>
        /// <param name="ReadStartAddress"></param>
        /// <param name="ReadLength"></param>
        /// <param name="WriteStartAddress"></param>
        /// <param name="WriteLength"></param>
        public static MemoryStream CreateReadWriteHeader(UInt16  InvocationId,
                                                         UInt16  ReadStartAddress,
                                                         UInt16  ReadLength,
                                                         UInt16  WriteStartAddress,
                                                         UInt16  WriteLength)
        {

            var header = CreateGenericHeader(InvocationId,
                                             (UInt16) (11 + WriteLength * 2),
                                             FunctionCode.ReadWriteMultipleRegister);

            header.WriteWord(ReadStartAddress,  ByteOrder.HostToNetwork);
            header.WriteWord(ReadLength,           ByteOrder.HostToNetwork);
            header.WriteWord(WriteStartAddress, ByteOrder.HostToNetwork);
            header.WriteWord(WriteLength,          ByteOrder.HostToNetwork);
            header.WriteByte((Byte) (WriteLength * 2));

            return header;

        }

        #endregion

    }

}
