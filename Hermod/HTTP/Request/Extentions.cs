/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
    /// HTTP request extentions.
    /// </summary>
    public static class HTTPRequestExtentions
    {

        #region Reply(this HTTPRequest)

        /// <summary>
        /// Create a new HTTP response builder for the given request.
        /// </summary>
        /// <param name="HTTPRequest">A HTTP request.</param>
        public static HTTPResponseBuilder Reply(this HTTPRequest HTTPRequest)
        {
            return new HTTPResponseBuilder(HTTPRequest);
        }

        #endregion

    }

}
