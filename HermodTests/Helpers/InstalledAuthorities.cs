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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// Takes the certificate authorities of a test PKI out of the intermediate
    /// CA stores again, where Windows' TLS has put them.
    /// </summary>
    /// <remarks>
    /// On Windows, <see cref="System.Net.Security.SslStreamCertificateContext"/>
    /// checks whether the operating system can build the chain of a certificate
    /// it is given intermediates for. When it cannot, it adds the intermediates
    /// to the store "Intermediate Certification Authorities" so that SChannel
    /// can send them - of the local machine if it may write there, else of the
    /// current user - and, when the chain is still partial then, the last of
    /// them as well, root or not. Nothing ever takes them out again.
    ///
    /// A test PKI with an intermediate is exactly such a chain, and the tests
    /// give their CAs names of their own on every run, so every run left more
    /// CAs behind. On 2026-10-02 CurrentUser\CA held 3,970 certificates, 3,825
    /// of them from tests. Every load of that store costs about 0.14 ms of CPU
    /// per certificate in it, and it is loaded by every context that is built
    /// and by every chain build with custom trust: TLS handshakes took seconds,
    /// and SunSpecModbusTLSTests failed its 5 s handshake timeout.
    ///
    /// So a test that builds such a context, directly or through a server or
    /// client it starts, hands its CAs to <see cref="Remove"/> in its teardown.
    /// </remarks>
    public static class InstalledAuthorities
    {

        #region Remove(Authorities)

        /// <summary>
        /// Remove the given certificates from the intermediate CA stores of the
        /// current user and of the local machine, wherever they are found.
        /// Does nothing on other operating systems than Windows.
        /// </summary>
        /// <remarks>
        /// By thumbprint, so that nothing but these certificates is touched,
        /// whatever its name.
        ///
        /// The local machine's store first: a process that may write to it, as
        /// the elevated one on a CI runner does, is where .NET puts them. The
        /// current user's store lists the machine's certificates as well, but
        /// cannot take them out - "Access is denied" - so they have to be gone
        /// from there before the user's store is looked at. Where the machine's
        /// store may not be written, .NET could not have put anything there.
        ///
        /// A cleanup that is refused is said in the test's output and does not
        /// fail the test: what it leaves behind costs time, not correctness.
        /// </remarks>
        /// <param name="Authorities">The CA certificates of a test PKI. Nulls are skipped.</param>
        /// <returns>How many certificates were removed.</returns>
        public static Int32 Remove(params IEnumerable<X509Certificate2?> Authorities)
        {

            if (!OperatingSystem.IsWindows())
                return 0;

            var thumbprints = Authorities.
                                  OfType<X509Certificate2>().
                                  Select(authority => authority.Thumbprint).
                                  Distinct(StringComparer.OrdinalIgnoreCase).
                                  ToArray();

            if (thumbprints.Length == 0)
                return 0;

            return RemoveFrom(StoreLocation.LocalMachine, thumbprints) +
                   RemoveFrom(StoreLocation.CurrentUser,  thumbprints);

        }

        #endregion

        #region (private static) RemoveFrom(Location, Thumbprints)

        private static Int32 RemoveFrom(StoreLocation  Location,
                                        String[]       Thumbprints)
        {

            using var store = new X509Store(StoreName.CertificateAuthority, Location);

            try
            {
                store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
            }
            catch (CryptographicException e)
            {

                // The machine's store, unelevated, is the usual case and needs
                // no word: nothing can have been added there either.
                if (Location != StoreLocation.LocalMachine)
                    TestContext.Out.WriteLine($"Could not open {Location}\\CA to take test CAs out of it: {e.Message}");

                return 0;

            }

            // Every certificate of the store comes out as an object of its own,
            // thousands of them on a machine where the tests had piled up.
            var all     = store.Certificates;
            var removed = 0;

            try
            {
                foreach (var installed in all)
                {
                    if (Thumbprints.Contains(installed.Thumbprint, StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            store.Remove(installed);
                            removed++;
                        }
                        catch (CryptographicException e)
                        {
                            TestContext.Out.WriteLine($"Could not take '{installed.Subject}' out of {Location}\\CA: {e.Message}");
                        }
                    }
                }
            }
            finally
            {
                foreach (var certificate in all)
                    certificate.Dispose();
            }

            return removed;

        }

        #endregion

    }

}
