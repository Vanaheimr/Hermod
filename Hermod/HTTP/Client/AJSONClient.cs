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
using System.Net.Security;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract base class for all HTTP/JSON clients.
    /// </summary>
    public abstract class AJSONClient : AHTTPClient
    {

        #region Events

        #region OnJSONError

        /// <summary>
        /// A delegate called whenever a JSON error occured.
        /// </summary>
        public delegate void OnSOAPErrorDelegate(DateTime Timestamp, Object Sender, JObject JSON);

        /// <summary>
        /// An event fired whenever a JSON error occured.
        /// </summary>
        public event OnSOAPErrorDelegate OnJSONError;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an abstract HTTP/JSON client.
        /// </summary>
        /// <param name="ClientId">A unqiue identification of this client.</param>
        /// <param name="Hostname">The hostname to connect to.</param>
        /// <param name="RemotePort">The remote TCP port to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="HTTPVirtualHost">An optional HTTP virtual host name to use.</param>
        /// <param name="UserAgent">An optional HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional timeout for upstream queries.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The default number of maximum transmission retries.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public AJSONClient(String                               ClientId,
                           HTTPHostname                         Hostname,
                           IPPort                               RemotePort,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                           LocalCertificateSelectionCallback    ClientCertificateSelector    = null,
                           HTTPHostname?                        HTTPVirtualHost              = null,
                           String                               UserAgent                    = DefaultHTTPUserAgent,
                           TimeSpan?                            RequestTimeout               = null,
                           TransmissionRetryDelayDelegate       TransmissionRetryDelay       = null,
                           Byte?                                MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                           DNSClient                            DNSClient                    = null)

            : base(ClientId,
                   Hostname,
                   RemotePort,
                   RemoteCertificateValidator,
                   ClientCertificateSelector,
                   HTTPVirtualHost,
                   UserAgent,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   DNSClient)

        { }

        #endregion


        #region (protected) SendJSONError(Timestamp, Sender, JSON)

        /// <summary>
        /// Notify that a JSON error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error received.</param>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="JSON">The JSON fault/error.</param>
        protected void SendJSONError(DateTime  Timestamp,
                                     Object    Sender,
                                     JObject   JSON)
        {

            DebugX.Log("AJSONClient => JSON Fault: " + JSON != null ? JSON.ToString() : "<null>");

            OnJSONError?.Invoke(Timestamp, Sender, JSON);

        }

        #endregion


    }

}
