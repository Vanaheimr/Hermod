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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>Static options for <see cref="DmarcReportService"/>.</summary>
    public sealed record DmarcReportingOptions(
        String    OrgName,
        String    ReportFromDisplay,   // e.g. "DMARC Reports <dmarc-reports@mx.example>"
        String    ReportFromAddress,   // bare address used as envelope sender
        String    ReportingDomain,     // domain of the report sender (report filenames / message-ids)
        TimeSpan  Interval,
        Boolean   EnableForensic
    );

}
