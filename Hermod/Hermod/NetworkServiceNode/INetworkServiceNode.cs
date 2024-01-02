/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The common interface of service nodes.
    /// </summary>
    public interface INetworkServiceNode
    {

        /// <summary>
        /// The unique identification of this charging node.
        /// </summary>
        NetworkServiceNode_Id  Id                { get; }

        /// <summary>
        /// The multi-language name of this charging node.
        /// </summary>
        I18NString             Name              { get; }

        /// <summary>
        /// The multi-language description of this charging node.
        /// </summary>
        I18NString             Description       { get; }


        /// <summary>
        /// The optional default HTTP API.
        /// </summary>
        HTTPAPI?               DefaultHTTPAPI    { get; }


        /// <summary>
        /// The DNS client used by the charging node.
        /// </summary>
        DNSClient              DNSClient         { get; }


        #region HTTP APIs

        /// <summary>
        /// An enumeration of all HTTP APIs.
        /// </summary>
        IEnumerable<HTTPAPI> HTTPAPIs { get; }


        public Boolean AddHTTPAPI(String   HTTPAPIId,
                                  HTTPAPI  HTTPAPI);

        public HTTPAPI? GetHTTPAPI(String HTTPAPIId);

        #endregion


        //ToDo: Add HTTP WebSocket Servers
        //ToDo: Add Trackers
        //ToDo: Add Overlay Networks

        //ToDo: Add ADataStores!?!


    }

}
