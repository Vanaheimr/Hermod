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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// A handle to an in-flight request, returned by
    /// <see cref="HTTP2ClientConnection.StartRequestAsync"/> once its HEADERS are
    /// sent: the allocated <see cref="StreamId"/> (usable with
    /// <see cref="HTTP2ClientConnection.UpdatePriorityAsync"/> to reprioritize it)
    /// and the <see cref="Response"/> task that completes when the response arrives.
    /// </summary>
    public sealed record HTTP2RequestHandle(UInt32 StreamId, Task<HTTP2Response> Response);

}
