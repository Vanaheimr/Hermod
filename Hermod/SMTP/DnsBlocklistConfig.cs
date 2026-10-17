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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// What a client gets whose address a DNS blocklist names.
    /// </summary>
    public enum DnsBlocklistAction
    {

        /// <summary>
        /// Its recipients are refused at RCPT, 554 - all but the postmaster, who must
        /// be reachable even for it (RFC 5321 §4.5.1). What it sends the postmaster
        /// is marked.
        /// </summary>
        Reject,

        /// <summary>
        /// Its mail is accepted and marked: <see cref="DnsVerificationResult.Blocklists"/>
        /// and an X-DNSBL header. What becomes of it is the storage's to decide.
        /// </summary>
        Mark

    }


    /// <summary>
    /// The DNS blocklists (RFC 5782) asked about the address of every client that
    /// is not signed in and brings mail for the local domains - once a connection.
    /// </summary>
    /// <remarks>
    /// A list answers with an address in 127.0.0.0/8 for an address it names. An
    /// answer in 127.255.255.0/24 is a list refusing the question - Spamhaus does so
    /// for a question that came through a public resolver - and an answer outside
    /// 127.0.0.0/8 a resolver that answers every name: neither names anybody, and
    /// both are logged. A list that does not answer in time names nobody either.
    /// </remarks>
    public sealed record DnsBlocklistConfig
    {

        /// <summary>
        /// The zones asked, e.g. zen.spamhaus.org.
        /// </summary>
        public required IReadOnlyList<String>  Zones                   { get; init; }

        /// <summary>
        /// What a client gets that one of them names.
        /// </summary>
        public DnsBlocklistAction               Action                  { get; init; } = DnsBlocklistAction.Mark;

        /// <summary>
        /// How long a list is waited for.
        /// </summary>
        public TimeSpan                         Timeout                 { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Whether loopback, private, link-local and shared addresses are asked about
        /// too - no list names them, so not by default.
        /// </summary>
        public Boolean                          CheckPrivateAddresses   { get; init; } = false;

    }

}
