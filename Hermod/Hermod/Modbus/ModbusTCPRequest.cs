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

        public Byte[]           EntirePDU       { get; internal set; }

        #endregion

        #region Constructor(s)

        public ModbusTCPRequest(ModbusTCPClient  ModbusClient,
                                UInt16           TransactionId,
                                FunctionCode     FunctionCode,
                                Byte[]           EntirePDU)
        {

            this.ModbusClient   = ModbusClient;
            this.Timestamp      = Illias.Timestamp.Now;
            this.TransactionId  = TransactionId;
            this.FunctionCode   = FunctionCode;
            this.EntirePDU      = EntirePDU;

        }

        #endregion

    }

}
