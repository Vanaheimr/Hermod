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

using System.Reflection;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTPTestServer tests.
    /// </summary>
    [TestFixture]
    public class HTTPTestServerTests
    {

        //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
        //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");

        #region EmbeddedAssemblyFiles_01()

        [Test]
        public async Task EmbeddedAssemblyFiles_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.MapResourceAssembliesFolder(
                HTTPHostname.Any,
                HTTPPath.Root,
                [
                    new Tuple<String, Assembly>("org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.HTTPRoot.", typeof(HTTPTestServerTests).Assembly)
                    //new Tuple<String, Assembly>(UsersAPI.              HTTPRoot, typeof(UsersAPI).              Assembly)
                ],
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                DefaultFilename:     "index.html"
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/helloWorld.txt")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String?.Trim() ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World!"));

        }

        #endregion


        #region Paths_01()

        [Test]
        public async Task Paths_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "check",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                   ContentType     = HTTPContentType.Text.PLAIN,
                                   Content         = $"Hello World!".ToUTF8Bytes()
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/check")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World!"));

        }

        #endregion


        #region PathsAndVariables_01()

        [Test]
        public async Task PathsAndVariables_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "{filename}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World: '{filename}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test3.txt")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World: 'test3.txt'!"));

        }

        #endregion

        #region PathsAndVariables_02()

        [Test]
        public async Task PathsAndVariables_02()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/test2/{filename}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World: '{filename}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1/test2/test3.txt")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World: 'test3.txt'!"));

        }

        #endregion

        #region PathsAndVariables_03()

        [Test]
        public async Task PathsAndVariables_03()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1/AAA.log/test2/BBB.log/CCC.log")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World: 'AAA.log'/'BBB.log'/'CCC.log'!"));

        }

        #endregion


        #region PathsAndVariables_Override_Allowed_01()

        [Test]
        public async Task PathsAndVariables_Override_Allowed_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                AllowReplacement:    URLReplacement.Allow,
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       Content         = $"Hello World v1: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       Content         = $"Hello World v2: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1/AAA.log/test2/BBB.log/CCC.log")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World v2: 'AAA.log'/'BBB.log'/'CCC.log'!"));

        }

        #endregion

        #region PathsAndVariables_Override_NotAllowed_01()

        [Test]
        public async Task PathsAndVariables_Override_NotAllowed_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       Content         = $"Hello World v1: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var ex = Assert.Throws<InvalidOperationException>(
                         () => httpAPI.AddHandler(

                                   HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                                   HTTPMethod:          HTTPMethod.GET,
                                   HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                                   HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                                   HTTPDelegate:        request => {

                                       if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                                           request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                                           request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                                           return Task.FromResult(
                                                      new HTTPResponse.Builder(request) {
                                                          HTTPStatusCode  = HTTPStatusCode.OK,
                                                          Content         = $"Hello World v2: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                                      }.AsImmutable
                                                  );

                                       return Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.BadRequest
                                                  }.AsImmutable
                                              );

                                   }
                               )
            );

            Assert.That(ex?.Message,  Is.EqualTo("Cannot override existing RequestHandlers!"));

        }

        #endregion

        #region PathsAndVariablesContentType_Override_Allowed_01()

        [Test]
        public async Task PathsAndVariablesContentType_Override_Allowed_01()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                AllowReplacement:    URLReplacement.Allow,
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World v1: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World v2: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var response   = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1/AAA.log/test2/BBB.log/CCC.log")));
            Assert.That(response, Is.Not.Null);

            var httpBody   = response.HTTPBodyAsUTF8String ?? "";

            Assert.That(requestLogger,   Has.Count.EqualTo(1));
            Assert.That(responseLogger,  Has.Count.EqualTo(1));
            Assert.That(httpBody,        Is.EqualTo("Hello World v2: 'AAA.log'/'BBB.log'/'CCC.log'!"));

        }

        #endregion

        #region PathsAndVariablesContentType_Override_NotAllowed_02()

        [Test]
        public async Task PathsAndVariablesContentType_Override_NotAllowed_02()
        {

            var httpServer      = await HTTPTestServerX.StartNew();
            var httpAPI         = httpServer.AddHTTPAPI();
            var requestLogger   = new List<HTTPRequest>();
            var responseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                        request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                        request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World v1: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var ex = Assert.Throws<InvalidOperationException>(
                         () => httpAPI.AddHandler(

                                   HTTPPath.Root + "/test1/{filename1}/test2/{filename2}/{filename3}",
                                   HTTPMethod:          HTTPMethod.GET,
                                   HTTPRequestLogger:   (ts, server, request,           ct) => { requestLogger. Add(request);  return Task.CompletedTask; },
                                   HTTPResponseLogger:  (ts, server, request, response, ct) => { responseLogger.Add(response); return Task.CompletedTask; },
                                   HTTPDelegate:        request => {

                                       if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1) &&
                                           request.ParsedURLParametersX.TryGetValue("filename2", out var filename2) &&
                                           request.ParsedURLParametersX.TryGetValue("filename3", out var filename3))
                                           return Task.FromResult(
                                                      new HTTPResponse.Builder(request) {
                                                          HTTPStatusCode  = HTTPStatusCode.OK,
                                                          ContentType     = HTTPContentType.Text.PLAIN,
                                                          Content         = $"Hello World v2: '{filename1}'/'{filename2}'/'{filename3}'!".ToUTF8Bytes()
                                                      }.AsImmutable
                                                  );

                                       return Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.BadRequest
                                                  }.AsImmutable
                                              );

                                   }
                               )
            );

            Assert.That(ex?.Message,  Is.EqualTo("Cannot override existing RequestHandlers!"));

        }

        #endregion


        #region SamePath_MultipleMethods_KeepAlives_01()

        [Test]
        public async Task SamePath_MultipleMethods_KeepAlives_01()
        {

            var httpServer          = await HTTPTestServerX.StartNew();
            var httpAPI             = httpServer.AddHTTPAPI();
            var getRequestLogger    = new List<HTTPRequest>();
            var getResponseLogger   = new List<HTTPResponse>();
            var postRequestLogger   = new List<HTTPRequest>();
            var postResponseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "{filename}",
                HTTPMethod:          HTTPMethod.GET,
                //HTTPContentType:     HTTPContentType.Text.PLAIN,
                HTTPRequestLogger:   (ts, server, request,           ct) => { getRequestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { getResponseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.HTML_UTF8,
                                       Content         = $"GET: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            httpAPI.AddHandler(

                HTTPPath.Root + "{filename}",
                HTTPMethod:          HTTPMethod.POST,
                //HTTPContentType:     HTTPContentType.Text.PLAIN,
                HTTPRequestLogger:   (ts, server, request,           ct) => { postRequestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { postResponseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Application.JSON_UTF8,
                                       Content         = $"POST: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );


            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);


            var getResponse   = await httpClient.Item1.SendRequest(
                                    httpClient.Item1.CreateRequest(
                                        HTTPMethod.GET,
                                        HTTPPath.Parse("/test3.txt")
                                    )
                                );

            Assert.That(getResponse.HTTPBodyAsUTF8String,   Is.EqualTo("GET: 'test3.txt'!"));


            var postResponse  = await httpClient.Item1.SendRequest(
                                    httpClient.Item1.CreateRequest(
                                        HTTPMethod.POST,
                                        HTTPPath.Parse("/test3.txt")
                                    )
                                );

            Assert.That(postResponse.HTTPBodyAsUTF8String,  Is.EqualTo("POST: 'test3.txt'!"));

        }

        #endregion

        #region SamePath_MultipleContentTypes_KeepAlives_01()

        [Test]
        public async Task SamePath_MultipleContentTypes_KeepAlives_01()
        {

            var httpServer  = await HTTPTestServerX.StartNew();
            var api1        = httpServer.AddHTTPAPI();

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:       HTTPMethod.GET,
                HTTPContentType:  HTTPContentType.Text.HTML_UTF8,
                HTTPDelegate:     request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.HTML_UTF8,
                                       Content         = $"Hello World: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:       HTTPMethod.GET,
                HTTPContentType:  HTTPContentType.Application.JSON_UTF8,
                HTTPDelegate:     request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Application.JSON_UTF8,
                                       Content         = new JObject(new JProperty("Hello World", filename)).ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );


            //var client = new HTTPClient(URL.Parse($"http://localhost:{httpServer.TCPPort}/test3.txt"));
            //var xx = await client.GET(HTTPPath.Parse("/test3.txt"));


            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var d1 = await httpClient.Item1.SendRequest(
                               httpClient.Item1.CreateRequest(
                                   HTTPMethod.GET,
                                   HTTPPath.Parse("/test3.txt"),
                                   Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.HTML_UTF8)
                               )
                           );
            Assert.That(d1, Is.Not.Null);

            var httpBody1 = d1.HTTPBodyAsUTF8String ?? "";


            var d2 = await httpClient.Item1.SendRequest(
                               httpClient.Item1.CreateRequest(
                                   HTTPMethod.GET,
                                   HTTPPath.Parse("/test3.txt"),
                                   Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8)
                               )
                           );
            Assert.That(d2, Is.Not.Null);

            var httpBody2 = d2.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1,  Is.EqualTo("Hello World: 'test3.txt'!"));
            Assert.That(httpBody2,  Is.EqualTo("{\"Hello World\":\"test3.txt\"}"));

        }

        #endregion

        #region SamePath_MultipleContentTypes_KeepAlives_02()

        [Test]
        public async Task SamePath_MultipleContentTypes_KeepAlives_02()
        {

            var httpServer          = await HTTPTestServerX.StartNew();
            var httpAPI             = httpServer.AddHTTPAPI();
            var getRequestLogger    = new List<HTTPRequest>();
            var getResponseLogger   = new List<HTTPResponse>();
            var postRequestLogger   = new List<HTTPRequest>();
            var postResponseLogger  = new List<HTTPResponse>();

            httpAPI.AddHandler(

                HTTPPath.Root + "{filename}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPContentType:     HTTPContentType.Text.HTML_UTF8,
                HTTPRequestLogger:   (ts, server, request,           ct) => { getRequestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { getResponseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.HTML_UTF8,
                                       Content         = $"GET HTML: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            httpAPI.AddHandler(

                HTTPPath.Root + "{filename}",
                HTTPMethod:          HTTPMethod.GET,
                HTTPContentType:     HTTPContentType.Application.JSON_UTF8,
                HTTPRequestLogger:   (ts, server, request,           ct) => { postRequestLogger. Add(request);  return Task.CompletedTask; },
                HTTPResponseLogger:  (ts, server, request, response, ct) => { postResponseLogger.Add(response); return Task.CompletedTask; },
                HTTPDelegate:        request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Application.JSON_UTF8,
                                       Content         = $"GET JSON: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );


            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);


            var getResponse   = await httpClient.Item1.SendRequest(
                                    httpClient.Item1.CreateRequest(
                                        HTTPMethod.GET,
                                        HTTPPath.Parse("/test3.txt"),
                                        Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.HTML_UTF8)
                                    )
                                );

            Assert.That(getResponse. HTTPBodyAsUTF8String,  Is.EqualTo("GET HTML: 'test3.txt'!"));


            var postResponse  = await httpClient.Item1.SendRequest(
                                    httpClient.Item1.CreateRequest(
                                        HTTPMethod.GET,
                                        HTTPPath.Parse("/test3.txt"),
                                        Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Application.JSON_UTF8)
                                    )
                                );

            Assert.That(postResponse.HTTPBodyAsUTF8String,  Is.EqualTo("GET JSON: 'test3.txt'!"));

        }

        #endregion


        #region MultipleRequests_ExplicitKeepAlives_01()

        [Test]
        public async Task MultipleRequests_ExplicitKeepAlives_01()
        {

            var httpServer  = await HTTPTestServerX.StartNew();

            var api1        = httpServer.AddHTTPAPI();

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var api2        = httpServer.AddHTTPAPI(HTTPPath.Parse("/api2/test/"));

            api2.AddHandler(HTTPPath.Root + "{filename1}",
                            HTTPMethod:    HTTPMethod.GET,
                            HTTPDelegate:  request => {

                                if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1))
                                    return Task.FromResult(
                                               new HTTPResponse.Builder(request) {
                                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                                   ContentType     = HTTPContentType.Text.PLAIN,
                                                   Content         = $"Hello World (/api2/test/): '{filename1}'!".ToUTF8Bytes(),
                                                   Connection      = ConnectionType.KeepAlive
                                               }.AsImmutable
                                           );

                                return Task.FromResult(
                                           new HTTPResponse.Builder(request) {
                                               HTTPStatusCode  = HTTPStatusCode.BadRequest
                                           }.AsImmutable
                                       );

                            });

            api2.AddHandler(HTTPPath.Root + "/test2/{filename2}",
                            HTTPMethod:    HTTPMethod.GET,
                            HTTPDelegate:  request => {

                                if (request.ParsedURLParametersX.TryGetValue("filename2", out var filename2))
                                    return Task.FromResult(
                                               new HTTPResponse.Builder(request) {
                                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                                   ContentType     = HTTPContentType.Text.PLAIN,
                                                   Content         = $"Hello World (/api2/test/test2/): '{filename2}'!".ToUTF8Bytes(),
                                                   Connection      = ConnectionType.KeepAlive
                                               }.AsImmutable
                                           );

                                return Task.FromResult(
                                           new HTTPResponse.Builder(request) {
                                               HTTPStatusCode  = HTTPStatusCode.BadRequest
                                           }.AsImmutable
                                       );

                            });


            //var client = new HTTPClient(URL.Parse($"http://localhost:{httpServer.TCPPort}/test3.txt"));
            //var xx = await client.GET(HTTPPath.Parse("/test3.txt"));


            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var file1      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            var port1      = httpClient.Item1.CurrentLocalPort;
            var httpBody1  = file1.HTTPBodyAsUTF8String ?? "";

            var file2      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2.txt")));
            var port2      = httpClient.Item1.CurrentLocalPort;
            var httpBody2  = file2.HTTPBodyAsUTF8String ?? "";

            var file3      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2/test3.txt")));
            var port3      = httpClient.Item1.CurrentLocalPort;
            var httpBody3  = file3.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1,  Is.EqualTo("Hello World: 'test1.txt'!"));
            Assert.That(httpBody2,  Is.EqualTo("Hello World (/api2/test/): 'test2.txt'!"));
            Assert.That(httpBody3,  Is.EqualTo("Hello World (/api2/test/test2/): 'test3.txt'!"));

            Assert.That(port1, Is.EqualTo(port2));
            Assert.That(port2, Is.EqualTo(port3));

        }

        #endregion

        #region MultipleRequests_ExplicitConnectionClose_01()

        [Test]
        public async Task MultipleRequests_ExplicitConnectionClose_01()
        {

            var httpServer  = await HTTPTestServerX.StartNew();

            var api1        = httpServer.AddHTTPAPI();

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World: '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var api2        = httpServer.AddHTTPAPI(HTTPPath.Parse("/api2/test/"));

            api2.AddHandler(HTTPPath.Root + "{filename1}",
                            HTTPMethod:    HTTPMethod.GET,
                            HTTPDelegate:  request => {

                                if (request.ParsedURLParametersX.TryGetValue("filename1", out var filename1))
                                    return Task.FromResult(
                                               new HTTPResponse.Builder(request) {
                                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                                   ContentType     = HTTPContentType.Text.PLAIN,
                                                   Content         = $"Hello World (/api2/test/): '{filename1}'!".ToUTF8Bytes(),
                                                   Connection      = ConnectionType.Close
                                               }.AsImmutable
                                           );

                                return Task.FromResult(
                                           new HTTPResponse.Builder(request) {
                                               HTTPStatusCode  = HTTPStatusCode.BadRequest
                                           }.AsImmutable
                                       );

                            });

            api2.AddHandler(HTTPPath.Root + "/test2/{filename2}",
                            HTTPMethod:    HTTPMethod.GET,
                            HTTPDelegate:  request => {

                                if (request.ParsedURLParametersX.TryGetValue("filename2", out var filename2))
                                    return Task.FromResult(
                                               new HTTPResponse.Builder(request) {
                                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                                   ContentType     = HTTPContentType.Text.PLAIN,
                                                   Content         = $"Hello World (/api2/test/test2/): '{filename2}'!".ToUTF8Bytes(),
                                                   Connection      = ConnectionType.Close
                                               }.AsImmutable
                                           );

                                return Task.FromResult(
                                           new HTTPResponse.Builder(request) {
                                               HTTPStatusCode  = HTTPStatusCode.BadRequest
                                           }.AsImmutable
                                       );

                            });


            //var client = new HTTPClient(URL.Parse($"http://localhost:{httpServer.TCPPort}/test3.txt"));
            //var xx = await client.GET(HTTPPath.Parse("/test3.txt"));


            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var httpClient = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            var file1      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            var port1      = httpClient.Item1.CurrentLocalPort;
            var httpBody1  = file1.HTTPBodyAsUTF8String ?? "";

            var file2      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2.txt")));
            var port2      = httpClient.Item1.CurrentLocalPort;
            var httpBody2  = file2.HTTPBodyAsUTF8String ?? "";

            var file3      = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2/test3.txt")));
            var port3      = httpClient.Item1.CurrentLocalPort;
            var httpBody3  = file3.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1,  Is.EqualTo("Hello World: 'test1.txt'!"));
            Assert.That(httpBody2,  Is.EqualTo("Hello World (/api2/test/): 'test2.txt'!"));
            Assert.That(httpBody3,  Is.EqualTo("Hello World (/api2/test/test2/): 'test3.txt'!"));

            Assert.That(port1, Is.Not.EqualTo(port2));
            Assert.That(port2, Is.Not.EqualTo(port3));

        }

        #endregion


        #region ClientServer_ChunkedEncoding_Test01()

        [Test]
        public async Task ClientServer_ChunkedEncoding_Test01()
        {

            var httpServer  = await HTTPTestServerX.StartNew();
            var api1              = httpServer.AddHTTPAPI();

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:    HTTPMethod.GET,
                HTTPDelegate:  request => {

                    try
                    {

                        if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                            return Task.FromResult(
                                       new HTTPResponse.Builder(request) {
                                           HTTPStatusCode    = HTTPStatusCode.OK,
                                           ContentType       = HTTPContentType.Text.PLAIN,
                                           ContentStream     = new ChunkedTransferEncodingStream(request.NetworkStream!, true),
                                           TransferEncoding  = "chunked",
                                           Connection        = ConnectionType.KeepAlive,
                                           Trailer           = "Expires, ETag",
                                           ChunkWorker       = async (response, stream) => {
                                                                   try
                                                                   {

                                                                       await stream.WriteAsync($"Hello World - Teil 1: '{filename}'!".ToUTF8Bytes(), response.CancellationToken);
                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Hello World - Teil 1 written!");
                                                                       await Task.Delay(100);

                                                                       await stream.WriteAsync($"Hello World - Teil 2: '{filename}'!".ToUTF8Bytes(), response.CancellationToken);
                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Hello World - Teil 2 written!");
                                                                       await Task.Delay(150);

                                                                       await stream.WriteAsync($"Hello World - Teil 3: '{filename}'!".ToUTF8Bytes(), response.CancellationToken);
                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Hello World - Teil 3 written!");
                                                                       await Task.Delay(200);

                                                                       await stream.WriteAsync($"Hello World - Teil 4: '{filename}'!".ToUTF8Bytes(), response.CancellationToken);
                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Hello World - Teil 4 written!");
                                                                       await Task.Delay(250);

                                                                       await stream.WriteAsync($"Hello World - Teil 5: '{filename}'!".ToUTF8Bytes(), response.CancellationToken);
                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Hello World - Teil 5 written!");
                                                                       await Task.Delay(300);

                                                                       await stream.Finish(
                                                                           new Dictionary<String, String> {
                                                                               { "Expires", "Wed, 21 Oct 2025 07:28:00 GMT" },
                                                                               { "ETag",    "abc123" }
                                                                           },
                                                                           response.CancellationToken
                                                                       );
                                                                       //DebugX.Log("Finished!");

                                                                       await stream.FlushAsync();
                                                                       //DebugX.Log("Flushed!");

                                                                   } catch (Exception e)
                                                                   {
                                                                       DebugX.Log(e.Message);
                                                                   }
                                                               }
                                       }.AsImmutable
                                   );

                    }
                    catch (Exception e)
                    {
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Error: {e.Message}".ToUTF8Bytes()
                                   }.AsImmutable
                               );
                    }

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );


            //var client = new HTTPClient(URL.Parse($"http://localhost:{httpServer.TCPPort}/test3.txt"));
            //var xx = await client.GET(HTTPPath.Parse("/test3.txt"));


            var httpClient    = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);

            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var httpResponse  = await httpClient.Item1.SendRequest(httpClient.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test3.txt")));
            Assert.That(httpResponse, Is.Not.Null);

            if (httpResponse is not null)
            {

                var chunks = new List<(TimeSpan, String)>();
                var trailers = await httpResponse.ReadAllChunks(chunk => chunks.Add((chunk.Elapsed, chunk.Data.ToUTF8String())));

                Assert.That(chunks[0].Item2, Is.EqualTo("Hello World - Teil 1: 'test3.txt'!"));
                Assert.That(chunks[1].Item2, Is.EqualTo("Hello World - Teil 2: 'test3.txt'!"));
                Assert.That(chunks[2].Item2, Is.EqualTo("Hello World - Teil 3: 'test3.txt'!"));
                Assert.That(chunks[3].Item2, Is.EqualTo("Hello World - Teil 4: 'test3.txt'!"));
                Assert.That(chunks[4].Item2, Is.EqualTo("Hello World - Teil 5: 'test3.txt'!"));
                Assert.That(trailers.Count(), Is.EqualTo(2));

                var delayDiffs = new List<TimeSpan>();
                for (var i = 1; i < chunks.Count; i++)
                    delayDiffs.Add(chunks[i].Item1 - chunks[i - 1].Item1);

            }

        }

        #endregion


        #region DNSSRV_Tests_01()

        [Test]
        public async Task DNSSRV_Tests_01()
        {

            var httpServer1  = await HTTPTestServerX.StartNew(IPv4Address.Localhost);
            var api1         = httpServer1.AddHTTPAPI();

            api1.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World (api1): '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpServer2  = await HTTPTestServerX.StartNew(IPv4Address.Parse("127.0.0.1"));
            var api2         = httpServer2.AddHTTPAPI();

            api2.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World (api2): '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );

            var httpServer3  = await HTTPTestServerX.StartNew(IPv4Address.Parse("127.0.0.1"));
            var api3         = httpServer3.AddHTTPAPI();

            api3.AddHandler(
                HTTPPath.Root + "{filename}",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => {

                    if (request.ParsedURLParametersX.TryGetValue("filename", out var filename))
                        return Task.FromResult(
                                   new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Hello World (api3): '{filename}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.KeepAlive
                                   }.AsImmutable
                               );

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest
                               }.AsImmutable
                           );

                }
            );


            // Might not work on Windows, but you can try:
            // netsh interface ipv4 add address "Loopback Pseudo-Interface 1" 127.0.0.2 255.0.0.0
            // netsh interface ipv4 add address "Loopback Pseudo-Interface 1" 127.0.0.3 255.0.0.0
            //
            // Removal:
            // netsh interface ipv4 delete address name="Loopback Pseudo-Interface 1" addr=127.0.0.2
            // netsh interface ipv4 delete address name="Loopback Pseudo-Interface 1" addr=127.0.0.3
            // 
            // netsh interface ipv4 show addresses "Loopback Pseudo-Interface 1"

            var dnsClient  = new DNSClient(SearchForIPv4DNSServers: true, SearchForIPv6DNSServers: false);
            dnsClient.CacheA  (DomainName.Parse("api1.example.local"), IPv4Address.Parse("127.0.0.1"));
            dnsClient.CacheA  (DomainName.Parse("api2.example.local"), IPv4Address.Parse("127.0.0.1"));
            dnsClient.CacheA  (DomainName.Parse("api3.example.local"), IPv4Address.Parse("127.0.0.1"));

            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 10, 20, api1.HTTPServer.TCPPort, DomainName.Parse("api1.example.local"));
            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 10, 80, api2.HTTPServer.TCPPort, DomainName.Parse("api2.example.local"));
            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 20,  0, api3.HTTPServer.TCPPort, DomainName.Parse("api3.example.local"));

            var srv = await dnsClient.Query_DNSService(
                                DomainName.Parse("api.example.local"),
                                SRV_Spec.  TLS  ("ocpp")
                            );
            Assert.That(srv, Is.Not.Null);
            Assert.That(srv.Count(), Is.EqualTo(3));

            var srv1 = srv.FirstOrDefault(srv => srv.Target.FullName.Contains("api1"));
            var srv2 = srv.FirstOrDefault(srv => srv.Target.FullName.Contains("api2"));
            var srv3 = srv.FirstOrDefault(srv => srv.Target.FullName.Contains("api3"));

            Assert.That(srv1!.Target,    Is.EqualTo(DomainName.Parse("api1.example.local")));
            Assert.That(srv1!.Priority,  Is.EqualTo(10));
            Assert.That(srv1!.Weight,    Is.EqualTo(20));

            Assert.That(srv2!.Target,    Is.EqualTo(DomainName.Parse("api2.example.local")));
            Assert.That(srv2!.Priority,  Is.EqualTo(10));
            Assert.That(srv2!.Weight,    Is.EqualTo(80));

            Assert.That(srv3!.Target,    Is.EqualTo(DomainName.Parse("api3.example.local")));
            Assert.That(srv3!.Priority,  Is.EqualTo(20));
            Assert.That(srv3!.Weight,    Is.EqualTo(0));

            Assert.That(srv1!.Port,      Is.Not.EqualTo(srv2!.Port));
            Assert.That(srv2!.Port,      Is.Not.EqualTo(srv3!.Port));



            var httpClient1 = await HTTPTestClient.ConnectNew(IPv4Address.Localhost, httpServer1.TCPPort);

            var response1a   = await httpClient1.Item1.SendRequest(httpClient1.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response1a, Is.Not.Null);

            var httpBody1a = response1a.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1a,  Is.EqualTo("Hello World (api1): 'test1.txt'!"));


            var response1b   = await httpClient1.Item1.SendRequest(httpClient1.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test2.txt")));
            Assert.That(response1b, Is.Not.Null);

            var httpBody1b = response1b.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1b,  Is.EqualTo("Hello World (api1): 'test2.txt'!"));






            var httpClient2 = await HTTPTestClient.ConnectNew(IPv4Address.Parse("127.0.0.1"), httpServer2.TCPPort);

            var response2   = await httpClient2.Item1.SendRequest(httpClient2.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response2, Is.Not.Null);

            var httpBody2 = response2.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody2,  Is.EqualTo("Hello World (api2): 'test1.txt'!"));




            var httpClient3 = await HTTPTestClient.ConnectNew(IPv4Address.Parse("127.0.0.1"), httpServer3.TCPPort);

            var response3 = await httpClient3.Item1.SendRequest(httpClient3.Item1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response3, Is.Not.Null);

            var httpBody3 = response3.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody3, Is.EqualTo("Hello World (api3): 'test1.txt'!"));




            var httpClientSRV = await HTTPTestClient.ConnectNew(DomainName.Parse("api.example.local"), SRV_Spec.TLS("ocpp"), DNSClient: dnsClient);

            var responseSRV = await httpClientSRV.SendRequest(httpClientSRV.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(responseSRV, Is.Not.Null);

            var httpBodySRV = responseSRV.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBodySRV, Is.EqualTo("Hello World (api1): 'test1.txt'!")
                                    .Or.EqualTo("Hello World (api2): 'test1.txt'!"));

        }

        #endregion


    }

}
