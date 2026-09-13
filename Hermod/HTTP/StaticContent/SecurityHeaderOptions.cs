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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The security related header fields sent with static content.
    /// </summary>
    public sealed record SecurityHeaderOptions
    {

        #region Data

        /// <summary>
        /// The default Content-Security-Policy: everything from this origin
        /// only, no inline scripts or styles, images may be data: URLs.
        /// </summary>
        public const String DefaultContentSecurityPolicy
            = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
              "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

        /// <summary>
        /// Two years, including subdomains. Only ever send it over TLS.
        /// </summary>
        public const String DefaultStrictTransportSecurity
            = "max-age=63072000; includeSubDomains";

        #endregion

        #region Properties

        /// <summary>
        /// The Content-Security-Policy for HTML documents, or null to send none.
        /// </summary>
        public String?  ContentSecurityPolicy      { get; init; } = DefaultContentSecurityPolicy;

        /// <summary>
        /// The Referrer-Policy for HTML documents, or null to send none.
        /// </summary>
        public String?  ReferrerPolicy             { get; init; } = "strict-origin-when-cross-origin";

        /// <summary>
        /// The Permissions-Policy for HTML documents, or null to send none.
        /// </summary>
        public String?  PermissionsPolicy          { get; init; } = "camera=(), microphone=(), geolocation=()";

        /// <summary>
        /// The X-Frame-Options for HTML documents (frame-ancestors in the
        /// Content-Security-Policy is the modern equivalent), or null to send none.
        /// </summary>
        public String?  FrameOptions               { get; init; } = "DENY";

        /// <summary>
        /// The Strict-Transport-Security for every response, or null to send
        /// none. Set it only when the server actually speaks TLS.
        /// </summary>
        public String?  StrictTransportSecurity    { get; init; } = null;


        /// <summary>
        /// The default options: a strict same-origin policy without HSTS.
        /// </summary>
        public static SecurityHeaderOptions Default { get; } = new();

        #endregion

    }


    /// <summary>
    /// Extension methods for adding security related header fields.
    /// </summary>
    public static class SecurityHeaderExtensions
    {

        #region WithCommonSecurityHeaders(this Builder, Options = null)

        /// <summary>
        /// Header fields for every response: no MIME sniffing, and HSTS when configured.
        /// </summary>
        /// <param name="Builder">An HTTP response builder.</param>
        /// <param name="Options">The security header options.</param>
        public static HTTPResponse.Builder WithCommonSecurityHeaders(this HTTPResponse.Builder  Builder,
                                                                     SecurityHeaderOptions?     Options   = null)
        {

            Builder.SetHeaderField("X-Content-Type-Options", "nosniff");

            if (Options?.StrictTransportSecurity is { Length: > 0 } hsts)
                Builder.SetHeaderField("Strict-Transport-Security", hsts);

            return Builder;

        }

        #endregion

        #region WithDocumentSecurityHeaders(this Builder, Options)

        /// <summary>
        /// Header fields for HTML documents, on top of the common ones.
        /// </summary>
        /// <param name="Builder">An HTTP response builder.</param>
        /// <param name="Options">The security header options.</param>
        public static HTTPResponse.Builder WithDocumentSecurityHeaders(this HTTPResponse.Builder  Builder,
                                                                       SecurityHeaderOptions      Options)
        {

            Builder.WithCommonSecurityHeaders(Options);

            if (Options.ContentSecurityPolicy is { Length: > 0 } csp)
                Builder.SetHeaderField("Content-Security-Policy", csp);

            if (Options.ReferrerPolicy is { Length: > 0 } referrer)
                Builder.SetHeaderField("Referrer-Policy", referrer);

            if (Options.PermissionsPolicy is { Length: > 0 } permissions)
                Builder.SetHeaderField("Permissions-Policy", permissions);

            if (Options.FrameOptions is { Length: > 0 } frames)
                Builder.SetHeaderField("X-Frame-Options", frames);

            return Builder;

        }

        #endregion

    }

}
