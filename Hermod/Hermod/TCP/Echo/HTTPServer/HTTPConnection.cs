/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A HTTP connection.
    /// </summary>
    public class HTTPConnection : TCPConnection
    {

        #region Data

        private UInt32 keepAliveMessageCount;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this connection should be kept alive.
        /// </summary>
        public Boolean  IsHTTPKeepAlive    { get; set; }

        /// <summary>
        /// The number of requests handled by this connection.
        /// </summary>
        public UInt32   KeepAliveMessageCount
            => Interlocked.CompareExchange(ref keepAliveMessageCount, 0u, 0u);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP connection.
        /// </summary>
        /// <param name="Connection">The underlying TCP connection.</param>
        public HTTPConnection(TCPConnection Connection)

            : base(Connection.TCPServer,
                   Connection.TCPClient,
                   Connection.ServerCertificateSelector,
                   Connection.ClientCertificateValidator,
                   Connection.LocalCertificateSelector,
                   Connection.AllowedTLSProtocols,
                   Connection.SSLStream,
                   Connection.ReadTimeout,
                   Connection.WriteTimeout)

        {

            

        }

        #endregion



        public void IncrementKeepAliveMessageCount()
            => Interlocked.Increment(ref keepAliveMessageCount);


        public override JObject ToJSON()
        {

            var json = base.ToJSON();

            if (IsHTTPKeepAlive)
            {
                json.Add(new JProperty("isHTTPKeepAlive",        IsHTTPKeepAlive));
                json.Add(new JProperty("keepAliveMessageCount",  KeepAliveMessageCount));
            }

            return json;

        }



        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"HTTP {base.ToString()}";

        #endregion

    }

}
