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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The transfer profile settings of the rendezvous service.
    /// </summary>
    public sealed class TransferProfilesOptions
    {

        /// <summary>
        /// The settings used for rendezvous without an explicit transfer profile.
        /// </summary>
        public TransferProfileSettings  Balanced       { get; set; } = TransferProfileSettings.Defaults(TransferProfile.Balanced);

        /// <summary>
        /// The settings used for low latency rendezvous, e.g. chat or SSH.
        /// </summary>
        public TransferProfileSettings  Interactive    { get; set; } = TransferProfileSettings.Defaults(TransferProfile.Interactive);

        /// <summary>
        /// The settings used for high throughput rendezvous, e.g. file transfers.
        /// </summary>
        public TransferProfileSettings  Bulk           { get; set; } = TransferProfileSettings.Defaults(TransferProfile.Bulk);


        /// <summary>
        /// Return the settings of the given transfer profile.
        /// </summary>
        /// <param name="Profile">A transfer profile.</param>
        public TransferProfileSettings this[TransferProfile Profile]

            => Profile switch {
                   TransferProfile.Interactive  => Interactive,
                   TransferProfile.Bulk         => Bulk,
                   _                            => Balanced
               };

    }

}
