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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Sockets.TCP;
using System.Net.Sockets;
using eu.Vanaheimr.Hermod.HTTP;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines
    /// with ending 0x00 or 0x0d 0x0a (\r\n) characters.
    /// </summary>
    public class CSVProcessor : IBoomerangReceiver<Object, DateTime, String, String[], TCPResult<String>>,
                                IBoomerangSender<Object, DateTime, String, String[], TCPResult<String>>
    {

        #region Properties

        #region ServiceBanner

        public String ServiceBanner { get; set; }

        #endregion

        #endregion

        #region Events

        public event StartedEventHandler                                                            OnStarted;

        public event BoomerangSenderHandler<Object, DateTime, String, String[], TCPResult<String>>  OnNotification;

        public event CompletedEventHandler                                                          OnCompleted;


        public event ExceptionOccuredEventHandler                                                   OnExceptionOccured;

        #endregion

        #region Constructor(s)

        public CSVProcessor()
        { }

        #endregion



        public TCPResult<String> ProcessBoomerang(Object Message1, DateTime Timestamp, String ConnectionId, String[] CSVArray)
        {

            var _StringBuilder  = new StringBuilder();
            var ClientClose     = false;

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
                            ClientClose = true;
                            break;

                        case "noop":
                            _StringBuilder.AppendLine("OK");
                            break;

                        case "gettime":
                            _StringBuilder.AppendLine(DateTime.Now.ToUniversalTime().ToString("o"));
                            break;

                        case "getconnectionid":
                            //_StringBuilder.AppendLine(_TCPCSVServer.ConnectionIdBuilder(RemoteSocket) + "\r\n");
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
                    return OnNotificationLocal(Message1,
                                               Timestamp,
                                               ConnectionId,
                                               CSVArray);

            }

            #region Generate result string

            //if (ResultList.Count > 0)
            //{

            //    var GlobalResult = (ResultList.Select(r => r.Status > 0).Aggregate((a, b) => a || b)) ? CSVStatus.ERROR : CSVStatus.OK;
            //    var ReturnString = ResultList.Select(r => r.ToString()).Aggregate((a, b) => a + "|" + b);

            //    Message.WriteToResponseStream(Encoding.UTF8.GetBytes(GlobalResult.ToString() + "\r\n" + ReturnString));
            //    Message.WriteToResponseStream(0x00);

            //}

//            Message.WriteToResponseStream("Unknown data stream '" + CSVArray.Aggregate((a, b) => a + "/" + b) + "'\r\n");

            #endregion

            return new TCPResult<String>(_StringBuilder.ToString(), ClientClose);

        }

        public void ProcessExceptionOccured(object Sender, DateTime Timestamp, Exception ExceptionMessage)
        {
            throw new NotImplementedException();
        }

        public void ProcessCompleted(object Sender, DateTime Timestamp, string Message = null)
        {
            throw new NotImplementedException();
        }



    }

}
