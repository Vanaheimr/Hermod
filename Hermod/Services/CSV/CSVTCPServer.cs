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

namespace eu.Vanaheimr.Hermod.Services.CSV
{

    /// <summary>
    /// A TCP service accepting incoming CSV lines
    /// with ending 0x00 or 0x0d 0x0a (\r\n) characters.
    /// </summary>
    public class CSVTCPServer
    {

        #region Data

        private const String DefaultServiceBanner = "Vanaheimr Hermod CSV TCP Service v0.5";

        /// <summary>
        /// The internal TCP server.
        /// </summary>
        private readonly List<TCPServer> InternalTCPServers;

        /// <summary>
        /// The internal delegate called for every new TCP connection.
        /// </summary>
        private readonly TCPServer.OnNewClientConnectionDelegate NewConnectionDelegate;

        #endregion

        #region Properties

        #region ServiceBanner

        /// <summary>
        /// The TCP service banner transmitted to a TCP client
        /// at connection initialization.
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

        #region OnTCPPortAdded

        /// <summary>
        /// An event fired whenever a new TCP Port was added to the service.
        /// </summary>
        public event OnTCPPortAddedDelegate OnTCPPortAdded;

        #endregion

        #region OnTCPPortRemoved

        /// <summary>
        /// An event fired whenever a new TCP Port was removed from the service.
        /// </summary>
        public event OnTCPPortRemovedDelegate OnTCPPortRemoved;

        #endregion

        #region OnNewConnection

        /// <summary>
        /// An event fired whenever a new connection was made.
        /// </summary>
        public event OnNewConnectionDelegate OnNewConnection;

        #endregion

        #region OnDataAvailable

        /// <summary>
        /// An event fired whenever new data is available.
        /// </summary>
        public event OnDataAvailableDelegate OnDataAvailable;

        #endregion

        #region OnResult

        /// <summary>
        /// An event fired whenever an result is available.
        /// </summary>
        public event OnResultDelegate OnResult;

        #endregion

        #region OnError

        /// <summary>
        /// An event fired whenever an error occured.
        /// </summary>
        public event OnExceptionOccurredDelegate OnError;

        #endregion

        #region OnConnectionClosed

        /// <summary>
        /// An event fired whenever a connection was closed.
        /// </summary>
        public event OnConnectionClosedDelegate OnConnectionClosed;

        #endregion

        #region OnStoppeded

        /// <summary>
        /// An event fired whenever the service stopped.
        /// </summary>
        public event OnStoppededDelegate OnStopped;

        #endregion

        #endregion

        #region Constructor(s)

        #region (private) CSVTCPServer(SplitCharacters = null, ServiceBanner = DefaultServiceBanner)

        /// <summary>
        /// Create a new TCP service accepting incoming CSV lines.
        /// </summary>
        /// <param name="SplitCharacters">The characters to split the incoming CSV lines.</param>
        /// <param name="ServiceBanner">The identifiying banner of the service.</param>
        private CSVTCPServer(Char[] SplitCharacters  = null,
                             String ServiceBanner    = DefaultServiceBanner)
        {

            this.ServiceBanner          = ServiceBanner;
            this.SplitCharacters        = (SplitCharacters != null) ? SplitCharacters : new Char[1] { '/' };
            this.InternalTCPServers     = new List<TCPServer>();
            this.NewConnectionDelegate  = newTCPConnection => {

                                            #region Initial stuff

#if __MonoCS__
                                            // Code for Mono C# compiler
#else
                                            Thread.CurrentThread.Name = "CSV TCP from " +
                                                    newTCPConnection.RemoteHost.ToString() +
                                                    ":" +
                                                    newTCPConnection.RemotePort.ToString();
#endif

                                            if (OnNewConnection != null)
                                                OnNewConnection(this, DateTime.Now, newTCPConnection.RemoteHost + ":" + newTCPConnection.RemotePort);

                                            newTCPConnection.ReadTimeout = 180000;
                                            newTCPConnection.WriteToResponseStream(this.ServiceBanner);
                                            newTCPConnection.WriteToResponseStream("\r\n");
                                            //newTCPConnection.WriteToResponseStream(0x00);
                                            newTCPConnection.NoDelay     = true;

                                            Byte Byte;
                                            var Buffer        = new Byte[1024];
                                            int bytesread     = 0;
                                            var MemoryStream  = new MemoryStream();
                                            var EndOfCSVLine  = 0U;
                                            var ClientClose   = false;
                                            var ServerClose   = false;

                                            #endregion

                                            try
                                            {

                                                do
                                                {

                                                    //bytesread = newTCPConnection.Read(Buffer);

                                                    switch (newTCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: 180000))
                                                    {

                                                        #region DataAvailable

                                                        case TCPClientResponse.DataAvailable: 

                                                            #region Check for CSV line ending...

                                                            if (Byte == 0x00)
                                                                EndOfCSVLine = 2;

                                                            else if (Byte == 0x0d)
                                                                EndOfCSVLine = 1;

                                                            else if (EndOfCSVLine == 1)
                                                            {
                                                                if (Byte == 0x0a)
                                                                    EndOfCSVLine = 2;
                                                                else
                                                                    throw new IOException("Protocol Error: Invalid CSV line ending!");
                                                            }

                                                            #endregion

                                                            #region ...or append read value(s) to internal buffer

                                                            if (EndOfCSVLine == 0)
                                                                MemoryStream.WriteByte(Byte);

                                                            #endregion


                                                            #region If end-of-line -> process data...

                                                            else if (EndOfCSVLine == 2)
                                                            {

                                                                if (MemoryStream.Length > 0 && OnDataAvailable != null)
                                                                {

                                                                    #region Check UTF8 encoding

                                                                    var CSVLine = String.Empty;

                                                                    try
                                                                    {
                                                                        CSVLine = Encoding.UTF8.GetString(MemoryStream.ToArray());
                                                                    }
                                                                    catch (Exception)
                                                                    {
                                                                        throw new IOException("Protocol Error: Invalid UTF8 encoding!");
                                                                    }

                                                                    #endregion

                                                                    #region Check CSV separation

                                                                    String[] CSVArray = null;

                                                                    try
                                                                    {

                                                                        CSVArray = CSVLine.Trim().
                                                                                            Split(SplitCharacters,
                                                                                                StringSplitOptions.RemoveEmptyEntries);

                                                                    }
                                                                    catch (Exception)
                                                                    {
                                                                        throw new IOException("Protocol Error: Invalid CSV data!");
                                                                    }

                                                                    #endregion

                                                                    #region Check if a CSV command was entered

                                                                    if (CSVArray.Length == 1)
                                                                    {

                                                                        var Command = CSVArray[0].ToLower().Trim();

                                                                        if (!Command.Contains("="))
                                                                        {

                                                                            switch (Command)
                                                                            {

                                                                                case "bye":
                                                                                case "exit":
                                                                                case "quit":
                                                                                case "logout":
                                                                                    newTCPConnection.WriteToResponseStream("Bye!\r\n");
                                                                                    ClientClose = true;
                                                                                    break;

                                                                                case "noop" :
                                                                                    newTCPConnection.WriteToResponseStream("OK\r\n");
                                                                                    break;

                                                                                case "gettime":
                                                                                    newTCPConnection.WriteToResponseStream(DateTime.Now.ToUniversalTime().ToString("o") + "\r\n");
                                                                                    break;

                                                                                case "getconnectionid":
                                                                                    newTCPConnection.WriteToResponseStream(newTCPConnection.RemoteHost.ToString() + ":" +
                                                                                                                           newTCPConnection.RemotePort.ToString() + "\r\n");
                                                                                    break;

                                                                                case "help":
                                                                                    newTCPConnection.WriteToResponseStream("bye        Close the TCP connection\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("exit        Close the TCP connection\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("quit        Close the TCP connection\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("logout        Close the TCP connection\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("noop        Do nothing, but keep the TCP connection alive\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("GetTime        Get the current server time\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("SetTimeout    Set the timeout for this TCP connection [milliseconds]\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("help        Get help\r\n");
                                                                                    newTCPConnection.WriteToResponseStream("\r\n");
                                                                                    break;

                                                                                default:
                                                                                    newTCPConnection.WriteToResponseStream("Command Error!\r\n");
                                                                                    break;

                                                                            }

                                                                        }

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
                                                                                            newTCPConnection.WriteToResponseStream("SetTimeout=" + UInt32Value + "ms\r\n");
                                                                                        }
                                                                                        else
                                                                                            newTCPConnection.WriteToResponseStream("Command Error!\r\n");
                                                                                        break;

                                                                                    default:
                                                                                        newTCPConnection.WriteToResponseStream("Command Error!\r\n");
                                                                                        break;

                                                                                }

                                                                            }
                                                                            else
                                                                                newTCPConnection.WriteToResponseStream("Command Error!\r\n");

                                                                        }

                                                                    }

                                                                    #endregion

                                                                    else
                                                                    {

                                                                        #region Call OnDataAvailable delegate

                                                                        var ResultList = new List<CSVResult>();

                                                                        OnDataAvailable(this,
                                                                                        DateTime.Now,
                                                                                        "TCP:" + newTCPConnection.RemoteHost.ToString() + ":" + newTCPConnection.RemotePort.ToString(),
                                                                                        CSVArray,
                                                                                        ResultList);

                                                                        #endregion

                                                                        #region Call OnResult delegate

                                                                        if (ResultList.Count > 0)
                                                                            if (OnResult != null)
                                                                                OnResult(this, DateTime.Now, newTCPConnection.RemoteHost.ToString() + ":" + newTCPConnection.RemotePort.ToString(), ResultList);

                                                                        #endregion

                                                                        #region Generate result string

                                                                        if (ResultList.Count > 0)
                                                                        {

                                                                            var GlobalResult = (ResultList.Select(r => r.Status > 0).Aggregate((a, b) => a || b)) ? CSVStatus.ERROR : CSVStatus.OK;
                                                                            var ReturnString = ResultList.Select(r => r.ToString()).Aggregate((a, b) => a + "|" + b);

                                                                            newTCPConnection.WriteToResponseStream(Encoding.UTF8.GetBytes(GlobalResult.ToString() + "\r\n" + ReturnString));
//                                                                                   newTCPConnection.WriteToResponseStream(0x00);

                                                                        }
                                                                        else
                                                                        {
                                                                            newTCPConnection.WriteToResponseStream("Unknown data stream '" + CSVArray.Aggregate((a, b) => a + "/" + b) + "'\r\n");
//                                                                                   newTCPConnection.WriteToResponseStream(0x00);
                                                                        }

                                                                        #endregion

                                                                    }

                                                                }

                                                                MemoryStream.SetLength(0);
                                                                MemoryStream.Seek(0, SeekOrigin.Begin);
                                                                EndOfCSVLine = 0;

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

                                                    if (OnError != null)
                                                        OnError(this, DateTime.Now, newTCPConnection.RemoteHost + ":" + newTCPConnection.RemotePort, ioe, MemoryStream);

                                                }

                                            }

                                            catch (Exception e)
                                            {

                                                if (OnError != null)
                                                    OnError(this, DateTime.Now, newTCPConnection.RemoteHost + ":" + newTCPConnection.RemotePort, e, MemoryStream);

                                            }

                                            #endregion

                                            #region Close the TCP connection

                                            try
                                            {
                                                newTCPConnection.Close();
                                            }
                                            catch (Exception e)
                                            { }

                                            if (OnConnectionClosed != null)
                                                OnConnectionClosed(this,
                                                                    DateTime.Now,
                                                                    newTCPConnection.RemoteHost + ":" + newTCPConnection.RemotePort,
                                                                    ServerClose ? ConnectionClosedBy.Server : ConnectionClosedBy.Client);

                                            #endregion

                                        };

        }

        #endregion

        #region CSVTCPServer(IPPort,   SplitCharacters = null, ServiceBanner = DefaultServiceBanner)

        /// <summary>
        /// Create a new TCP service accepting incoming CSV lines.
        /// </summary>
        /// <param name="IPPort">The IP port to bind to.</param>
        /// <param name="SplitCharacters">The characters to split the incoming CSV lines.</param>
        /// <param name="ServiceBanner">The identifiying banner of the service.</param>
        public CSVTCPServer(IPPort IPPort,
                            Char[] SplitCharacters  = null,
                            String ServiceBanner    = DefaultServiceBanner)

            : this(SplitCharacters, ServiceBanner)

        {
            AddTCPPort(IPPort);
        }

        #endregion

        #region CSVTCPServer(IPPorts,  SplitCharacters = null, ServiceBanner = DefaultServiceBanner)

        /// <summary>
        /// Create a new TCP service accepting incoming CSV lines.
        /// </summary>
        /// <param name="IPPorts">The IP ports to bind to.</param>
        /// <param name="SplitCharacters">The characters to split the incoming CSV lines.</param>
        /// <param name="ServiceBanner">The identifiying banner of the service.</param>
        public CSVTCPServer(IEnumerable<IPPort> IPPorts,
                            Char[]              SplitCharacters  = null,
                            String              ServiceBanner    = DefaultServiceBanner)

            : this(SplitCharacters, ServiceBanner)

        {

            foreach (var IPPort in IPPorts)
                AddTCPPort(IPPort);

        }

        #endregion

        #endregion


        #region Start()

        /// <summary>
        /// Start the service.
        /// </summary>
        public void Start()
        {

            if (OnStarted != null)
                OnStarted(this, DateTime.Now);

            foreach (var InternalTCPServer in InternalTCPServers)
            {

                InternalTCPServer.Start();

                if (OnTCPPortAdded != null)
                    OnTCPPortAdded(this, DateTime.Now, InternalTCPServer.Port);

            }

        }

        #endregion

        #region AddTCPPort(TCPPort)

        /// <summary>
        /// Add an additional listening TCP port.
        /// </summary>
        public void AddTCPPort(IPPort TCPPort)
        {

            if (TCPPort == null)
                throw new ArgumentNullException("Invalid TCP port!");

            var NewTCPServer = new TCPServer(TCPPort, this.NewConnectionDelegate, Autostart: false);

            InternalTCPServers.Add(NewTCPServer);

            NewTCPServer.Start();

            if (OnTCPPortAdded != null)
                OnTCPPortAdded(this, DateTime.Now, TCPPort);

        }

        #endregion

        #region RemoveTCPPort(TCPPort)

        /// <summary>
        /// Add an additional listening TCP port.
        /// </summary>
        public Boolean RemoveTCPPort(IPPort TCPPort)
        {

            if (TCPPort == null)
                throw new ArgumentNullException("Invalid TCP port!");

            var TCPServer = InternalTCPServers.
                                Where(s => s.Port == TCPPort).
                                FirstOrDefault();

            if (TCPServer == null)
                return false;

            TCPServer.StopAndWait();
            InternalTCPServers.Remove(TCPServer);

            if (OnTCPPortRemoved != null)
                OnTCPPortRemoved(this, DateTime.Now, TCPPort);

            return true;

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop the service.
        /// </summary>
        public void Stop()
        {

            foreach (var InternalTCPServer in InternalTCPServers)
            {

                InternalTCPServer.StopAndWait();

                if (OnTCPPortRemoved != null)
                    OnTCPPortRemoved(this, DateTime.Now, InternalTCPServer.Port);

            }

            if (OnStopped != null)
                OnStopped(this, DateTime.Now);

        }

        #endregion

    }

}
