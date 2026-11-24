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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// What a WebSocket server shows over QUIC is asked of its selector for
    /// every connection: made once for as long as the selector answers with
    /// the same certificate, made again once it answers with another, the one
    /// before kept for the connections that have it, and kept where the
    /// selector cannot answer.
    /// </summary>
    [TestFixture]
    public class QUICCertificatesTests
    {

        #region Data

        private readonly List<IDisposable>  toDispose  = [];

        private X509Certificate2             answer     = default!;
        private X509Certificate2Collection?  sentAlong;
        private Exception?                   failWith;

        private readonly List<Exception>     said       = [];

        [SetUp]
        public void AnswerWithOne()
        {
            answer     = ACertificate("first");
            sentAlong  = null;
            failWith   = null;
            said.Clear();
        }

        [TearDown]
        public void DisposeWhatATestMade()
        {

            foreach (var disposable in toDispose)
                disposable.Dispose();

            toDispose.Clear();

            // Made by ACertificate and so disposed above already: said again
            // for the analyzer, which does not follow it there.
            answer.Dispose();

        }

        #endregion

        #region (private) ACertificate(Name)

        private X509Certificate2 ACertificate(String Name)
        {

            var key          = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var certificate  = new CertificateRequest($"CN={Name}", key, HashAlgorithmName.SHA256).
                                   CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            toDispose.Add(key);
            toDispose.Add(certificate);

            return certificate;

        }

        #endregion

        #region (private) Certificates()

        private QUICCertificates Certificates()
        {

            var certificates = new QUICCertificates(
                                   nobody => failWith is not null
                                                 ? throw failWith
                                                 : (answer, sentAlong),
                                   RawPublicKeyMode.Off,
                                   said.Add
                               );

            toDispose.Add(certificates);

            return certificates;

        }

        #endregion


        #region TheSameAnswer_IsShownByWhatWasMadeForItBefore()

        [Test]
        public void TheSameAnswer_IsShownByWhatWasMadeForItBefore()
        {

            var certificates  = Certificates();

            var first         = certificates.ForTheNextConnection();
            var second        = certificates.ForTheNextConnection();

            Assert.Multiple(() => {
                Assert.That(second,                         Is.SameAs(first));
                Assert.That(first.Certificate.Thumbprint,   Is.EqualTo(answer.Thumbprint));
            });

        }

        #endregion

        #region AnotherAnswer_IsShownToTheNextConnection_AndWhatWasShownBeforeStillSigns()

        [Test]
        public void AnotherAnswer_IsShownToTheNextConnection_AndWhatWasShownBeforeStillSigns()
        {

            var certificates  = Certificates();
            var before        = certificates.ForTheNextConnection();

            answer            = ACertificate("second");

            var after         = certificates.ForTheNextConnection();

            Assert.Multiple(() => {
                Assert.That(after.Certificate.Thumbprint,          Is.EqualTo(answer.Thumbprint));
                Assert.That(after,                                 Is.Not.SameAs(before));
                Assert.That(() => before.SignCertificateVerify([ 1, 2, 3 ]), Throws.Nothing, "a connection that was shown the one before can no longer sign with it");
            });

        }

        #endregion

        #region OtherIntermediates_AreAnotherAnswer()

        [Test]
        public void OtherIntermediates_AreAnotherAnswer()
        {

            var certificates  = Certificates();
            var alone         = certificates.ForTheNextConnection();

            sentAlong         = [ ACertificate("intermediate") ];

            var withChain     = certificates.ForTheNextConnection();

            Assert.Multiple(() => {
                Assert.That(withChain,                    Is.Not.SameAs(alone));
                Assert.That(withChain.IntermediatesDer,   Has.Count.EqualTo(1));
                Assert.That(alone.    IntermediatesDer,   Is.Empty);
            });

        }

        #endregion

        #region ASelectorThatCannotAnswer_LeavesWhatWasShownBefore_AndThatIsSaid()

        [Test]
        public void ASelectorThatCannotAnswer_LeavesWhatWasShownBefore_AndThatIsSaid()
        {

            var certificates  = Certificates();
            var before        = certificates.ForTheNextConnection();

            failWith          = new InvalidOperationException("no certificate now");

            var after         = certificates.ForTheNextConnection();

            Assert.Multiple(() => {
                Assert.That(after,                Is.SameAs(before));
                Assert.That(said,                 Has.Count.EqualTo(1));
                Assert.That(said[0].Message,      Is.EqualTo("no certificate now"));
            });

        }

        #endregion

        #region ASelectorThatCannotAnswerAtAll_ShowsNothing()

        [Test]
        public void ASelectorThatCannotAnswerAtAll_ShowsNothing()
        {

            failWith = new InvalidOperationException("no certificate at all");

            var certificates = Certificates();

            Assert.Multiple(() => {
                Assert.That(() => certificates.ForTheNextConnection(), Throws.InvalidOperationException.With.Message.EqualTo("no certificate at all"));
                Assert.That(said, Is.Empty, "where nothing was shown before, the refusal is the caller's to say");
            });

        }

        #endregion

        #region OnceLetGoOf_NothingThatWasShownSignsAnyMore()

        [Test]
        public void OnceLetGoOf_NothingThatWasShownSignsAnyMore()
        {

            var certificates  = Certificates();
            var first         = certificates.ForTheNextConnection();

            answer            = ACertificate("second");

            var second        = certificates.ForTheNextConnection();

            certificates.Dispose();

            Assert.Multiple(() => {
                Assert.That(() => first. SignCertificateVerify([ 1, 2, 3 ]), Throws.InstanceOf<ObjectDisposedException>());
                Assert.That(() => second.SignCertificateVerify([ 1, 2, 3 ]), Throws.InstanceOf<ObjectDisposedException>());
                Assert.That(() => certificates.ForTheNextConnection(),       Throws.InstanceOf<ObjectDisposedException>());
            });

        }

        #endregion

    }

}
