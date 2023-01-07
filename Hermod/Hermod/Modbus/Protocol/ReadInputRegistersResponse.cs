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
    /// The Modbus/TCP Read Input Registers response.
    /// 
    /// The register data in the response message are packed as two bytes per register, with the
    /// binary contents right justified within each byte. For each register, the first byte contains the
    /// high order bits and the second contains the low order bits.
    /// </summary>
    public class ReadInputRegistersResponse : ModbusTCPResponse<ReadInputRegistersRequest>
    {

        #region Properties

        /// <summary>
        /// The enumeration of input registers.
        /// </summary>
        public IEnumerable<UInt16>  InputRegisters    { get; }

        #endregion

        #region Constructor(s)

        #region ReadInputRegistersResponse(Request, ResponseBytes)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadInputRegistersResponse(ReadInputRegistersRequest  Request,
                                          Byte[]                     ResponseBytes,
                                          DateTime?                  ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   ResponseBytes)

        {

            var inputRegisters = new List<UInt16>();

            //for (var i=0; i<Request.NumberOfRegisters; i++)
            //    registers.Add((ResponseBytes[9 + i / 8] & (1 << (i % 8))) == (1 << (i % 8)));

            this.InputRegisters  = inputRegisters;

        }

        #endregion

        #region ReadInputRegistersResponse(Request, Coils)

        /// <summary>
        /// Create a new Modbus/TCP Read Holding Registers Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Holding Registers request leading to this response.</param>
        /// <param name="InputRegisters">An enumeration of input registers.</param>
        public ReadInputRegistersResponse(ReadInputRegistersRequest  Request,
                                          UInt16                     TransactionId,
                                          IEnumerable<UInt16>        InputRegisters,
                                          UInt16                     ProtocolId          = 0,
                                          Byte                       UnitIdentifier      = 0,
                                          DateTime?                  ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   TransactionId,
                   ProtocolId,
                   (UInt16) (3 + (InputRegisters.Count() / 8) + (InputRegisters.Count() % 8 > 0 ? 1 : 0)),
                   FunctionCode.ReadInputRegisters,
                   UnitIdentifier,
                   (Byte)       ((InputRegisters.Count() / 8) + (InputRegisters.Count() % 8 > 0 ? 1 : 0)),
                   new Byte[9 +  (InputRegisters.Count() / 8) + (InputRegisters.Count() % 8 > 0 ? 1 : 0)])

        {

            this.InputRegisters  = InputRegisters;
            var inputRegisters   = InputRegisters.ToArray();

            for (var i = 0; i < inputRegisters.Length; i++)
            {
                //if (inputRegisters[i])
                //    EntirePDU[9 + i / 8] |= (Byte) (1 << (i % 8));
            }

        }

        #endregion

        #endregion

    }

}
