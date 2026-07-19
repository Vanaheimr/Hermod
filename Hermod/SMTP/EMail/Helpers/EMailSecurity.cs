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
    /// The security level of an e-mail.
    /// </summary>
    public enum EMailSecurity
    {

        /// <summary>
        /// Do not use any security features.
        /// </summary>
        none,

        /// <summary>
        /// Choose wether to sign the e-mail automagically.
        /// Will not fail if signing is not possible.
        /// </summary>
        autosign,

        /// <summary>
        /// Signing the e-mail is mandatory.
        /// </summary>
        sign,

        /// <summary>
        /// Choose wether to sign and encrypt the e-mail automagically.
        /// Will not fail if signing and encryption is not possible.
        /// </summary>
        auto,

        /// <summary>
        /// Signing <i>and</i> encrypting the e-mail is mandatory.
        /// </summary>
        /// <remarks>
        /// There is deliberately no "encrypt-only" mode. While OpenPGP/PGP-MIME (RFC 3156) permits
        /// encrypting without signing, an encrypted-but-unsigned e-mail offers confidentiality but
        /// <i>no authenticity</i>: the recipient cannot tell who actually sent it, so anyone holding
        /// the recipient's public key could forge a message that decrypts cleanly in their name. It
        /// also opens the door to surreptitious-forwarding / message-tampering attacks that a signature
        /// over the plaintext prevents. Meaningful secure mail therefore wants <i>authenticated
        /// encryption</i> — confidentiality together with a verifiable sender — so <see cref="encrypt"/>
        /// always signs as well (see <c>Builder.EncodeBodyparts</c>, which uses
        /// <c>OpenPGP.EncryptSignAndZip</c>). If a genuine encrypt-only use case ever arises it must be
        /// added as its own explicit mode and code path, never as a silent side effect.
        /// </remarks>
        encrypt

    }

}
