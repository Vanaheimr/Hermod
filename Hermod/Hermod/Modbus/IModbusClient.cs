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
    /// The common interface of a Modbus client.
    /// </summary>
    public interface IModbusClient : IModbusDevice
    {

        //Task<ModbusTCPResponse> ReadCoils           (UInt16 StartAddress, UInt16 NumberOfCoils);
        //Task<Byte[]> ReadDiscreteInputs  (UInt16 StartAddress, UInt16 NumberOfInputs);
        //Task<Byte[]> ReadHoldingRegister (UInt16 StartAddress, UInt16 NumberOfInputs);
        //Task<Byte[]> ReadInputRegister   (UInt16 StartAddress, UInt16 NumberOfInputs);

        //Task<Byte[]> ReadWriteMultipleRegister(UInt16 ReadStartAddress, UInt16 NumberOfInputs, UInt16 WriteStartAddress, Byte[] Values);

        //Task<Byte[]> WriteMultipleCoils       (UInt16 StartAddress,     UInt16 NumberOfBits, Byte[] Values);
        //Task<Byte[]> WriteMultipleRegister    (UInt16 StartAddress,     Byte[] Values);

        //Task<Byte[]> WriteSingleCoils         (UInt16 StartAddress,     Boolean OnOff);
        //Task<Byte[]> WriteSingleRegister      (UInt16 StartAddress,     Byte[] Values);


    }

}
