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
    /// A Modbus/TCP Read Coils Response.
    /// </summary>
    public class ReadCoilsResponse : ModbusTCPResponse<ReadCoilsRequest>
    {

        #region Properties

        /// <summary>
        /// The enumeration of coils.
        /// </summary>
        public IEnumerable<Boolean>  Coils    { get; }

        #endregion

        #region Constructor(s)

        #region ReadCoilsResponse(Request, ResponseBytes)

        /// <summary>
        /// Create a new Modbus/TCP Read Coils Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Coils request leading to this response.</param>
        /// <param name="ResponseBytes">The array of bytes to be parsed.</param>
        public ReadCoilsResponse(ReadCoilsRequest  Request,
                                 Byte[]            ResponseBytes)

            : base(Request,
                   ResponseBytes)

        {

            var coils = new List<Boolean>();

            for (var i=0; i<Request.NumberOfCoils; i++)
                coils.Add((ResponseBytes[9 + i / 8] & (1 << (i % 8))) == (1 << (i % 8)));

            this.Coils          = coils;

        }

        #endregion

        #region ReadCoilsResponse(Request, Coils)

        /// <summary>
        /// Create a new Modbus/TCP Read Coils Response.
        /// </summary>
        /// <param name="Request">The Modbus/TCP Read Coils request leading to this response.</param>
        /// <param name="Coils">An enumeration of coils.</param>
        public ReadCoilsResponse(ReadCoilsRequest      Request,
                                 UInt16                TransactionId,
                                 IEnumerable<Boolean>  Coils,
                                 UInt16                ProtocolId       = 0,
                                 Byte                  UnitIdentifier   = 0)

            : base(Request,
                   TransactionId,
                   ProtocolId,
                   (UInt16) (3 + (Coils.Count() / 8) + (Coils.Count() % 8 > 0 ? 1 : 0)),
                   FunctionCode.ReadCoils,
                   UnitIdentifier,
                   (Byte)       ((Coils.Count() / 8) + (Coils.Count() % 8 > 0 ? 1 : 0)),
                   new Byte[9 +  (Coils.Count() / 8) + (Coils.Count() % 8 > 0 ? 1 : 0)])

        {

            this.Coils         = Coils;
            var coils          = Coils.ToArray();

            for (var i = 0; i < coils.Length; i++)
            {
                if (coils[i])
                    EntirePDU[9 + i / 8] |= (Byte) (1 << (i % 8));
            }

        }

        #endregion

        #endregion

    }

}
