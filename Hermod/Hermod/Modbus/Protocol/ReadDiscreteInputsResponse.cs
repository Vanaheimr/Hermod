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
    /// The Modbus/TCP Read Discrete Inputs response.
    /// 
    /// The discrete inputs in the response message are packed as one input per bit of the data field.
    /// Status is indicated as 1= ON; 0= OFF. The LSB of the first data byte contains the input
    /// addressed in the query. The other inputs follow toward the high order end of this byte, and
    /// from low order to high order in subsequent bytes.
    /// 
    /// If the returned input quantity is not a multiple of eight, the remaining bits in the final data byte
    /// will be padded with zeros (toward the high order end of the byte). The Byte Count field
    /// specifies the quantity of complete bytes of data.
    /// </summary>
    public class ReadDiscreteInputsResponse : ModbusTCPResponse<ReadDiscreteInputsRequest>
    {

        #region Properties

        /// <summary>
        /// The enumeration of discrete inputs.
        /// </summary>
        public IEnumerable<Boolean>  DiscreteInputs    { get; }

        #endregion

        #region Constructor(s)

        #region ReadDiscreteInputsResponse(Request, ResponseBytes)

        /// <summary>
        /// Create a new Modbus/TCP Read Discrete Inputs Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Discrete Inputs request leading to this response.</param>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadDiscreteInputsResponse(ReadDiscreteInputsRequest  Request,
                                          Byte[]                     ResponseBytes,
                                          DateTime?                  ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   ResponseBytes)

        {

            var coils = new List<Boolean>();

            for (var i=0; i<Request.NumberOfInputs; i++)
                coils.Add((ResponseBytes[9 + i / 8] & (1 << (i % 8))) == (1 << (i % 8)));

            this.DiscreteInputs          = coils;

        }

        #endregion

        #region ReadDiscreteInputsResponse(Request, Coils)

        /// <summary>
        /// Create a new Modbus/TCP Read Discrete Inputs Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Discrete Inputs request leading to this response.</param>
        /// <param name="DiscreteInputs">An enumeration of discrete inputs.</param>
        public ReadDiscreteInputsResponse(ReadDiscreteInputsRequest  Request,
                                          UInt16                     TransactionId,
                                          IEnumerable<Boolean>       DiscreteInputs,
                                          UInt16                     ProtocolId          = 0,
                                          Byte                       UnitIdentifier      = 0,
                                          DateTime?                  ResponseTimestamp   = null)

            : base(Request,
                   ResponseTimestamp,
                   TransactionId,
                   ProtocolId,
                   (UInt16) (3 + (DiscreteInputs.Count() / 8) + (DiscreteInputs.Count() % 8 > 0 ? 1 : 0)),
                   FunctionCode.ReadDiscreteInputs,
                   UnitIdentifier,
                   (Byte)       ((DiscreteInputs.Count() / 8) + (DiscreteInputs.Count() % 8 > 0 ? 1 : 0)),
                   new Byte[9 +  (DiscreteInputs.Count() / 8) + (DiscreteInputs.Count() % 8 > 0 ? 1 : 0)])

        {

            this.DiscreteInputs  = DiscreteInputs;
            var discreteInputs   = DiscreteInputs.ToArray();

            for (var i = 0; i < discreteInputs.Length; i++)
            {
                if (discreteInputs[i])
                    EntirePDU[9 + i / 8] |= (Byte) (1 << (i % 8));
            }

        }

        #endregion

        #endregion

    }

}
