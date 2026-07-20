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

using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed record DkimConfig
    {
        public required string  Domain          { get; init; }
        public required string  Selector        { get; init; }
        public required string  PrivateKeyPem   { get; init; }
        public          string  Canonicalization{ get; init; } = "relaxed/relaxed";
        public          string  SignedHeaders   { get; init; } = "from:to:subject:date:message-id:mime-version:content-type";
        public          int     BodyLengthLimit { get; init; } = 0;  // 0 = no limit
    }

}
