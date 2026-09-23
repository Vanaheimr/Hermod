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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3
{

    /// <summary>
    /// The RFC 7692 offer-negotiation table, run again under this namespace.
    ///
    /// The table itself lives with the HTTP/2 fixture and already asserts both copies of
    /// <c>WebSocketDeflate</c>, so this class adds no assertions — it adds a *gate*. The
    /// two conformance repositories select their tests by fully-qualified name
    /// (<c>Hermod.Tests.HTTP2</c> here, <c>Hermod.Tests.HTTP3</c> there), so a fixture
    /// that exists only under the HTTP/2 namespace never runs in HTTP/3's CI. Inheriting
    /// it here gives the HTTP/3 copy of the negotiation a witness in the pipeline that
    /// actually guards the HTTP/3 stack — the one place it had none.
    /// </summary>
    [TestFixture]
    public class WebSocketDeflateNegotiationTests : Tests.HTTP2.WebSocketDeflateNegotiationTests
    { }

}
