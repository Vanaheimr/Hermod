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

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Globalization;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// One outbound TLS session outcome, fed to the TLS-RPT aggregator (RFC 8460).
/// </summary>
/// <param name="PolicyDomain">The Policy Domain against which the MTA-STS or DANE policy is defined (RFC 8460 §4.4):
/// the recipient domain, or the smart host's (RFC 8461 §3.4) - whose TLS-RPT policy this belongs to.</param>
/// <param name="PolicyType">The governing policy type.</param>
/// <param name="MxHost">The receiving MX host name.</param>
/// <param name="ReceivingIp">The receiving MX IP address (if known).</param>
/// <param name="SendingIp">Our sending IP address (if known).</param>
/// <param name="Success">Whether a compliant TLS session was established.</param>
/// <param name="FailureType">On failure, the RFC 8460 §4.3 result-type; null on success.</param>
/// <param name="PolicyString">The policy applied, as the "policy-string" of RFC 8460 §4.4/§4.5: the lines of the
/// MTA-STS policy, or the TLSA records as "usage selector matching-type data"; empty without a policy.</param>
/// <param name="FailureReasonCode">On failure, what exactly went wrong - "an error code or error text" (RFC 8460
/// §4.3.3) - as the "failure-reason-code" of the report; a certificate-not-trusted and a validation-failure
/// SHOULD have one (§4.3.1).</param>
public sealed record TlsRptEvent(String                  PolicyDomain,
                                 TlsRptPolicyType        PolicyType,
                                 String?                 MxHost,
                                 String?                 ReceivingIp,
                                 String?                 SendingIp,
                                 Boolean                 Success,
                                 String?                 FailureType,
                                 IReadOnlyList<String>?  PolicyString       = null,
                                 String?                 FailureReasonCode  = null);
