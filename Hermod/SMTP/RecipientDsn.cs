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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Per-recipient DSN settings (RFC 3461 §4.1, §4.2): NOTIFY and ORCPT as the RCPT command
/// gave them.
/// </summary>
public sealed class RecipientDsn
{

    public required string      Recipient           { get; init; }

    /// <summary>
    /// The NOTIFY parameter, or null when the RCPT command had none. The difference matters: a
    /// relay passes on only what it received (RFC 3461 §5.2.1 (c)), and without NOTIFY a failure
    /// is still reported while a NOTIFY without FAILURE silences it (§5.2.6).
    /// </summary>
    public          DsnNotify?  Notify              { get; init; }

    /// <summary>
    /// The ORCPT parameter as received - address type, ";", and the address as xtext.
    /// </summary>
    public          string?     OriginalRecipient   { get; init; }

    /// <summary>
    /// Whether a failure is reported: unless NOTIFY was given without FAILURE (RFC 3461 §5.2.6).
    /// </summary>
    public          bool        ReportsFailure
        => Notify is null || Notify.Value.HasFlag(DsnNotify.Failure);

    /// <summary>
    /// Whether delivery is reported: only when NOTIFY asked for SUCCESS (RFC 3461 §5.2.2, §5.2.3).
    /// </summary>
    public          bool        ReportsSuccess
        => Notify is not null && Notify.Value.HasFlag(DsnNotify.Success);

    /// <summary>
    /// Whether a delay may be reported: unless NOTIFY was given without DELAY (RFC 3461 §5.2.5).
    /// </summary>
    public          bool        ReportsDelay
        => Notify is null || Notify.Value.HasFlag(DsnNotify.Delay);

}
