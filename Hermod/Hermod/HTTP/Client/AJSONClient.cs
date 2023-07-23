/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Net.Security;
using System.Security.Authentication;
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
        protected static readonly HTTPPath  DefaultURLPathPrefix  = HTTPPath.Parse("/");

        #endregion

        #region Events

        #region OnJSONError

        /// <summary>
        /// A delegate called whenever a JSON error occured.
        /// </summary>
        public delegate void OnJSONErrorDelegate(DateTime Timestamp, Object Sender, JObject JSON);

        /// <summary>
        /// An event fired whenever a JSON error occured.
        /// </summary>
        public event OnJSONErrorDelegate? OnJSONError;

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
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal buffers.</param>
        /// <param name="DisableLogging">Disable logging.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        protected AJSONClient(URL                                   RemoteURL,
                              HTTPHostname?                         VirtualHostname              = null,
                              String?                               Description                  = null,
                              Boolean?                              PreferIPv4                   = null,
                              RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                              LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                              X509Certificate?                      ClientCert                   = null,
                              SslProtocols?                         TLSProtocol                  = null,
                              String?                               HTTPUserAgent                = DefaultHTTPUserAgent,
                              TimeSpan?                             RequestTimeout               = null,
                              TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                              UInt16?                               MaxNumberOfRetries           = null,
                              UInt32?                               InternalBufferSize           = null,
                              Boolean?                              DisableLogging               = false,
                              DNSClient?                            DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   ClientCertificateSelector,
                   ClientCert,
                   TLSProtocol,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,
                   false,
                   DisableLogging,
                   null,
                   DNSClient)

        { }

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
        public async Task<HTTPResponse<T>>

            Query<T>(JObject                                                         JSONRequest,
                     Func<HTTPResponse<JObject>,                   HTTPResponse<T>>  OnSuccess,
                     Func<DateTime, Object, HTTPResponse<JObject>, HTTPResponse<T>>  OnJSONFault,
                     Func<DateTime, Object, HTTPResponse,          HTTPResponse<T>>  OnHTTPError,
                     Func<DateTime, Object, Exception,             HTTPResponse<T>>  OnException,
                     Action<HTTPRequest.Builder>?                                    HTTPRequestBuilder    = null,
                     ClientRequestLogHandler?                                        RequestLogDelegate    = null,
                     ClientResponseLogHandler?                                       ResponseLogDelegate   = null,

                     EventTracking_Id?                                               EventTrackingId       = null,
                     TimeSpan?                                                       RequestTimeout        = null,
                     Byte                                                            NumberOfRetry         = 0,
                     CancellationToken                                               CancellationToken     = default)

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

            var requestBuilder = CreateRequest(HTTPMethod.POST, RemoteURL.Path);
            requestBuilder.Host               = VirtualHostname ?? RemoteURL.Hostname;
            requestBuilder.Content            = JSONRequest.ToUTF8Bytes();
            requestBuilder.ContentType        = HTTPContentType.JSON_UTF8;
            requestBuilder.UserAgent          = HTTPUserAgent;
            //_RequestBuilder.FakeURLPrefix      = "https://" + (VirtualHostname ?? Hostname);
            requestBuilder.Accept.Add(HTTPContentType.JSON_UTF8);

            HTTPRequestBuilder?.Invoke(requestBuilder);

            var httpResponse = await Execute(requestBuilder,
                                             RequestLogDelegate,
                                             ResponseLogDelegate,

                                             EventTrackingId,
                                             RequestTimeout ?? DefaultRequestTimeout,
                                             NumberOfRetry,
                                             CancellationToken);


            if (httpResponse                is not null          &&
                httpResponse.HTTPStatusCode == HTTPStatusCode.OK &&
                httpResponse.HTTPBody       is not null          &&
                httpResponse.HTTPBody.Length > 0)
            {

                try
                {

            //        var OnHTTPErrorLocal = OnHTTPError;
            //    if (OnHTTPErrorLocal != null)
            //        return OnHTTPErrorLocal(Timestamp.Now, this, HttpResponseTask?.Result);

            //    return new HTTPResponse<JObject>(HttpResponseTask?.Result,
            //                                     new JObject(new JProperty("HTTPError", "")),
            //                                     IsFault: true) as HTTPResponse<T>;

            //}

            //try
            //{

                    var JSON = JObject.Parse(httpResponse.HTTPBody.ToUTF8String());

                    var OnSuccessLocal = OnSuccess;
                    if (OnSuccessLocal is not null)
                        return OnSuccessLocal(new HTTPResponse<JObject>(httpResponse, JSON));

                    //var OnSOAPFaultLocal = OnSOAPFault;
                    //if (OnSOAPFaultLocal != null)
                    //    return OnSOAPFaultLocal(Timestamp.Now, this, new HTTPResponse<XElement>(HttpResponseTask.Result, SOAPXML));

                    return HTTPResponse<JObject>.IsFault(httpResponse,
                                                         new JObject(new JProperty("fault", ""))) as HTTPResponse<T>;


                } catch (Exception e)
                {

                    OnException?.Invoke(Timestamp.Now, this, e);

                    //var OnFaultLocal = OnSOAPFault;
                    //if (OnFaultLocal != null)
                    //    return OnFaultLocal(new HTTPResponse<XElement>(HttpResponseTask.Result, e));

                    return HTTPResponse<JObject>.IsFault(httpResponse,
                                                         new JObject(new JProperty("exception", e.Message))) as HTTPResponse<T>;

                }

            }

            else
            {

                DebugX.LogT("HTTPRespose is null! (" + requestBuilder.Path.ToString() + ")");

                var OnHTTPErrorLocal = OnHTTPError;
                if (OnHTTPErrorLocal is not null)
                    return OnHTTPErrorLocal(Timestamp.Now, this, httpResponse);

                return HTTPResponse<JObject>.IsFault(httpResponse,
                                                     new JObject(
                                                         new JProperty("HTTPError", true)
                                                     )) as HTTPResponse<T>;

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
