/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

    public enum SMTPStatusCode
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

}
