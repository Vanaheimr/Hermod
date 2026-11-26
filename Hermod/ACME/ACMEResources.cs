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

using System.Net;
using System.Globalization;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// What a certificate is for: a DNS name, or an IP address (RFC 8738).
    /// </summary>
    /// <param name="Type">"dns" or "ip".</param>
    /// <param name="Value">The name - "*.example.org" for a wildcard - or the address.</param>
    public sealed record ACMEIdentifier(String Type,
                                        String Value)
    {

        /// <summary>
        /// A DNS name or, where the text is one, an IP address.
        /// </summary>
        public static ACMEIdentifier From(String NameOrAddress)

            => System.Net.IPAddress.TryParse(NameOrAddress, out var address)
                   ? new ("ip",  address.ToString())
                   : new ("dns", NameOrAddress.Trim().TrimEnd('.').ToLowerInvariant());

        internal static ACMEIdentifier Parse(JToken? JSON)

            => new (JSON?["type"]?.Value<String>()  ?? "dns",
                    JSON?["value"]?.Value<String>() ?? "");

        internal JObject ToJSON()

            => new (
                   new JProperty("type",   Type),
                   new JProperty("value",  Value)
               );

        /// <inheritdoc/>
        public override String ToString()
            => Value;

    }


    /// <summary>
    /// The directory of an ACME server (RFC 8555, section 7.1.1): where to ask
    /// for what.
    /// </summary>
    public sealed class ACMEDirectory
    {

        /// <summary>Where to get a fresh nonce.</summary>
        public Uri                          NewNonce                  { get; }

        /// <summary>Where to open an account, or find the one of a key.</summary>
        public Uri                          NewAccount                { get; }

        /// <summary>Where to order a certificate.</summary>
        public Uri                          NewOrder                  { get; }

        /// <summary>Where to revoke a certificate, if the server says.</summary>
        public Uri?                         RevokeCertificate         { get; }

        /// <summary>Where to ask when a certificate should be renewed (RFC 9773), if the server says.</summary>
        public Uri?                         RenewalInfo               { get; }

        /// <summary>The terms of service one agrees to by opening an account, if any.</summary>
        public Uri?                         TermsOfService            { get; }

        /// <summary>Whether an account can only be opened with an external account binding.</summary>
        public Boolean                      ExternalAccountRequired   { get; }

        /// <summary>The certificate profiles the server offers, by name, with their descriptions.</summary>
        public IReadOnlyDictionary<String, String>  Profiles          { get; }

        internal ACMEDirectory(JObject JSON, Uri DirectoryURL)
        {

            Uri  Required(String Name)  => URL(Name) ?? throw new ACMEException($"The ACME directory at {DirectoryURL} names no '{Name}'.");
            Uri? URL     (String Name)  => JSON[Name]?.Value<String>() is String text && Uri.TryCreate(text, UriKind.Absolute, out var url) ? url : null;

            var meta                      = JSON["meta"] as JObject;

            this.NewNonce                 = Required("newNonce");
            this.NewAccount               = Required("newAccount");
            this.NewOrder                 = Required("newOrder");
            this.RevokeCertificate        = URL("revokeCert");
            this.RenewalInfo              = URL("renewalInfo");
            this.TermsOfService           = meta?["termsOfService"]?.Value<String>() is String terms && Uri.TryCreate(terms, UriKind.Absolute, out var termsURL) ? termsURL : null;
            this.ExternalAccountRequired  = meta?["externalAccountRequired"]?.Value<Boolean>() ?? false;
            this.Profiles                 = (meta?["profiles"] as JObject)?.Properties().
                                                ToDictionary(property => property.Name,
                                                             property => property.Value.Type == JTokenType.String ? property.Value.Value<String>() ?? "" : property.Value.ToString()) ??
                                            new Dictionary<String, String>();

        }

    }


    /// <summary>
    /// An ACME account (RFC 8555, section 7.1.2).
    /// </summary>
    /// <param name="URL">Its URL: the "kid" of every request made for it.</param>
    /// <param name="Status">"valid", "deactivated" or "revoked".</param>
    /// <param name="Contacts">Its contact URLs, as "mailto:" ones.</param>
    /// <param name="Created">Whether it was opened by the request that returned it, rather than found.</param>
    public sealed record ACMEAccount(Uri                  URL,
                                     String               Status,
                                     IReadOnlyList<String> Contacts,
                                     Boolean              Created);


    /// <summary>
    /// An order of a certificate (RFC 8555, section 7.1.3).
    /// </summary>
    public sealed class ACMEOrder
    {

        /// <summary>Where the order is: what is asked to learn what became of it.</summary>
        public Uri                            URL              { get; }

        /// <summary>"pending", "ready", "processing", "valid" or "invalid".</summary>
        public String                         Status           { get; }

        /// <summary>Until when the order may be completed.</summary>
        public DateTimeOffset?                Expires          { get; }

        /// <summary>What the certificate is to be for.</summary>
        public IReadOnlyList<ACMEIdentifier>  Identifiers      { get; }

        /// <summary>The authorizations to be won before the order is ready.</summary>
        public IReadOnlyList<Uri>             Authorizations   { get; }

        /// <summary>Where to send the certificate signing request.</summary>
        public Uri                            Finalize         { get; }

        /// <summary>Where the certificate is, once it is issued.</summary>
        public Uri?                           Certificate      { get; }

        /// <summary>The certificate profile asked for, if any.</summary>
        public String?                        Profile          { get; }

        /// <summary>Why the order failed, if it did.</summary>
        public ACMEProblem?                   Error            { get; }

        internal ACMEOrder(JObject JSON, Uri URL)
        {

            this.URL             = URL;
            this.Status          = JSON["status"]?.Value<String>() ?? "invalid";
            this.Expires         = ACMEJSON.Timestamp(JSON["expires"]);
            this.Identifiers     = (JSON["identifiers"]    as JArray)?.Select(ACMEIdentifier.Parse).ToArray() ?? [];
            this.Authorizations  = (JSON["authorizations"] as JArray)?.Select(url => new Uri(url.Value<String>()!)).ToArray() ?? [];
            this.Finalize        = new Uri(JSON["finalize"]?.Value<String>() ?? throw new ACMEException($"The order at {URL} names no 'finalize' URL."));
            this.Certificate     = JSON["certificate"]?.Value<String>() is String certificate ? new Uri(certificate) : null;
            this.Profile         = JSON["profile"]?.Value<String>();
            this.Error           = ACMEProblem.Parse(JSON["error"] as JObject);

        }

    }


    /// <summary>
    /// An authorization (RFC 8555, section 7.1.4): the proof, still to be given
    /// or given already, that the account controls one identifier.
    /// </summary>
    public sealed class ACMEAuthorization
    {

        /// <summary>Where the authorization is.</summary>
        public Uri                           URL          { get; }

        /// <summary>"pending", "valid", "invalid", "deactivated", "expired" or "revoked".</summary>
        public String                        Status       { get; }

        /// <summary>The identifier - without "*." for a wildcard.</summary>
        public ACMEIdentifier                Identifier   { get; }

        /// <summary>Whether it is for a wildcard certificate, which only dns-01 can prove.</summary>
        public Boolean                       Wildcard     { get; }

        /// <summary>Until when it holds.</summary>
        public DateTimeOffset?               Expires      { get; }

        /// <summary>The ways it may be proven, one of which is enough.</summary>
        public IReadOnlyList<ACMEChallenge>  Challenges   { get; }

        internal ACMEAuthorization(JObject JSON, Uri URL)
        {

            this.URL         = URL;
            this.Status      = JSON["status"]?.Value<String>() ?? "invalid";
            this.Identifier  = ACMEIdentifier.Parse(JSON["identifier"]);
            this.Wildcard    = JSON["wildcard"]?.Value<Boolean>() ?? false;
            this.Expires     = ACMEJSON.Timestamp(JSON["expires"]);
            this.Challenges  = (JSON["challenges"] as JArray)?.OfType<JObject>().Select(challenge => new ACMEChallenge(challenge)).ToArray() ?? [];

        }

    }


    /// <summary>
    /// One way to prove control of an identifier (RFC 8555, section 8).
    /// </summary>
    public sealed class ACMEChallenge
    {

        /// <summary>"http-01", "dns-01", "tls-alpn-01", ...</summary>
        public String        Type        { get; }

        /// <summary>Where to say that the answer is in place.</summary>
        public Uri           URL         { get; }

        /// <summary>"pending", "processing", "valid" or "invalid".</summary>
        public String        Status      { get; }

        /// <summary>The token that goes into the answer.</summary>
        public String        Token       { get; }

        /// <summary>What went wrong, if it did.</summary>
        public ACMEProblem?  Error       { get; }

        internal ACMEChallenge(JObject JSON)
        {
            this.Type    = JSON["type"]?.Value<String>()   ?? "";
            this.URL     = new Uri(JSON["url"]?.Value<String>() ?? throw new ACMEException("A challenge names no URL."));
            this.Status  = JSON["status"]?.Value<String>() ?? "pending";
            this.Token   = JSON["token"]?.Value<String>()  ?? "";
            this.Error   = ACMEProblem.Parse(JSON["error"] as JObject);
        }

    }


    /// <summary>
    /// When the certificate authority would have a certificate renewed
    /// (ACME Renewal Information, RFC 9773).
    /// </summary>
    /// <param name="Start">The start of the suggested window.</param>
    /// <param name="End">Its end.</param>
    /// <param name="ExplanationURL">Why, where the window was moved for a reason - a revocation coming, say.</param>
    /// <param name="RetryAfter">When to ask again.</param>
    public sealed record ACMERenewalInfo(DateTimeOffset   Start,
                                         DateTimeOffset   End,
                                         Uri?             ExplanationURL,
                                         TimeSpan?        RetryAfter);


    internal static class ACMEJSON
    {

        public static DateTimeOffset? Timestamp(JToken? JSON)

            => JSON?.Type switch {
                   JTokenType.Date    => JSON.Value<DateTime>() is var date ? new DateTimeOffset(date.ToUniversalTime()) : null,
                   JTokenType.String  => DateTimeOffset.TryParse(JSON.Value<String>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) ? timestamp : null,
                   _                  => null
               };

    }

}
