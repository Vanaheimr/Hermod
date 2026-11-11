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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Whether a key-pinned endpoint offers raw public keys (RFC 7250).
    /// </summary>
    public enum RawPublicKeyMode
    {

        /// <summary>
        /// Never: the key comes in a certificate, which is checked by its key pin only.
        /// </summary>
        Off,

        /// <summary>
        /// Raw public keys first, certificates as well.
        /// </summary>
        Prefer,

        /// <summary>
        /// Raw public keys only.
        /// </summary>
        Require

    }


    /// <summary>
    /// The type of the credential a peer presented in its TLS Certificate message
    /// (RFC 7250, the IANA "TLS Certificate Types").
    /// </summary>
    public enum TLSCertificateType : Byte
    {

        /// <summary>
        /// An X.509 certificate.
        /// </summary>
        X509          = 0,

        /// <summary>
        /// A raw public key: a DER encoded SubjectPublicKeyInfo.
        /// </summary>
        RawPublicKey  = 2

    }


    /// <summary>
    /// Where the key pins of an endpoint come from.
    /// </summary>
    public enum KeyPinState
    {

        /// <summary>
        /// Configured: at commissioning, by a technician or by the operator.
        /// </summary>
        Configured,

        /// <summary>
        /// Learned on first use, and not yet confirmed.
        /// </summary>
        Learned,

        /// <summary>
        /// Learned on first use, and confirmed since.
        /// </summary>
        Confirmed

    }


    /// <summary>
    /// The trust of one key-pinned TLS endpoint: up to four key pins, where
    /// they come from, whether raw public keys are offered, and whether the
    /// first key shown is learned.
    /// </summary>
    /// <remarks>
    /// An endpoint is either CA-checked or key-pinned, never both, and never
    /// "whichever fits". An endpoint with a key pin policy checks the key of
    /// its peer against the pins and nothing else: no chain, no name, no
    /// validity period. A mismatch is always a refusal, and there is no way
    /// back to the CA path. An endpoint without one is CA-checked as before.
    /// </remarks>
    public sealed class KeyPinPolicy
    {

        #region Data

        /// <summary>
        /// The maximum number of key pins of an endpoint: the current key, the
        /// announced next one, and room for a transition.
        /// </summary>
        public const Int32 MaxPins = 4;

        #endregion

        #region Properties

        /// <summary>
        /// The key pins of the keys the peer may present.
        /// </summary>
        public IReadOnlyList<KeyPin>  Pins               { get; }

        /// <summary>
        /// Where the key pins come from.
        /// </summary>
        public KeyPinState            State              { get; }

        /// <summary>
        /// Whether raw public keys are offered (RFC 7250).
        /// </summary>
        public RawPublicKeyMode       RawPublicKeys      { get; }

        /// <summary>
        /// Whether an endpoint without key pins learns the key it is shown first.
        /// </summary>
        public Boolean                TrustOnFirstUse    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new key pin policy.
        /// </summary>
        /// <param name="Pins">The key pins of the keys the peer may present, at most four.</param>
        /// <param name="State">Where the key pins come from.</param>
        /// <param name="RawPublicKeys">Whether raw public keys are offered (RFC 7250).</param>
        /// <param name="TrustOnFirstUse">Whether an endpoint without key pins learns the key it is shown first.</param>
        public KeyPinPolicy(IEnumerable<KeyPin>  Pins,
                            KeyPinState          State             = KeyPinState.Configured,
                            RawPublicKeyMode     RawPublicKeys     = RawPublicKeyMode.Off,
                            Boolean              TrustOnFirstUse   = false)
        {

            var pins = Pins.Distinct().ToArray();

            if (pins.Length > MaxPins)
                throw new ArgumentException($"An endpoint has at most {MaxPins} key pins, not {pins.Length}!", nameof(Pins));

            if (pins.Length == 0 && !TrustOnFirstUse)
                throw new ArgumentException("A key-pinned endpoint needs a key pin or trust on first use. Without both it is CA-checked and has no key pin policy.", nameof(Pins));

            if (pins.Length == 0 && State != KeyPinState.Configured)
                throw new ArgumentException($"An endpoint without key pins has none that were {State.ToString().ToLowerInvariant()}!", nameof(State));

            if (State == KeyPinState.Learned && pins.Length != 1)
                throw new ArgumentException($"A key pin learned on first use is a single one, not {pins.Length}!", nameof(Pins));

            this.Pins             = pins;
            this.State            = State;
            this.RawPublicKeys    = RawPublicKeys;
            this.TrustOnFirstUse  = TrustOnFirstUse;

        }

        #endregion


        #region (static) PinnedTo      (params Pins)

        /// <summary>
        /// A policy for the given configured key pins, without raw public keys
        /// and without trust on first use.
        /// </summary>
        /// <param name="Pins">The key pins of the keys the peer may present, at most four.</param>
        public static KeyPinPolicy PinnedTo(params KeyPin[] Pins)

            => new (Pins);

        #endregion

        #region (static) LearnOnFirstUse(RawPublicKeys = Off)

        /// <summary>
        /// A policy without key pins that learns the key it is shown first.
        /// </summary>
        /// <param name="RawPublicKeys">Whether raw public keys are offered (RFC 7250).</param>
        public static KeyPinPolicy LearnOnFirstUse(RawPublicKeyMode RawPublicKeys = RawPublicKeyMode.Off)

            => new ([],
                    KeyPinState.Configured,
                    RawPublicKeys,
                    TrustOnFirstUse: true);

        #endregion


        #region Learned  (Pin)

        /// <summary>
        /// The policy after the given key pin was learned on first use.
        /// </summary>
        /// <param name="Pin">The key pin of the key the peer showed first.</param>
        public KeyPinPolicy Learned(KeyPin Pin)
        {

            if (Pins.Count > 0 || !TrustOnFirstUse)
                throw new InvalidOperationException("Only an endpoint with trust on first use and without key pins learns a key pin!");

            return new ([ Pin ],
                        KeyPinState.Learned,
                        RawPublicKeys,
                        TrustOnFirstUse);

        }

        #endregion

        #region Contains (Pin)

        /// <summary>
        /// Whether the given key pin is one of this endpoint's.
        /// </summary>
        /// <param name="Pin">A key pin.</param>
        public Boolean Contains(KeyPin Pin)

            => Pins.Contains(Pin);

        #endregion


        #region (override) ToString()

        public override String ToString()

            => String.Concat(
                   Pins.Count == 0
                       ? "no key pins"
                       : $"{Pins.Count} key pin{(Pins.Count == 1 ? "" : "s")} ({State.ToString().ToLowerInvariant()})",
                   TrustOnFirstUse
                       ? ", trust on first use"
                       : "",
                   RawPublicKeys != RawPublicKeyMode.Off
                       ? $", raw public keys: {RawPublicKeys}"
                       : ""
               );

        #endregion

    }

}
