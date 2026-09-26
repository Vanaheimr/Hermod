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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 7239, the Forwarded field: the standardised replacement for the
    /// X-Forwarded-For family, which Hermod spoke and this one it did not.
    ///
    /// Most of what can go wrong here is in the grammar rather than in the
    /// semantics. A value is a token *or a quoted string*, and the quoted one
    /// may contain the very characters that separate elements and pairs - so a
    /// parser that splits on commas and semicolons before looking at the
    /// quotation marks takes one element apart into several, and what it
    /// invents that way is a forwarding chain someone will act on.
    /// </summary>
    [TestFixture]
    public class ForwardedFieldTests
    {

        #region Data

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

        private URL          URL
            => URL.Parse($"http://127.0.0.1:{httpServer!.TCPPort}");

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            httpServer = new HTTPServer(
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            httpAPI    = new HTTPAPI(httpServer);

            // Reports what the server made of the forwarding fields: the
            // elements it parsed, the addresses it recorded, and the socket it
            // did not let them overwrite.
            httpAPI.AddHandler(HTTPPath.Root + "whoami",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = String.Join(
                                                                            "\n",
                                                                            $"elements={ForwardedElement.Combine(request.Forwarded)}",
                                                                            $"forwardedFor={request.HTTPSource.ForwardedFor.AggregateWith(", ")}",
                                                                            $"socket={request.HTTPSource.IPAddress}"
                                                                        ).ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.Stop();
        }

        #endregion

        #region (private) WhoAmI(Fields)

        private async Task<Dictionary<String, String>> WhoAmI(params (String Name, String Value)[] Fields)
        {

            using var client = new HTTPClient(URL);

            var response = await client.RunRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Root + "whoami",
                                     RequestBuilder:  builder => {
                                         foreach (var (name, value) in Fields)
                                             builder.SetHeaderField(name, value);
                                     },
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            return response.HTTPBodyAsUTF8String!.
                       Split('\n').
                       Select(line => line.Split('=', 2)).
                       ToDictionary(parts => parts[0], parts => parts[1]);

        }

        #endregion


        #region ASingleElementIsParsed()

        [Test]
        public void ASingleElementIsParsed()
        {

            Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43", out var elements), Is.True);

            var element = elements!.Single();

            Assert.Multiple(() => {
                Assert.That(element.For?.IPAddress?.ToString(),  Is.EqualTo("192.0.2.43"));
                Assert.That(element.For?.Port,                   Is.Null);
                Assert.That(element.By,                          Is.Null);
            });

        }

        #endregion

        #region AChainKeepsItsOrder()

        /// <summary>
        /// One element per hop, in the order they were traversed - the first is
        /// the one nearest the client.
        /// </summary>
        [Test]
        public void AChainKeepsItsOrder()
        {

            Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43, for=198.51.100.17, for=203.0.113.60", out var elements), Is.True);

            Assert.That(
                elements!.Select(element => element.For!.IPAddress!.ToString()),
                Is.EqualTo(new[] { "192.0.2.43", "198.51.100.17", "203.0.113.60" })
            );

        }

        #endregion

        #region AQuotedValueMayContainTheSeparators()

        /// <summary>
        /// The trap, measured rather than assumed: a quoted value may contain
        /// both the comma that separates elements and the semicolon that
        /// separates pairs. One element, two parameters, and the extension
        /// value comes back whole.
        ///
        /// The IPv6 case below is *not* an instance of this - an address
        /// contains neither separator - which is why it is a second test and
        /// not the same one. Reverting the quote-awareness fails this test
        /// alone.
        /// </summary>
        [Test]
        public void AQuotedValueMayContainTheSeparators()
        {

            Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43;note=\"a,b;c\"", out var elements), Is.True);

            var element = elements!.Single();

            Assert.Multiple(() => {
                Assert.That(elements!.Count(),                   Is.EqualTo(1));
                Assert.That(element.For?.IPAddress?.ToString(),  Is.EqualTo("192.0.2.43"));
                Assert.That(element.Parameters.Count,            Is.EqualTo(2));
                Assert.That(element.Parameters[1].Value,         Is.EqualTo("a,b;c"));
            });

        }

        #endregion

        #region AnIPv6NodeKeepsItsBracketsAndPort()

        /// <summary>
        /// An IPv6 node has to be quoted - ":" and "[" are not token
        /// characters - and the brackets are what keep the port findable
        /// inside it.
        /// </summary>
        [Test]
        public void AnIPv6NodeKeepsItsBracketsAndPort()
        {

            Assert.That(ForwardedElement.TryParseAll("for=\"[2001:db8:cafe::17]:4711\"", out var elements), Is.True);

            var node = elements!.Single().For!;

            Assert.Multiple(() => {
                Assert.That(elements!.Count(),        Is.EqualTo(1));
                Assert.That(node.NodeName,            Is.EqualTo("[2001:db8:cafe::17]"));
                Assert.That(node.IPAddress,           Is.Not.Null);
                Assert.That(node.Port?.ToUInt16(),    Is.EqualTo(4711));
            });

        }

        #endregion

        #region ABareIPv6NodeIsRejected()

        /// <summary>
        /// RFC 7239, Section 6: the brackets are not decoration. Without them
        /// there is no way to tell the last group of an address from a port,
        /// and guessing would be wrong for exactly the addresses that carry
        /// one.
        ///
        /// Two rules reject it, and they are worth keeping apart. Unquoted, the
        /// value is not even a token, since ":" is not a token character; that
        /// is the field grammar's doing and has nothing to do with addresses.
        /// Quoted, it reaches the node parser, and *that* is where the brackets
        /// are required.
        /// </summary>
        [Test]
        public void ABareIPv6NodeIsRejected()
        {

            Assert.Multiple(() => {
                Assert.That(ForwardedElement.TryParseAll("for=2001:db8:cafe::17",    out _), Is.False);
                Assert.That(ForwardedElement.TryParseAll("for=\"2001:db8:cafe::17\"", out _), Is.False);
            });

        }

        #endregion

        #region AllFourParametersAreRead()

        [Test]
        public void AllFourParametersAreRead()
        {

            Assert.That(ForwardedElement.TryParseAll("by=203.0.113.43;for=192.0.2.60;host=example.com;proto=http", out var elements), Is.True);

            var element = elements!.Single();

            Assert.Multiple(() => {
                Assert.That(element.By?. IPAddress?.ToString(),  Is.EqualTo("203.0.113.43"));
                Assert.That(element.For?.IPAddress?.ToString(),  Is.EqualTo("192.0.2.60"));
                Assert.That(element.Host,                        Is.EqualTo("example.com"));
                Assert.That(element.Proto,                       Is.EqualTo("http"));
            });

        }

        #endregion

        #region ANodeMayDeclineToIdentifyItself()

        /// <summary>
        /// Section 6.2 and 6.3: "unknown" and an obfuscated identifier are both
        /// valid nodes, and neither is an address. A reader that treats them as
        /// one has made an address up.
        /// </summary>
        [Test]
        public void ANodeMayDeclineToIdentifyItself()
        {

            Assert.That(ForwardedElement.TryParseAll("for=unknown, for=_hidden, for=\"_SEVKISEK\"", out var elements), Is.True);

            var nodes = elements!.Select(element => element.For!).ToArray();

            Assert.Multiple(() => {
                Assert.That(nodes[0].IsUnknown,     Is.True);
                Assert.That(nodes[0].IPAddress,     Is.Null);
                Assert.That(nodes[1].IsObfuscated,  Is.True);
                Assert.That(nodes[1].IPAddress,     Is.Null);
                Assert.That(nodes[2].NodeName,      Is.EqualTo("_SEVKISEK"));
            });

        }

        #endregion

        #region ARepeatedParameterIsRejected()

        /// <summary>
        /// Section 4: a parameter must not occur more than once per element.
        /// Two "for" values in one element mean two proxies wrote into the same
        /// one, and there is no defensible way to pick a winner.
        /// </summary>
        [Test]
        public void ARepeatedParameterIsRejected()
        {

            Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43;for=198.51.100.17", out _), Is.False);

        }

        #endregion

        #region AMalformedElementRejectsTheWholeField()

        /// <summary>
        /// Half a forwarding chain is worse than none, because it looks like a
        /// whole one.
        /// </summary>
        [Test]
        public void AMalformedElementRejectsTheWholeField()
        {

            Assert.Multiple(() => {
                Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43, for=\"unterminated",  out _), Is.False);
                Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43, nonsense",            out _), Is.False);
                Assert.That(ForwardedElement.TryParseAll("for=192.0.2.43, for=999.0.2.43",      out _), Is.False);
            });

        }

        #endregion

        #region ParameterNamesAreCaseInsensitive()

        [Test]
        public void ParameterNamesAreCaseInsensitive()
        {

            Assert.That(ForwardedElement.TryParseAll("FOR=192.0.2.43;PROTO=https", out var elements), Is.True);

            var element = elements!.Single();

            Assert.Multiple(() => {
                Assert.That(element.For?.IPAddress?.ToString(),  Is.EqualTo("192.0.2.43"));
                Assert.That(element.Proto,                       Is.EqualTo("https"));
            });

        }

        #endregion

        #region TheQuotingComesBackOnTheWayOut()

        /// <summary>
        /// A value that is not a token has to be quoted on the way out, and
        /// ":" and "[" are not token characters - so every node with a port and
        /// every IPv6 node needs it back. A round trip that loses the quotes
        /// produces a field the next hop cannot parse.
        /// </summary>
        [Test]
        public void TheQuotingComesBackOnTheWayOut()
        {

            var text = "for=192.0.2.43;proto=https, for=\"[2001:db8:cafe::17]:4711\";host=example.com";

            Assert.That(ForwardedElement.TryParseAll(text, out var elements), Is.True);

            Assert.That(ForwardedElement.Combine(elements!), Is.EqualTo(text));

        }

        #endregion

        #region AnElementCanBeBuiltToBeSent()

        /// <summary>
        /// The other direction: a proxy appending itself to the chain.
        ///
        /// The expanded address is Hermod's own IPv6Address.ToString(), which
        /// does not compress zero groups the way RFC 5952 recommends. That is
        /// valid here - RFC 7239 wants an IPv6address, and this is one - and it
        /// is written out rather than worked around, because it is a property
        /// of the IP layer rather than of this field.
        /// </summary>
        [Test]
        public void AnElementCanBeBuiltToBeSent()
        {

            var element = new ForwardedElement(
                              For:    ForwardedNode.From(IPv6Address.Parse("2001:db8:cafe::17"), IPPort.Parse(4711)),
                              By:     ForwardedNode.Obfuscated("_gateway"),
                              Proto:  "https"
                          );

            Assert.That(
                element.ToString(),
                Is.EqualTo("by=_gateway;for=\"[2001:0db8:cafe:0000:0000:0000:0000:0017]:4711\";proto=https")
            );

        }

        #endregion

        #region AnAlreadyBracketedAddressIsNotBracketedTwice()

        /// <summary>
        /// IPv6Address.ToString() brackets "::" and "::1" and no other address,
        /// so a node built from one of those two would come out as [[::1]] if
        /// the brackets RFC 7239 wants were added unconditionally.
        /// </summary>
        [Test]
        public void AnAlreadyBracketedAddressIsNotBracketedTwice()
        {

            Assert.Multiple(() => {
                Assert.That(ForwardedNode.From(IPv6Address.Parse("::1")).NodeName,  Is.EqualTo("[::1]"));
                Assert.That(ForwardedNode.From(IPv6Address.Parse("::")). NodeName,  Is.EqualTo("[::]"));
            });

        }

        #endregion


        #region TheServerReadsTheForwardingChain()

        [Test]
        public async Task TheServerReadsTheForwardingChain()
        {

            var seen = await WhoAmI(("Forwarded", "for=192.0.2.43;proto=https, for=\"[2001:db8:cafe::17]:4711\""));

            Assert.Multiple(() => {
                Assert.That(seen["elements"],      Is.EqualTo("for=192.0.2.43;proto=https, for=\"[2001:db8:cafe::17]:4711\""));
                Assert.That(seen["forwardedFor"],  Is.EqualTo("192.0.2.43, 2001:0db8:cafe:0000:0000:0000:0000:0017"));
            });

        }

        #endregion

        #region ForwardedIsPreferredOverXForwardedFor()

        /// <summary>
        /// Section 7.4: where a proxy sent both, the standardised one is the
        /// one whose grammar can say which hop each address belongs to.
        /// </summary>
        [Test]
        public async Task ForwardedIsPreferredOverXForwardedFor()
        {

            var seen = await WhoAmI(("Forwarded",       "for=192.0.2.43"),
                                    ("X-Forwarded-For", "198.51.100.17, 203.0.113.60"));

            Assert.That(seen["forwardedFor"], Is.EqualTo("192.0.2.43"));

        }

        #endregion

        #region TheClientAddressIsNotDroppedFromXForwardedFor()

        /// <summary>
        /// The older path skipped the first entry of X-Forwarded-For, which is
        /// the client - the one address the field exists to carry. The socket
        /// it kept instead is the *immediate* peer, so nothing was standing in
        /// for the dropped entry.
        /// </summary>
        [Test]
        public async Task TheClientAddressIsNotDroppedFromXForwardedFor()
        {

            var seen = await WhoAmI(("X-Forwarded-For", "192.0.2.43, 198.51.100.17, 203.0.113.60"));

            Assert.That(seen["forwardedFor"], Is.EqualTo("192.0.2.43, 198.51.100.17, 203.0.113.60"));

        }

        #endregion

        #region TheSocketIsNeverReplacedByAHeaderField()

        /// <summary>
        /// Section 8.1: all of this is hearsay, and a client with no proxy in
        /// front of it can write whatever it likes. It is recorded beside the
        /// peer address, never instead of it.
        /// </summary>
        [Test]
        public async Task TheSocketIsNeverReplacedByAHeaderField()
        {

            var seen = await WhoAmI(("Forwarded", "for=192.0.2.43"));

            Assert.That(seen["socket"], Is.EqualTo("127.0.0.1"));

        }

        #endregion

    }

}
