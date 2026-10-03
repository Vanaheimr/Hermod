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
    /// The message-size limit of the WebSocket framing, run again under this namespace.
    ///
    /// The fixture lives with HTTP/2 and already runs against both copies of
    /// <c>WebSocketConnection</c>, so this class adds no assertions. It puts them
    /// under <c>Hermod.Tests.HTTP3</c>, the name the HTTP/3 conformance repository
    /// selects its tests by, as <see cref="WebSocketDeflateNegotiationTests"/> does.
    /// </summary>
    [TestFixture]
    public class WebSocketMessageLimitTests : Tests.HTTP2.WebSocketMessageLimitTests
    { }

}
