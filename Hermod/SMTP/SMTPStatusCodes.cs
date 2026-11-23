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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public enum SMTPStatusCodes
    {

        SystemStatus                                    = 211,
        HelpMessage                                     = 214,
        ServiceReady                                    = 220,
        ServiceClosingTransmissionChannel               = 221,
        AuthenticationSuccessful                        = 235,
        Ok                                              = 250,
        UserNotLocalWillForward                         = 251,
        CannotVerifyUserWillAttemptDelivery             = 252,

        AuthenticationChallenge                         = 334,
        StartMailInput                                  = 354,

        ServiceNotAvailable                             = 421,
        PasswordTransitionNeeded                        = 432,
        MailboxBusy                                     = 450,
        ErrorInProcessing                               = 451,
        InsufficientStorage                             = 452,
        TemporaryAuthenticationFailure                  = 454,

        CommandUnrecognized                             = 500,
        SyntaxError                                     = 501,
        CommandNotImplemented                           = 502,
        BadCommandSequence                              = 503,
        CommandParameterNotImplemented                  = 504,
        AuthenticationRequired                          = 530,
        AuthenticationMechanismTooWeak                  = 534,
        EncryptionRequiredForAuthenticationMechanism    = 538,
        MailboxUnavailable                              = 550,
        UserNotLocalTryAlternatePath                    = 551,
        ExceededStorageAllocation                       = 552,
        MailboxNameNotAllowed                           = 553,
        TransactionFailed                               = 554

    }


    /// <summary>
    /// A reply code read as RFC 5321 §4.2.1 has it: "a sender-SMTP MUST be prepared to handle
    /// codes not specified in this document and MUST do so by interpreting the first digit only."
    /// A 252 to MAIL is a 250, a 350 to DATA a 354.
    /// </summary>
    public static class SMTPStatusCodesExtensions
    {

        /// <summary>
        /// 2yz, Positive Completion reply (RFC 5321 §4.2.1).
        /// </summary>
        public static Boolean IsPositiveCompletion  (this SMTPStatusCodes Code)
            => (Int32) Code is >= 200 and < 300;

        /// <summary>
        /// 3yz, Positive Intermediate reply (RFC 5321 §4.2.1).
        /// </summary>
        public static Boolean IsPositiveIntermediate(this SMTPStatusCodes Code)
            => (Int32) Code is >= 300 and < 400;

    }

}
