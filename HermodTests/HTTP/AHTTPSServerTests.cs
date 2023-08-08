/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Tests.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTPS
{

    /// <summary>
    /// Hermod HTTPS server tests endpoints.
    /// </summary>
    public abstract class AHTTPSServerTests
    {

        #region Data

        protected readonly HTTPServer                     httpsServer;

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

        public AHTTPSServerTests(IPPort HTTPSPort)
        {

            // Root CA
            rootCA_RSAKeyPair           = PKIFactory.GenerateRSAKeyPair(2048);
            rootCA_X509v3               = PKIFactory.CreateRootCA(
                                              rootCA_RSAKeyPair,
                                              "AHTTPSServerTests Root CA"
                                          );

            // Server CA
            serverCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            serverCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              serverCA_RSAKeyPair,
                                              "AHTTPSServerTests Server CA",
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );

            serverRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            serverCertificate           = PKIFactory.CreateServerCertificate(
                                              serverRSAKeyPair,
                                              "AHTTPSServerTests Server Certificate",
                                              serverCA_RSAKeyPair.Private,
                                              serverCA_X509v3
                                          ).ToDotNet(serverRSAKeyPair.Private);

            // Client CA
            clientCA_RSAKeyPair         = PKIFactory.GenerateRSAKeyPair(2048);
            clientCA_X509v3             = PKIFactory.CreateIntermediateCA(
                                              clientCA_RSAKeyPair,
                                              "AHTTPSServerTests Client CA",
                                              rootCA_RSAKeyPair.Private,
                                              rootCA_X509v3
                                          );

            clientRSAKeyPair            = PKIFactory.GenerateRSAKeyPair(2048);
            clientCertificate           = PKIFactory.CreateServerCertificate(
                                              clientRSAKeyPair,
                                              "AHTTPSServerTests Client Certificate",
                                              clientCA_RSAKeyPair.Private,
                                              clientCA_X509v3
                                          ).ToDotNet(clientRSAKeyPair.Private);


            // HTTPS server configuration
            httpsServer                 = new HTTPServer(

                                              HTTPPort:                    HTTPSPort,

                                              ServerCertificateSelector:  (tcpServer, tcpClient) => {
                                                  return serverCertificate;
                                              },

                                              AutoStart:                   true

                                          );

        }

        #endregion


        #region Init_HTTPSServer()

        [OneTimeSetUp]
        public void Init_HTTPSServer()
        {

            #region GET     /

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region GET     /NotForEveryone

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "NotForEveryone",
                                         HTTPDelegate: async request => {

                                             if (request.Authorization is HTTPBasicAuthentication httpBasicAuthentication)
                                             {

                                                 //return new HTTPResponse.Builder(request) {
                                                 //           HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                 //           Server                     = "Hermod Test Server",
                                                 //           Date                       = Timestamp.Now,
                                                 //           AccessControlAllowOrigin   = "*",
                                                 //           AccessControlAllowMethods  = new[] { "GET" },
                                                 //           AccessControlAllowHeaders  = new[] { "Authorization" },
                                                 //           WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                 //           Connection                 = "close"
                                                 //       }.AsImmutable;

                                                 if (httpBasicAuthentication.Username == "testUser1" ||
                                                     httpBasicAuthentication.Password == "testPassword1")
                                                 {
                                                     return new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            AccessControlAllowHeaders  = new[] { "Authorization" },
                                                            ContentType                = HTTPContentType.TEXT_UTF8,
                                                            Content                    = $"Hello '{httpBasicAuthentication.Username}'!".ToUTF8Bytes(),
                                                            Connection                 = "close"
                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                          AsImmutable;
                                                 }

                                                 // HTTP 403 Forbidden for authentication is ok, but authorization is still not given!
                                                 if (httpBasicAuthentication.Username == "testUser2" ||
                                                     httpBasicAuthentication.Password == "testPassword2")
                                                 {
                                                     return new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.Forbidden,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            AccessControlAllowHeaders  = new[] { "Authorization" },
                                                            ContentType                = HTTPContentType.TEXT_UTF8,
                                                            Content                    = $"Sorry '{httpBasicAuthentication.Username}' please contact your administrator!".ToUTF8Bytes(),
                                                            WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                            Connection                 = "close"
                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                          AsImmutable;
                                                 }

                                             }

                                             return new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            AccessControlAllowHeaders  = new[] { "Authorization" },
                                                            WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                            Connection                 = "close"
                                                        }.AsImmutable;


                                         });

            #endregion


            #region POST    /mirror/queryString

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "queryString",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = request.QueryString.GetString("q", "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region POST    /mirror/httpBody

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region MIRROR  /mirror/httpBody

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.MIRROR,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "MIRROR" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion


            #region GET     /chunked

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunked",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           TransferEncoding           = "chunked",
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region GET     /chunkedSlow

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunkedSlow",
                                         HTTPDelegate: async request => {

                                             var responseStream  = new MemoryStream();
                                             responseStream.Write((new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes());

                                             return new HTTPResponse.Builder(request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = "Hermod Test Server",
                                                        Date                       = Timestamp.Now,
                                                        AccessControlAllowOrigin   = "*",
                                                        AccessControlAllowMethods  = new[] { "GET" },
                                                        TransferEncoding           = "chunked",
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        ContentStream              = responseStream,
                                                        Connection                 = "close"
                                                    }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                      AsImmutable;

                                         });

            #endregion

            #region GET     /chunkedTrailerHeaders

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunkedTrailerHeaders",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           TransferEncoding           = "chunked",
                                                                           Trailer                    = "X-Message-Length, X-Protocol-Version",
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\nX-Message-Length: 13\r\nX-Protocol-Version: 1.0\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion


            #region POST    /mirrorBody2

            httpsServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirrorBody2",
                                         HTTPDelegate: async request => {

                                             var queryParameter = request.HTTPBodyAsUTF8String ?? "";

                                             return new HTTPResponse.Builder(request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = "Hermod Test Server",
                                                        Date                       = Timestamp.Now,
                                                        AccessControlAllowOrigin   = "*",
                                                        AccessControlAllowMethods  = new[] { "GET" },
                                                        AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = queryParameter.Reverse().ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable;

                                         });

            #endregion

        }

        #endregion

        #region Shutdown_HTTPSServer()

        [OneTimeTearDown]
        public void Shutdown_HTTPSServer()
        {
            httpsServer?.Shutdown();
        }

        #endregion


    }

}
