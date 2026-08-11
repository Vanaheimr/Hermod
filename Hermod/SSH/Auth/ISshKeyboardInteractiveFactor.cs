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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>One keyboard-interactive prompt (RFC 4256): the text and whether the response should echo.</summary>
    public sealed record SshPrompt(String Text, Boolean Echo);


    /// <summary>
    /// A keyboard-interactive challenge as presented to the client: an optional name and instruction plus
    /// the individual prompts (RFC 4256 SSH_MSG_USERAUTH_INFO_REQUEST).
    /// </summary>
    public sealed record SshKeyboardInteractiveChallenge(String                    Name,
                                                         String                    Instruction,
                                                         IReadOnlyList<SshPrompt>  Prompts);


    /// <summary>
    /// A server-side keyboard-interactive factor (RFC 4256): it declares the prompts to send and validates
    /// the client's responses. A one-prompt, non-echoing factor is exactly how TOTP second-factor codes are
    /// collected.
    /// </summary>
    public interface ISshKeyboardInteractiveFactor
    {

        /// <summary>The challenge name (often empty).</summary>
        String  Name         { get; }

        /// <summary>The instruction shown above the prompts (often empty).</summary>
        String  Instruction  { get; }

        /// <summary>The prompts to present (usually a single "Verification code:" with echo off).</summary>
        IReadOnlyList<SshPrompt>  Prompts  { get; }

        /// <summary>Validate the responses in prompt order.</summary>
        ValueTask<Boolean> ValidateAsync(IReadOnlyList<String>  Responses,
                                         CancellationToken      CancellationToken = default);

    }

}
