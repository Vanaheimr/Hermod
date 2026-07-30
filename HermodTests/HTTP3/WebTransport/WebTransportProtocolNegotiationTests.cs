/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.WebTransport;

/// <summary>
/// WebTransport application protocol negotiation (draft-ietf-webtrans-http3-13 §3.3): the ALPN-like
/// exchange via the structured-fields headers WT-Available-Protocols (list of strings) and
/// WT-Protocol (item string).
/// </summary>
[TestFixture]
public class WebTransportProtocolNegotiationTests
{
    // ---- Unit: structured-fields encoding (RFC 9651) ---------------------------------------

    [Test]
    public void SerializeProtocolList_QuotesAndEscapes()
    {
        Assert.That(WebTransportProtocols.SerializeProtocolList(["chat-v2", "chat-v1"]),
                    Is.EqualTo("\"chat-v2\", \"chat-v1\""));
        // SF strings escape DQUOTE and backslash (RFC 9651 §4.1.6).
        Assert.That(WebTransportProtocols.SerializeProtocol("a\"b\\c"), Is.EqualTo("\"a\\\"b\\\\c\""));
        // Unrepresentable characters (outside %x20-7E) and empty lists are errors.
        Assert.Throws<ArgumentException>(() => WebTransportProtocols.SerializeProtocol("umlaut-ä"));
        Assert.Throws<ArgumentException>(() => WebTransportProtocols.SerializeProtocolList([]));
    }

    [Test]
    public void TryParseProtocolList_ParsesStrings_AndIgnoresParameters()
    {
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\", \"b\"", out List<string> list1), Is.True);
        Assert.That(list1, Is.EqualTo(new[] { "a", "b" }));

        // Parameters have no semantics and are skipped over (draft §3.3) — including string/number values.
        // (RFC 9651 §4.2.3.2: parameters attach to the member WITHOUT whitespace before the ";".)
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\";q=0.9;note=\"x,y\", \"b\";flag", out List<string> list2), Is.True);
        Assert.That(list2, Is.EqualTo(new[] { "a", "b" }));

        // Escapes in the string value.
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\\\"b\\\\c\"", out List<string> list3), Is.True);
        Assert.That(list3, Is.EqualTo(new[] { "a\"b\\c" }));
    }

    [Test]
    public void TryParseProtocolList_NonStringMember_InvalidatesWholeField()
    {
        // draft §3.3: any value type other than string invalidates the ENTIRE field.
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\", token", out _), Is.False);
        Assert.That(WebTransportProtocols.TryParseProtocolList("42, \"a\"", out _), Is.False);
        Assert.That(WebTransportProtocols.TryParseProtocolList("(\"a\" \"b\")", out _), Is.False); // inner list
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\",", out _), Is.False);        // dangling comma
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"a\" \"b\"", out _), Is.False);   // missing comma
        Assert.That(WebTransportProtocols.TryParseProtocolList("\"unterminated", out _), Is.False);
        Assert.That(WebTransportProtocols.TryParseProtocolList("", out _), Is.False);
    }

    [Test]
    public void TryParseProtocol_ItemString_WithParametersIgnored()
    {
        Assert.That(WebTransportProtocols.TryParseProtocol("\"chat\"", out string p1), Is.True);
        Assert.That(p1, Is.EqualTo("chat"));

        Assert.That(WebTransportProtocols.TryParseProtocol(" \"chat\";v=2 ", out string p2), Is.True);
        Assert.That(p2, Is.EqualTo("chat"));

        Assert.That(WebTransportProtocols.TryParseProtocol("chat", out _), Is.False);         // token, not a string
        Assert.That(WebTransportProtocols.TryParseProtocol("\"a\", \"b\"", out _), Is.False); // list, not an item
        Assert.That(WebTransportProtocols.TryParseProtocol("?1", out _), Is.False);           // boolean
    }

    // ---- Integration: end to end via real CONNECT headers -----------------------------------

    [Test]
    public void Negotiation_EndToEnd_ServerPicksFromClientList()
    {
        // The server supports chat-v2/chat-v1 and takes the first (= client-preferred) offer.
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = Pair(
            selector: (_, offered) => offered.FirstOrDefault(p => p is "chat-v2" or "chat-v1"));
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong id = client.ConnectWebTransport("localhost", "/wt",
            availableProtocols: ["chat-v3", "chat-v2", "chat-v1"]);
        WebTransportSession? session = null;
        for (int r = 0; r < 20 && session is null; r++) { Pump(client, server); client.TryGetWebTransportSession(id, out session); }

        Assert.That(session, Is.Not.Null);
        Assert.That(session!.NegotiatedProtocol, Is.EqualTo("chat-v2"));       // client view
        Assert.That(_serverSession!.NegotiatedProtocol, Is.EqualTo("chat-v2")); // server view
    }

    [Test]
    public void Negotiation_SelectorChoiceOutsideOfferedList_IsDropped()
    {
        // draft §3.3 MUST: the server's choice must come from the offered list — otherwise no WT-Protocol.
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = Pair(
            selector: (_, _) => "not-offered");
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong id = client.ConnectWebTransport("localhost", "/wt", availableProtocols: ["chat"]);
        WebTransportSession? session = null;
        for (int r = 0; r < 20 && session is null; r++) { Pump(client, server); client.TryGetWebTransportSession(id, out session); }

        Assert.That(session, Is.Not.Null);
        Assert.That(session!.NegotiatedProtocol, Is.Null);
        Assert.That(_serverSession!.NegotiatedProtocol, Is.Null);
    }

    [Test]
    public void Negotiation_WithoutClientOffer_YieldsNoProtocol()
    {
        // Without WT-Available-Protocols the server may not choose anything (the selector gets no offer).
        bool selectorCalled = false;
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = Pair(
            selector: (_, _) => { selectorCalled = true; return "chat"; });
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong id = client.ConnectWebTransport("localhost", "/wt");
        WebTransportSession? session = null;
        for (int r = 0; r < 20 && session is null; r++) { Pump(client, server); client.TryGetWebTransportSession(id, out session); }

        Assert.That(session, Is.Not.Null);
        Assert.That(selectorCalled, Is.False, "Without an offer list the selector must not be asked.");
        Assert.That(session!.NegotiatedProtocol, Is.Null);
        Assert.That(_serverSession!.NegotiatedProtocol, Is.Null);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private WebTransportSession? _serverSession;

    private (Http3ClientConnection, Http3ServerConnection, ServerCertificate) Pair(
        Func<Http3Request, IReadOnlyList<string>, string?> selector)
    {
        _serverSession = null;
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
            webTransportMaxSessions: 4,
            webTransportHandler: _ => session => _serverSession = session,
            webTransportProtocolSelector: selector);
        var client = new Http3ClientConnection("localhost", certificateValidation: validation, webTransportMaxSessions: 4);
        client.Start();
        for (int r = 0; r < 20 && !client.HandshakeConfirmed; r++) Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();
        for (int r = 0; r < 5; r++) Pump(client, server); // SETTINGS both ways
        return (client, server, cert);
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
