﻿/*
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

using Microsoft.VisualBasic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;
using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics;
using System.Threading.Tasks;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTPTestServer tests.
    /// </summary>
    [TestFixture]
    public class HTTPTestServerTests
    {

        #region Setup/Teardown

        //private HTTPTestServerX httpServer;

        [OneTimeSetUp]
        public async Task Init_TEChunkedAPI()
        {

            


            //var api2 = httpServer.AddHTTPAPI(HTTPPath.Parse("/api2/test/"));

            //api2.AddHandler(HTTPPath.Root + "{filename1}",
            //                HTTPMethod:   HTTPMethod.GET,
            //                HTTPDelegate: async request => {
            //                    return new HTTPResponse.Builder(request) {
            //                               HTTPStatusCode  = HTTPStatusCode.OK,
            //                               ContentType     = HTTPContentType.Text.PLAIN,
            //                               Content         = "Hello World (/api2/test/)!".ToUTF8Bytes()
            //                           }.AsImmutable;
            //                });

            //api2.AddHandler(HTTPPath.Root + "/test2/{filename2}",
            //                HTTPMethod:   HTTPMethod.GET,
            //                HTTPDelegate: async request => {
            //                    return new HTTPResponse.Builder(request) {
            //                               HTTPStatusCode  = HTTPStatusCode.OK,
            //                               ContentType     = HTTPContentType.Text.PLAIN,
            //                               Content         = "Hello World (/api2/test/test2/)!".ToUTF8Bytes()
            //                           }.AsImmutable;
            //                });

        }

        [OneTimeTearDown]
        public async Task Shutdown_TEChunkedAPI()
        {

            //if (httpServer is not null)
            //    await httpServer.DisposeAsync();

        }

        #endregion


        #region HTTPTestServerTest_01()

        [Test]
        public async Task HTTPTestServerTest_01()
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


            //var client = new HTTPClient(URL.Parse($"http://localhost:{httpServer.TCPPort}/test3.txt"));
            //var xx = await client.GET(HTTPPath.Parse("/test3.txt"));


            var httpClient = await HTTPTestClient.ConnectNew(httpServer.TCPPort);

            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var dd = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test3.txt")));
            Assert.That(dd.Item2, Is.Not.Null);

            var httpBody = dd.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody,  Is.EqualTo("Hello World: 'test3.txt'!"));

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


            var httpClient = await HTTPTestClient.ConnectNew(httpServer.TCPPort);

            var file1      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            var port1      = httpClient.CurrentLocalTCPPort;
            var httpBody1  = file1.Item2?.HTTPBodyAsUTF8String ?? "";

            var file2      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2.txt")));
            var port2      = httpClient.CurrentLocalTCPPort;
            var httpBody2  = file2.Item2?.HTTPBodyAsUTF8String ?? "";

            var file3      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2/test3.txt")));
            var port3      = httpClient.CurrentLocalTCPPort;
            var httpBody3  = file3.Item2?.HTTPBodyAsUTF8String ?? "";

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


            var httpClient = await HTTPTestClient.ConnectNew(httpServer.TCPPort);

            var file1      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            var port1      = httpClient.CurrentLocalTCPPort;
            var httpBody1  = file1.Item2?.HTTPBodyAsUTF8String ?? "";

            var file2      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2.txt")));
            var port2      = httpClient.CurrentLocalTCPPort;
            var httpBody2  = file2.Item2?.HTTPBodyAsUTF8String ?? "";

            var file3      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api2/test/test2/test3.txt")));
            var port3      = httpClient.CurrentLocalTCPPort;
            var httpBody3  = file3.Item2?.HTTPBodyAsUTF8String ?? "";

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


            var httpClient = await HTTPTestClient.ConnectNew(httpServer.TCPPort);

            //var response1  = await httpClient.SendText("GET /test1.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
            //var response2  = await httpClient.SendText("GET /test2.txt HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");


            var response      = await httpClient.SendRequest(httpClient.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test3.txt")));
            var httpResponse  = response.Item2;
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










        #region HTTPTestServer_DNSTest_01()

        [Test]
        public async Task HTTPTestServer_DNSTest_01()
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

            var dnsClient  = new DNSClient();
            dnsClient.CacheA  (DomainName.Parse("api1.example.local"), IPv4Address.Parse("127.0.0.1"));
            dnsClient.CacheA  (DomainName.Parse("api2.example.local"), IPv4Address.Parse("127.0.0.1"));
            dnsClient.CacheA  (DomainName.Parse("api3.example.local"), IPv4Address.Parse("127.0.0.1"));

            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 10, 20, api1.HTTPTestServer.TCPPort, DomainName.Parse("api1.example.local"));
            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 10, 80, api2.HTTPTestServer.TCPPort, DomainName.Parse("api2.example.local"));
            dnsClient.CacheSRV(DNSServiceName.Parse("_ocpp._tls.api.example.local"), 20,  0, api3.HTTPTestServer.TCPPort, DomainName.Parse("api3.example.local"));

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

            var response1a   = await httpClient1.SendRequest(httpClient1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response1a.Item2, Is.Not.Null);

            var httpBody1a = response1a.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1a,  Is.EqualTo("Hello World (api1): 'test1.txt'!"));


            var response1b   = await httpClient1.SendRequest(httpClient1.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test2.txt")));
            Assert.That(response1b.Item2, Is.Not.Null);

            var httpBody1b = response1b.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody1b,  Is.EqualTo("Hello World (api1): 'test2.txt'!"));






            var httpClient2 = await HTTPTestClient.ConnectNew(IPv4Address.Parse("127.0.0.1"), httpServer2.TCPPort);

            var response2   = await httpClient2.SendRequest(httpClient2.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response2.Item2, Is.Not.Null);

            var httpBody2 = response2.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody2,  Is.EqualTo("Hello World (api2): 'test1.txt'!"));




            var httpClient3 = await HTTPTestClient.ConnectNew(IPv4Address.Parse("127.0.0.1"), httpServer3.TCPPort);

            var response3 = await httpClient3.SendRequest(httpClient3.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(response3.Item2, Is.Not.Null);

            var httpBody3 = response3.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBody3, Is.EqualTo("Hello World (api3): 'test1.txt'!"));




            var httpClientSRV = await HTTPTestClient.ConnectNew(URL.Parse("http://api.example.local"), SRV_Spec.TLS("ocpp"), DNSClient: dnsClient);

            var responseSRV = await httpClientSRV.SendRequest(httpClientSRV.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/test1.txt")));
            Assert.That(responseSRV.Item2, Is.Not.Null);

            var httpBodySRV = responseSRV.Item2?.HTTPBodyAsUTF8String ?? "";

            Assert.That(httpBodySRV, Is.EqualTo("Hello World (api2): 'test1.txt'!"));


        }

        #endregion


    }

}
