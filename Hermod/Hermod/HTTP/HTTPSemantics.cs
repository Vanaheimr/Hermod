/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP tokens and well-known.
    /// </summary>
    public static class HTTPSemantics
    {

        /// <summary>
        /// Logfile...
        /// </summary>
        public static readonly HTTPEventSource_Id LogEvents = HTTPEventSource_Id.Parse("/LogEvents");

        // ---------------------------------------------------------------------------------

        /// <summary>
        /// A token for a 'callback' parameter within the HTTP query string.
        /// </summary>
        public const String CALLBACK  = "callback";

        /// <summary>
        /// A token for a 'skip' parameter within the HTTP query string.
        /// </summary>
        public const String SKIP      = "skip";

        /// <summary>
        /// A token for a 'take' parameter within the HTTP query string.
        /// </summary>
        public const String TAKE      = "take";

    }

}
