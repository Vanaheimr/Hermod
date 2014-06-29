/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using eu.Vanaheimr.Styx.Arrows;
using eu.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines
    /// with ending 0x00 or 0x0d 0x0a (\r\n) characters.
    /// </summary>
    public class TCPCSVServer : IArrowReceiver<TCPConnection>,
                                IBoomerangSender<Object, DateTime, String, String[], TCPResult<String>>
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
        /// The characters to split the incoming CSV lines.
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

        public event StartedEventHandler                                                            OnStarted;

        public event NewConnectionHandler                                                           OnNewConnection;

        public event BoomerangSenderHandler<Object, DateTime, String, String[], TCPResult<String>>  OnNotification;

        public event ConnectionClosedHandler                                                        OnConnectionClosed;

        public event CompletedEventHandler                                                          OnCompleted;


        public event ExceptionOccuredEventHandler                                                   OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP service accepting incoming CSV lines.
        /// </summary>
        /// <param name="SplitCharacters">The characters to split the incoming CSV lines.</param>
        public TCPCSVServer(Char[]  SplitCharacters = null)
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

            var OnNewConnectionLocal = OnNewConnection;
            if (OnNewConnectionLocal != null)
                OnNewConnectionLocal(TCPConnection.TCPServer,
                                     TCPConnection.ServerTimestamp,
                                     TCPConnection.TCPServer.IPSocket,
                                     TCPConnection.RemoteSocket,
                                     "ConnectionId");

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
                                    EndOfCSVLine = EOLSearch.Found;

                                // \r
                                else if (Byte == 0x0d)
                                    EndOfCSVLine = EOLSearch.R_Read;

                            }

                            // \n after a \r
                            else if (EndOfCSVLine == EOLSearch.R_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfCSVLine = EOLSearch.Found;
                                else
                                    EndOfCSVLine = EOLSearch.NotYet;
                            }

                            #endregion

                            #region ...or append read value(s) to internal buffer

                            if (EndOfCSVLine == EOLSearch.NotYet)
                                MemoryStream.WriteByte(Byte);

                            #endregion


                            #region If end-of-line -> process data...

                            else if (EndOfCSVLine == EOLSearch.Found)
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

                                        Result = OnNotification(TCPConnection.TCPServer,
                                                                DateTime.Now,
                                                                "ConnectionId", //ConnectionIdBuilder(RemoteSocket),
                                                                CSVArray);

                                        TCPConnection.WriteLineToResponseStream(Result.Value);

                                        ClientClose = Result.ClientClose;

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
                TCPConnection.Close();
            }
            catch (Exception e)
            { }

            var OnConnectionClosedLocal = OnConnectionClosed;
            if (OnConnectionClosedLocal != null)
                OnConnectionClosedLocal(TCPConnection.TCPServer,
                                        DateTime.Now,
                                        TCPConnection.TCPServer.IPSocket,
                                        TCPConnection.RemoteSocket,
                                        "ConnectionId", //ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort),
                                        ServerClose ? ConnectionClosedBy.Server : ConnectionClosedBy.Client);

            #endregion

        }

        #endregion

        #region ProcessExceptionOccured(Sender, Timestamp, ExceptionMessage)

        public void ProcessExceptionOccured(Object Sender, DateTime Timestamp, Exception ExceptionMessage)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, Message = null)

        public void ProcessCompleted(Object Sender, DateTime Timestamp, String Message = null)
        {
            throw new NotImplementedException();
        }

        #endregion


    }

}
