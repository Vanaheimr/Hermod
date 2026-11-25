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
/// DSN parameters a sender attaches to an outgoing message (RFC 3461) to request delivery status
/// notifications from the receiving server. Each goes on the wire only when it was given.
/// </summary>
/// <param name="Notify">The NOTIFY of every recipient (§4.1), or null for none: the server's default then applies, which reports failures. <see cref="DsnNotify.Never"/> is NOTIFY=NEVER - no DSN under any conditions.</param>
/// <param name="Ret">RET (§4.3): whether a failed DSN returns the whole message or its header only; null for none.</param>
/// <param name="EnvId">The envelope identifier (§4.4), as the DSN will show it: printable US-ASCII, space included. It goes on the wire as xtext, at most 100 characters of it.</param>
public sealed record DsnParameters(DsnNotify?  Notify   = null,
                                   DsnRet?     Ret      = null,
                                   string?     EnvId    = null)
{

    /// <summary>
    /// No DSN requested — the receiver applies its default (usually failures only).
    /// </summary>
    public static readonly DsnParameters None = new ();

    /// <summary>
    /// The ENVID of a message relayed, as xtext, as it was received: RFC 3461 §5.2.1 (a), "the
    /// ENVID parameter MUST be supplied when the message is relayed, with the same value" - not
    /// decoded and encoded again. When given, it is the ENVID, and <see cref="EnvId"/> is not.
    /// </summary>
    internal string? ReceivedEnvId { get; init; }

    /// <summary>
    /// Whether a DSN parameter was given at all: a NOTIFY, a RET, an ENVID.
    /// </summary>
    public bool IsRequested => Notify is not null || Ret is not null || EnvId is not null || ReceivedEnvId is not null;

    /// <summary>
    /// The ENVID as it goes on MAIL: xtext (RFC 3461 §4, §4.4).
    /// </summary>
    internal string? EnvIdXText
        => ReceivedEnvId ?? (EnvId is null ? null : DsnCommands.XText(EnvId));

    /// <summary>
    /// Why <see cref="EnvId"/> cannot be an ENVID, or null when it can - or when there is none.
    /// RFC 3461 §4.4: "the value of the ENVID parameter prior to encoding as 'xtext' MUST consist
    /// entirely of printable (graphic and white space) characters from the US-ASCII repertoire",
    /// and "The ENVID parameter MAY be up to 100 characters in length". A line break is none of
    /// them - in a command line it would end the MAIL command, and what follows would be a
    /// command of its own.
    /// </summary>
    public string? EnvIdError

        => EnvId is null || ReceivedEnvId is not null
               ? null
               : EnvId.Length == 0
                     ? "The ENVID is empty"
                     : EnvId.Any(c => c is < ' ' or > '~')
                           ? "The ENVID has a character that is not printable US-ASCII"
                           : DsnCommands.XText(EnvId).Length > 100
                                 ? "The ENVID is longer than 100 characters as xtext"
                                 : null;

}
