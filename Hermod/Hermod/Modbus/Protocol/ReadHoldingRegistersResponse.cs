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

    /// <summary>
    /// The Modbus/TCP Read Holding Registers response.
    /// 
    /// The register data in the response message are packed as two bytes per register, with the
    /// binary contents right justified within each byte. For each register, the first byte contains the
    /// high order bits and the second contains the low order bits.
    /// </summary>
    public class ReadHoldingRegistersResponse : ModbusTCPResponse<ReadHoldingRegistersRequest>
    {

        #region Properties

        /// <summary>
        /// The enumeration of holding registers.
        /// </summary>
        public IEnumerable<UInt16>  HoldingRegisters    { get; }

        #endregion

        #region Constructor(s)

        #region ReadHoldingRegistersResponse(Request, ResponseBytes)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadHoldingRegistersResponse(ReadHoldingRegistersRequest  Request,
                                            Byte[]                       ResponseBytes)

            : base(Request,
                   ResponseBytes)

        {

            var holdingRegisters = new List<UInt16>();

            //for (var i=0; i<Request.NumberOfRegisters; i++)
            //    registers.Add((ResponseBytes[9 + i / 8] & (1 << (i % 8))) == (1 << (i % 8)));

            this.HoldingRegisters  = holdingRegisters;

        }

        #endregion

        #region ReadHoldingRegistersResponse(Request, Coils)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="HoldingRegisters">An enumeration of holding registers.</param>
        public ReadHoldingRegistersResponse(ReadHoldingRegistersRequest  Request,
                                            UInt16                       TransactionId,
                                            IEnumerable<UInt16>          HoldingRegisters,
                                            UInt16                       ProtocolId       = 0,
                                            Byte                         UnitIdentifier   = 0)

            : base(Request,
                   TransactionId,
                   ProtocolId,
                   (UInt16) (3 + (HoldingRegisters.Count() / 8) + (HoldingRegisters.Count() % 8 > 0 ? 1 : 0)),
                   FunctionCode.ReadHoldingRegisters,
                   UnitIdentifier,
                   (Byte)       ((HoldingRegisters.Count() / 8) + (HoldingRegisters.Count() % 8 > 0 ? 1 : 0)),
                   new Byte[9 +  (HoldingRegisters.Count() / 8) + (HoldingRegisters.Count() % 8 > 0 ? 1 : 0)])

        {

            this.HoldingRegisters  = HoldingRegisters;
            var holdingRegisters   = HoldingRegisters.ToArray();

            for (var i = 0; i < holdingRegisters.Length; i++)
            {
                //if (holdingRegisters[i])
                //    EntirePDU[9 + i / 8] |= (Byte) (1 << (i % 8));
            }

        }

        #endregion

        #endregion

    }

}
