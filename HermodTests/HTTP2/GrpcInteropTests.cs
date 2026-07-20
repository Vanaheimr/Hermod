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

using System.Text;
using System.Net.Security;
using System.Buffers.Binary;

using Grpc.Core;
using Grpc.Net.Client;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// gRPC over our from-scratch HTTP/2 stack: a Greeter service covering all
    /// four call types (unary, server-/client-streaming, bidi) on the streaming
    /// seam (grpc-status travels in HTTP/2 trailers), driven by both our own
    /// HTTP2Client (hand-rolled length-prefix framing) and the real .NET gRPC
    /// client (<c>Grpc.Net.Client</c>) as the production reference peer.
    /// In-process.
    /// </summary>
    [TestFixture]
    public class GrpcInteropTests
    {

        #region Minimal protobuf (a message with a single string field #1)

        private static void WriteVarint(List<Byte> buf, UInt64 v) { while (v >= 0x80) { buf.Add((Byte) (v | 0x80)); v >>= 7; } buf.Add((Byte) v); }
        private static UInt64 ReadVarint(Byte[] b, ref Int32 pos) { UInt64 r = 0; var sh = 0; while (true) { var by = b[pos++]; r |= (UInt64) (by & 0x7F) << sh; if ((by & 0x80) == 0) break; sh += 7; } return r; }

        private static Byte[] EncodeStr(String s)
        {
            var utf8 = Encoding.UTF8.GetBytes(s);
            var buf  = new List<Byte> { 0x0A };   // field 1, wire type 2 (length-delimited)
            WriteVarint(buf, (UInt64) utf8.Length);
            buf.AddRange(utf8);
            return buf.ToArray();
        }

        private static String DecodeStr(Byte[] msg)
        {
            var pos = 0; var val = "";
            while (pos < msg.Length)
            {
                var tag = ReadVarint(msg, ref pos); var field = (Int32) (tag >> 3); var wire = (Int32) (tag & 7);
                if (field == 1 && wire == 2) { var len = (Int32) ReadVarint(msg, ref pos); val = Encoding.UTF8.GetString(msg, pos, len); pos += len; }
                else switch (wire) { case 0: ReadVarint(msg, ref pos); break; case 2: pos += (Int32) ReadVarint(msg, ref pos); break; case 5: pos += 4; break; case 1: pos += 8; break; default: pos = msg.Length; break; }
            }
            return val;
        }

        #endregion

        #region gRPC length-prefixed framing

        private static Byte[] Frame(Byte[] message)
        {
            var f = new Byte[5 + message.Length];
            f[0] = 0;   // not compressed
            BinaryPrimitives.WriteUInt32BigEndian(f.AsSpan(1, 4), (UInt32) message.Length);
            message.CopyTo(f, 5);
            return f;
        }

        private static List<Byte[]> Deframe(Byte[] data)
        {
            var msgs = new List<Byte[]>(); var pos = 0;
            while (pos + 5 <= data.Length)
            {
                var len = (Int32) BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos + 1, 4));
                pos += 5;
                if (pos + len > data.Length) break;
                msgs.Add(data[pos..(pos + len)]); pos += len;
            }
            return msgs;
        }

        #endregion

        #region The Greeter service (on our streaming seam)

        private static async Task Greeter(IHTTP2RequestStream req, IHTTP2ResponseStream resp, CancellationToken ct)
        {
            var path   = req.Headers.FirstOrDefault(h => h.Name == ":path").Value ?? "";
            var reader = new GrpcMessageReader(req.ReadAsync);

            await resp.WriteHeadersAsync([(":status", "200"), ("content-type", "application/grpc")], ct);

            if (path.EndsWith("/SayHello"))   // unary
            {
                var name = DecodeStr(await reader.NextAsync(ct) ?? []);
                await resp.WriteAsync(Frame(EncodeStr($"Hello, {name}!")), ct);
                await resp.CompleteAsync([("grpc-status", "0")], ct);
            }
            else if (path.EndsWith("/SayHelloStream"))   // server-streaming
            {
                var name = DecodeStr(await reader.NextAsync(ct) ?? []);
                for (var i = 1; i <= 3; i++)
                    await resp.WriteAsync(Frame(EncodeStr($"Hello #{i}, {name}!")), ct);
                await resp.CompleteAsync([("grpc-status", "0")], ct);
            }
            else if (path.EndsWith("/SayHelloClientStream"))   // client-streaming: read all, reply once
            {
                var names = new List<String>();
                Byte[]? m;
                while ((m = await reader.NextAsync(ct)) is not null)
                    names.Add(DecodeStr(m));
                await resp.WriteAsync(Frame(EncodeStr($"Hello, {String.Join(" and ", names)}!")), ct);
                await resp.CompleteAsync([("grpc-status", "0")], ct);
            }
            else if (path.EndsWith("/SayHelloBidi"))   // bidi: reply to each request message as it arrives
            {
                Byte[]? m;
                while ((m = await reader.NextAsync(ct)) is not null)
                    await resp.WriteAsync(Frame(EncodeStr($"Hello, {DecodeStr(m)}!")), ct);
                await resp.CompleteAsync([("grpc-status", "0")], ct);
            }
            else
                await resp.CompleteAsync([("grpc-status", "12"), ("grpc-message", "Method not found")], ct);   // UNIMPLEMENTED
        }

        private static Task<(List<(String, String)>, Byte[]?)> BufferedUnused(UInt32 s, List<(String Name, String Value)> h, Byte[]? b, CancellationToken c)
            => Task.FromResult<(List<(String, String)>, Byte[]?)>(([(":status", "200")], null));

        private TestH2Server srv       = null!;
        private String       authority = "";

        [OneTimeSetUp]
        public async Task StartServer()
        {
            srv       = await TestH2Server.StartAsync(BufferedUnused, StreamingHandler: Greeter);
            authority = $"localhost:{srv.Port}";
        }

        [OneTimeTearDown]
        public async Task StopServer()
            => await srv.DisposeAsync();

        private GrpcChannel NewChannel()
        {
            var handler = new SocketsHttpHandler {
                SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            };
            return GrpcChannel.ForAddress($"https://localhost:{srv.Port}", new GrpcChannelOptions { HttpHandler = handler });
        }

        #endregion


        #region OurClient_Unary()

        [Test]
        public async Task OurClient_Unary()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var resp = await conn.SendRequestAsync("POST", "https", authority, "/helloworld.Greeter/SayHello",
                           ExtraHeaders: [("content-type", "application/grpc"), ("te", "trailers")],
                           Body: Frame(EncodeStr("Ada")));
            var reply = Deframe(resp.Body);
            Assert.Multiple(() =>
            {
                Assert.That(resp.Status, Is.EqualTo(200));
                Assert.That(resp.HeaderValue("content-type") ?? "", Does.StartWith("application/grpc"));
                Assert.That(reply, Has.Count.EqualTo(1));
                Assert.That(DecodeStr(reply[0]), Is.EqualTo("Hello, Ada!"), "unary response message");
                Assert.That(resp.Trailers.Any(t => t is { Name: "grpc-status", Value: "0" }), Is.True, "grpc-status 0 in trailers");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_ServerStreaming()

        [Test]
        public async Task OurClient_ServerStreaming()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var sresp = await conn.SendRequestAsync("POST", "https", authority, "/helloworld.Greeter/SayHelloStream",
                            ExtraHeaders: [("content-type", "application/grpc"), ("te", "trailers")],
                            Body: Frame(EncodeStr("Ada")));
            var msgs = Deframe(sresp.Body).Select(DecodeStr).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(msgs, Is.EqualTo(new[] { "Hello #1, Ada!", "Hello #2, Ada!", "Hello #3, Ada!" }), "3 streamed messages");
                Assert.That(sresp.Trailers.Any(t => t is { Name: "grpc-status", Value: "0" }), Is.True, "grpc-status 0 in trailers");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_ClientStreaming()

        [Test]
        public async Task OurClient_ClientStreaming()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var cs = await conn.StartStreamingRequestAsync("POST", "https", authority, "/helloworld.Greeter/SayHelloClientStream",
                         ExtraHeaders: [("content-type", "application/grpc"), ("te", "trailers")]);
            foreach (var n in new[] { "Ada", "Grace", "Lin" })
                await cs.WriteAsync(Frame(EncodeStr(n)));
            await cs.CompleteRequestAsync();

            var csHead = await cs.GetResponseAsync();
            var csBody = new List<Byte>();
            Byte[]? csChunk;
            while ((csChunk = await cs.ReadAsync()) is not null)
                csBody.AddRange(csChunk);
            var csReply    = Deframe(csBody.ToArray()).Select(DecodeStr).ToList();
            var csTrailers = await cs.GetTrailersAsync();

            Assert.Multiple(() =>
            {
                Assert.That(csHead.Status, Is.EqualTo(200), "head :status 200");
                Assert.That(csReply, Has.Count.EqualTo(1));
                Assert.That(csReply[0], Is.EqualTo("Hello, Ada and Grace and Lin!"), "single joined reply");
                Assert.That(csTrailers.Any(t => t is { Name: "grpc-status", Value: "0" }), Is.True, "grpc-status 0 in trailers");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region OurClient_Bidi()

        [Test]
        public async Task OurClient_Bidi()
        {
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var bd = await conn.StartStreamingRequestAsync("POST", "https", authority, "/helloworld.Greeter/SayHelloBidi",
                         ExtraHeaders: [("content-type", "application/grpc"), ("te", "trailers")]);
            var bdHead   = await bd.GetResponseAsync();   // server writes response HEADERS before reading
            var bdReader = new GrpcMessageReader(ct => new ValueTask<Byte[]?>(bd.ReadAsync(ct)));
            var bdReplies = new List<String>();
            foreach (var n in new[] { "Ada", "Grace", "Lin" })
            {
                await bd.WriteAsync(Frame(EncodeStr(n)));
                var r = await bdReader.NextAsync(CancellationToken.None);
                if (r is not null) bdReplies.Add(DecodeStr(r));
            }
            await bd.CompleteRequestAsync();
            var bdEnd      = await bdReader.NextAsync(CancellationToken.None);   // drains to end-of-stream (null)
            var bdTrailers = await bd.GetTrailersAsync();

            Assert.Multiple(() =>
            {
                Assert.That(bdHead.Status, Is.EqualTo(200), "head :status 200");
                Assert.That(bdReplies, Is.EqualTo(new[] { "Hello, Ada!", "Hello, Grace!", "Hello, Lin!" }), "a reply per request message, interleaved");
                Assert.That(bdEnd, Is.Null, "stream ends cleanly");
                Assert.That(bdTrailers.Any(t => t is { Name: "grpc-status", Value: "0" }), Is.True, "grpc-status 0 in trailers");
            });
            await conn.CloseAsync();
        }

        #endregion

        #region RealGrpc_Unary()

        [Test]
        public async Task RealGrpc_Unary()
        {
            using var channel = NewChannel();
            var invoker = channel.CreateCallInvoker();
            var marsh   = Marshallers.Create<String>(EncodeStr, DecodeStr);
            var opts    = new CallOptions();

            var sayHello = new Method<String, String>(MethodType.Unary, "helloworld.Greeter", "SayHello", marsh, marsh);
            var reply    = await invoker.AsyncUnaryCall(sayHello, null, opts, "World").ResponseAsync;
            Assert.That(reply, Is.EqualTo("Hello, World!"), "real gRPC unary");
        }

        #endregion

        #region RealGrpc_ServerStreaming()

        [Test]
        public async Task RealGrpc_ServerStreaming()
        {
            using var channel = NewChannel();
            var invoker = channel.CreateCallInvoker();
            var marsh   = Marshallers.Create<String>(EncodeStr, DecodeStr);
            var opts    = new CallOptions();

            var method   = new Method<String, String>(MethodType.ServerStreaming, "helloworld.Greeter", "SayHelloStream", marsh, marsh);
            using var call = invoker.AsyncServerStreamingCall(method, null, opts, "World");
            var got = new List<String>();
            while (await call.ResponseStream.MoveNext(CancellationToken.None)) got.Add(call.ResponseStream.Current);
            Assert.That(got, Is.EqualTo(new[] { "Hello #1, World!", "Hello #2, World!", "Hello #3, World!" }), "real gRPC server-streaming");
        }

        #endregion

        #region RealGrpc_UnknownMethod_Unimplemented()

        [Test]
        public async Task RealGrpc_UnknownMethod_Unimplemented()
        {
            using var channel = NewChannel();
            var invoker = channel.CreateCallInvoker();
            var marsh   = Marshallers.Create<String>(EncodeStr, DecodeStr);
            var opts    = new CallOptions();

            var unknown = new Method<String, String>(MethodType.Unary, "helloworld.Greeter", "Nope", marsh, marsh);
            var ex = Assert.ThrowsAsync<RpcException>(async () => await invoker.AsyncUnaryCall(unknown, null, opts, "x").ResponseAsync);
            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unimplemented), "unknown method -> UNIMPLEMENTED");
        }

        #endregion

        #region RealGrpc_ClientStreaming()

        [Test]
        public async Task RealGrpc_ClientStreaming()
        {
            using var channel = NewChannel();
            var invoker = channel.CreateCallInvoker();
            var marsh   = Marshallers.Create<String>(EncodeStr, DecodeStr);
            var opts    = new CallOptions();

            var method = new Method<String, String>(MethodType.ClientStreaming, "helloworld.Greeter", "SayHelloClientStream", marsh, marsh);
            using var call = invoker.AsyncClientStreamingCall(method, null, opts);
            foreach (var n in new[] { "Ada", "Grace" })
                await call.RequestStream.WriteAsync(n);
            await call.RequestStream.CompleteAsync();
            Assert.That(await call.ResponseAsync, Is.EqualTo("Hello, Ada and Grace!"), "real gRPC client-streaming joined reply");
        }

        #endregion

        #region RealGrpc_Bidi()

        [Test]
        public async Task RealGrpc_Bidi()
        {
            using var channel = NewChannel();
            var invoker = channel.CreateCallInvoker();
            var marsh   = Marshallers.Create<String>(EncodeStr, DecodeStr);
            var opts    = new CallOptions();

            var method = new Method<String, String>(MethodType.DuplexStreaming, "helloworld.Greeter", "SayHelloBidi", marsh, marsh);
            using var call = invoker.AsyncDuplexStreamingCall(method, null, opts);
            var got = new List<String>();
            foreach (var n in new[] { "Ada", "Grace", "Lin" })
            {
                await call.RequestStream.WriteAsync(n);
                if (await call.ResponseStream.MoveNext(CancellationToken.None))
                    got.Add(call.ResponseStream.Current);
            }
            await call.RequestStream.CompleteAsync();
            Assert.That(got, Is.EqualTo(new[] { "Hello, Ada!", "Hello, Grace!", "Hello, Lin!" }), "real gRPC bidi: a reply per request message");
        }

        #endregion


        #region (nested) GrpcMessageReader

        /// <summary>
        /// Reassembles gRPC length-prefixed messages ([1-byte flag][4-byte BE
        /// length][message]) from a stream of arbitrary byte chunks, retaining a
        /// running buffer across calls. NextAsync returns the next whole message,
        /// or null at end-of-stream.
        /// </summary>
        private sealed class GrpcMessageReader(Func<CancellationToken, ValueTask<Byte[]?>> Read)
        {

            private readonly List<Byte> buffer = [];

            public async Task<Byte[]?> NextAsync(CancellationToken CancellationToken)
            {
                while (true)
                {
                    if (buffer.Count >= 5)
                    {
                        var len = (buffer[1] << 24) | (buffer[2] << 16) | (buffer[3] << 8) | buffer[4];
                        if (buffer.Count >= 5 + len)
                        {
                            var msg = buffer.GetRange(5, len).ToArray();
                            buffer.RemoveRange(0, 5 + len);
                            return msg;
                        }
                    }

                    var chunk = await Read(CancellationToken);
                    if (chunk is null)
                        return null;
                    buffer.AddRange(chunk);
                }
            }

        }

        #endregion

    }

}
