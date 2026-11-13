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

using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// What became of the key a peer presented.
    /// </summary>
    public enum KeyPinOutcome
    {

        /// <summary>
        /// Its key pin is one of the endpoint's.
        /// </summary>
        Accepted,

        /// <summary>
        /// The endpoint had no key pins and learns this one on first use.
        /// </summary>
        Learned,

        /// <summary>
        /// Refused: another key, no key, a malformed key or the wrong type.
        /// </summary>
        Refused

    }


    /// <summary>
    /// The result of checking the key a peer presented against a key pin policy.
    /// </summary>
    /// <param name="Outcome">Accepted, learned or refused.</param>
    /// <param name="Pin">The key pin of the presented key, unless there was none or it was malformed.</param>
    /// <param name="CertificateType">Whether the key came in a certificate or raw.</param>
    /// <param name="Reason">Why the key was refused.</param>
    public sealed record KeyPinValidation(KeyPinOutcome       Outcome,
                                          KeyPin?             Pin,
                                          TLSCertificateType  CertificateType,
                                          String?             Reason)
    {

        /// <summary>
        /// Whether the connection may go on: the key was accepted or learned.
        /// </summary>
        public Boolean IsAccepted
            => Outcome != KeyPinOutcome.Refused;

        public override String ToString()

            => Outcome switch {
                   KeyPinOutcome.Accepted  => $"Accepted key pin {Pin} ({CertificateType})",
                   KeyPinOutcome.Learned   => $"Learned key pin {Pin} on first use ({CertificateType})",
                   _                       => $"Refused: {Reason}"
               };

    }


    /// <summary>
    /// The one place that compares key pins, for every TLS stack: given the key
    /// a peer presented and the type it came in, it answers accepted, learned
    /// or refused.
    /// </summary>
    /// <remarks>
    /// Chain, name, validity period and revocation of a certificate are not
    /// looked at: a key-pinned endpoint checks the key, and only the key. The
    /// TLS stack still verifies CertificateVerify against that key, which is
    /// what binds the key to the handshake.
    ///
    /// The handlers made here are the only place where the policy errors of
    /// SslStream are ignored, and only for an endpoint with a key pin policy:
    /// there is no flag that switches the CA check off without one.
    /// </remarks>
    public static class KeyPinValidator
    {

        #region Validate(Policy, Certificate)

        /// <summary>
        /// Check the key in the given certificate against the given policy.
        /// </summary>
        /// <param name="Policy">The key pin policy of the endpoint.</param>
        /// <param name="Certificate">The certificate the peer presented, if any.</param>
        public static KeyPinValidation Validate(KeyPinPolicy       Policy,
                                                X509Certificate2?  Certificate)
        {

            if (Certificate is null)
                return new (KeyPinOutcome.Refused, null, TLSCertificateType.X509, "The peer presented no certificate.");

            return Validate(Policy,
                            Certificate.PublicKey.ExportSubjectPublicKeyInfo(),
                            TLSCertificateType.X509);

        }

        #endregion

        #region Validate(Policy, SubjectPublicKeyInfo, CertificateType)

        /// <summary>
        /// Check the given key against the given policy.
        /// </summary>
        /// <param name="Policy">The key pin policy of the endpoint.</param>
        /// <param name="SubjectPublicKeyInfo">The DER encoded SubjectPublicKeyInfo of the presented key.</param>
        /// <param name="CertificateType">Whether the key came in a certificate or raw.</param>
        public static KeyPinValidation Validate(KeyPinPolicy        Policy,
                                                ReadOnlySpan<Byte>  SubjectPublicKeyInfo,
                                                TLSCertificateType  CertificateType)
        {

            if (CertificateType == TLSCertificateType.RawPublicKey &&
                Policy.RawPublicKeys == RawPublicKeyMode.Off)
                return new (KeyPinOutcome.Refused, null, CertificateType, "The peer presented a raw public key, which this endpoint does not offer.");

            if (CertificateType == TLSCertificateType.X509 &&
                Policy.RawPublicKeys == RawPublicKeyMode.Require)
                return new (KeyPinOutcome.Refused, null, CertificateType, "The peer presented a certificate, and this endpoint requires a raw public key.");

            if (SubjectPublicKeyInfo.IsEmpty)
                return new (KeyPinOutcome.Refused, null, CertificateType, "The peer presented no key.");

            if (!SubjectPublicKeyInfoCheck.IsValid(SubjectPublicKeyInfo, CertificateType, out var reason))
                return new (KeyPinOutcome.Refused, null, CertificateType, reason);

            var pin = KeyPin.FromSubjectPublicKeyInfo(SubjectPublicKeyInfo);

            if (Policy.Pins.Count == 0)
                return Policy.TrustOnFirstUse
                           ? new (KeyPinOutcome.Learned, pin, CertificateType, null)
                           : new (KeyPinOutcome.Refused, pin, CertificateType, "This endpoint has no key pins.");

            if (Policy.Contains(pin))
                return new (KeyPinOutcome.Accepted, pin, CertificateType, null);

            return new (KeyPinOutcome.Refused,
                        pin,
                        CertificateType,
                        $"The key pin {pin} of the peer's key is not among the {Policy.Pins.Count} key pin{(Policy.Pins.Count == 1 ? "" : "s")} of this endpoint.");

        }

        #endregion


        #region ForServerCertificates<T>(Policy, OnValidated = null)

        /// <summary>
        /// A validator of server certificates, for Hermod's TLS clients
        /// (HTTP, WebSocket, ...), that checks the key pin and nothing else.
        /// </summary>
        /// <typeparam name="T">The type of the TLS client.</typeparam>
        /// <param name="Policy">The key pin policy of the endpoint.</param>
        /// <param name="OnValidated">Told every result, e.g. to keep a learned key pin. An exception thrown there ends the handshake.</param>
        public static RemoteTLSServerCertificateValidationHandler<T> ForServerCertificates<T>(KeyPinPolicy                Policy,
                                                                                               Action<KeyPinValidation>?  OnValidated   = null)
            where T : class

            => ForServerCertificates<T>(() => Policy, OnValidated);


        /// <summary>
        /// A validator of server certificates, for Hermod's TLS clients
        /// (HTTP, WebSocket, ...), that checks the key pin and nothing else.
        /// </summary>
        /// <typeparam name="T">The type of the TLS client.</typeparam>
        /// <param name="Policy">The key pin policy of the endpoint, asked for every handshake, so that a learned or changed key pin applies from the next connection on.</param>
        /// <param name="OnValidated">Told every result, e.g. to keep a learned key pin. An exception thrown there ends the handshake.</param>
        public static RemoteTLSServerCertificateValidationHandler<T> ForServerCertificates<T>(Func<KeyPinPolicy>         Policy,
                                                                                               Action<KeyPinValidation>?  OnValidated   = null)
            where T : class

            => WithKeyPins<T>(

                   (sender, certificate, certificateChain, tlsClient, policyErrors)

                       => ToTLSValidationResult(
                              Validate(Policy(), certificate),
                              OnValidated
                          ),

                   Policy,
                   OnValidated

               );

        #endregion

        #region WithKeyPins<T>(Validator, Policy, OnValidated = null) / TryGetKeyPins(Validator, ...)

        // What stands behind a validator, kept beside it and no longer than it lives.
        private sealed record KeyPinsBehind(Func<KeyPinPolicy?> Policy, Action<KeyPinValidation>? OnValidated);

        private static readonly ConditionalWeakTable<Delegate, KeyPinsBehind> keyPinsBehind = new();

        /// <summary>
        /// Say which key pins stand behind a validator of server certificates.
        /// </summary>
        /// <remarks>
        /// A validator is asked about a certificate, and a raw public key
        /// (RFC 7250) has none. A TLS client that is handed a validator
        /// which says its key pins can do what the validator would have
        /// done with a certificate: where the key pins offer raw public
        /// keys, it offers them, checks the key its server shows against the
        /// key pins, and tells the result - see ATLSClient.ServerKeyPins.
        /// Where they offer none, the validator is asked, as before.
        ///
        /// The validators ForServerCertificates makes say their key pins
        /// themselves. This is for a validator that does more around them.
        /// The validator has to reach the client as it is: one wrapped into
        /// another delegate on the way says nothing any more.
        /// </remarks>
        /// <typeparam name="T">The type of the TLS client.</typeparam>
        /// <param name="Validator">A validator of server certificates that checks the key pins of the given policy.</param>
        /// <param name="Policy">The key pin policy of the endpoint, asked for every handshake; null where it is no longer key-pinned.</param>
        /// <param name="OnValidated">Told every result of a check the client made itself.</param>
        /// <returns>The validator given.</returns>
        public static RemoteTLSServerCertificateValidationHandler<T> WithKeyPins<T>(RemoteTLSServerCertificateValidationHandler<T>  Validator,
                                                                                    Func<KeyPinPolicy?>                             Policy,
                                                                                    Action<KeyPinValidation>?                       OnValidated   = null)
            where T : class
        {

            keyPinsBehind.AddOrUpdate(Validator, new KeyPinsBehind(Policy, OnValidated));

            return Validator;

        }


        /// <summary>
        /// The key pins that stand behind a validator of server certificates,
        /// where it says them.
        /// </summary>
        /// <param name="Validator">A validator of server certificates.</param>
        /// <param name="Policy">The key pin policy of the endpoint, to be asked for every handshake.</param>
        /// <param name="OnValidated">To be told every result of a check.</param>
        public static Boolean TryGetKeyPins(Delegate?                       Validator,
                                            out Func<KeyPinPolicy?>?        Policy,
                                            out Action<KeyPinValidation>?   OnValidated)
        {

            if (Validator is not null &&
                keyPinsBehind.TryGetValue(Validator, out var behind))
            {
                Policy       = behind.Policy;
                OnValidated  = behind.OnValidated;
                return true;
            }

            Policy       = null;
            OnValidated  = null;
            return false;

        }

        #endregion

        #region ForClientCertificates<T>(Policy, OnValidated = null)

        /// <summary>
        /// A validator of client certificates, for Hermod's TLS servers, that
        /// checks the key pin and nothing else.
        /// </summary>
        /// <remarks>
        /// Which identity a key stands for is not decided here: a certificate
        /// accepted by its key pin contributes its key pin, never its subject.
        /// </remarks>
        /// <typeparam name="T">The type of the TLS server.</typeparam>
        /// <param name="Policy">The key pins of the keys clients may present.</param>
        /// <param name="OnValidated">Told every result. An exception thrown there ends the handshake.</param>
        public static RemoteTLSClientCertificateValidationHandler<T> ForClientCertificates<T>(KeyPinPolicy                Policy,
                                                                                               Action<KeyPinValidation>?  OnValidated   = null)
            where T : class

            => ForClientCertificates<T>(() => Policy, OnValidated);


        /// <summary>
        /// A validator of client certificates, for Hermod's TLS servers, that
        /// checks the key pin and nothing else.
        /// </summary>
        /// <remarks>
        /// Which identity a key stands for is not decided here: a certificate
        /// accepted by its key pin contributes its key pin, never its subject.
        /// </remarks>
        /// <typeparam name="T">The type of the TLS server.</typeparam>
        /// <param name="Policy">The key pins of the keys clients may present, asked for every handshake.</param>
        /// <param name="OnValidated">Told every result. An exception thrown there ends the handshake.</param>
        public static RemoteTLSClientCertificateValidationHandler<T> ForClientCertificates<T>(Func<KeyPinPolicy>         Policy,
                                                                                               Action<KeyPinValidation>?  OnValidated   = null)
            where T : class

            => (sender, certificate, certificateChain, tlsServer, policyErrors)

                   => ToTLSValidationResult(
                          Validate(Policy(), certificate),
                          OnValidated
                      );

        #endregion

        #region ForSslStream            (Policy, OnValidated = null)

        /// <summary>
        /// A certificate validation callback for SslStream, in either role
        /// (e.g. HTTP2Client and HTTP2Server), that checks the key pin and
        /// nothing else.
        /// </summary>
        /// <param name="Policy">The key pin policy of the endpoint.</param>
        /// <param name="OnValidated">Told every result. An exception thrown there ends the handshake.</param>
        public static RemoteCertificateValidationCallback ForSslStream(KeyPinPolicy                Policy,
                                                                       Action<KeyPinValidation>?  OnValidated   = null)

            => ForSslStream(() => Policy, OnValidated);


        /// <summary>
        /// A certificate validation callback for SslStream, in either role
        /// (e.g. HTTP2Client and HTTP2Server), that checks the key pin and
        /// nothing else.
        /// </summary>
        /// <param name="Policy">The key pin policy of the endpoint, asked for every handshake.</param>
        /// <param name="OnValidated">Told every result. An exception thrown there ends the handshake.</param>
        public static RemoteCertificateValidationCallback ForSslStream(Func<KeyPinPolicy>         Policy,
                                                                       Action<KeyPinValidation>?  OnValidated   = null)

            => (sender, certificate, certificateChain, policyErrors) => {

                   using var loaded = certificate is not null and not X509Certificate2
                                          ? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData())
                                          : null;

                   var validation = Validate(
                                        Policy(),
                                        loaded ?? certificate as X509Certificate2
                                    );

                   OnValidated?.Invoke(validation);

                   return validation.IsAccepted;

               };

        #endregion


        #region (private) ToTLSValidationResult(Validation, OnValidated)

        private static TLSValidationResult ToTLSValidationResult(KeyPinValidation           Validation,
                                                                 Action<KeyPinValidation>?  OnValidated)
        {

            OnValidated?.Invoke(Validation);

            return Validation.Outcome switch {
                       KeyPinOutcome.Accepted  => TLSValidationResult.Success(),
                       KeyPinOutcome.Learned   => TLSValidationResult.Success($"Key pin {Validation.Pin} learned on first use."),
                       _                       => TLSValidationResult.Failed(Validation.Reason ?? "The key was refused.")
                   };

        }

        #endregion

    }

}
