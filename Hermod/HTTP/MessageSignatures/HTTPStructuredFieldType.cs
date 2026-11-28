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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The three top-level types of an RFC 9651 Structured Field. A field's
    /// definition says which one it is; nothing in the value does.
    /// </summary>
    /// <remarks>
    /// RFC 9421 needs to know it for the <c>sf</c> and <c>key</c> component
    /// parameters (Sections 2.1.1 and 2.1.2): a field signed in its strict
    /// serialization has to be parsed first, and a field whose type is not known
    /// is an error, not a guess.
    /// </remarks>
    public enum HTTPStructuredFieldType
    {

        /// <summary>
        /// A List (RFC 9651, Section 3.1).
        /// </summary>
        List,

        /// <summary>
        /// A Dictionary (RFC 9651, Section 3.2).
        /// </summary>
        Dictionary,

        /// <summary>
        /// An Item (RFC 9651, Section 3.3).
        /// </summary>
        Item

    }

}
