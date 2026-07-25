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
    /// The outcome of <see cref="HTTP2ClientConnection.DownloadAsync"/>: how much
    /// arrived, and how much work it took to get there.
    /// </summary>
    /// <param name="Status">Status of the response that finished the download (200 or 206).</param>
    /// <param name="Headers">Header fields of the first response — the ones describing the representation as a whole.</param>
    /// <param name="BytesWritten">Total bytes written to the destination across all attempts.</param>
    /// <param name="Attempts">How many requests it took (1 when nothing went wrong).</param>
    /// <param name="Resumes">How many of those were <c>Range</c> continuations of an interrupted transfer.</param>
    /// <param name="Restarts">How many times the transfer had to start over because the representation had changed underneath it.</param>
    /// <param name="Validator">The validator the resumes were guarded by, or null if the server offered none.</param>
    /// <param name="DigestVerification">
    /// Whether the assembled representation was checked against the server's
    /// <c>Repr-Digest</c> (RFC 9530) — only ever anything but
    /// <see cref="HTTPDigestVerification.NotPresent"/> when
    /// <see cref="HTTP2ClientOptions.VerifyDigests"/> is on and the server answered
    /// the <c>Want-Repr-Digest</c>. A mismatch throws rather than appearing here,
    /// so this distinguishes "verified" from "there was nothing to verify against"
    /// — which for a download spliced out of range responses is the difference
    /// between knowing the file is right and merely hoping so.
    /// </param>
    public sealed record HTTP2DownloadResult(Int32                             Status,
                                             List<(String Name, String Value)> Headers,
                                             Int64                             BytesWritten,
                                             Int32                             Attempts,
                                             Int32                             Resumes,
                                             Int32                             Restarts,
                                             String?                           Validator,
                                             HTTPDigestVerification            DigestVerification = HTTPDigestVerification.NotPresent)
    {

        /// <summary>Whether the download completed without any interruption at all.</summary>
        public Boolean WasUninterrupted
            => Attempts == 1;

        public String? HeaderValue(String Name)
            => Headers.FirstOrDefault(header => header.Name == Name).Value;

    }

}
