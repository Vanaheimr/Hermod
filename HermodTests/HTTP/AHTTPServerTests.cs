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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Hermod HTTP server tests endpoints.
    /// </summary>
    public abstract class AHTTPServerTests
    {

        #region Data

        protected readonly HTTPServer  httpServer;
        protected readonly HTTPAPI          httpAPI;

        #endregion

        #region Constructor(s)

        public AHTTPServerTests(IPPort HTTPPort)
        {

            httpServer  = new HTTPServer(
                              TCPPort:    HTTPPort,
                              AutoStart:  true
                          );

            httpAPI     = new HTTPAPI(
                              httpServer
                          );

        }

        #endregion


        #region Init_HTTPServer()

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            #region GET     /

            httpAPI.AddHandler(HTTPPath.Root,
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

            #region GET     /close

            httpAPI.AddHandler(HTTPPath.Root + "close",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode  = HTTPStatusCode.OK,
                                                                            Server          = "Hermod Test Server",
                                                                            ContentType     = HTTPContentType.Text.PLAIN,
                                                                            Content         = request.KeepAliveMessageCount.ToString().ToUTF8Bytes(),
                                                                            Connection      = ConnectionType.Close
                                                                        }.AsImmutable));

            #endregion


            #region GET     /keepalive

            httpAPI.AddHandler(HTTPPath.Root + "keepalive",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode  = HTTPStatusCode.OK,
                                                                            Server          = "Hermod Test Server",
                                                                            ContentType     = HTTPContentType.Text.PLAIN,
                                                                            Content         = request.KeepAliveMessageCount.ToString().ToUTF8Bytes(),
                                                                            Connection      = ConnectionType.KeepAlive
                                                                        }.AsImmutable));

            #endregion


            #region HEAD    /

            httpAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.HEAD,
                               HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode  = HTTPStatusCode.OK,
                                                                            Server          = "Hermod Test Server",
                                                                            Date            = Timestamp.Now,
                                                                            ContentType     = HTTPContentType.Text.PLAIN,
                                                                            Content         = "Hello World!".ToUTF8Bytes(),
                                                                            Connection      = ConnectionType.Close
                                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                          AsImmutable));

            #endregion

            #region OPTIONS /

            httpAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.OPTIONS,
                               HTTPDelegate: request => Task.FromResult(
                                                                        new HTTPResponse.Builder(request) {
                                                                            HTTPStatusCode  = HTTPStatusCode.NoContent,
                                                                            Server          = "Hermod Test Server",
                                                                            Date            = Timestamp.Now,
                                                                            Connection      = ConnectionType.Close
                                                                        }.AsImmutable));

            #endregion

            #region GET     /NotForEveryone

            #region GET     /notmodified

            httpAPI.AddHandler(HTTPPath.Root + "notmodified",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                         new HTTPResponse.Builder(request) {
                                                                             HTTPStatusCode  = HTTPStatusCode.NotModified,
                                                                             Server          = "Hermod Test Server",
                                                                             Date            = Timestamp.Now,
                                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                                             Content         = "This body must not be sent.".ToUTF8Bytes(),
                                                                             Connection      = ConnectionType.Close
                                                                         }.AsImmutable));

            #endregion


            #region GET     /resetcontent

            httpAPI.AddHandler(HTTPPath.Root + "resetcontent",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                         new HTTPResponse.Builder(request) {
                                                                             HTTPStatusCode  = HTTPStatusCode.ResetContent,
                                                                             Server          = "Hermod Test Server",
                                                                             Date            = Timestamp.Now,
                                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                                             Content         = "This body must not be sent.".ToUTF8Bytes(),
                                                                             Connection      = ConnectionType.Close
                                                                         }.AsImmutable));

            #endregion


            httpAPI.AddHandler(HTTPPath.Root + "NotForEveryone",
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

                                                 if (httpBasicAuthentication.Username == "testUser1" &&
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
                                                 if (httpBasicAuthentication.Username == "testUser2" &&
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

            httpAPI.AddHandler(HTTPPath.Root + "mirror" + "queryString",
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

            httpAPI.AddHandler(HTTPPath.Root + "mirror" + "httpBody",
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

            httpAPI.AddHandler(HTTPPath.Root + "mirror" + "httpBody",
                               HTTPMethod:   HTTPMethod.MIRROR,
                               HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = [ "MIRROR" ],
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = ConnectionType.Close
                                                                       }.AsImmutable));

            #endregion


            #region QUERY   /query

            httpAPI.AddHandler(HTTPPath.Root + "query",
                               HTTPMethod:   HTTPMethod.QUERY,
                               HTTPDelegate: request => Task.FromResult(
                                                                         new HTTPResponse.Builder(request) {
                                                                             HTTPStatusCode             = HTTPStatusCode.OK,
                                                                             Server                     = "Hermod Test Server",
                                                                             Date                       = Timestamp.Now,
                                                                             AccessControlAllowOrigin   = "*",
                                                                             AccessControlAllowMethods  = [ "QUERY" ],
                                                                             ContentType                = HTTPContentType.Text.PLAIN,
                                                                             Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                             Connection                 = ConnectionType.Close
                                                                         }.AsImmutable));

            #endregion


            #region GET     /chunked

            var chunks = new[] { "5", "Hello", "1", " ", "6", "World!", "0" };

            httpAPI.AddHandler(HTTPPath.Root + "chunked",
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
                                                                           Content                    = (chunks.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = ConnectionType.Close
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region GET     /chunkedSlow

            httpAPI.AddHandler(HTTPPath.Root + "chunkedSlow",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => {

                                             var responseStream  = new MemoryStream();
                                             responseStream.Write((chunks.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes());
                                             responseStream.Position = 0;

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

            httpAPI.AddHandler(HTTPPath.Root + "chunkedTrailerHeaders",
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
                                                                           Content                    = (chunks.AggregateWith("\r\n") + "\r\nX-Message-Length: 13\r\nX-Protocol-Version: 1.0\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = ConnectionType.Close
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion


            #region GET     /chunkedAutomaticTrailerHeaders

            httpAPI.AddHandler(HTTPPath.Root + "chunkedAutomaticTrailerHeaders",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           TransferEncoding           = "chunked",
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = ConnectionType.Close,
                                                                           AutomaticallyChunkContent  = true,
                                                                           TrailingHeaders            = {
                                                                               ["X-Message-Length"]    = "13",
                                                                               ["X-Protocol-Version"]  = "1.0"
                                                                           }
                                                                       }.AsImmutable));

            #endregion


            #region GET     /chunkedLiveTrailerHeaders

            httpAPI.AddHandler(HTTPPath.Root + "chunkedLiveTrailerHeaders",
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
                                                                           ContentStream              = new ChunkedTransferEncodingStream(request.NetworkStream!, true),
                                                                           Connection                 = ConnectionType.Close,
                                                                           ChunkWorker                = async (response, stream) => {
                                                                                                            await stream.WriteAsync(
                                                                                                                      "Hello World!".ToUTF8Bytes(),
                                                                                                                      response.CancellationToken
                                                                                                                  );
                                                                                                            await stream.Finish(
                                                                                                                      new Dictionary<String, String> {
                                                                                                                          ["X-Message-Length"]    = "13",
                                                                                                                          ["X-Protocol-Version"]  = "1.0"
                                                                                                                      },
                                                                                                                      response.CancellationToken
                                                                                                                  );
                                                                                                            await stream.FlushAsync(response.CancellationToken);
                                                                                                        }
                                                                       }.AsImmutable));

            #endregion


            #region GET     /chunkedLiveExtensions

            httpAPI.AddHandler(HTTPPath.Root + "chunkedLiveExtensions",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode  = HTTPStatusCode.OK,
                                                                           Server          = "Hermod Test Server",
                                                                           Date            = Timestamp.Now,
                                                                           TransferEncoding = "chunked",
                                                                           ContentType     = HTTPContentType.Text.PLAIN,
                                                                           ContentStream   = new ChunkedTransferEncodingStream(request.NetworkStream!, true),
                                                                           Connection      = ConnectionType.Close,
                                                                           ChunkWorker     = async (response, stream) => {
                                                                                                 await stream.WriteAsync(
                                                                                                           "Hello ".ToUTF8Bytes(),
                                                                                                           [
                                                                                                               new KeyValuePair<String, String?>("part", "one"),
                                                                                                               new KeyValuePair<String, String?>("part", "two")
                                                                                                           ],
                                                                                                           response.CancellationToken
                                                                                                       );

                                                                                                 await stream.WriteAsync(
                                                                                                           "World!".ToUTF8Bytes(),
                                                                                                           [
                                                                                                               new KeyValuePair<String, String?>("flag",    null),
                                                                                                               new KeyValuePair<String, String?>("comment", "two words")
                                                                                                           ],
                                                                                                           response.CancellationToken
                                                                                                       );

                                                                                                 await stream.Finish(CancellationToken: response.CancellationToken);
                                                                                                 await stream.FlushAsync(response.CancellationToken);
                                                                                             }
                                                                       }.AsImmutable
                                                                   ));

            #endregion


            #region QUERY   /query/extensions

            httpAPI.AddHandler(HTTPPath.Root + "query" + "extensions",
                               HTTPMethod:   HTTPMethod.QUERY,
                               HTTPDelegate: request => {

                                                  var requestBody = request.HTTPBodyAsUTF8String ?? "";
                                                  var extensions  = request.ChunkExtensions.SingleOrDefault();
                                                  var values      = extensions is not null && extensions.TryGetValue("part", out var partValues)
                                                                        ? String.Join("|", partValues)
                                                                        : "";

                                                  return Task.FromResult(
                                                             new HTTPResponse.Builder(request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                                 Content         = $"{requestBody}|{values}".ToUTF8Bytes(),
                                                                 Connection      = ConnectionType.Close
                                                             }.AsImmutable
                                                         );
                                              });

            #endregion


            #region QUERY   /query/trailer

            httpAPI.AddHandler(HTTPPath.Root + "query" + "trailer",
                               HTTPMethod:   HTTPMethod.QUERY,
                               HTTPDelegate: request => {

                                                 var requestBody = request.HTTPBodyAsUTF8String ?? "";

                                                 request.TrailingHeaders.TryGetValue("X-Query-Metadata", out var metadata);

                                                 return Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode             = HTTPStatusCode.OK,
                                                                Server                     = "Hermod Test Server",
                                                                Date                       = Timestamp.Now,
                                                                AccessControlAllowOrigin   = "*",
                                                                AccessControlAllowMethods  = [ "QUERY" ],
                                                                ContentType                = HTTPContentType.Text.PLAIN,
                                                                Content                    = $"{requestBody.Reverse()}|{metadata}".ToUTF8Bytes(),
                                                                Connection                 = ConnectionType.Close
                                                            }.AsImmutable
                                                        );

                                             });

            #endregion


            #region POST    /mirrorBody2

            httpAPI.AddHandler(HTTPPath.Root + "mirrorBody2",
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

            #region GET     /events

            #region GET     /events/multiline

            httpAPI.AddHandler(HTTPPath.Root + "events" + "multiline",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                           new HTTPResponse.Builder(request) {
                                                               HTTPStatusCode  = HTTPStatusCode.OK,
                                                               ContentType     = HTTPContentType.Text.EVENTSTREAM,
                                                               CacheControl    = "no-cache",
                                                               Content         = "event: status\nid: 7\ndata: {\ndata:   \"message\": \"multiline\"\ndata: }\n\n".ToUTF8Bytes(),
                                                               Connection      = ConnectionType.Close
                                                           }.AsImmutable
                                                       ));

            #endregion


            #region GET     /events/reconnect

            httpAPI.AddHandler(HTTPPath.Root + "events" + "reconnect",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => {

                                   var lastEventId = request.LastEventId ?? 0;
                                   var eventData   = lastEventId >= 2
                                                         ? ""
                                                         : lastEventId == 1
                                                               ? "event: status\nid: 2\ndata: {\"message\":\"second\"}\n\n"
                                                               : "retry: 100\n\nevent: status\nid: 1\ndata: {\"message\":\"first\"}\n\nevent: status\nid: 2\ndata: {\"message\":\"second\"}\n\n";

                                   return Task.FromResult(
                                              new HTTPResponse.Builder(request) {
                                                  HTTPStatusCode  = HTTPStatusCode.OK,
                                                  ContentType     = HTTPContentType.Text.EVENTSTREAM,
                                                  CacheControl    = "no-cache",
                                                  Content         = eventData.ToUTF8Bytes(),
                                                  Connection      = ConnectionType.Close
                                              }.AsImmutable
                                          );

                               });

            #endregion


            httpAPI.AddHandler(HTTPPath.Root + "events",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode  = HTTPStatusCode.OK,
                                                                           ContentType     = HTTPContentType.Text.EVENTSTREAM,
                                                                           CacheControl    = "no-cache",
                                                                           Content         = "retry: 100\n\nevent: status\nid: 1\ndata: {\"message\":\"from Hermod server\"}\n\n".ToUTF8Bytes(),
                                                                           Connection      = ConnectionType.Close
                                                                       }.AsImmutable));

            #endregion


            #region GET     /events/live

            httpAPI.AddHandler(HTTPPath.Root + "events" + "live",
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                           new HTTPResponse.Builder(request) {
                                                               HTTPStatusCode  = HTTPStatusCode.OK,
                                                               ContentType     = HTTPContentType.Text.EVENTSTREAM,
                                                               CacheControl    = "no-cache",
                                                               Connection      = ConnectionType.Close,
                                                               HTTPSSEWorker   = async (response, writer) => {
                                                                                     await writer.WriteHeartbeat("live-worker", response.CancellationToken);
                                                                                     await writer.WriteAsync("event: status\nid: 1\ndata: {\"message\":\"from live SSE worker\"}\n\n");
                                                                                     await writer.FlushAsync(response.CancellationToken);
                                                                                 }
                                                           }.AsImmutable
                                                       ));

            #endregion

        }

        #endregion

        #region Shutdown_HTTPServer()

        [OneTimeTearDown]
        public async Task Shutdown_HTTPServer()
        {
            await httpServer.Stop();
        }

        #endregion


    }

}
