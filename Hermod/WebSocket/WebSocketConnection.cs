/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Net;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A web socket connection.
    /// </summary>
    public class WebSocketConnection
    {

        #region Data

        private readonly Dictionary<String, String> httpHeaders;

        private readonly Dictionary<String, Object> customData;

        #endregion

        #region Properties

        public DateTime                                   Created                       { get; }

        public CancellationTokenSource                    CancellationTokenSource       { get; }

        public WebSocketServer                            WebSocketServer               { get; }

        public TcpClient                                  TcpClient                     { get; }

        public IPSocket                                   LocalSocket                   { get; }

        public IPSocket                                   RemoteSocket                  { get; }

        public String                                     HTTPMethod                    { get; internal set; }

        public HTTPPath                                   HTTPPath                      { get; internal set; }

        public String                                     HTTPVersion                   { get; internal set; }


        public IEnumerable<KeyValuePair<String, String>>  HTTPHeaders
                   => httpHeaders;

        #endregion

        #region Constructor(s)

        public WebSocketConnection(WebSocketServer                            WebSocketServer,
                                   TcpClient                                  TcpClient,
                                   IEnumerable<KeyValuePair<String, String>>  HTTPHeaders   = null,
                                   IEnumerable<KeyValuePair<String, Object>>  CustomData    = null)
        {

            this.Created                  = DateTime.UtcNow;
            this.CancellationTokenSource  = new CancellationTokenSource();
            this.WebSocketServer          = WebSocketServer;
            this.TcpClient                = TcpClient;
            this.LocalSocket              = new IPSocket(TcpClient.Client.LocalEndPoint  as IPEndPoint);
            this.RemoteSocket             = new IPSocket(TcpClient.Client.RemoteEndPoint as IPEndPoint);
            this.httpHeaders              = HTTPHeaders != null
                                                ? HTTPHeaders.ToDictionary(kvp => kvp.Key,
                                                                           kvp => kvp.Value)
                                                : new Dictionary<String, String>();
            this.customData               = CustomData != null
                                                ? CustomData. ToDictionary(kvp => kvp.Key,
                                                                           kvp => kvp.Value)
                                                : new Dictionary<String, Object>();

        }

        #endregion


        internal void AddHTTPHeader(String  Key,
                                    String  Value)
        {

            httpHeaders.Add(Key,
                             Value);

        }

        public String GetHTTPHeader(String Key)
        {

            if (httpHeaders.TryGetValue(Key, out String Value))
                return Value;

            return "";

        }



        public void AddCustomData(String  Key,
                                  Object  Value)
        {

            customData.Add(Key,
                           Value);

        }

        public T GetCustomData<T>(String  Key)
        {

            if (customData.TryGetValue(Key, out Object data) && data is T dataT)
                return dataT;

            return default;

        }

        public T? TryGetCustomData<T>(String Key)
            where T : struct
        {

            if (customData.TryGetValue(Key, out Object data) && data is T dataT)
                return dataT;

            return default;

        }

    }

}
