/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.IO;
using System.Linq;
using System.Text;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// This processor will accept incoming TCP connections and
    /// decode the transmitted data to UTF8 encoded comma-separated
    /// text lines with 0x00, 0x0a (\n) or 0x0d 0x0a (\r\n)
    /// end-of-line characters.
    /// </summary>
    public class TCPCSVProcessor : IArrowReceiver<TCPConnection>,
                                   IBoomerangSender<TCPConnection, DateTime, String[], String>
    {

        #region Data

        private const String DefaultServiceBanner  = "Vanaheimr Hermod TCP/CSV Service v0.9";
        private const UInt32 ReadTimeout           = 180000U;

        #endregion

        #region Properties

        #region ServiceBanner

        public String ServiceBanner { get; set; }

        #endregion

        #region SplitCharacters

        private readonly Char[] _SplitCharacters;

        /// <summary>
        /// The characters to split the incoming CSV text lines.
        /// </summary>
        public Char[] SplitCharacters
        {
            get
            {
                return _SplitCharacters;
            }
        }

        #endregion

        #endregion

        #region Events

        public event StartedEventHandler                                                OnStarted;

        public event BoomerangSenderHandler<TCPConnection, DateTime, String[], String>  OnNotification;

        public event CompletedEventHandler                                              OnCompleted;


        public event ExceptionOccuredEventHandler                                       OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This processor will accept incoming TCP connections and
        /// decode the transmitted data to UTF8 encoded comma-separated
        /// text lines with 0x00, 0x0a (\n) or 0x0d 0x0a (\r\n)
        /// end-of-line characters.
        /// </summary>
        /// <param name="SplitCharacters">The characters to split the incoming CSV text lines.</param>
        public TCPCSVProcessor(Char[]  SplitCharacters = null)
        {

            this._SplitCharacters  = (SplitCharacters != null) ? SplitCharacters : new Char[1] { '/' };
            this.ServiceBanner     = DefaultServiceBanner;

        }

        #endregion



        #region ProcessArrow(TCPConnection)

        public void ProcessArrow(TCPConnection TCPConnection)
        {

            #region Start

            TCPConnection.WriteLineToResponseStream(ServiceBanner);
            TCPConnection.NoDelay = true;

            Byte Byte;
            var Buffer        = new Byte[1024];
            var MemoryStream  = new MemoryStream();
            var EndOfCSVLine  = EOLSearch.NotYet;
            var ClientClose   = false;
            var ServerClose   = false;

            #endregion

            try
            {

                do
                {

                    switch (TCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
                    {

                        #region DataAvailable

                        case TCPClientResponse.DataAvailable: 

                            #region Check for CSV line ending...

                            if (EndOfCSVLine == EOLSearch.NotYet)
                            {

                                // 0x00 or \n
                                if (Byte == 0x00 || Byte == 0x0a)
                                    EndOfCSVLine = EOLSearch.EoL_Found;

                                // \r
                                else if (Byte == 0x0d)
                                    EndOfCSVLine = EOLSearch.R_Read;

                            }

                            // \n after a \r
                            else if (EndOfCSVLine == EOLSearch.R_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfCSVLine = EOLSearch.EoL_Found;
                                else
                                    EndOfCSVLine = EOLSearch.NotYet;
                            }

                            #endregion

                            #region ...or append read value(s) to internal buffer

                            if (EndOfCSVLine == EOLSearch.NotYet)
                                MemoryStream.WriteByte(Byte);

                            #endregion


                            #region If end-of-line -> process data...

                            else if (EndOfCSVLine == EOLSearch.EoL_Found)
                            {

                                if (MemoryStream.Length > 0 && OnNotification != null)
                                {

                                    #region Check UTF8 encoding

                                    var CSVLine = String.Empty;

                                    try
                                    {
                                        CSVLine = Encoding.UTF8.GetString(MemoryStream.ToArray());
                                    }
                                    catch (Exception)
                                    {
                                        TCPConnection.WriteLineToResponseStream("Protocol Error: Invalid UTF8 encoding!");
                                    }

                                    #endregion

                                    #region Check CSV separation

                                    String[] CSVArray = null;

                                    try
                                    {

                                        CSVArray = CSVLine.Trim().
                                                            Split(SplitCharacters,
                                                                  StringSplitOptions.None).
                                                            Select(v => v.Trim()).
                                                            ToArray();

                                    }
                                    catch (Exception)
                                    {
                                        TCPConnection.WriteLineToResponseStream("Protocol Error: Invalid CSV data!");
                                    }

                                    #endregion

                                    #region Call OnNotification delegate

                                    TCPResult<String> Result = null;

                                    var OnNotificationLocal = OnNotification;
                                    if (OnNotificationLocal != null)
                                    {

                                        TCPConnection.WriteLineToResponseStream(OnNotification(TCPConnection,
                                                                                DateTime.Now,
                                                                                CSVArray));

                                    }

                                    #endregion

                                    //Message.WriteToResponseStream(0x00);

                                }

                                MemoryStream.SetLength(0);
                                MemoryStream.Seek(0, SeekOrigin.Begin);
                                EndOfCSVLine = EOLSearch.NotYet;

                            }

                            #endregion

                            break;

                        #endregion

                        #region CanNotRead

                        case TCPClientResponse.CanNotRead:
                            ServerClose = true;
                            break;

                        #endregion

                        #region ClientClose

                        case TCPClientResponse.ClientClose:
                            ClientClose = true;
                            break;

                        #endregion

                        #region Timeout

                        case TCPClientResponse.Timeout:
                            ServerClose = true;
                            break;

                        #endregion

                    }

                } while (!ClientClose && !ServerClose);

            }

            #region Process exceptions

            catch (IOException ioe)
            {

                    if (ioe.Message.StartsWith("Unable to read data from the transport connection")) { }
                else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) { }

                else
                {

                    //if (OnError != null)
                    //    OnError(this, DateTime.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                //if (OnError != null)
                //    OnError(this, DateTime.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {
                TCPConnection.Close((ClientClose) ? ConnectionClosedBy.Client : ConnectionClosedBy.Server);
            }
            catch (Exception e)
            { }

            #endregion

        }

        #endregion

        #region ProcessExceptionOccured(Sender, Timestamp, ExceptionMessage)

        public void ProcessExceptionOccured(Object     Sender,
                                            DateTime   Timestamp,
                                            Exception  ExceptionMessage)
        {

            var OnExceptionOccuredLocal = OnExceptionOccured;
            if (OnExceptionOccuredLocal != null)
                OnExceptionOccuredLocal(Sender,
                                        Timestamp,
                                        ExceptionMessage);

        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, Message = null)

        public void ProcessCompleted(Object    Sender,
                                     DateTime  Timestamp,
                                     String    Message = null)
        {

            var OnCompletedLocal = OnCompleted;
            if (OnCompletedLocal != null)
                OnCompletedLocal(Sender,
                                 Timestamp,
                                 Message);

        }

        #endregion


    }

}
