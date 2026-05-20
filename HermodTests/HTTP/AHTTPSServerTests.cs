/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.PKI;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTPS
{

    /// <summary>
    /// Hermod HTTPS server tests endpoints.
    /// </summary>
    public abstract class AHTTPSServerTests
    {

        #region Data

        protected readonly HTTPServer                httpsServer;
        protected readonly HTTPAPI                        httpsAPI;

        protected AsymmetricCipherKeyPair                 rootCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   rootCA_X509v3;
        protected X509Certificate2                        rootCA;

        protected AsymmetricCipherKeyPair                 serverCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   serverCA_X509v3;
        protected X509Certificate2                        serverCA;

        protected AsymmetricCipherKeyPair                 serverRSAKeyPair;
        protected X509Certificate2                        serverCertificate;

        protected AsymmetricCipherKeyPair                 clientCA_RSAKeyPair;
        protected Org.BouncyCastle.X509.X509Certificate   clientCA_X509v3;
        protected X509Certificate2                        clientCA;

        protected AsymmetricCipherKeyPair                 clientRSAKeyPair;
        protected X509Certificate2                        clientCertificate;

        #endregion

        #region Constructor(s)

        public AHTTPSServerTests(IPPort HTTPSPort)
        {

            // Root CA
            rootCA_RSAKeyPair           = PKIFactory.GenerateRSAKeyPair(2048);
            rootCA_X509v3               = PKIFactory.CreateRootCACertificate(
                                              "AHTTPSServerTests Root CA",
                                              rootCA_RSAKeyPair
                                          );
            rootCA                      = rootCA_X509v3.ToDotNet();



            // Server CA
            serverCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            serverCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              "AHTTPSServerTests Server CA",
                                              serverCA_RSAKeyPair.Public,
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );
            serverCA                    = serverCA_X509v3.ToDotNet();

            serverRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            serverCertificate           = PKIFactory.SignServerCertificate(
                                              "AHTTPSServerTests Server Certificate",
                                              null,
                                              serverRSAKeyPair.Public,
                                              serverCA_RSAKeyPair.Private,
                                              serverCA_X509v3
                                          ).ToDotNet(serverRSAKeyPair.Private);



            // Client CA
            clientCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            clientCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              "AHTTPSServerTests Client CA",
                                              clientCA_RSAKeyPair.Public,
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );
            clientCA                    = clientCA_X509v3.ToDotNet();

            clientRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            clientCertificate           = PKIFactory.SignServerCertificate(
                                              "AHTTPSServerTests Client Certificate",
                                              null,
                                              clientRSAKeyPair.Public,
                                              clientCA_RSAKeyPair.Private,
                                              clientCA_X509v3
                                          ).ToDotNet(clientRSAKeyPair.Private);


            // HTTPS server configuration
            httpsServer                 = new HTTPServer(

                                              TCPPort:                    HTTPSPort,

                                              ServerCertificateSelector:  (tcpServer, tcpClient) => {
                                                  return serverCertificate;
                                              },

                                               AutoStart:                   true

                                           );

            httpsAPI                    = new HTTPAPI(
                                              httpsServer
                                          );

        }

        #endregion


        #region Init_HTTPSServer()

        [OneTimeSetUp]
        public void Init_HTTPSServer()
        {

            #region GET     /

            httpsAPI.AddHandler(HTTPPath.Root,
                                HTTPMethod:   HTTPMethod.GET,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = [ "GET" ],
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = "Hello World!".ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                          AsImmutable));

            #endregion

            #region GET     /NotForEveryone

            httpsAPI.AddHandler(HTTPPath.Root + "NotForEveryone",
                                HTTPMethod:   HTTPMethod.GET,
                                HTTPDelegate: request => {

                                              if (request.Authorization is HTTPBasicAuthentication httpBasicAuthentication)
                                              {

                                                  //return new HTTPResponse.Builder(request) {
                                                  //           HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                  //           Server                     = "Hermod Test Server",
                                                  //           Date                       = Timestamp.Now,
                                                  //           AccessControlAllowOrigin   = "*",
                                                  //           AccessControlAllowMethods  = [ "GET" ],
                                                  //           AccessControlAllowHeaders  = [ "Authorization" ],
                                                  //           WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                  //           Connection                 = ConnectionType.Close
                                                  //       }.AsImmutable;

                                                  if (httpBasicAuthentication.Username == "testUser1" ||
                                                      httpBasicAuthentication.Password == "testPassword1")
                                                  {
                                                      return Task.FromResult(
                                                                 new HTTPResponse.Builder(request) {
                                                                     HTTPStatusCode             = HTTPStatusCode.OK,
                                                                     Server                     = "Hermod Test Server",
                                                                     Date                       = Timestamp.Now,
                                                                     AccessControlAllowOrigin   = "*",
                                                                     AccessControlAllowMethods  = [ "GET" ],
                                                                     AccessControlAllowHeaders  = [ "Authorization" ],
                                                                     ContentType                = HTTPContentType.Text.PLAIN,
                                                                     Content                    = $"Hello '{httpBasicAuthentication.Username}'!".ToUTF8Bytes(),
                                                                     Connection                 = ConnectionType.Close
                                                                 }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                   AsImmutable
                                                             );
                                                  }

                                                  // HTTP 403 Forbidden for authentication is ok, but authorization is still not given!
                                                  if (httpBasicAuthentication.Username == "testUser2" ||
                                                      httpBasicAuthentication.Password == "testPassword2")
                                                  {
                                                      return Task.FromResult(
                                                                 new HTTPResponse.Builder(request) {
                                                                     HTTPStatusCode             = HTTPStatusCode.Forbidden,
                                                                     Server                     = "Hermod Test Server",
                                                                     Date                       = Timestamp.Now,
                                                                     AccessControlAllowOrigin   = "*",
                                                                     AccessControlAllowMethods  = [ "GET" ],
                                                                     AccessControlAllowHeaders  = [ "Authorization" ],
                                                                     ContentType                = HTTPContentType.Text.PLAIN,
                                                                     Content                    = $"Sorry '{httpBasicAuthentication.Username}' please contact your administrator!".ToUTF8Bytes(),
                                                                     WWWAuthenticate            = WWWAuthenticate.Basic("Access to the staging site"),
                                                                     Connection                 = ConnectionType.Close
                                                                 }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                   AsImmutable
                                                             );
                                                  }

                                              }

                                              return Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                             Server                     = "Hermod Test Server",
                                                             Date                       = Timestamp.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = [ "GET" ],
                                                             AccessControlAllowHeaders  = [ "Authorization" ],
                                                             WWWAuthenticate            = WWWAuthenticate.Basic("Access to the staging site"),
                                                             Connection                 = ConnectionType.Close
                                                         }.AsImmutable
                                                     );

                                          });

            #endregion


            #region POST    /mirror/queryString

            httpsAPI.AddHandler(HTTPPath.Root + "mirror" + "queryString",
                                HTTPMethod:   HTTPMethod.POST,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = [ "GET" ],
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = request.QueryString.GetString("q", "").Reverse().ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.AsImmutable));

            #endregion

            #region POST    /mirror/httpBody

            httpsAPI.AddHandler(HTTPPath.Root + "mirror" + "httpBody",
                                HTTPMethod:   HTTPMethod.POST,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = [ "GET" ],
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.AsImmutable));

            #endregion

            #region MIRROR  /mirror/httpBody

            httpsAPI.AddHandler(HTTPPath.Root + "mirror" + "httpBody",
                                HTTPMethod:   HTTPMethod.MIRROR,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = new[] { "MIRROR" },
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.AsImmutable));

            #endregion


            #region GET     /chunked

            httpsAPI.AddHandler(HTTPPath.Root + "chunked",
                                HTTPMethod:   HTTPMethod.GET,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = [ "GET" ],
                                                                            TransferEncoding           = "chunked",
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                          AsImmutable));

            #endregion

            #region GET     /chunkedSlow

            httpsAPI.AddHandler(HTTPPath.Root + "chunkedSlow",
                                HTTPMethod:   HTTPMethod.GET,
                                HTTPDelegate: request => {

                                              var responseStream  = new MemoryStream();
                                              responseStream.Write((new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes());

                                              return Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode             = HTTPStatusCode.OK,
                                                             Server                     = "Hermod Test Server",
                                                             Date                       = Timestamp.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = [ "GET" ],
                                                             TransferEncoding           = "chunked",
                                                             ContentType                = HTTPContentType.Text.PLAIN,
                                                             ContentStream              = responseStream,
                                                             Connection                 = ConnectionType.Close
                                                         }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                           AsImmutable
                                                     );

                                          });

            #endregion

            #region GET     /chunkedTrailerHeaders

            httpsAPI.AddHandler(HTTPPath.Root + "chunkedTrailerHeaders",
                                HTTPMethod:   HTTPMethod.GET,
                                HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                                            Server                     = "Hermod Test Server",
                                                                            Date                       = Timestamp.Now,
                                                                            AccessControlAllowOrigin   = "*",
                                                                            AccessControlAllowMethods  = [ "GET" ],
                                                                            TransferEncoding           = "chunked",
                                                                            Trailer                    = "X-Message-Length, X-Protocol-Version",
                                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                                            Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\nX-Message-Length: 13\r\nX-Protocol-Version: 1.0\r\n\r\n").ToUTF8Bytes(),
                                                                            Connection                 = ConnectionType.Close
                                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                          AsImmutable));

            #endregion


            #region POST    /mirrorBody2

            httpsAPI.AddHandler(HTTPPath.Root + "mirrorBody2",
                                HTTPMethod:   HTTPMethod.POST,
                                HTTPDelegate: request => {

                                              var queryParameter = request.HTTPBodyAsUTF8String ?? "";

                                              return Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode             = HTTPStatusCode.OK,
                                                             Server                     = "Hermod Test Server",
                                                             Date                       = Timestamp.Now,
                                                             AccessControlAllowOrigin   = "*",
                                                             AccessControlAllowMethods  = [ "GET" ],
                                                             AccessControlAllowHeaders  = [ "Content-Type", "Accept", "Authorization" ],
                                                             ContentType                = HTTPContentType.Text.PLAIN,
                                                             Content                    = queryParameter.Reverse().ToUTF8Bytes(),
                                                             Connection                 = ConnectionType.Close
                                                         }.AsImmutable
                                                     );

                                          });

            #endregion

        }

        #endregion

        #region Shutdown_HTTPSServer()

        [OneTimeTearDown]
        public async Task Shutdown_HTTPSServer()
        {
            await httpsServer.Stop();
        }

        #endregion


    }

}
