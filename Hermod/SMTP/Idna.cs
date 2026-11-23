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

using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Internationalized domain names (RFC 5890) as SMTP needs them. RFC 6531 §3.2: "When doing
    /// lookups, the SMTPUTF8-aware SMTP client or server MUST either use a Unicode-aware DNS
    /// library, or transform the internationalized domain name to A-label form"; §3.7.1: EHLO,
    /// the greeting and the reply to EHLO have "only ASCII (LDH label or A-label [RFC5890])
    /// domains". A domain written in U-labels and the same domain in A-labels are one domain.
    /// </summary>
    public static class Idna
    {

        private static readonly IdnMapping idn = new () { AllowUnassigned = true, UseStd3AsciiRules = false };


        /// <summary>
        /// The domain in A-labels: "bücher.test" is "xn--bcher-kva.test". A name in ASCII - LDH
        /// labels, A-labels, an address literal - is returned as it is; so is one that is no valid
        /// internationalized name, to fail wherever a wrong name fails. A trailing dot is kept.
        /// </summary>
        /// <param name="Domain">A domain name, in U-labels, A-labels or both.</param>
        public static String ToAscii(String Domain)
        {

            if (String.IsNullOrEmpty(Domain) || IsAscii(Domain))
                return Domain;

            var root = Domain.EndsWith('.');

            try
            {
                return idn.GetAscii(root ? Domain[..^1] : Domain) + (root ? "." : "");
            }
            catch (ArgumentException)
            {
                return Domain;
            }

        }


        /// <summary>
        /// The domain in U-labels: "xn--bcher-kva.test" is "bücher.test". A name without A-labels,
        /// or one that is no valid internationalized name, is returned as it is.
        /// </summary>
        /// <param name="Domain">A domain name, in U-labels, A-labels or both.</param>
        public static String ToUnicode(String Domain)
        {

            if (String.IsNullOrEmpty(Domain) || !Domain.Contains("xn--", StringComparison.OrdinalIgnoreCase))
                return Domain;

            var root = Domain.EndsWith('.');

            try
            {
                return idn.GetUnicode(root ? Domain[..^1] : Domain) + (root ? "." : "");
            }
            catch (ArgumentException)
            {
                return Domain;
            }

        }


        /// <summary>
        /// Whether two domain names are the same domain, whichever labels either is written in
        /// (and in whatever case).
        /// </summary>
        public static Boolean SameDomain(String? A, String? B)

            => A is not null &&
               B is not null &&
               String.Equals(ToAscii(A).TrimEnd('.'), ToAscii(B).TrimEnd('.'), StringComparison.OrdinalIgnoreCase);


        private static Boolean IsAscii(String Text)
        {

            foreach (var c in Text)
                if (c > 0x7F)
                    return false;

            return true;

        }

    }

}
