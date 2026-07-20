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
/// The outcome of a DANE TLSA lookup for one MX host.
/// </summary>
/// <param name="Status">The usability of the lookup.</param>
/// <param name="Records">The TLSA records returned (only trustworthy when <see cref="Status"/> is <see cref="DaneStatus.Secure"/>).</param>
/// <param name="Detail">An optional human-readable explanation.</param>
public sealed record DaneResult(DaneStatus            Status,
                                IReadOnlyList<TLSA>   Records,
                                String?               Detail   = null)
{

    /// <summary>DANE applies and the certificate must be matched against <see cref="Records"/>.</summary>
    public Boolean  IsUsable
        => Status == DaneStatus.Secure && Records.Count > 0;

    /// <summary>The lookup proved the destination is DANE-protected but the records could not be trusted; delivery must be deferred.</summary>
    public Boolean  MustDefer
        => Status == DaneStatus.Bogus;

    public static DaneResult None(String? Detail = null)
        => new (DaneStatus.NoRecord, [], Detail);

}
