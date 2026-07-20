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
/// notifications from the receiving server.
/// </summary>
public sealed record DsnParameters(DsnNotify   Notify   = DsnNotify.Never,
                                   DsnRet      Ret      = DsnRet.Full,
                                   string?     EnvId    = null)
{

    /// <summary>No DSN requested — the receiver applies its default (usually failures only).</summary>
    public static readonly DsnParameters None = new (DsnNotify.Never);

    /// <summary>Whether a DSN was actually requested (a NOTIFY other than Never, or an ENVID).</summary>
    public bool IsRequested => Notify != DsnNotify.Never || EnvId is not null;

}
