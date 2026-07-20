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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// What happened to the message (RFC 8098 §3.2.6.2, "disposition-type").
    /// </summary>
    public enum MessageDispositionType
    {
        /// <summary>The message was displayed to the user (a classic "read receipt").</summary>
        Displayed,
        /// <summary>The message was deleted without being displayed.</summary>
        Deleted,
        /// <summary>The message was forwarded/redirected without the user necessarily seeing it.</summary>
        Dispatched,
        /// <summary>The message was processed by some automatic agent without being displayed.</summary>
        Processed
    }

}
