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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

public sealed class QueuedMail
{

    // Constructed only inside this library (via MailSender or the internal report/relay paths);
    // external callers must go through MailSender with a typed EMail/EMailEnvelop instead of
    // hand-crafting a raw message string. [JsonConstructor] keeps FileMailQueue persistence working.
    [JsonConstructor]
    internal QueuedMail() { }

    public required String    Id                { get; init; }
    public required String    EnvelopeFrom      { get; init; }
    public required String[]  EnvelopeTo        { get; init; }
    public required String    MessageContent    { get; init; }
    public required String    TargetDomain      { get; init; }
    public DateTimeOffset     QueuedAt          { get; init; }  = Timestamp.Now;
    public DateTimeOffset     NextRetry         { get; set;  }  = Timestamp.Now;
    public UInt16             RetryCount        { get; set;  }  = 0;
    public String?            LastError         { get; set;  }
    public QueueItemStatus    Status            { get; set;  }  = QueueItemStatus.Pending;
    public DateTimeOffset?    DeliveredAt       { get; set;  }
    public String?            RemoteMx          { get; set;  }
    public String?            RemoteResponse    { get; set;  }
    public Boolean            RequireTls        { get; init; } = false;  // RFC 8689

    // DSN request (RFC 3461) attached by the sender; carried onto the outbound MAIL FROM / RCPT TO
    // (if the remote advertises DSN) and used to decide whether to emit a success DSN on delivery.
    public DsnNotify          Notify            { get; init; } = DsnNotify.Never;
    public DsnRet             Ret               { get; init; } = DsnRet.Full;
    public String?            EnvId             { get; init; }

    // Transport priority (MT-PRIORITY, RFC 6710): -9..9, higher is more urgent. Used to order the
    // queue and re-emitted on the outbound MAIL FROM when the next hop advertises MT-PRIORITY.
    public SByte              Priority          { get; init; } = 0;

}
