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

using System.IO.Ports;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Modbus.Toolbox;
using System.Text;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    /// <summary>
    /// A Janitza UMG 604.
    /// </summary>
    public class JanitzaUMG604
    {

        public static class Addr
        {

            public static readonly ushort REALTIME          = 0;  // UInt64
            public static readonly ushort SYSTIME           = 4;  // UInt32
            public static readonly ushort Serialnumber      = 10176;

            public static readonly ushort U1                = 1317;
            public static readonly ushort U2                = 1319;
            public static readonly ushort U3                = 1321;
            public static readonly ushort U4                = 1323;

            public static readonly ushort I1                = 1325;
            public static readonly ushort I2                = 1327;
            public static readonly ushort I3                = 1329;
            public static readonly ushort I4                = 1331;

            public static readonly ushort CosPhi1           = 1423;
            public static readonly ushort CosPhi2           = 1425;
            public static readonly ushort CosPhi3           = 1427;
            public static readonly ushort CosPhi4           = 1429;

            public static readonly ushort Frequency         = 1439;

        }

        #region Data

        private readonly IModbusClient ModbusClient;

        #endregion

        #region Properties

        #region Device

        #region RealTime

        public DateTime RealTime
        {
            get
            {

                var response = ModbusClient.ReadHoldingRegisters(Addr.REALTIME, 4).Result;

                return ByteExtensions.UNIXTime.AddSeconds(System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt64(response.EntirePDU, 9)));



                if (ModbusClient.TryReadDateTime32(Addr.SYSTIME, out var value))
                    return value;

                Debug.Print("Could not read SysTime!");

                return DateTime.MinValue;

            }
        }

        #endregion

        #region SysTime

        public DateTime SysTime
        {
            get
            {

                var response = ModbusClient.ReadHoldingRegisters(Addr.SYSTIME, 2).Result;

                return ByteExtensions.UNIXTime.AddSeconds(System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(response.EntirePDU, 9)));



                if (ModbusClient.TryReadDateTime32(Addr.SYSTIME, out var value))
                    return value;

                Debug.Print("Could not read SysTime!");

                return DateTime.MinValue;

            }
        }

        #endregion

        #region Serialnumber

        /// <summary>
        /// The serial number of the device.
        /// </summary>
        public Int64 Serialnumber
        {
            get
            {

                if (ModbusClient.ReadInt32(Addr.Serialnumber, out var value))
                    return value;

                Debug.Print("Could not read the device serialnumber!");

                return -1;

            }
        }

        #endregion

        #region Productnumber

        /// <summary>
        /// The product number of the device.
        /// </summary>
        public Int32 Productnumber
        {
            get
            {

                var response = ModbusClient.ReadHoldingRegisters(10178, 2).Result;

                return System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(response.EntirePDU, 9));

                if (ModbusClient.ReadInt32(10178, out var value))
                    return value;

                Debug.Print("Could not read the device productnumber!");

                return -1;

            }
        }

        #endregion

        #region DeviceName

        /// <summary>
        /// Device name
        /// </summary>
        public String DeviceName
        {
            get
            {

                // Always 64 characters!
                if (ModbusClient.TryReadString(10072, 32, out var value))
                    return value ?? String.Empty;

                Debug.Print("Could not read device name!");

                return String.Empty;

            }
        }

        #endregion

        #region DeviceDescription

        /// <summary>
        /// Device description
        /// </summary>
        public String DeviceDescription
        {
            get
            {

                // Always 128 characters!
                if (ModbusClient.TryReadString(10104, 64, out var value))
                    return value ?? String.Empty;

                Debug.Print("Could not read device description!");

                return String.Empty;

            }
        }

        #endregion

        // 10168 string _LANGUAGE 16 Spracheauswahl, Hompage
        // 13437 string _RELEASE 16 Firmware-Release

        #endregion

        #region Date

        #region Day

        /// <summary>
        /// (1 .. 31)
        /// </summary>
        public Int16 Day
        {
            get
            {

                var response = ModbusClient.ReadHoldingRegisters(19000, 40).Result;

                return System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response.EntirePDU, 9));



                if (ModbusClient.ReadInt16(6, out var value))
                    return value;

                Debug.Print("Could not read the device day!");

                return -1;

            }
        }

        #endregion

        #region Month

        /// <summary>
        /// (0=Jan, .. 11=Dez)
        /// </summary>
        public Int16 Month
        {
            get
            {

                if (ModbusClient.ReadInt16(7, out var value))
                    return value;

                Debug.Print("Could not read the device month!");

                return -1;

            }
        }

        #endregion

        #region Year

        /// <summary>
        /// Jahr
        /// </summary>
        public Int16 Year
        {
            get
            {

                if (ModbusClient.ReadInt16(8, out var value))
                    return value;

                Debug.Print("Could not read the device year!");

                return -1;

            }
        }

        #endregion


        #region Hour

        /// <summary>
        /// (1 .. 24)
        /// </summary>
        public Int16 Hour
        {
            get
            {

                if (ModbusClient.ReadInt16(9, out var value))
                    return value;

                Debug.Print("Could not read the device hour!");

                return -1;

            }
        }

        #endregion

        #region Min

        /// <summary>
        /// (1 .. 59)
        /// </summary>
        public Int16 Min
        {
            get
            {

                if (ModbusClient.ReadInt16(10, out var value))
                    return value;

                Debug.Print("Could not read the device minute!");

                return -1;

            }
        }

        #endregion

        #region Sec

        /// <summary>
        /// (1 .. 59)
        /// </summary>
        public Int16 Sec
        {
            get
            {

                if (ModbusClient.ReadInt16(11, out var value))
                    return value;

                Debug.Print("Could not read the device second!");

                return -1;

            }
        }

        #endregion


        #region Weekday

        /// <summary>
        /// (0=So, .. 6=Mo)
        /// </summary>
        public Int16 Weekday
        {
            get
            {

                if (ModbusClient.ReadInt16(12, out var value))
                    return value;

                Debug.Print("Could not read the device weekday!");

                return -1;

            }
        }

        #endregion

        #endregion

        #region Current values

        #region U1

        /// <summary>
        /// Voltage U1
        /// </summary>
        public Single U1
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.U1, out var value))
                    return value;

                Debug.Print("Could not read voltage U1!");

                return -1;

            }
        }

        #endregion

        #region U2

        /// <summary>
        /// Voltage U2
        /// </summary>
        public Single U2
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.U2, out var value))
                    return value;

                Debug.Print("Could not read voltage U2!");

                return -1;

            }
        }

        #endregion

        #region U3

        /// <summary>
        /// Voltage U3
        /// </summary>
        public Single U3
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.U3, out var value))
                    return value;

                Debug.Print("Could not read voltage U3!");

                return -1;

            }
        }

        #endregion

        #region U4

        /// <summary>
        /// Voltage U4
        /// </summary>
        public Single U4
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.U4, out var value))
                    return value;

                Debug.Print("Could not read voltage U4!");

                return -1;

            }
        }

        #endregion


        #region I1

        /// <summary>
        /// Current I1
        /// </summary>
        public Single I1
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.I1, out var value))
                    return value;

                Debug.Print("Could not read current I1!");

                return -1;

            }
        }

        #endregion

        #region I2

        /// <summary>
        /// Current I2
        /// </summary>
        public Single I2
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.I2, out var value))
                    return value;

                Debug.Print("Could not read current I2!");

                return -1;

            }
        }

        #endregion

        #region I3

        /// <summary>
        /// Current I3
        /// </summary>
        public Single I3
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.I3, out var value))
                    return value;

                Debug.Print("Could not read current I3!");

                return -1;

            }
        }

        #endregion

        #region I4

        /// <summary>
        /// Current I4
        /// </summary>
        public Single I4
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.I4, out var value))
                    return value;

                Debug.Print("Could not read current I4!");

                return -1;

            }
        }

        #endregion


        // 1333 float _PLN[0] W Wirkleistung L1
        // 1335 float _PLN[1] W Wirkleistung L2
        // 1337 float _PLN[2] W Wirkleistung L3
        // 1339 float _PLN[3] W Wirkleistung L4

        // 1341 float _QLN[0] VAr Blindleistung L1
        // 1343 float _QLN[1] VAr Blindleistung L2
        // 1345 float _QLN[2] VAr Blindleistung L3
        // 1347 float _QLN[3] VAr Blindleistung L4

        // 1349 float _SLN[0] VA Scheinleistung L1
        // 1351 float _SLN[1] VA Scheinleistung L2
        // 1353 float _SLN[2] VA Scheinleistung L3
        // 1355 float _SLN[3] VA Scheinleistung L4

        // 1415 float _PHASE[0] � Phase UL1 IL1
        // 1417 float _PHASE[1] � Phase UL2 IL2
        // 1419 float _PHASE[2] � Phase UL3 IL3
        // 1421 float _PHASE[3] � Phase UL4 IL4


        #region CosPhi1

        /// <summary>
        /// cos phi L1
        /// </summary>
        public Single CosPhi1
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.CosPhi1, out var value))
                    return value;

                Debug.Print("Could not read cos phi of L1!");

                return -1;

            }
        }

        #endregion

        #region CosPhi2

        /// <summary>
        /// cos phi L2
        /// </summary>
        public Single CosPhi2
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.CosPhi2, out var value))
                    return value;

                Debug.Print("Could not read cos phi of L2!");

                return -1;

            }
        }

        #endregion

        #region CosPhi3

        /// <summary>
        /// cos phi L3
        /// </summary>
        public Single CosPhi3
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.CosPhi3, out var value))
                    return value;

                Debug.Print("Could not read cos phi of L3!");

                return -1;

            }
        }

        #endregion

        #region CosPhi4

        /// <summary>
        /// cos phi L4
        /// </summary>
        public Single CosPhi4
        {
            get
            {

                if (ModbusClient.TryReadSingle(Addr.CosPhi4, out var value))
                    return value;

                Debug.Print("Could not read cos phi of L4!");

                return -1;

            }
        }

        #endregion


        #region Frequency

        /// <summary>
        /// Frequency
        /// </summary>
        public Single Frequency
        {
            get
            {

                if (ModbusClient.TryReadSingle(1439, out var value))
                    return value;

                Debug.Print("Could not read grid frequency!");

                return -1;

            }
        }

        #endregion

        #endregion

        #region Averaged values

        // 2767 float _ULN_AVG[0] V
        // 2769 float _ULN_AVG[1] V
        // 2771 float _ULN_AVG[2] V
        // 2773 float _ULN_AVG[3] V

        // 2775 float _ILN_AVG[0] A
        // 2777 float _ILN_AVG[1] A
        // 2779 float _ILN_AVG[2] A
        // 2781 float _ILN_AVG[3] A

        // 2783 float _PLN_AVG[0] W
        // 2785 float _PLN_AVG[1] W
        // 2787 float _PLN_AVG[2] W
        // 2789 float _PLN_AVG[3] W

        // 2791 float _QLN_AVG[0] VAr
        // 2793 float _QLN_AVG[1] VAr
        // 2795 float _QLN_AVG[2] VAr
        // 2797 float _QLN_AVG[3] VAr

        // 2799 float _SLN_AVG[0] VA
        // 2801 float _SLN_AVG[1] VA
        // 2803 float _SLN_AVG[2] VA
        // 2805 float _SLN_AVG[3] VA

        // 2829 float _FREQ_AVG Hz

        #endregion

        #region Minimal values

        // 3179 float _ULN_MIN[0] V
        // 3181 float _ULN_MIN[1] V
        // 3183 float _ULN_MIN[2] V
        // 3185 float _ULN_MIN[3] V

        // 3193 float _FREQ_MIN Hz

        #endregion

        #region Maximal values

        // 4509 float _ULN_MAX[0] V
        // 4511 float _ULN_MAX[1] V
        // 4513 float _ULN_MAX[2] V
        // 4515 float _ULN_MAX[3] V

        // 4517 float _ILN_MAX[0] A
        // 4519 float _ILN_MAX[1] A
        // 4521 float _ILN_MAX[2] A
        // 4523 float _ILN_MAX[3] A

        // 4525 float _PLN_MAX[0] W
        // 4527 float _PLN_MAX[1] W
        // 4529 float _PLN_MAX[2] W
        // 4531 float _PLN_MAX[3] W

        // 4533 float _QLN_MAX[0] VAr
        // 4535 float _QLN_MAX[1] VAr
        // 4537 float _QLN_MAX[2] VAr
        // 4539 float _QLN_MAX[3] VAr

        // 4541 float _SLN_MAX[0] VA
        // 4543 float _SLN_MAX[1] VA
        // 4545 float _SLN_MAX[2] VA
        // 4547 float _SLN_MAX[3] VA

        // 4571 float _FREQ_MAX Hz

        #endregion

        #region Average time span

        // n Mittelungszeit / Sekunden
        //     0 5
        //     1 10
        //     2 15
        //     3 30
        //     4 60
        //     5 300
        //     6 480
        //     7 600
        //     8 900

        // 5245 short _ULN_AVG_T[0] n
        // 5246 short _ULN_AVG_T[1] n
        // 5247 short _ULN_AVG_T[2] n
        // 5248 short _ULN_AVG_T[3] n

        // 5249 short _ILN_AVG_T[0] n
        // 5250 short _ILN_AVG_T[1] n
        // 5251 short _ILN_AVG_T[2] n
        // 5252 short _ILN_AVG_T[3] n

        // 5253 short _PLN_AVG_T[0] n
        // 5254 short _PLN_AVG_T[1] n
        // 5255 short _PLN_AVG_T[2] n
        // 5256 short _PLN_AVG_T[3] n

        // 5257 short _QLN_AVG_T[0] n
        // 5258 short _QLN_AVG_T[1] n
        // 5259 short _QLN_AVG_T[2] n
        // 5260 short _QLN_AVG_T[3] n

        // 5261 short _SLN_AVG_T[0] n
        // 5262 short _SLN_AVG_T[1] n
        // 5263 short _SLN_AVG_T[2] n
        // 5264 short _SLN_AVG_T[3] n

        // 5276 short _FREQ_AVG_T   n

        #endregion

        #region Event timestamps

        // ZEITPUNKT (UTC)

        // 5615 uint _ULN_MIN_T[0] s
        // 5617 uint _ULN_MIN_T[1] s
        // 5619 uint _ULN_MIN_T[2] s
        // 5621 uint _ULN_MIN_T[3] s

        // 5629 uint _FREQ_MIN_T   s


        // 6945 uint _ULN_MAX_T[0] s
        // 6947 uint _ULN_MAX_T[1] s
        // 6949 uint _ULN_MAX_T[2] s
        // 6951 uint _ULN_MAX_T[3] s

        // 6953 uint _ILN_MAX_T[0] s
        // 6955 uint _ILN_MAX_T[1] s
        // 6957 uint _ILN_MAX_T[2] s
        // 6959 uint _ILN_MAX_T[3] s

        // 6961 uint _PLN_MAX_T[0] s
        // 6963 uint _PLN_MAX_T[1] s
        // 6965 uint _PLN_MAX_T[2] s
        // 6967 uint _PLN_MAX_T[3] s

        // 6969 uint _QLN_MAX_T[0] s
        // 6971 uint _QLN_MAX_T[1] s
        // 6973 uint _QLN_MAX_T[2] s
        // 6975 uint _QLN_MAX_T[3] s

        // 6977 uint _SLN_MAX_T[0] s
        // 6979 uint _SLN_MAX_T[1] s
        // 6981 uint _SLN_MAX_T[2] s
        // 6983 uint _SLN_MAX_T[3] s

        // 7007 uint _FREQ_MAX_T   s

        #endregion

        #region Averaged maximal values

        // 8333 float _ULN_AVG_MAX[0] V
        // 8335 float _ULN_AVG_MAX[1] V
        // 8337 float _ULN_AVG_MAX[2] V
        // 8339 float _ULN_AVG_MAX[3] V

        // 8341 float _ILN_AVG_MAX[0] A
        // 8343 float _ILN_AVG_MAX[1] A
        // 8345 float _ILN_AVG_MAX[2] A
        // 8347 float _ILN_AVG_MAX[3] A

        // 8349 float _PLN_AVG_MAX[0] W
        // 8351 float _PLN_AVG_MAX[1] W
        // 8353 float _PLN_AVG_MAX[2] W
        // 8355 float _PLN_AVG_MAX[3] W

        // 8357 float _QLN_AVG_MAX[0] VAr
        // 8359 float _QLN_AVG_MAX[1] VAr
        // 8361 float _QLN_AVG_MAX[2] VAr
        // 8363 float _QLN_AVG_MAX[3] VAr

        // 8365 float _SLN_AVG_MAX[0] VA
        // 8367 float _SLN_AVG_MAX[1] VA
        // 8369 float _SLN_AVG_MAX[2] VA
        // 8371 float _SLN_AVG_MAX[3] VA

        // 8395 float _FREQ_AVG_MAX Hz

        #endregion

        #region Timestamp of averaged maximal values

        // 9721 uint _ULN_AVG_MAX_T[0] s Spannung, Mittelwert, Maximum, L1-N
        // 9723 uint _ULN_AVG_MAX_T[1] s Spannung, Mittelwert, Maximum, L2-N
        // 9725 uint _ULN_AVG_MAX_T[2] s Spannung, Mittelwert, Maximum, L3-N
        // 9727 uint _ULN_AVG_MAX_T[3] s Spannung, Mittelwert, Maximum, L4-N

        // 9729 uint _ILN_AVG_MAX_T[0] s Strom, Mittelwert, Maximum, L1-N
        // 9731 uint _ILN_AVG_MAX_T[1] s Strom, Mittelwert, Maximum, L2-N
        // 9733 uint _ILN_AVG_MAX_T[2] s Strom, Mittelwert, Maximum, L3-N
        // 9735 uint _ILN_AVG_MAX_T[3] s Strom, Mittelwert, Maximum, L4-N

        // 9737 uint _PLN_AVG_MAX_T[0] s Wirkleistung, Mittelwert, Maximum, L1
        // 9739 uint _PLN_AVG_MAX_T[1] s Wirkleistung, Mittelwert, Maximum, L2
        // 9741 uint _PLN_AVG_MAX_T[2] s Wirkleistung, Mittelwert, Maximum, L3
        // 9743 uint _PLN_AVG_MAX_T[3] s Wirkleistung, Mittelwert, Maximum, L4

        // 9745 uint _QLN_AVG_MAX_T[0] s Blindleistung, Mittelwert, Maximum, L1
        // 9747 uint _QLN_AVG_MAX_T[1] s Blindleistung, Mittelwert, Maximum, L2
        // 9749 uint _QLN_AVG_MAX_T[2] s Blindleistung, Mittelwert, Maximum, L3
        // 9751 uint _QLN_AVG_MAX_T[3] s Blindleistung, Mittelwert, Maximum, L4

        // 9753 uint _SLN_AVG_MAX_T[0] s Scheinleistung, Mittelwert, Maximum, L1
        // 9755 uint _SLN_AVG_MAX_T[1] s Scheinleistung, Mittelwert, Maximum, L2
        // 9757 uint _SLN_AVG_MAX_T[2] s Scheinleistung, Mittelwert, Maximum, L3
        // 9759 uint _SLN_AVG_MAX_T[3] s Scheinleistung, Mittelwert, Maximum, L4

        // 9783 uint _FREQ_AVG_MAX_T s Frequenz, Mittelwert, Maxwert

        #endregion

        #region (Re-)Active energy (Arbeit)

        // 9807 float _WH_S[0] VAh Scheinarbeit, L1
        // 9809 float _WH_S[1] VAh Scheinarbeit, L2
        // 9811 float _WH_S[2] VAh Scheinarbeit, L3
        // 9813 float _WH_S[3] VAh Scheinarbeit, L4

        // 9819 float _WH[0] Wh Wirkarbeit, L1
        // 9821 float _WH[1] Wh Wirkarbeit, L2
        // 9823 float _WH[2] Wh Wirkarbeit, L3
        // 9825 float _WH[3] Wh Wirkarbeit, L4

        // 9831 float _QH[0] VArh Blindarbeit, L1
        // 9833 float _QH[1] VArh Blindarbeit, L2
        // 9835 float _QH[2] VArh Blindarbeit, L3
        // 9837 float _QH[3] VArh Blindarbeit, L4

        // 9843 float _WH_V[0] Wh Wirkarbeit, Bezug, L1
        // 9845 float _WH_V[1] Wh Wirkarbeit, Bezug, L2
        // 9847 float _WH_V[2] Wh Wirkarbeit, Bezug, L3
        // 9849 float _WH_V[3] Wh Wirkarbeit, Bezug, L4

        // 9855 float _WH_Z[0] Wh Wirkarbeit, Lieferung, L1
        // 9857 float _WH_Z[1] Wh Wirkarbeit, Lieferung, L2
        // 9859 float _WH_Z[2] Wh Wirkarbeit, Lieferung, L3
        // 9861 float _WH_Z[3] Wh Wirkarbeit, Lieferung, L4

        // 9915 float _IQH[0] VArh Blindarbeit, induktiv, L1
        // 9917 float _IQH[1] VArh Blindarbeit, induktiv, L2
        // 9919 float _IQH[2] VArh Blindarbeit, induktiv, L3
        // 9921 float _IQH[3] VArh Blindarbeit, induktiv, L4

        // 9927 float _CQH[0] VArh Blindarbeit, kapazitiv, L1
        // 9929 float _CQH[1] VArh Blindarbeit, kapazitiv, L2
        // 9931 float _CQH[2] VArh Blindarbeit, kapazitiv, L3
        // 9933 float _CQH[3] VArh Blindarbeit, kapazitiv, L4

        #endregion

        #endregion

        #region Constructor(s)

        #region JanitzaUMG604(via Serialport...)

        /// <summary>
        /// Create a new Janitza UMG 604.
        /// </summary>
        /// <param name="PortName">The communications port. The default is COM2.</param>
        /// <param name="BaudRate">The baud rate. The default is 115200.</param>
        /// <param name="Parity">One of the enumeration values that represents the parity-checking protocol. The default is None.</param>
        /// <param name="DataBits">The data bits length.</param>
        /// <param name="StopBits">One of the StopBits values.</param>
        /// <param name="ReadTimeout">The number of milliseconds before a time-out occurs when a read operation does not finish. The default is 1 second.</param>
        /// <param name="WriteTimeout">The number of milliseconds before a time-out occurs. The default is 1 second.</param>
        /// <param name="UnitAddress"></param>
        public JanitzaUMG604(String    PortName       = "COM1",
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

        #region JanitzaUMG604(IPAddress,  RemotePort = null, UnitAddress = null)

        /// <summary>
        /// Create a new Janitza UMG 604 Modbus client and connect to a remote Modbus/TCP device.
        /// </summary>
        /// <param name="IPAddress">An remote IP address.</param>
        /// <param name="RemotePort">An optional remote TCP/IP port [default: 502].</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        public JanitzaUMG604(IIPAddress  IPAddress,
                             IPPort?     RemotePort    = null,
                             Byte?       UnitAddress   = null)
        {

            this.ModbusClient = new ModbusTCPClient(IPAddress,
                                                    RemotePort,
                                                    UnitAddress,
                                                    1);

        }

        #endregion

        #region JanitzaUMG604(RemoteHostname, RemotePort = null, UnitAddress = null)

        /// <summary>
        /// Create a new Janitza UMG 604 Modbus client and connect to a remote Modbus/TCP device.
        /// </summary>
        /// <param name="RemoteHostname">A remote hostname.</param>
        /// <param name="RemotePort">An optional remote TCP/IP port [default: 502].</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        public JanitzaUMG604(HTTPHostname  RemoteHostname,
                             IPPort?       RemotePort    = null,
                             Byte?         UnitAddress   = null)
        {

            this.ModbusClient = new ModbusTCPClient(RemoteHostname,
                                                    RemotePort,
                                                    UnitAddress,
                                                    1);

        }

        #endregion

        #endregion



        #region ReadSingle(StartingAddress)

        public Single ReadSingle(UInt16 StartingAddress)
        {
            return ModbusClient.Read<Single>(StartingAddress, 2, array => BitConverter.ToSingle(array.Reverse(3, 4), 0));
        }

        #endregion

        #region ReadSingles(StartingAddress, Num)

        public Single[] ReadSingles(UInt16 StartingAddress, Int32 Num)
        {
            return ModbusClient.Read<Single[]>(StartingAddress, (ushort)(2 * Num), array => MultiConverters.NetworkBytesToHostSingle(array));
        }

        #endregion

        #region ReadDateTime32(StartingAddress)

        public DateTime ReadDateTime32(UInt16 StartingAddress)
        {
            return ModbusClient.Read<DateTime>(StartingAddress, 2, array => ByteExtensions.UNIXTime.AddSeconds(BitConverter.ToInt32(array.Reverse(3, 4), 0)), OnError: DateTime.MinValue);
        }

        #endregion


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
