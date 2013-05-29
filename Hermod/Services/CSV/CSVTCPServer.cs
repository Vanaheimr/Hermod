/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Collections.Generic;

using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.Services
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines with an ending 0x00 character.
    /// </summary>
    public class CSVTCPServer : ICSVTCPServer
    {

        #region Data

        /// <summary>
        /// The internal TCP server.
        /// </summary>
        private readonly TCPServer InternalTCPServer;

        #endregion

        #region Properties

        #region ServiceBanner

        /// <summary>
        /// The TCP service banner transmitted to an TCP client on
        /// connection setup.
        /// </summary>
        public String ServiceBanner { get; set; }

        #endregion

        #region SplitCharacters

        /// <summary>
        /// The characters to split the incoming CSV lines.
        /// </summary>
        public Char[] SplitCharacters { get; private set; }

        #endregion

        #endregion

        #region Events

        #region OnStarted

        /// <summary>
        /// An event fired whenever the service started.
        /// </summary>
        public event OnStartedDelegate OnStarted;

        #endregion

        #region OnNewConnection

        /// <summary>
        /// An event fired whenever a new connection was made.
        /// </summary>
        public event NewConnectionDelegate OnNewConnection;

        #endregion

        #region OnDataAvailable

        /// <summary>
        /// An event fired whenever new data is available.
        /// </summary>
        public event DataAvailableDelegate OnDataAvailable;

        #endregion

        #region OnResult

        /// <summary>
        /// An event fired whenever an result is available.
        /// </summary>
        public event ResultDelegate OnResult;

        #endregion

        #region OnExceptionOccurred

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event ExceptionOccurredDelegate OnExceptionOccurred;

        #endregion

        #region OnConnectionClosed

        /// <summary>
        /// An event fired whenever a connection was closed.
        /// </summary>
        public event NewConnectionDelegate OnConnectionClosed;

        #endregion

        #region OnStoppeded

        /// <summary>
        /// An event fired whenever the service stopped.
        /// </summary>
        public event OnStoppededDelegate OnStopped;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP service accepting incoming CSV lines.
        /// </summary>
        /// <param name="IPPort">The IP port to bind.</param>
        /// <param name="SplitCharacters">The characters to split the incoming CSV lines.</param>
        /// <param name="ServiceBanner">The identifiying banner of the service.</param>
        public CSVTCPServer(IPPort IPPort, Char[] SplitCharacters = null, String ServiceBanner = "Vanaheimr Hermod CSV TCP Service v0.2")
        {

            this.ServiceBanner    = ServiceBanner;
            this.SplitCharacters  = (SplitCharacters != null) ? SplitCharacters : new Char[1] { '/' };

            InternalTCPServer = new TCPServer(IPPort,

                                              newTCPConnection => {

                                                  if (OnNewConnection != null)
                                                      OnNewConnection(this, DateTime.Now, newTCPConnection.RemoteHost, newTCPConnection.RemotePort);

                                                  newTCPConnection.NoDelay = true;
                                                  newTCPConnection.WriteToResponseStream(ServiceBanner);
                                                  newTCPConnection.WriteToResponseStream("\r\n");
                                                  newTCPConnection.WriteToResponseStream(0x00);

                                                  Byte Byte;
                                                  var MemoryStream = new MemoryStream();

                                                  try
                                                  {

                                                      while (newTCPConnection.IsConnected)
                                                      {

                                                          Byte = newTCPConnection.ReadByte();

                                                          if (Byte != 0x00)
                                                              MemoryStream.WriteByte(Byte);

                                                          else
                                                          {

                                                              if (OnDataAvailable != null)
                                                              {

                                                                  var data     = MemoryStream.ToArray();
                                                                  var results  = new List<CSVResult>();

                                                                  String[] CSVData = null;

                                                                  try
                                                                  {

                                                                      CSVData = Encoding.UTF8.GetString(data).
                                                                                              Trim().
                                                                                              Split(SplitCharacters,
                                                                                                    StringSplitOptions.RemoveEmptyEntries);

                                                                  }
                                                                  catch (Exception)
                                                                  {
                                                                      results.Add(new CSVResult(CSVStatus.ERROR, "Invalid CSV string: " + data));
                                                                  }

                                                                  if (data.Length > 0)
                                                                      OnDataAvailable(this,
                                                                                      results,
                                                                                      DateTime.Now,
                                                                                      CSVData);

                                                                  if (OnResult != null)
                                                                      OnResult(this, DateTime.Now, results);

                                                                  newTCPConnection.WriteToResponseStream(Encoding.UTF8.GetBytes(results.Select(r => r.ToString()).Aggregate((a, b) => a + "|" + b)));
                                                                  newTCPConnection.WriteToResponseStream("\r\n");
                                                                  newTCPConnection.WriteToResponseStream(0x00);
                                                                  Thread.Sleep(10);
                                                                  newTCPConnection.Close();

                                                              }

                                                          }

                                                      }

                                                  }
                                                  catch (Exception e)
                                                  {
                                                      if (OnExceptionOccurred != null)
                                                          OnExceptionOccurred(this, DateTime.Now, newTCPConnection.RemoteHost, newTCPConnection.RemotePort, e, MemoryStream);
                                                  }

                                                  if (OnConnectionClosed != null)
                                                      OnConnectionClosed(this, DateTime.Now, newTCPConnection.RemoteHost, newTCPConnection.RemotePort);

                                                  newTCPConnection.Close();

                                              },

                                              Autostart: false);

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start the service.
        /// </summary>
        public void Start()
        {

            InternalTCPServer.Start();

            if (OnStarted != null)
                OnStarted(this, DateTime.Now);

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop the service.
        /// </summary>
        public void Stop()
        {

            InternalTCPServer.StopAndWait();

            if (OnStopped != null)
                OnStopped(this, DateTime.Now);

        }

        #endregion

    }

}
