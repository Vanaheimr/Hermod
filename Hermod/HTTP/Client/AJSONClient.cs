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
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract HTTP/JSON client.
    /// </summary>
    public abstract class AJSONClient : AHTTPClient
    {

        #region Data

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public new const          String    DefaultHTTPUserAgent  = "GraphDefined HTTP/JSON Client";

        /// <summary>
        /// The default URL path prefix.
        /// </summary>
        protected static readonly HTTPPath  DefaultURLPathPrefix      = HTTPPath.Parse("/");

        #endregion

        #region Properties

        /// <summary>
        /// The default URL path prefix.
        /// </summary>
        public HTTPPath  URLPathPrefix    { get; }

        #endregion

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
        /// Create a new abstract HTTP/JSON client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the OICP HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this CPO client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        protected AJSONClient(URL                                  RemoteURL,
                              HTTPHostname?                        VirtualHostname              = null,
                              String                               Description                  = null,
                              RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                              LocalCertificateSelectionCallback    ClientCertificateSelector    = null,
                              X509Certificate                      ClientCert                   = null,
                              String                               HTTPUserAgent                = DefaultHTTPUserAgent,
                              HTTPPath?                            URLPathPrefix                = null,
                              TimeSpan?                            RequestTimeout               = null,
                              TransmissionRetryDelayDelegate       TransmissionRetryDelay       = null,
                              UInt16?                              MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                              DNSClient                            DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   RemoteCertificateValidator,
                   ClientCertificateSelector,
                   ClientCert,
                   HTTPUserAgent      ?? DefaultHTTPUserAgent,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries ?? DefaultMaxNumberOfRetries,
                   false,
                   null,
                   DNSClient)

        {

            this.URLPathPrefix  = URLPathPrefix ?? DefaultURLPathPrefix;

        }

        #endregion


        #region Query(JSONRequest, OnSuccess, OnJSONFault, OnHTTPError, OnException, RequestTimeout = null)

        /// <summary>
        /// Create a new JSON request task.
        /// </summary>
        /// <typeparam name="T">The type of the return data structure.</typeparam>
        /// <param name="JSONRequest">The JSON request.</param>
        /// <param name="OnSuccess">The delegate to call for every successful result.</param>
        /// <param name="OnJSONFault">The delegate to call whenever a JSON fault occured.</param>
        /// <param name="OnHTTPError">The delegate to call whenever a HTTP error occured.</param>
        /// <param name="OnException">The delegate to call whenever an exception occured.</param>
        /// <param name="RequestTimeout">An optional timeout of the HTTP client [default 60 sec.]</param>
        /// <returns>The data structured after it had been processed by the OnSuccess delegate, or a fault.</returns>
        public async Task<HTTPResponse<T>>

            Query<T>(JObject                                                         JSONRequest,
                     Func<HTTPResponse<JObject>,                   HTTPResponse<T>>  OnSuccess,
                     Func<DateTime, Object, HTTPResponse<JObject>, HTTPResponse<T>>  OnJSONFault,
                     Func<DateTime, Object, HTTPResponse,          HTTPResponse<T>>  OnHTTPError,
                     Func<DateTime, Object, Exception,             HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>                                     HTTPRequestBuilder    = null,
                     ClientRequestLogHandler                                         RequestLogDelegate    = null,
                     ClientResponseLogHandler                                        ResponseLogDelegate   = null,

                     CancellationToken?                                              CancellationToken     = null,
                     EventTracking_Id                                                EventTrackingId       = null,
                     TimeSpan?                                                       RequestTimeout        = null,
                     Byte                                                            NumberOfRetry         = 0)

        {

            #region Initial checks

            if (JSONRequest == null)
                throw new ArgumentNullException(nameof(JSONRequest),  "The JSON request must not be null!");

            if (OnSuccess   == null)
                throw new ArgumentNullException(nameof(OnSuccess),    "The 'OnSuccess'-delegate must not be null!");

            if (OnJSONFault == null)
                throw new ArgumentNullException(nameof(OnJSONFault),  "The 'OnJSONFault'-delegate must not be null!");

            if (OnHTTPError == null)
                throw new ArgumentNullException(nameof(OnHTTPError),  "The 'OnHTTPError'-delegate must not be null!");

            if (OnException == null)
                throw new ArgumentNullException(nameof(OnException),  "The 'OnException'-delegate must not be null!");

            #endregion

            var _RequestBuilder = CreateRequest(HTTPMethod.POST, URLPathPrefix);
            _RequestBuilder.Host               = VirtualHostname ?? Hostname;
            _RequestBuilder.Content            = JSONRequest.ToUTF8Bytes();
            _RequestBuilder.ContentType        = HTTPContentType.JSON_UTF8;
            _RequestBuilder.UserAgent          = HTTPUserAgent;
            //_RequestBuilder.FakeURLPrefix      = "https://" + (VirtualHostname ?? Hostname);

            HTTPRequestBuilder?.Invoke(_RequestBuilder);

            var HttpResponse = await Execute(_RequestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,
                                             CancellationToken.HasValue  ? CancellationToken.Value : new CancellationTokenSource().Token,
                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry);


            if (HttpResponse                 != null              &&
                HttpResponse.HTTPStatusCode  == HTTPStatusCode.OK &&
                HttpResponse.HTTPBody        != null              &&
                HttpResponse.HTTPBody.Length > 0)
            {

                try
                {

            //        var OnHTTPErrorLocal = OnHTTPError;
            //    if (OnHTTPErrorLocal != null)
            //        return OnHTTPErrorLocal(DateTime.UtcNow, this, HttpResponseTask?.Result);

            //    return new HTTPResponse<JObject>(HttpResponseTask?.Result,
            //                                     new JObject(new JProperty("HTTPError", "")),
            //                                     IsFault: true) as HTTPResponse<T>;

            //}

            //try
            //{

                    var JSON = JObject.Parse(HttpResponse.HTTPBody.ToUTF8String());

                    var OnSuccessLocal = OnSuccess;
                    if (OnSuccessLocal != null)
                        return OnSuccessLocal(new HTTPResponse<JObject>(HttpResponse, JSON));

                    //var OnSOAPFaultLocal = OnSOAPFault;
                    //if (OnSOAPFaultLocal != null)
                    //    return OnSOAPFaultLocal(DateTime.UtcNow, this, new HTTPResponse<XElement>(HttpResponseTask.Result, SOAPXML));

                    return new HTTPResponse<JObject>(HttpResponse,
                                                     new JObject(new JProperty("fault", "")),
                                                     IsFault: true) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(DateTime.UtcNow, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal != null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return new HTTPResponse<JObject>(HttpResponse,
                                                     new JObject(new JProperty("exception", e.Message)),
                                                     IsFault: true) as HTTPResponse<T>;

                }

            }

            else
            {

                DebugX.LogT("HTTPRepose is null! (" + _RequestBuilder.Path.ToString() + ")");

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal != null)
                    return OnHTTPErrorLocal(DateTime.UtcNow, this, HttpResponse);

                return new HTTPResponse<JObject>(HttpResponse,
                                                 new JObject(
                                                     new JProperty("HTTPError", true)
                                                 ),
                                                 IsFault: true) as HTTPResponse<T>;

            }

        }

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
