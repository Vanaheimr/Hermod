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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Private keys as other tools write them: one Ed25519 key that <c>ssh-keygen -p -Z</c> (OpenSSH 10.2)
    /// encrypted with every cipher it offers, passphrase <c>hermod</c>, and an Ed25519 key in PKCS#8 as
    /// <c>openssl genpkey</c> (OpenSSL 3.5) wrote it, plain and encrypted with the same passphrase. The
    /// fixtures are the tools' own output, so reading them back right is reading the tools right.
    /// </summary>
    [TestFixture]
    public class PrivateKeyImportTests
    {

        #region Data

        private const String Passphrase = "hermod";

        /// <summary>
        /// The public half of the openssh-key-v1 fixtures, from <c>ssh-keygen</c>'s <c>.pub</c>.
        /// </summary>
        private const String OpenSshPublicKey = "AAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqDXTpGqJA6e1YGuguub3vNQqAvHlHI";

        /// <summary>
        /// The raw public key of the PKCS#8 fixtures, from <c>openssl pkey -pubout</c>.
        /// </summary>
        private const String Pkcs8PublicKey = "Fy/9EYd5K82EOZct3hEgclJKgYPlNrmqacDRRl2He90=";

        private static readonly Dictionary<String, String> EncryptedByCipher = new () {

            ["aes128-ctr"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczEyOC1jdHIAAAAGYmNyeXB0AAAAGAAAABCaLStr0x
                      p+CwNPQ33A09/YAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoILRWM3q2c3uCbvLqIwlAT1q/kRHW0o/wrBa87
                      cYjgOKW3t18aaPxD7Y5f9kIdDA3znNxFRGXN3/SsXe+ZxL2b/oJ6N+1cno0UJS6i8x/DR5
                      TBosShc2SxkUWjU223cQDRlMelBcChU3WljgDcj0a7Nf6/Mz48YSPukI3C54O/FndGfuKJ
                      4vWTfh0pmgqpz8pKt6bzUoi5ulXzbncu/IbRM=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes192-ctr"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczE5Mi1jdHIAAAAGYmNyeXB0AAAAGAAAABAoLLrRhp
                      +sCVpbE5DZr1ItAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoKJtcEAj9VzhBPmYPz6oWIawdp0TMB/bNt5fVO
                      d/dS8BNM0QfhsbjUtBpuboW4Da2oOExHEezrwucyVwUq1E1YTSYQgkZb23lYeAe3b67G0L
                      snBrYMySp4LvCAUCFvcqdKRJ+g/6SNfKrfPYAV9Hmco83dEnpIKiXKlKdzhRauLX4AemWb
                      XtJNg7NK6JgSSm6pkT2qNVhg5BvAfBTCv7hZU=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes256-ctr"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABBwVm9Buj
                      rPG6EbjVrRXw6bAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoAtP+/9IKGs55XMRex9K73jFm+I9YFffhmxsF1
                      mquR9MnKrEB4/Q2FZBwDvADVhNbgz4HdqooGPdTP58OevFSlszyX177fCtY9V2qExUb/jA
                      sQON+RKUE9TivGzV04MR8+QSTmc40NgIdMVv0JQ2F6UAjU38Gvbv5ycS5NUsrNVncv13Zw
                      /OrO1PeiNn3LBLoeUMQZg3Jakjejbm2E9pfYI=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes128-cbc"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczEyOC1jYmMAAAAGYmNyeXB0AAAAGAAAABD2xpcBwo
                      mgrFdi7dd4YjmrAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoP6uITZlkru2c4pGqKyd8ds4XF8M0CH1Nw+XKM
                      O6EfOPeDC89GfB28/poRUmuGDiz+974FNhj5x6jgk+ONJfi8T3uj/YQvZCrCTBHUah4dBK
                      1tiusm9TW/4w0n5z+cvxbp8Wmgj/ryIM41S2mFXI69/XLe8VOib0s+4iuiyk2gcxekwZ0C
                      BFME76OGTzwnBZH7bEwEa9PnW/SoWZhW8B5W8=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes192-cbc"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczE5Mi1jYmMAAAAGYmNyeXB0AAAAGAAAABCw/+JkHW
                      m2MweVc+HcH7kdAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoDBZ5N1WEuDV8cKLoPz24Mav2YGhrPbML7wnba
                      jhwURjgerN3vTr9tseftKMLNXw/QCbYKMNRfPUD7VO94nGi5dqegsH2mQ3Pri997DFdc+s
                      isibpR8v8GIr366LQAC7vh+M26XAtoa01KDup0Xe1//BO9KMFLjBil9m1n1t3UMyXlKS15
                      uJszZMoqYguc1zPHSIo3lbk7ljq0Pbog9BvIE=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes256-cbc"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jYmMAAAAGYmNyeXB0AAAAGAAAABDWrSDIGz
                      cOQXpTu7Xl4cp6AAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIJ1xbHyoMxMl6FqD
                      XTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoLUIS+pwFY5iodJFa5AonD2hSxtTsoIt8KKlkp
                      w18CT9qQZu0EE38JlFAhgHLBYCLvcWtqumiFDUSg6812ImqURNXI4pzBf7chgBrit0OyBC
                      8pnA0rVfTiZFR4n++h76des75+OkHqSfTtYikx39I0+fGDgHXdn363x3h4tfTSZyTamWzv
                      dRs12YHnKK28WKs0pHmGOCbuOULRkzgoCrFV8=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes128-gcm@openssh.com"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAAFmFlczEyOC1nY21Ab3BlbnNzaC5jb20AAAAGYmNyeXB0AA
                      AAGAAAABCubFvvtU6Eu8+UE6fZ9zFKAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAA
                      IJ1xbHyoMxMl6FqDXTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoMPHSpsQ5QiuqHJBRhN0OI
                      iV6MkKGc7+WYopWjG1UoDVIIm9I87friHXRXKbAx4PgjyKeilOIDndVM7dCzCz5ozS7qyW
                      t5k6vc9nQbwPxBwHxRcf7VseLIvhk06olQtN9JLaxGPkTiS/z+sh3+6T8sLgIRIWgvr6HA
                      +GoFGEpA2lB1nDVu90KsIgFtGTL9P9/cgC2hGO3W1jtKkl/f0EycAEdRBlYoip4TnZeV3E
                      mKxj
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["aes256-gcm@openssh.com"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAAFmFlczI1Ni1nY21Ab3BlbnNzaC5jb20AAAAGYmNyeXB0AA
                      AAGAAAABAPfSXc/gccxzMLH8r+BhbxAAAABAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAA
                      IJ1xbHyoMxMl6FqDXTpGqJA6e1YGuguub3vNQqAvHlHIAAAAoINCTeADV+HzpxNMzs/YSV
                      qQQZtOVvV3PCiOH7EynWgNo0iQdYwaMigrV1R+7pQG10i7G2/nSJFTbnw7tvLKbM92BwJ0
                      4Cu/iqK+fFwr1aGg7RXiWH6LDnRRLqR6Iq28XfOSEQwh4sWpawTEvhYXizamzDLmMZvadK
                      8oNhZ0YGdPfhIYdSz3X35IgF6Mpk1avXhDF7WkPD5S8G/+FLPNEoJCewStAPDmDAM74pPd
                      FUID
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["chacha20-poly1305@openssh.com"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAAHWNoYWNoYTIwLXBvbHkxMzA1QG9wZW5zc2guY29tAAAABm
                      JjcnlwdAAAABgAAAAQqxWJ+RhxK1vtaBom0089TwAAAAQAAAABAAAAMwAAAAtzc2gtZWQy
                      NTUxOQAAACCdcWx8qDMTJehag106RqiQOntWBroLrm97zUKgLx5RyAAAAJho4WuyZzxwAQ
                      ecMMO3JZ48ZPLzNT/rtOXAGpA7FxTu35HL0s2T1oio7X5/xxOv9ACjnqbyIMKAfKwvxAgE
                      Rvtp74ON8rK4LfYUfcVlphYT2mTomTcihlO/m5gFczuNCRLc42W5JUx6pjYElCzF14sq1D
                      ZY3Qikj8B05uV6shitp5HStitHKVFY9bsAw/d7S4RYriHWibbU597Y/bSmsTRbD4hdbG2c
                      tY4=
                      -----END OPENSSH PRIVATE KEY-----
                      """,

            ["3des-cbc"] = """
                      -----BEGIN OPENSSH PRIVATE KEY-----
                      b3BlbnNzaC1rZXktdjEAAAAACDNkZXMtY2JjAAAABmJjcnlwdAAAABgAAAAQnR1l0JKeNo
                      6SNy6bJm3wFQAAAAQAAAABAAAAMwAAAAtzc2gtZWQyNTUxOQAAACCdcWx8qDMTJehag106
                      RqiQOntWBroLrm97zUKgLx5RyAAAAJjJEjlli5/p904DQcg5UoxAUOcUblRufQT3zgzjev
                      Txti334u7N4Q2cHQTIuEs/Ad1OdaqQ16wJbgPJp4/YpkMI9bnw5utoifiy69LBKXi1biuc
                      GvIkaDdZ6bymhu8cDA2qrAH91JCLFxg7JgsdgSC91Lbc2tCnWtdWIE22zPggJN15FJziG3
                      8gxV9/GJ+SWBAv1BNZY+62DQ==
                      -----END OPENSSH PRIVATE KEY-----
                      """

        };

        private const String Pkcs8Plain = """
                                          -----BEGIN PRIVATE KEY-----
                                          MC4CAQAwBQYDK2VwBCIEIGZ+fqocBmFxpOATNiM6cHQA5dU0OjhDS3WyWGmBKHvY
                                          -----END PRIVATE KEY-----
                                          """;

        private const String Pkcs8Encrypted = """
                                              -----BEGIN ENCRYPTED PRIVATE KEY-----
                                              MIGjMF8GCSqGSIb3DQEFDTBSMDEGCSqGSIb3DQEFDDAkBBCDd2qwEgLM0pCUD2qx
                                              dXtcAgIIADAMBggqhkiG9w0CCQUAMB0GCWCGSAFlAwQBKgQQ4XqFiRdjDMQ9TWhr
                                              oTcBEwRANV425eihQixm3lxg/MK1A5yLQVgDcQPBEP6D85cj0SOzKpqHwceIG21h
                                              s/DSnlnGuU9c6UJ73qYhtTOjlxZAkg==
                                              -----END ENCRYPTED PRIVATE KEY-----
                                              """;

        #endregion


        #region AnEncryptedOpenSshKey_IsRead

        [TestCase("aes128-ctr")]
        [TestCase("aes192-ctr")]
        [TestCase("aes256-ctr")]
        [TestCase("aes128-cbc")]
        [TestCase("aes192-cbc")]
        [TestCase("aes256-cbc")]
        [TestCase("aes128-gcm@openssh.com")]
        [TestCase("aes256-gcm@openssh.com")]
        [TestCase("chacha20-poly1305@openssh.com")]
        public void AnEncryptedOpenSshKey_IsRead(String Cipher)
        {

            var loaded = SshKeyGenerator.LoadPrivateKey(EncryptedByCipher[Cipher], Passphrase);

            Assert.Multiple(() => {
                Assert.That(Convert.ToBase64String(loaded.Key.PublicKeyBlob), Is.EqualTo(OpenSshPublicKey), "the key ssh-keygen encrypted");
                Assert.That(loaded.Comment,                                   Is.EqualTo("hermod-fixture"));
            });

        }

        #endregion

        #region AWrongPassphrase_IsAnAuthenticationError

        /// <summary>
        /// A wrong passphrase is told apart from a broken file: by the tag of an AEAD cipher, and by the
        /// check integers of the others.
        /// </summary>
        [TestCase("aes128-ctr")]
        [TestCase("aes256-cbc")]
        [TestCase("aes256-gcm@openssh.com")]
        [TestCase("chacha20-poly1305@openssh.com")]
        public void AWrongPassphrase_IsAnAuthenticationError(String Cipher)
        {

            Assert.Throws<SshAuthenticationException>(() => SshKeyGenerator.LoadPrivateKey(EncryptedByCipher[Cipher], "not-hermod"));
            Assert.Throws<SshAuthenticationException>(() => SshKeyGenerator.LoadPrivateKey(EncryptedByCipher[Cipher]), "and no passphrase at all");

        }

        #endregion

        #region ThreeDes_IsRefused_WithAWayOut

        [Test]
        public void ThreeDes_IsRefused_WithAWayOut()
        {

            var refused = Assert.Throws<SshWireException>(() => SshKeyGenerator.LoadPrivateKey(EncryptedByCipher["3des-cbc"], Passphrase));

            Assert.That(refused!.Message, Does.Contain("3des-cbc").And.Contain("ssh-keygen -p -Z"));

        }

        #endregion

        #region AnEd25519Pkcs8Key_IsRead

        [TestCase(false)]
        [TestCase(true)]
        public void AnEd25519Pkcs8Key_IsRead(Boolean Encrypted)
        {

            var loaded = SshKeyGenerator.LoadPrivateKey(Encrypted ? Pkcs8Encrypted : Pkcs8Plain, Encrypted ? Passphrase : null);

            Assert.Multiple(() => {
                Assert.That(loaded.Key.AlgorithmNames[0], Is.EqualTo(SshAlgorithmNames.HostKey.Ed25519));
                Assert.That(loaded.Key.PublicKeyBlob,     Is.EqualTo(SshEd25519.EncodePublicKeyBlob(Convert.FromBase64String(Pkcs8PublicKey))), "the key openssl generated");
            });

        }

        /// <summary>
        /// Without a passphrase it is asked for, as for an openssh-key-v1 key. A wrong one cannot be told
        /// apart from a broken file in PKCS#8, so the refusal names both.
        /// </summary>
        [Test]
        public void AnEncryptedEd25519Pkcs8Key_NeedsItsPassphrase()
        {

            var wrong = Assert.Throws<SshWireException>(() => SshKeyGenerator.LoadPrivateKey(Pkcs8Encrypted, "not-hermod"));

            Assert.Multiple(() => {
                Assert.That(wrong!.Message, Does.Contain("wrong passphrase"));
                Assert.Throws<SshAuthenticationException>(() => SshKeyGenerator.LoadPrivateKey(Pkcs8Encrypted), "no passphrase at all");
            });

        }

        #endregion

    }

}
