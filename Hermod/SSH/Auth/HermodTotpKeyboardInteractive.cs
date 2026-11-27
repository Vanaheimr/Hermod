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

using System.Text;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A keyboard-interactive factor (RFC 4256) that asks for the token of Hermod's own time-based one-time
    /// password (<see cref="TOTPGenerator"/>, the format TOTP.ts shares) - with its shared secret, validity
    /// time, length, alphabet and hash algorithm as a <see cref="TOTPConfig"/> says - and not bound to the
    /// session it is sent in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the variant a peer can take part in without knowing anything of the session: an OpenSSH
    /// <c>sshd</c> checks the same token through PAM (<c>pam_exec</c> and the SSHTOTPTool), and an OpenSSH
    /// <c>ssh</c> sends it from an <c>SSH_ASKPASS</c> program. What it gives up is the binding: a token read
    /// by a server that is not the one meant is good at the real one for as long as it is valid. Where both
    /// ends are ours, <see cref="SshSessionBoundTotp"/> (<c>totp@open.charging.cloud</c>) keeps it.
    /// </para>
    /// <para>
    /// No token is remembered: one that is valid can be used again within its time slot and the slots on
    /// either side of it. A short <see cref="TOTPConfig.ValidityTime"/> is what keeps that window small.
    /// </para>
    /// </remarks>
    public sealed class HermodTotpKeyboardInteractive : ISshKeyboardInteractiveFactor
    {

        #region Data

        /// <summary>
        /// The prompt a factor asks with unless it is given another.
        /// </summary>
        public const String DefaultPrompt = "One-time password: ";

        private readonly TOTPConfig    config;
        private readonly TimeProvider  timeProvider;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public String                    Name           { get; }

        /// <inheritdoc/>
        public String                    Instruction    { get; }

        /// <inheritdoc/>
        public IReadOnlyList<SshPrompt>  Prompts        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a keyboard-interactive factor for the token of the given configuration.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it. It must not ask for channel binding.</param>
        /// <param name="Prompt">The prompt the client is shown (echo off).</param>
        /// <param name="Name">The name of the challenge, often empty.</param>
        /// <param name="Instruction">The instruction shown above the prompt, often empty.</param>
        /// <param name="TimeProvider">The clock, or null for the system's.</param>
        public HermodTotpKeyboardInteractive(TOTPConfig     Config,
                                             String         Prompt         = DefaultPrompt,
                                             String         Name           = "",
                                             String         Instruction    = "",
                                             TimeProvider?  TimeProvider   = null)
        {

            ArgumentNullException.ThrowIfNull(Config);

            if (String.IsNullOrWhiteSpace(Config.SharedSecret))
                throw new ArgumentException("A one-time password needs a shared secret.", nameof(Config));

            if (Config.TLSChannelBinding == true)
                throw new ArgumentException("A token bound to its session cannot be asked for over keyboard-interactive: " +
                                            $"the client would need the session's exporter material - use {SshSessionBoundTotp.MethodName} for that.",
                                            nameof(Config));

            // What the generator refuses - a short secret, white space in it, a length or validity time out of
            // range - is refused here, where the server is set up, and not at the first user's sign-in.
            try
            {
                Generate(Config, DateTimeOffset.UnixEpoch);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"The one-time password configuration is not usable: {e.Message}", nameof(Config), e);
            }

            this.config        = Config;
            this.timeProvider  = TimeProvider ?? TimeProvider.System;
            this.Name          = Name;
            this.Instruction   = Instruction;
            this.Prompts       = [ new SshPrompt(Prompt, Echo: false) ];

        }

        #endregion


        #region Generate(Config, Timestamp = null)

        /// <summary>
        /// The token of the given configuration at the given time.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static String Generate(TOTPConfig       Config,
                                      DateTimeOffset?  Timestamp   = null)

            => TOTPGenerator.GenerateTOTP(
                   Config.SharedSecret,
                   Config.ValidityTime,
                   Config.Length ?? 12,
                   Config.Alphabet,
                   Timestamp,
                   null,
                   Config.HashAlgorithm
               ).Current;

        #endregion

        #region Verify  (Config, Token, Timestamp = null)

        /// <summary>
        /// Whether the given token is the one of the given configuration at the given time - or the one of
        /// the time slot before or after it, for two clocks that do not agree to the second. Compared in
        /// constant time.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="Token">The token as it was sent.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static Boolean Verify(TOTPConfig       Config,
                                     String?          Token,
                                     DateTimeOffset?  Timestamp   = null)
        {

            if (String.IsNullOrEmpty(Token))
                return false;

            var (previous, current, next, _, _) = TOTPGenerator.GenerateTOTPs(
                                                      Config.SharedSecret,
                                                      Config.ValidityTime,
                                                      Config.Length ?? 12,
                                                      Config.Alphabet,
                                                      Timestamp,
                                                      null,
                                                      Config.HashAlgorithm
                                                  );

            var sent    = Encoding.UTF8.GetBytes(Token);

            // All three compared, whichever matches: how long it takes says nothing about which.
            var matches = false;

            foreach (var expected in new[] { previous, current, next })
                matches |= CryptographicOperations.FixedTimeEquals(sent, Encoding.UTF8.GetBytes(expected));

            return matches;

        }

        #endregion

        #region Answer  (Config, TimeProvider = null)

        /// <summary>
        /// The client's side: an answer to a keyboard-interactive challenge (<see cref="Client.SSHClientOptions.KeyboardInteractive"/>)
        /// that gives the current token of the given configuration to every prompt of it.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="TimeProvider">The clock, or null for the system's.</param>
        public static Func<SshKeyboardInteractiveChallenge, CancellationToken, ValueTask<String[]>> Answer(TOTPConfig     Config,
                                                                                                         TimeProvider?  TimeProvider   = null)
        {

            ArgumentNullException.ThrowIfNull(Config);

            var clock = TimeProvider ?? TimeProvider.System;

            return (challenge, _) => {
                var token = Generate(Config, clock.GetUtcNow());
                return ValueTask.FromResult(challenge.Prompts.Select(_ => token).ToArray());
            };

        }

        #endregion


        #region ValidateAsync(Responses, CancellationToken = default)

        /// <inheritdoc/>
        public ValueTask<Boolean> ValidateAsync(IReadOnlyList<String>  Responses,
                                                CancellationToken      CancellationToken = default)

            => ValueTask.FromResult(Responses.Count == 1 &&
                                    Verify(config, Responses[0], timeProvider.GetUtcNow()));

        #endregion

    }

}
