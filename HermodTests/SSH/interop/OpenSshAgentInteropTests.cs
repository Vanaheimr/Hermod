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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M8: talk to the real platform ssh-agent (Windows named pipe / SSH_AUTH_SOCK) if one is available.</summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshAgentInteropTests
    {

        #region RealAgent_ListsAndSigns

        [Test]
        [CancelAfter(20000)]
        public async Task RealAgent_ListsAndSigns(CancellationToken CancellationToken)
        {

            SshAgentClient agent;
            try
            {
                agent = await SshAgentClient.ConnectAsync(CancellationToken);
            }
            catch (Exception e)
            {
                Assert.Ignore($"No ssh-agent reachable: {e.Message}");
                return;
            }

            await using (agent)
            {

                var identities = await agent.ListIdentitiesAsync(CancellationToken);
                if (identities.Count == 0)
                    Assert.Ignore("The ssh-agent holds no identities (run 'ssh-add' to load one).");

                // Sign with the first identity and verify against its public key.
                var data      = Encoding.UTF8.GetBytes("hermod agent interop " + Guid.NewGuid());
                var identity  = identities[0];
                var signature = await agent.SignAsync(identity.PublicKeyBlob, data, CancellationToken: CancellationToken);

                Assert.That(SshSignature.Verify(identity.PublicKeyBlob, data, signature), Is.True,
                            "the real agent's signature must verify against the advertised public key");

            }

        }

        #endregion

    }

}
