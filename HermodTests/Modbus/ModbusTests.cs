/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Net.Sockets;

using NUnit.Framework;
using NUnit.Framework.Legacy;

using org.GraphDefined.Vanaheimr.Hermod.Modbus;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus
{

    public class ModbusTests
    {

        #region Data

        private ModbusTCPServer?  ModbusTCPServer;
        //private TcpClient         TCPClient;
        //private NetworkStream     TCPStream;

        #endregion


        #region SetupEachTest()

        [SetUp]
        public virtual void SetupEachTest()
        {

            ModbusTCPServer = new ModbusTCPServer(
                                  TCPPort:   IPPort.Parse(24694),
                                  AutoStart: true
                              );

        }

        #endregion

        #region ShutdownEachTest()

        [TearDown]
        public virtual void ShutdownEachTest()
        {
            ModbusTCPServer?.Shutdown();
            ModbusTCPServer = null;
        }

        #endregion


        #region RAWTest1()

        [Test]
        public async Task RAWTest1()
        {

            ClassicAssert.IsNotNull(ModbusTCPServer);

            if (ModbusTCPServer is null)
                return;

            ReadHoldingRegistersRequest? readHoldingRegistersRequest1 = null;
            ReadHoldingRegistersRequest? readHoldingRegistersRequest2 = null;

            ModbusTCPServer.OnReadHoldingRegistersRequest += (timestamp,
                                                              modbusTCPServer,
                                                              remoteSocket,
                                                              connectionId,
                                                              request) => {

                readHoldingRegistersRequest1 = request;

                return Task.CompletedTask;

            };

            ModbusTCPServer.OnReadHoldingRegisters += (timestamp,
                                                       modbusTCPServer,
                                                       remoteSocket,
                                                       connectionId,
                                                       request) => {

                readHoldingRegistersRequest2 = request;

                return Task.FromResult(
                           new ReadHoldingRegistersResponse(request,
                               new Byte[17] {

                                   // Transaction identification
                                   request.EntirePDU[0], request.EntirePDU[1],

                                   // Protocol identification (always zero)
                                   request.EntirePDU[2], request.EntirePDU[3],

                                   // Length of frame
                                   0x00, 0x0B,

                                   // Unit address
                                   request.EntirePDU[6],

                                   // Function code
                                   request.EntirePDU[7],

                                   // Number of bytes
                                   0x08,

                                   0x00, 0x01,
                                   0x00, 0x02,
                                   0x00, 0x03,
                                   0x00, 0x04

                               }
                           )
                       );

            };

            var tcpClient = new TcpClient("127.0.0.1", 24694);
            var tcpStream = tcpClient.GetStream();

            await tcpStream.WriteAsync(new Byte[12] {

                                           // Transaction identification
                                           0x00, 0x01,

                                           // Protocol identification (always zero)
                                           0x00, 0x00,

                                           // Length of frame
                                           0x00, 0x06,

                                           // Unit address
                                           0x01,

                                           // Function code
                                           0x03,

                                           // Starting address
                                           0x00, 0x09,

                                           // Number of registers
                                           0x00, 0x04

                                       });

            await tcpStream.FlushAsync();


            //await Task.Delay(1000);

            var buffer    = new Byte[5000];
            var read      = tcpStream.Read(buffer, 0, buffer.Length);

            ClassicAssert.IsNotNull(readHoldingRegistersRequest1);
            ClassicAssert.IsNotNull(readHoldingRegistersRequest2);
            ClassicAssert.IsTrue   (read > 0);

            Array.Resize(ref buffer, read);

            var response  = new ReadHoldingRegistersResponse(buffer);

        }

        #endregion


        #region ReadHoldingRegisters_Test1()

        [Test]
        public async Task ReadHoldingRegisters_Test1()
        {

            ClassicAssert.IsNotNull(ModbusTCPServer);

            if (ModbusTCPServer is null)
                return;

            ReadHoldingRegistersRequest? readHoldingRegistersRequest1 = null;

            ModbusTCPServer.OnReadHoldingRegistersRequest += (timestamp,
                                                              modbusTCPServer,
                                                              remoteSocket,
                                                              connectionId,
                                                              request) => {

                readHoldingRegistersRequest1 = request;

                return Task.CompletedTask;

            };


            var client = new ModbusTCPClient(IPv4Address.Localhost,
                                             IPPort.Parse(24694),
                                             StartingAddressOffset: 0);

            var response = await client.ReadHoldingRegisters(9, 4);

            ClassicAssert.IsNotNull(readHoldingRegistersRequest1);

            if (readHoldingRegistersRequest1 is not null)
            {
                ClassicAssert.AreEqual(9, readHoldingRegistersRequest1.StartingAddress);
                ClassicAssert.AreEqual(4, readHoldingRegistersRequest1.NumberOfRegisters);
            }

        }

        #endregion


    }

}
