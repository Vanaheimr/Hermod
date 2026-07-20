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

using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A single header field parsed from a raw message header block, keeping the original
    /// bytes (folding, whitespace, case) so both "simple" and "relaxed" canonicalization
    /// can be reproduced exactly.
    /// </summary>
    /// <param name="Name">The field name (e.g. "From").</param>
    /// <param name="RawValue">Everything after the first colon, unmodified (may span folded lines).</param>
    /// <param name="RawField">The whole field "Name:value" incl. folded continuation lines.</param>
    public sealed record DkimHeaderField(String Name, String RawValue, String RawField);

}
