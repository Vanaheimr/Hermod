﻿/*
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

using System.Text;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Illias;

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

        public String  ServiceBanner     { get; }

        /// <summary>
        /// The characters to split the incoming CSV text lines.
        /// </summary>
        public Char[]  SplitCharacters   { get; }

        #endregion

        #region Events

        public event StartedEventHandler?                                                OnStarted;

        public event BoomerangSenderHandler<TCPConnection, DateTime, String[], String>?  OnNotification;

        public event CompletedEventHandler?                                              OnCompleted;


        public event ExceptionOccurredEventHandler?                                       OnExceptionOccurred;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This processor will accept incoming TCP connections and
        /// decode the transmitted data to UTF8 encoded comma-separated
        /// text lines with 0x00, 0x0a (\n) or 0x0d 0x0a (\r\n)
        /// end-of-line characters.
        /// </summary>
        /// <param name="SplitCharacters">The characters to split the incoming CSV text lines.</param>
        public TCPCSVProcessor(Char[]?  SplitCharacters = null)
        {

            this.SplitCharacters  = SplitCharacters ?? ['/'];
            this.ServiceBanner    = DefaultServiceBanner;

        }

        #endregion



        #region ProcessArrow(EventTracking, TCPConnection)

        public void ProcessArrow(EventTracking_Id EventTracking, TCPConnection TCPConnection)
        {

            #region Start

            TCPConnection.WriteLineToResponseStream(ServiceBanner);
            TCPConnection.NoDelay = true;

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

                    switch (TCPConnection.TryRead(out Byte Byte, MaxInitialWaitingTimeMS: ReadTimeout))
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
                                    catch
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
                                    catch
                                    {
                                        TCPConnection.WriteLineToResponseStream("Protocol Error: Invalid CSV data!");
                                    }

                                    #endregion

                                    #region Call OnNotification delegate

                                    TCPResult<String> Result = null;

                                    var OnNotificationLocal = OnNotification;
                                    if (OnNotificationLocal != null)
                                    {

                                        TCPConnection.WriteLineToResponseStream(OnNotificationLocal(TCPConnection,
                                                                                                    Illias.Timestamp.Now,
                                                                                                    CSVArray));

                                    }

                                    #endregion

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
                    //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                //if (OnError != null)
                //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {
                TCPConnection.Close(ClientClose ? ConnectionClosedBy.Client : ConnectionClosedBy.Server);
            }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch
            { }
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.

            #endregion

        }

        #endregion

        #region ProcessExceptionOccurred(Sender, Timestamp, EventTracking, ExceptionMessage)

        public void ProcessExceptionOccurred(Object            Sender,
                                            DateTime          Timestamp,
                                            EventTracking_Id  EventTracking,
                                            Exception         ExceptionMessage)
        {

            OnExceptionOccurred?.Invoke(
                Sender,
                Timestamp,
                EventTracking,
                ExceptionMessage
            );

        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, EventTracking, Message = null)

        public void ProcessCompleted(Object            Sender,
                                     DateTime          Timestamp,
                                     EventTracking_Id  EventTracking,
                                     String?           Message = null)
        {

            OnCompleted?.Invoke(
                Sender,
                Timestamp,
                EventTracking,
                Message
            );

        }

        #endregion


    }

}
