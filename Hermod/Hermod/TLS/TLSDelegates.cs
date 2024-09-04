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

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Verifies the remote Transport Layer Security (TLS) certificate used for authentication.
    /// </summary>
    /// <param name="Sender">An object that contains state information for this validation.</param>
    /// <param name="Certificate">The certificate used to authenticate the remote party.</param>
    /// <param name="CertificateChain">The chain of certificate authorities associated with the remote certificate.</param>
    /// <param name="TLSClient">The TLS client.</param>
    /// <param name="PolicyErrors">One or more errors associated with the remote certificate.</param>
    public delegate (Boolean, IEnumerable<String>) RemoteTLSServerCertificateValidationHandler<T>(Object              Sender,
                                                                                                  X509Certificate2?   Certificate,
                                                                                                  X509Chain?          CertificateChain,
                                                                                                  T                   TLSClient,
                                                                                                  SslPolicyErrors     PolicyErrors)
        where T: class;

    /// <summary>
    /// Verifies the remote Transport Layer Security (TLS) certificate used for authentication.
    /// </summary>
    /// <param name="Sender">An object that contains state information for this validation.</param>
    /// <param name="Certificate">The certificate used to authenticate the remote party.</param>
    /// <param name="CertificateChain">The chain of certificate authorities associated with the remote certificate.</param>
    /// <param name="TLSServer">The TLS server.</param>
    /// <param name="PolicyErrors">One or more errors associated with the remote certificate.</param>
    public delegate (Boolean, IEnumerable<String>) RemoteTLSClientCertificateValidationHandler<T>(Object              Sender,
                                                                                                  X509Certificate2?   Certificate,
                                                                                                  X509Chain?          CertificateChain,
                                                                                                  T                   TLSServer,
                                                                                                  SslPolicyErrors     PolicyErrors)
        where T: class;


    ///// <summary>
    ///// Verifies the remote Transport Layer Security (TLS) certificate used for authentication.
    ///// </summary>
    ///// <param name="Sender">An object that contains state information for this validation.</param>
    ///// <param name="Certificate">The certificate used to authenticate the remote party.</param>
    ///// <param name="CertificateChain">The chain of certificate authorities associated with the remote certificate.</param>
    ///// <param name="WebSocketServer">The HTTP WebSocket server.</param>
    ///// <param name="PolicyErrors">One or more errors associated with the remote certificate.</param>
    //public delegate (Boolean, IEnumerable<String>) RemoteHTTPSClientCertificateValidationHandler(Object              Sender,
    //                                                                                             X509Certificate2?   Certificate,
    //                                                                                             X509Chain?          CertificateChain,
    //                                                                                             IHTTPServer         HTTPSServer,
    //                                                                                             SslPolicyErrors     PolicyErrors);


    ///// <summary>
    ///// Verifies the remote Transport Layer Security (TLS) certificate used for authentication.
    ///// </summary>
    ///// <param name="Sender">An object that contains state information for this validation.</param>
    ///// <param name="Certificate">The certificate used to authenticate the remote party.</param>
    ///// <param name="CertificateChain">The chain of certificate authorities associated with the remote certificate.</param>
    ///// <param name="WebSocketServer">The HTTP WebSocket server.</param>
    ///// <param name="PolicyErrors">One or more errors associated with the remote certificate.</param>
    //public delegate (Boolean, IEnumerable<String>) RemoteWebSocketClientCertificateValidationHandler(Object              Sender,
    //                                                                                                 X509Certificate2?   Certificate,
    //                                                                                                 X509Chain?          CertificateChain,
    //                                                                                                 IWebSocketServer    WebSocketServer,
    //                                                                                                 SslPolicyErrors     PolicyErrors);

    /// <summary>
    /// Selects the local Transport Layer Security (TLS) certificate used for authentication.
    /// </summary>
    /// <param name="Sender">An object that contains state information for this validation.</param>
    /// <param name="TargetHost">The host server specified by the client.</param>
    /// <param name="LocalCertificates">An enumeration of local certificates.</param>
    /// <param name="RemoteCertificate">The certificate used to authenticate the remote party.</param>
    /// <param name="AcceptableIssuers">An enumeration of certificate issuers acceptable to the remote party.</param>
    public delegate X509Certificate LocalCertificateSelectionHandler(Object                          Sender,
                                                                     String                          TargetHost,
                                                                     IEnumerable<X509Certificate2>   LocalCertificates,
                                                                     X509Certificate2?               RemoteCertificate,
                                                                     IEnumerable<String>             AcceptableIssuers);

}
