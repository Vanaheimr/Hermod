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

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public class DNSServerConfig
    {

        public DNSTransport  Transport       { get; }
        public DomainName?   ServerName      { get; }
        public IIPAddress    IPAddress       { get; }
        public IPPort        Port            { get; }
        public TimeSpan?     QueryTimeout    { get; set; }



        public DNSServerConfig(IIPAddress     IPAddress,
                               IPPort         Port,
                               DNSTransport?  Transport      = null,
                               TimeSpan?      QueryTimeout   = null)
        {

            this.Transport     = Transport ?? DNSTransport.UDP;
            this.IPAddress     = IPAddress;
            this.Port          = Port;
            this.QueryTimeout  = QueryTimeout;

        }


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{Transport.ToString().ToLower()}://{ServerName?.ToString() ?? IPAddress.ToString()}:{Port}",

                   QueryTimeout.HasValue
                       ? $"{Math.Round(QueryTimeout.Value.TotalSeconds)} sec."
                       : ""

               );

        #endregion

    }

}
