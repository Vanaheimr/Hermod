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

using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf
    /// https://modbus.org/docs/Modbus_Messaging_Implementation_Guide_V1_0b.pdf
    /// </summary>
    public class ModbusTCPRequest
    {

        #region Properties

        /// <summary>
        /// The Modbus/TCP client.
        /// </summary>
        public ModbusTCPClient  ModbusClient    { get; }

        /// <summary>
        /// The local TCP/IP socket used.
        /// </summary>
        public IPSocket         LocalSocket     { get; internal set; }

        /// <summary>
        /// The remote TCP/IP socket used.
        /// </summary>
        public IPSocket         RemoteSocket    { get; internal set; }

        /// <summary>
        /// The request timestamp.
        /// </summary>
        public DateTime         Timestamp       { get; }

        /// <summary>
        /// The Modbus/TCP transaction identification.
        /// </summary>
        public UInt16           TransactionId    { get; }

        /// <summary>
        /// The Modbus/TCP protocol identification.
        /// </summary>
        public UInt16           ProtocolId       { get; }

        /// <summary>
        /// The Modbus/TCP function code.
        /// </summary>
        public FunctionCode     FunctionCode    { get; }

        /// <summary>
        /// The Modbus/TCP unit identification.
        /// </summary>
        public Byte             UnitId          { get; }

        public Byte[]           EntirePDU       { get; internal set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create Read Header
        /// </summary>
        /// <param name="ModbusClient">A Modbus/TCP client.</param>
        /// <param name="TransactionId">A transaction identifier.</param>
        /// <param name="FunctionCode"></param>
        /// <param name="StartAddress">The starting address.</param>
        /// <param name="Length"></param>
        /// <param name="UnitId">An optional device/unit identifier.</param>
        /// <param name="ProtocolId">An optional protocol identifier.</param>
        public ModbusTCPRequest(ModbusTCPClient  ModbusClient,
                                UInt16           TransactionId,
                                FunctionCode     FunctionCode,
                                UInt16           StartAddress,
                                UInt16           Length,
                                Byte?            UnitId       = 0,
                                UInt16?          ProtocolId   = 0)

        {

            this.ModbusClient       = ModbusClient;
            this.Timestamp          = Illias.Timestamp.Now;
            this.TransactionId      = TransactionId;
            this.ProtocolId         = ProtocolId ?? 0;
            this.FunctionCode       = FunctionCode;
            this.UnitId             = UnitId     ?? 0;

            var invocationId        = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)  TransactionId));
            var protocolIdentifier  = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) (ProtocolId ?? 0)));
            var messageSize         = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)  6));
            var startAddress        = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) (StartAddress - 1)));
            var length              = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)  Length));

            this.EntirePDU          = new Byte[12];

            this.EntirePDU[0]       = invocationId[0];        // high byte
            this.EntirePDU[1]       = invocationId[1];        // low  byte

            this.EntirePDU[2]       = protocolIdentifier[0];  // high byte
            this.EntirePDU[3]       = protocolIdentifier[1];  // low  byte

            this.EntirePDU[4]       = messageSize[0];         // high byte
            this.EntirePDU[5]       = messageSize[1];         // low  byte

            this.EntirePDU[6]       = UnitId ?? 0;

            this.EntirePDU[7]       = FunctionCode.Value;

            this.EntirePDU[8]       = startAddress[0];        // high byte
            this.EntirePDU[9]       = startAddress[1];        // low  byte

            this.EntirePDU[10]      = length[0];              // high byte
            this.EntirePDU[11]      = length[1];              // low  byte

        }

        #endregion

    }

}
