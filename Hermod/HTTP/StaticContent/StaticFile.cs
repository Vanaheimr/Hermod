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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// One file of a static content bundle, ready to be sent to a client.
    /// </summary>
    /// <param name="RelativePath">The normalized path below the bundle root, e.g. "assets/app.3f9c1a.js".</param>
    /// <param name="Content">The file content.</param>
    /// <param name="ContentType">The HTTP content type derived from the file extension.</param>
    /// <param name="ETag">A strong entity tag of the content, including the quotes.</param>
    public sealed record StaticFile(String           RelativePath,
                                    Byte[]           Content,
                                    HTTPContentType  ContentType,
                                    String           ETag)
    {

        #region (static) Create(RelativePath, Content)

        /// <summary>
        /// Create a static file, deriving content type and entity tag.
        /// </summary>
        /// <param name="RelativePath">The normalized path below the bundle root.</param>
        /// <param name="Content">The file content.</param>
        public static StaticFile Create(String  RelativePath,
                                        Byte[]  Content)

            => new (RelativePath,
                    Content,
                    HTTPContentType.ForFileName(RelativePath),
                    ComputeETag(Content));

        #endregion

        #region (static) ComputeETag(Content)

        /// <summary>
        /// A strong entity tag from the content: the first 16 bytes of its
        /// SHA-256 hash as lower-case hex, quoted as the header field requires.
        /// </summary>
        /// <param name="Content">The content.</param>
        public static String ComputeETag(ReadOnlySpan<Byte> Content)

            => $"\"{Convert.ToHexStringLower(SHA256.HashData(Content).AsSpan(0, 16))}\"";

        #endregion

    }

}
