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

using System.IO.Ports;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// A Janitza UMG 103.
    /// </summary>
    public class JanitzaUMG103
    {

        public static class Addr
        {

            public static readonly ushort SYSTIME       = 4;

            public static readonly ushort U1            = 1000;
            public static readonly ushort U2            = 1002;
            public static readonly ushort U3            = 1004;

            public static readonly ushort I1            = 1012;
            public static readonly ushort I2            = 1014;
            public static readonly ushort I3            = 1016;

            public static readonly ushort CosPhi1       = 1044;
            public static readonly ushort CosPhi2       = 1046;
            public static readonly ushort CosPhi3       = 1048;

            public static readonly ushort Frequency     = 1226;

        }

        #region Data

        private readonly IModbusClient ModbusClient;

        #endregion

        #region Constructor(s)

        #region JanitzaUMG103(via Serialport...)

        /// <summary>
        /// Create a new Janitza UMG 103 Modbus client and connect to a remote Modbus/RTU device.
        /// </summary>
        /// <param name="PortName">The communications port. The default is COM1.</param>
        /// <param name="BaudRate">The baud rate. The default is 115200.</param>
        /// <param name="Parity">One of the enumeration values that represents the parity-checking protocol. The default is None.</param>
        /// <param name="DataBits">The data bits length.</param>
        /// <param name="StopBits">One of the StopBits values.</param>
        /// <param name="ReadTimeout">The number of milliseconds before a time-out occurs when a read operation does not finish. The default is 1 second.</param>
        /// <param name="WriteTimeout">The number of milliseconds before a time-out occurs. The default is 1 second.</param>
        /// <param name="UnitAddress"></param>
        public JanitzaUMG103(String    PortName       = "COM1",
                             Int32     BaudRate       = 115200,
                             Parity    Parity         = Parity.None,
                             Int32     DataBits       = 8,
                             StopBits  StopBits       = StopBits.One,
                             Int32     ReadTimeout    = 1500,
                             Int32     WriteTimeout   = 15000,
                             Byte      UnitAddress    = 1)
        {

            //    this.ModbusClient = new ModbusRTUClient(PortName,
            //                                            BaudRate,
            //                                            Parity,
            //                                            DataBits,
            //                                            StopBits,
            //                                            ReadTimeout,
            //                                            WriteTimeout,
            //                                            UnitAddress);

        }

        #endregion

        #region JanitzaUMG103(IPAddress,  RemotePort = null, UnitAddress = null)

        /// <summary>
        /// Create a new Janitza UMG 103 Modbus client and connect to a remote Modbus/TCP device.
        /// </summary>
        /// <param name="IPAddress">An remote IP address.</param>
        /// <param name="RemotePort">An optional remote TCP/IP port [default: 502].</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        public JanitzaUMG103(IIPAddress  IPAddress,
                             IPPort?     RemotePort    = null,
                             Byte?       UnitAddress   = null)
        {

            this.ModbusClient = new ModbusTCPClient(IPAddress,
                                                    RemotePort,
                                                    UnitAddress);

        }

        #endregion

        #region JanitzaUMG103(RemoteHostname, RemotePort = null, UnitAddress = null)

        /// <summary>
        /// Create a new Janitza UMG 103 Modbus client and connect to a remote Modbus/TCP device.
        /// </summary>
        /// <param name="RemoteHostname">A remote hostname.</param>
        /// <param name="RemotePort">An optional remote TCP/IP port [default: 502].</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        public JanitzaUMG103(HTTPHostname  RemoteHostname,
                             IPPort?       RemotePort    = null,
                             Byte?         UnitAddress   = null)
        {

            this.ModbusClient = new ModbusTCPClient(RemoteHostname,
                                                    RemotePort ?? IPPort.Parse(502),
                                                    UnitAddress);

        }

        #endregion

        #endregion


        // 200     Spannung Uln L1     10     SHORT      V     VT
        // 201     Spannung Uln L2     10     SHORT      V     VT
        // 202     Spannung Uln L3     10     SHORT      V     VT
        // ...
        // 206     Strom I L1        1000     SHORT      mA     CT
        // 207     Strom I L2        1000     SHORT      mA     CT
        // 208     Strom I L3        1000     SHORT      mA     CT
        // ...
        // 218     CosPhi L1          100     SHORT     -
        // 219     CosPhi L2          100     SHORT     -
        // 220     CosPhi L3          100     SHORT     -
        // ...
        // 275     Frequenz           100     USHORT     Hz


        // 73 Mittelungszeit_für alle U 0-8 CHAR    0 =  5 Sek.
        //                                          1 = 10 Sek.
        //                                          2 = 30 Sek.
        //                                          3 = 60 Sek.
        //                                          4 =  5 Min.
        //                                          5 =  8 Min.
        //                                          6 = 15 Min. (default)
        //                                          7 = 30 Min.
        //                                          8 = 60 Min.


        // 2000     Mittelw. U L1          1     FLOAT
        // 2002     Mittelw. U L2          1     FLOAT
        // 2004     Mittelw. U L3          1     FLOAT
        // ...
        // 2012     Mittelw. I L1          1     FLOAT
        // 2014     Mittelw. I L2          1     FLOAT
        // 2016     Mittelw. I L3          1     FLOAT
        // ...
        // 2044     Mittelw. CosPhi L1     1     FLOAT
        // 2046     Mittelw. CosPhi L2     1     FLOAT
        // 2048     Mittelw. CosPhi L3     1     FLOAT
        // ...
        // 2226     Mittelw. Frequenz      1     FLOAT


        // 3000     Maxwert. U L1          1     FLOAT
        // 3002     Maxwert. U L2          1     FLOAT
        // 3004     Maxwert. U L3          1     FLOAT
        // ...
        // 3012     Maxwert. I L1          1     FLOAT
        // 3014     Maxwert. I L2          1     FLOAT
        // 3016     Maxwert. I L3          1     FLOAT
        // ...
        // 3044     Maxwert. CosPhi L1     1     FLOAT
        // 3046     Maxwert. CosPhi L2     1     FLOAT
        // 3048     Maxwert. CosPhi L3     1     FLOAT
        // ...
        // 3226     Maxwert. Frequenz      1     FLOAT


        // 4000     Minwert U L1           1     FLOAT
        // 4002     Minwert U L2           1     FLOAT
        // 4004     Minwert U L3           1     FLOAT
        // ...
        // 4012     Minwert CosPhi L1      1     FLOAT
        // 4014     Minwert CosPhi L1      1     FLOAT
        // 4016     Minwert CosPhi L2      1     FLOAT
        // ...
        // 4104     Minwert Frequenz       1     FLOAT


        public void Close()
        {
            ModbusClient.Close();
        }

        public void Dispose()
        {
            ModbusClient.Dispose();
        }


    }

}
