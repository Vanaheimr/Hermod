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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A full message disposition as it appears in the "Disposition:" field of an MDN
    /// (RFC 8098 §3.2.6): action-mode/sending-mode; disposition-type.
    /// </summary>
    /// <param name="ActionMode">Whether the disposition was manual or automatic.</param>
    /// <param name="SendingMode">Whether the MDN was sent manually or automatically.</param>
    /// <param name="Type">What happened to the message.</param>
    public readonly record struct MessageDisposition(DispositionActionMode   ActionMode,
                                                     DispositionSendingMode  SendingMode,
                                                     MessageDispositionType  Type)
    {

        /// <summary>
        /// A "displayed" read receipt that was generated automatically.
        /// </summary>
        public static MessageDisposition DisplayedAutomatically
            => new (DispositionActionMode.AutomaticAction, DispositionSendingMode.SentAutomatically, MessageDispositionType.Displayed);

        /// <summary>
        /// A "displayed" read receipt that the user chose to send.
        /// </summary>
        public static MessageDisposition DisplayedManually
            => new (DispositionActionMode.ManualAction, DispositionSendingMode.SentManually, MessageDispositionType.Displayed);


        private static String ActionModeToken(DispositionActionMode mode)
            => mode == DispositionActionMode.ManualAction ? "manual-action" : "automatic-action";

        private static String SendingModeToken(DispositionSendingMode mode)
            => mode == DispositionSendingMode.SentManually ? "MDN-sent-manually" : "MDN-sent-automatically";

        /// <summary>
        /// The RFC 8098 "Disposition:" field value, e.g. "automatic-action/MDN-sent-automatically; displayed".
        /// </summary>
        public override String ToString()

            => $"{ActionModeToken(ActionMode)}/{SendingModeToken(SendingMode)}; {Type.ToString().ToLowerInvariant()}";

    }

}
