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
    /// A Modbus/TCP response.
    /// </summary>
    public class ModbusTCPResponse
    {

        #region Properties

        public ModbusTCPRequest?  Request             { get; }

        public DateTime           Timestamp           { get; }

        public IPSocket?          LocalSocket         { get; internal set; }

        public IPSocket?          RemoteSocket        { get; internal set; }

        /// <summary>
        /// The Modbus/TCP transaction identification.
        /// </summary>
        public UInt16             TransactionId       { get; }

        /// <summary>
        /// The Modbus/TCP protocol identification.
        /// </summary>
        public UInt16             ProtocolId          { get; }

        /// <summary>
        /// The Modbus/TCP PDU length.
        /// </summary>
        public UInt16             Length              { get; }

        /// <summary>
        /// The Modbus/TCP unit/device identification.
        /// </summary>
        public Byte               UnitIdentifier      { get; }

        /// <summary>
        /// The Modbus/TCP function code.
        /// </summary>
        public FunctionCode       FunctionCode        { get; }

        /// <summary>
        /// The number of bytes.
        /// </summary>
        public Byte               NumberOfBytes       { get; }

        public Byte[]             EntirePDU           { get; protected set; }

        #endregion

        #region Constructor(s)

        public ModbusTCPResponse(ModbusTCPRequest?  Request,
                                 DateTime?          ResponseTimestamp,
                                 Byte[]             PDU)
        {

            #region Initial checks

            if (PDU.Length  < 9)
                throw new ArgumentException("The given byte array is too short!",              nameof(PDU));

            if (PDU.Length != System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PDU, 4)) + 6)
                throw new ArgumentException("The length of the given byte array is invalid!",  nameof(PDU));

            var numberOfBytes     = PDU[8];

            if (numberOfBytes % 2 == 1)
                throw new ArgumentException("The length of the given byte array is invalid!",  nameof(PDU));

            if (PDU.Length != numberOfBytes + 9)
                throw new ArgumentException("The length of the given byte array is invalid!",  nameof(PDU));

            #endregion

            this.Request          = Request;
            this.Timestamp        = ResponseTimestamp ?? Illias.Timestamp.Now;
            this.LocalSocket      = Request?.RemoteSocket;
            this.RemoteSocket     = Request?.LocalSocket;
            this.EntirePDU        = PDU;

            this.TransactionId    = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PDU, 0));
            this.ProtocolId       = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PDU, 2));
            this.Length           = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(PDU, 4));
            this.UnitIdentifier   = PDU[6];
            this.FunctionCode     = FunctionCode.TryParseValue(PDU[7]) ?? throw new ArgumentException("Unknown function code '" + PDU[7] + "'!", nameof(Request));
            this.NumberOfBytes    = PDU[8];

        }

        public ModbusTCPResponse(ModbusTCPRequest?  Request,
                                 DateTime?          ResponseTimestamp,
                                 UInt16             TransactionId,
                                 UInt16             ProtocolId,
                                 UInt16             Length,
                                 FunctionCode       FunctionCode,
                                 Byte               UnitIdentifier,
                                 Byte               NumberOfBytes,
                                 Byte[]             PDU)
        {

            this.Request          = Request;
            this.Timestamp        = ResponseTimestamp ?? Illias.Timestamp.Now;
            this.LocalSocket      = Request?.RemoteSocket;
            this.RemoteSocket     = Request?.LocalSocket;
            this.TransactionId    = TransactionId;
            this.ProtocolId       = ProtocolId;
            this.Length           = Length;
            this.UnitIdentifier   = UnitIdentifier;
            this.FunctionCode     = FunctionCode;
            this.NumberOfBytes    = 0;
            this.EntirePDU        = PDU;

            var transactionId     = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) TransactionId));
            var protocolId        = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) ProtocolId));
            var length            = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16) Length));

            this.EntirePDU[0]     = transactionId[0];
            this.EntirePDU[1]     = transactionId[1];
            this.EntirePDU[2]     = protocolId[0];
            this.EntirePDU[3]     = protocolId[1];
            this.EntirePDU[4]     = length[0];
            this.EntirePDU[5]     = length[1];
            this.EntirePDU[6]     = UnitIdentifier;
            this.EntirePDU[7]     = FunctionCode.Value;
            this.EntirePDU[8]     = NumberOfBytes;

        }

        #endregion

    }


    /// <summary>
    /// A generic Modbus/TCP response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the Modbus/TCP request leading to this response.</typeparam>
    public class ModbusTCPResponse<TRequest> : ModbusTCPResponse

        where TRequest: ModbusTCPRequest

    {

        #region Properties

        public new TRequest?  Request    { get; }

        #endregion

        #region Constructor(s)

        public ModbusTCPResponse(TRequest?  Request,
                                 DateTime?  ResponseTimestamp,
                                 Byte[]     PDU)

            : base(Request,
                   ResponseTimestamp,
                   PDU)

        {

            this.Request = Request;

        }

        public ModbusTCPResponse(TRequest?     Request,
                                 DateTime?     ResponseTimestamp,
                                 UInt16        TransactionId,
                                 UInt16        ProtocolId,
                                 UInt16        Length,
                                 FunctionCode  FunctionCode,
                                 Byte          UnitIdentifier,
                                 Byte          NumberOfBytes,
                                 Byte[]        PDU)

            : base(Request,
                   ResponseTimestamp,
                   TransactionId,
                   ProtocolId,
                   Length,
                   FunctionCode,
                   UnitIdentifier,
                   NumberOfBytes,
                   PDU)

        {

            this.Request = Request;

        }

        #endregion


    }

}
