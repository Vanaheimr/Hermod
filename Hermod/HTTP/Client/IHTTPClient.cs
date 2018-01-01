/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Net.Sockets;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IHTTPClient : IDisposable
    {

        //int Available { get; }
        //X509Certificate ClientCert { get; }
        //bool Connected { get; }
        
        //string Hostname { get; }
        //LingerOption LingerState { get; set; }
        //LocalCertificateSelectionCallback LocalCertificateSelector { get; }
        //bool NoDelay { get; set; }
        RemoteCertificateValidationCallback RemoteCertificateValidator { get; }
        //IIPAddress  RemoteIPAddress    { get; }
        IPPort      RemotePort         { get; }
        //IPSocket    RemoteSocket       { get; }
        TimeSpan?   RequestTimeout     { get; }
        //byte TTL { get; set; }
        //string UserAgent { get; }

        //event HTTPClient.OnDataReadDelegate OnDataRead;

        //void Close();
        //HTTPRequestBuilder CreateRequest(HTTPMethod HTTPMethod, string URI, Action<HTTPRequestBuilder> BuilderAction = null);

        //Task<HTTPResponse> Execute(Func<HTTPClient, HTTPRequest> HTTPRequestDelegate, ClientRequestLogHandler RequestLogDelegate = null, ClientResponseLogHandler ResponseLogDelegate = null, CancellationToken? CancellationToken = default(CancellationToken?), EventTracking_Id EventTrackingId = null, TimeSpan? RequestTimeout = default(TimeSpan?), byte NumberOfRetry = 0);
        //Task<HTTPResponse> Execute(HTTPRequest Request, ClientRequestLogHandler RequestLogDelegate = null, ClientResponseLogHandler ResponseLogDelegate = null, CancellationToken? CancellationToken = default(CancellationToken?), EventTracking_Id EventTrackingId = null, TimeSpan? RequestTimeout = default(TimeSpan?), byte NumberOfRetry = 0);

        DNSClient   DNSClient          { get; }

    }

}