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
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The usability of a DANE (RFC 7672) TLSA lookup for a given MX host.
/// </summary>
public enum DaneStatus
{

    /// <summary>No TLSA records are published (or their absence is DNSSEC-authenticated). DANE does not apply; delivery may proceed opportunistically.</summary>
    NoRecord,

    /// <summary>Usable TLSA records were returned and DNSSEC-validated. TLS MUST be enforced and the server certificate MUST match a TLSA record.</summary>
    Secure,

    /// <summary>TLSA records were returned but DNSSEC validation failed (bogus / indeterminate). The destination MUST be treated as broken and delivery deferred.</summary>
    Bogus,

    /// <summary>TLSA records were returned but the zone is not DNSSEC-signed, so they are not authenticated and (per RFC 7672 §2.2) not usable for DANE.</summary>
    Insecure

}
