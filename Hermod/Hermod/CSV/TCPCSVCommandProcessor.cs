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

using System;
using System.Text;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines
    /// with ending 0x00 or 0x0d 0x0a (\r\n) characters.
    /// </summary>
    public class TCPCSVCommandProcessor : IBoomerangReceiver<TCPConnection, DateTime, String[], String>,
                                          IBoomerangSender  <String,        DateTime, String[], TCPResult<String>>
    {

        #region Properties

        public String ServiceBanner { get; set; }

        #endregion

        #region Events

        public event StartedEventHandler?                                                    OnStarted;

        public event BoomerangSenderHandler<String, DateTime, String[], TCPResult<String>>?  OnNotification;

        public event CompletedEventHandler?                                                  OnCompleted;


        public event ExceptionOccuredEventHandler?                                           OnExceptionOccured;

        #endregion

        #region Constructor(s)

        public TCPCSVCommandProcessor()
        { }

        #endregion


        #region ProcessBoomerang(TCPConnection, Timestamp, CSVArray)

        public String ProcessBoomerang(TCPConnection TCPConnection, DateTime Timestamp, String[] CSVArray)
        {

            var stringBuilder  = new StringBuilder();

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
                            stringBuilder.AppendLine("Bye!");
                            TCPConnection.Close(ConnectionClosedBy.Client);
                            break;

                        case "noop":
                            stringBuilder.AppendLine("OK");
                            break;

                        case "gettime":
                            stringBuilder.AppendLine(Illias.Timestamp.Now.ToString("o"));
                            break;

                        case "getconnectionid":
                            stringBuilder.AppendLine(TCPConnection.ConnectionId);
                            break;

                        case "help":
                            stringBuilder.AppendLine("bye              Close the TCP connection");
                            stringBuilder.AppendLine("exit             Close the TCP connection");
                            stringBuilder.AppendLine("quit             Close the TCP connection");
                            stringBuilder.AppendLine("logout           Close the TCP connection");
                            stringBuilder.AppendLine("noop             Do nothing, but keep the TCP connection alive");
                            stringBuilder.AppendLine("GetTime          Get the current server time");
                            stringBuilder.AppendLine("GetConnectionId  Get the identification of this TCP connection");
                            stringBuilder.AppendLine("SetTimeout       Set the timeout for this TCP connection [milliseconds]");
                            stringBuilder.AppendLine("help             Get help");
                            stringBuilder.AppendLine();
                            break;

                        default:
                            stringBuilder.AppendLine("Command Error!");
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
                                    stringBuilder.AppendLine("SetTimeout=" + UInt32Value + "ms");
                                }
                                else
                                    stringBuilder.AppendLine("Command Error!");
                                break;

                            default:
                                stringBuilder.AppendLine("Command Error!");
                                break;

                        }

                    }
                    else
                        stringBuilder.AppendLine("Command Error!");

                }

                #endregion

            }

            else
            {

                var onNotification = OnNotification;
                if (onNotification is not null)
                {

                    var result = onNotification(TCPConnection.ConnectionId,
                                                Timestamp,
                                                CSVArray);

                    if (result.ClientClose)
                        TCPConnection.Close(ConnectionClosedBy.Server);

                    return result.Value;

                }

            }

            return stringBuilder.ToString();

        }

        #endregion

        #region ProcessExceptionOccured(Sender, Timestamp, EventTracking, ExceptionMessage)

        public void ProcessExceptionOccured(Object            Sender,
                                            DateTime          Timestamp,
                                            EventTracking_Id  EventTracking,
                                            Exception         ExceptionMessage)
        {

            OnExceptionOccured?.Invoke(
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
