/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Xml.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// An abstract base class for all HTTP/SOAP clients.
    /// </summary>
    public abstract class ASOAPClient : AHTTPClient
    {

        #region Properties

        /// <summary>
        /// The default URI prefix.
        /// </summary>
        public String  URIPrefix   { get; }

        #endregion

        #region Events

        #region OnSOAPError

        /// <summary>
        /// A delegate called whenever a SOAP error occured.
        /// </summary>
        public delegate void OnSOAPErrorDelegate(DateTime Timestamp, Object Sender, XElement SOAPXML);

        /// <summary>
        /// An event fired whenever a SOAP error occured.
        /// </summary>
        public event OnSOAPErrorDelegate OnSOAPError;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an abstract SOAP client.
        /// </summary>
        /// <param name="ClientId">A unqiue identification of this client.</param>
        /// <param name="Hostname">The hostname to connect to.</param>
        /// <param name="RemotePort">The remote TCP port to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="HTTPVirtualHost">An optional HTTP virtual host name to use.</param>
        /// <param name="URIPrefix">An default URI prefix.</param>
        /// <param name="UserAgent">An optional HTTP user agent to use.</param>
        /// <param name="QueryTimeout">An optional timeout for upstream queries.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public ASOAPClient(String                               ClientId,
                           String                               Hostname,
                           IPPort                               RemotePort,
                           RemoteCertificateValidationCallback  RemoteCertificateValidator  = null,
                           X509Certificate                      ClientCert                  = null,
                           String                               HTTPVirtualHost             = null,
                           String                               URIPrefix                   = null,
                           String                               UserAgent                   = DefaultHTTPUserAgent,
                           TimeSpan?                            QueryTimeout                = null,
                           DNSClient                            DNSClient                   = null)

            : base(ClientId,
                   Hostname,
                   RemotePort,
                   RemoteCertificateValidator,
                   ClientCert,
                   HTTPVirtualHost,
                   UserAgent,
                   QueryTimeout,
                   DNSClient)

        {

            this.URIPrefix  = URIPrefix.Trim();

        }

        #endregion


        #region (protected) SendSOAPError(Timestamp, Sender, SOAPXML)

        /// <summary>
        /// Notify that an HTTP error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error received.</param>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="SOAPXML">The SOAP fault/error.</param>
        protected void SendSOAPError(DateTime  Timestamp,
                                     Object    Sender,
                                     XElement  SOAPXML)
        {

            DebugX.Log("AOICPUpstreamService => SOAP Fault: " + SOAPXML != null ? SOAPXML.ToString() : "<null>");

            OnSOAPError?.Invoke(Timestamp, Sender, SOAPXML);

        }

        #endregion


    }

}
