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
using System.Text;

using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines
    /// with ending 0x00 or 0x0d 0x0a (\r\n) characters.
    /// </summary>
    public class TCPCSVCommandProcessor : IBoomerangReceiver<TCPConnection, DateTime, String[], String>,
                                          IBoomerangSender  <String,        DateTime, String[], TCPResult<String>>
    {

        #region Properties

        #region ServiceBanner

        public String ServiceBanner { get; set; }

        #endregion

        #endregion

        #region Events

        public event StartedEventHandler                                                    OnStarted;

        public event BoomerangSenderHandler<String, DateTime, String[], TCPResult<String>>  OnNotification;

        public event CompletedEventHandler                                                  OnCompleted;


        public event ExceptionOccuredEventHandler                                           OnExceptionOccured;

        #endregion

        #region Constructor(s)

        public TCPCSVCommandProcessor()
        { }

        #endregion


        #region ProcessBoomerang(TCPConnection, Timestamp, CSVArray)

        public String ProcessBoomerang(TCPConnection TCPConnection, DateTime Timestamp, String[] CSVArray)
        {

            var _StringBuilder  = new StringBuilder();

            if (CSVArray.Length == 1)
            {

                var Command = CSVArray[0].ToLower().Trim();

                #region Parameterless command

                if (!Command.Contains("="))
                {

                    switch (Command)
                    {

                        case "bye":
                        case "exit":
                        case "quit":
                        case "logout":
                            _StringBuilder.AppendLine("Bye!");
                            TCPConnection.Close(ConnectionClosedBy.Client);
                            break;

                        case "noop":
                            _StringBuilder.AppendLine("OK");
                            break;

                        case "gettime":
                            _StringBuilder.AppendLine(DateTime.Now.ToUniversalTime().ToString("o"));
                            break;

                        case "getconnectionid":
                            _StringBuilder.AppendLine(TCPConnection.ConnectionId);
                            break;

                        case "help":
                            _StringBuilder.AppendLine("bye              Close the TCP connection");
                            _StringBuilder.AppendLine("exit             Close the TCP connection");
                            _StringBuilder.AppendLine("quit             Close the TCP connection");
                            _StringBuilder.AppendLine("logout           Close the TCP connection");
                            _StringBuilder.AppendLine("noop             Do nothing, but keep the TCP connection alive");
                            _StringBuilder.AppendLine("GetTime          Get the current server time");
                            _StringBuilder.AppendLine("GetConnectionId  Get the identification of this TCP connection");
                            _StringBuilder.AppendLine("SetTimeout       Set the timeout for this TCP connection [milliseconds]");
                            _StringBuilder.AppendLine("help             Get help");
                            _StringBuilder.AppendLine();
                            break;

                        default:
                            _StringBuilder.AppendLine("Command Error!");
                            break;

                    }

                }

                #endregion

                #region Commands with parameters

                else
                {

                    var Parameter = Command.Split(new Char[] { '=' }, 2, StringSplitOptions.None);

                    if (Parameter.Length == 2)
                    {

                        switch (Parameter[0].Trim())
                        {

                            case "settimeout":
                                var UInt32Value = 0U;
                                if (UInt32.TryParse(Parameter[1].Trim(), out UInt32Value))
                                {
                                    _StringBuilder.AppendLine("SetTimeout=" + UInt32Value + "ms");
                                }
                                else
                                    _StringBuilder.AppendLine("Command Error!");
                                break;

                            default:
                                _StringBuilder.AppendLine("Command Error!");
                                break;

                        }

                    }
                    else
                        _StringBuilder.AppendLine("Command Error!");

                }

                #endregion

            }

            else
            {

                var OnNotificationLocal = OnNotification;
                if (OnNotificationLocal != null)
                {

                    var Result = OnNotificationLocal(TCPConnection.ConnectionId,
                                                     Timestamp,
                                                     CSVArray);

                    if (Result.ClientClose)
                        TCPConnection.Close(ConnectionClosedBy.Server);

                    return Result.Value;

                }

            }

            return _StringBuilder.ToString();

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
