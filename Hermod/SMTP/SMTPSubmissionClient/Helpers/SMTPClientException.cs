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

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// An SMTP exception.
    /// </summary>
    public class SMTPClientException : SMTPException
    {

        /// <summary>
        /// Create a new SMTP exception.
        /// </summary>
        /// <param name="Message">The exception message.</param>
        public SMTPClientException(String Message)
            : base(Message, null)
        { }

        /// <summary>
        /// Create a new SMTP exception.
        /// </summary>
        /// <param name="Message">The exception message.</param>
        /// <param name="InnerException">An optional inner exception.</param>
        public SMTPClientException(String Message, Exception InnerException)
            : base(Message, InnerException)
        { }

    }


    /// <summary>
    /// The remote server closed the TCP connection unexpectedly (a graceful FIN or an abrupt reset)
    /// while the client was awaiting or reading a reply. Detected immediately, without waiting for a
    /// command timeout.
    /// </summary>
    public sealed class SMTPConnectionClosedException : SMTPClientException
    {
        public SMTPConnectionClosedException(String Message = "The SMTP server closed the connection unexpectedly.")
            : base(Message)
        { }
    }

    /// <summary>
    /// The remote server stopped responding — no (complete) reply arrived within the command timeout.
    /// </summary>
    public sealed class SMTPTimeoutException : SMTPClientException
    {
        public SMTPTimeoutException(String Message = "The SMTP server did not respond in time.")
            : base(Message)
        { }
    }

}
