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
    /// The outcome of AHTTPClient.DownloadAsync: what arrived in the destination,
    /// and how many requests it took to get it there.
    /// </summary>
    /// <param name="HTTPStatusCode">
    /// The status of the response that ended the download: 200 or 206 when it is
    /// complete, the server's own status when it refused, and ClientError, which
    /// no server sends, when the download did not finish: a body that broke off
    /// once too often or with nothing to resume from, a representation that
    /// changed under a destination that cannot seek, a digest that did not match.
    /// </param>
    /// <param name="Response">The response whose fields describe the representation as a whole: the first 200 or 206 since the last restart, or the response that ended the download when there was none.</param>
    /// <param name="BytesWritten">The octets of the representation now in the destination.</param>
    /// <param name="Attempts">How many requests it took, 1 when nothing broke off.</param>
    /// <param name="Resumes">How many of them asked for the rest with Range and If-Range.</param>
    /// <param name="Restarts">How many times the representation changed and the destination started over.</param>
    /// <param name="Validator">The strong entity-tag or the Last-Modified date the resumes were guarded by, or null when the server sent neither.</param>
    /// <param name="DigestVerification">The Repr-Digest check of the whole representation (RFC 9530, Section 3), when VerifyDigests is on.</param>
    /// <param name="Error">Why the download did not finish, or null.</param>
    public sealed record HTTPDownloadResult(HTTPStatusCode          HTTPStatusCode,
                                            HTTPResponse?           Response,
                                            UInt64                  BytesWritten,
                                            Byte                    Attempts,
                                            Byte                    Resumes,
                                            Byte                    Restarts,
                                            String?                 Validator,
                                            HTTPDigestVerification  DigestVerification   = HTTPDigestVerification.NotPresent,
                                            String?                 Error                = null)
    {

        /// <summary>
        /// Whether the destination holds the whole representation.
        /// </summary>
        public Boolean IsComplete

            => Error is null &&
               HTTPStatusCode.Code is 200 or 206;

        /// <summary>
        /// Whether it took one request, with nothing broken off.
        /// </summary>
        public Boolean WasUninterrupted

            => Attempts == 1;

    }

}
