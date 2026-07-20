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
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The result of a synchronous direct-to-MX delivery to one recipient domain
/// (<see cref="MailSender.SendDirectAsync(EMailEnvelop, CancellationToken)"/>).
/// </summary>
/// <param name="TargetDomain">The recipient domain that was delivered to.</param>
/// <param name="Recipients">The recipients at that domain.</param>
/// <param name="Result">The receiving server's verdict (status, SMTP code, response text, MX host).</param>
public sealed record DirectSendResult(String      TargetDomain,
                                      String[]    Recipients,
                                      SendResult  Result)
{

    /// <summary>
    /// Whether the receiving MX accepted the message.
    /// </summary>
    public Boolean IsOk
        => Result.Status == SendStatus.Success;

}
