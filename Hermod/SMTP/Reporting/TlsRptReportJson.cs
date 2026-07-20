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

/// <summary>Serializes a <see cref="TlsRptDomainReport"/> to the RFC 8460 §4 JSON schema.</summary>
public static class TlsRptReportJson
{

    public static String Build(TlsRptDomainReport  report,
                               String              organizationName,
                               String              contactInfo,
                               String              reportId)
    {

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {

            w.WriteStartObject();

            w.WriteString("organization-name", organizationName);

            w.WriteStartObject("date-range");
            w.WriteString("start-datetime", report.WindowBegin.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteString("end-datetime",   report.WindowEnd.UtcDateTime.ToString  ("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteEndObject();

            w.WriteString("contact-info", contactInfo);
            w.WriteString("report-id",    reportId);

            w.WriteStartArray("policies");
            foreach (var policy in report.Policies)
            {
                w.WriteStartObject();

                w.WriteStartObject("policy");
                w.WriteString("policy-type", policy.PolicyType);
                if (policy.MxHosts.Count > 0)
                {
                    w.WriteStartArray("mx-host");
                    foreach (var mx in policy.MxHosts)
                        w.WriteStringValue(mx);
                    w.WriteEndArray();
                }
                w.WriteString("policy-domain", report.PolicyDomain);
                w.WriteEndObject();

                w.WriteStartObject("summary");
                w.WriteNumber("total-successful-session-count", policy.SuccessCount);
                w.WriteNumber("total-failure-session-count",    policy.Failures.Sum(f => f.FailedSessionCount));
                w.WriteEndObject();

                if (policy.Failures.Count > 0)
                {
                    w.WriteStartArray("failure-details");
                    foreach (var f in policy.Failures)
                    {
                        w.WriteStartObject();
                        w.WriteString("result-type", f.ResultType);
                        if (f.SendingMtaIp        is not null) w.WriteString("sending-mta-ip",        f.SendingMtaIp);
                        if (f.ReceivingMxHostname is not null) w.WriteString("receiving-mx-hostname", f.ReceivingMxHostname);
                        if (f.ReceivingIp         is not null) w.WriteString("receiving-ip",          f.ReceivingIp);
                        w.WriteNumber("failed-session-count", f.FailedSessionCount);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();

        }

        return Encoding.UTF8.GetString(stream.ToArray());

    }

}
