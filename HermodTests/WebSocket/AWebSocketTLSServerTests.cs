/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Org.BouncyCastle.Crypto;

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;
using org.GraphDefined.Vanaheimr.Hermod.Tests.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTPS.WebSockets
{

    /// <summary>
    /// Hermod HTTP WebSocket TLS server tests endpoints.
    /// </summary>
    public abstract class AWebSocketTLSServerTests
    {

        #region Data

        protected AWebSocketServer?                       webSocketServer;
        protected IPPort                                  HTTPPort;
        protected IEnumerable<String>?                    SecWebSocketProtocols;

        protected AsymmetricCipherKeyPair                 rootCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   rootCA_X509v3;

        protected AsymmetricCipherKeyPair                 serverCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   serverCA_X509v3;

        protected AsymmetricCipherKeyPair                 serverRSAKeyPair;
        protected X509Certificate2                        serverCertificate;

        protected AsymmetricCipherKeyPair                 clientCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   clientCA_X509v3;

        protected AsymmetricCipherKeyPair                 clientRSAKeyPair;
        protected X509Certificate2                        clientCertificate;

        #endregion

        #region Constructor(s)

        public AWebSocketTLSServerTests(IPPort                HTTPPort,
                                        IEnumerable<String>?  SecWebSocketProtocols   = null)
        {

            this.HTTPPort               = HTTPPort;
            this.SecWebSocketProtocols  = SecWebSocketProtocols;

            // Root CA
            rootCA_RSAKeyPair           = PKIFactory.GenerateRSAKeyPair(2048);
            rootCA_X509v3               = PKIFactory.CreateRootCA(
                                              rootCA_RSAKeyPair,
                                              "AWebSocketTLSServerTests Root CA"
                                          );

            // Server CA
            serverCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            serverCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              serverCA_RSAKeyPair,
                                              "AWebSocketTLSServerTests Server CA",
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );

            serverRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            serverCertificate           = PKIFactory.CreateServerCertificate(
                                              serverRSAKeyPair,
                                              "AWebSocketTLSServerTests Server Certificate",
                                              serverCA_RSAKeyPair.Private,
                                              serverCA_X509v3
                                          ).ToDotNet(serverRSAKeyPair.Private);

            // Client CA
            clientCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            clientCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              clientCA_RSAKeyPair,
                                              "AWebSocketTLSServerTests Client CA",
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );

            clientRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            clientCertificate           = PKIFactory.CreateServerCertificate(
                                              clientRSAKeyPair,
                                              "AWebSocketTLSServerTests Client Certificate",
                                              clientCA_RSAKeyPair.Private,
                                              clientCA_X509v3
                                          ).ToDotNet(clientRSAKeyPair.Private);

        }

        #endregion

        #region Init_WebSocketTLSServer()

        [SetUp]
        public void Init_WebSocketTLSServer()
        {

            webSocketServer = new WebSocketServer(

                                  HTTPPort:                    HTTPPort,

                                  ServerCertificateSelector:  () => {

                                      return serverCertificate;

                                  },

                                  SecWebSocketProtocols:       SecWebSocketProtocols,

                                  AutoStart:                   true

                              );;

        }

        #endregion

        #region Shutdown_WebSocketTLSServer()

        [TearDown]
        public void Shutdown_WebSocketTLSServer()
        {
            webSocketServer?.Shutdown(Wait: true);
            webSocketServer = null;
        }

        #endregion


    }

}
