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

using org.GraphDefined.Vanaheimr.Illias;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

/// <summary>A parsed inbound TLS-RPT aggregate report (RFC 8460 §4).</summary>
public sealed record TlsRptReceivedReport(String?                              OrganizationName,
                                          String?                              ReportId,
                                          String?                              ContactInfo,
                                          DateTimeOffset?                      StartDateTime,
                                          DateTimeOffset?                      EndDateTime,
                                          IReadOnlyList<TlsRptReceivedPolicy>  Policies)
{
    public Int64 TotalSuccess => Policies.Sum(p => p.SuccessCount);
    public Int64 TotalFailure => Policies.Sum(p => p.FailureCount);
}
