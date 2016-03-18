/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The delegate for the SMTP request log.
    /// </summary>
    /// <param name="SMTPProcessor">The sending SMTP processor.</param>
    /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
    /// <param name="EMail">The incoming e-mail.</param>
    internal delegate void InternalRequestLogHandler(SMTPConnection SMTPProcessor, DateTime ServerTimestamp, EMail EMail);

    /// <summary>
    /// The delegate for the SMTP request log.
    /// </summary>
    /// <param name="SMTPProcessor">The sending SMTP processor.</param>
    /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
    /// <param name="EMail">The incoming e-mail.</param>
    /// <param name="Response">The outgoing response.</param>
    internal delegate void InternalAccessLogHandler(SMTPConnection SMTPProcessor, DateTime ServerTimestamp, EMail EMail, SMTPExtendedResponse Response);




    /// <summary>
    /// The delegate for the SMTP access log.
    /// </summary>
    /// <param name="SMTPServer">The sending SMTP server.</param>
    /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
    /// <param name="EMail">The incoming e-mail.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate void SMTPAccessLogHandler(SMTPServer SMTPServer, DateTime ServerTimestamp, EMail EMail, SMTPExtendedResponse Response);


    /// <summary>
    /// The delegate for the SMTP error log.
    /// </summary>
    /// <param name="SMTPProcessor">The sending SMTP processor.</param>
    /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
    /// <param name="EMail">The incoming e-mail.</param>
    /// <param name="SMTPResponse">The outgoing response.</param>
    /// <param name="Error">The occured error.</param>
    /// <param name="LastException">The last occured exception.</param>
    internal delegate void InternalErrorLogHandler (SMTPConnection SMTPProcessor, DateTime ServerTimestamp, String SMTPCommand, EMail EMail, SMTPExtendedResponse Response, String Error = null, Exception LastException = null);

    /// <summary>
    /// The delegate for the SMTP error log.
    /// </summary>
    /// <param name="SMTPServer">The sending SMTP server.</param>
    /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
    /// <param name="EMail">The incoming e-mail.</param>
    /// <param name="SMTPResponse">The outgoing response.</param>
    /// <param name="Error">The occured error.</param>
    /// <param name="LastException">The last occured exception.</param>
    public delegate void SMTPErrorLogHandler(SMTPServer SMTPServer, DateTime ServerTimestamp, String SMTPCommand, EMail EMail, SMTPExtendedResponse Response, String Error = null, Exception LastException = null);


    /// <summary>
    /// A SMTP delegate.
    /// </summary>
    /// <param name="EMail">The incoming e-mail.</param>
    /// <returns>A SMTP response.</returns>
    public delegate SMTPExtendedResponse SMTPDelegate(EMail EMail);


}
