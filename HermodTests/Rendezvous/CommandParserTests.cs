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

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// Tests for the control command parser.
    /// </summary>
    [TestFixture]
    public class CommandParserTests
    {

        #region (private) ParseConnect   (Text)

        private static ConnectPortsCommand ParseConnect(String Text)
        {

            var success = CommandParser.TryParse(Text, out var command, out var error);

            Assert.That(success, Is.True, $"Could not parse '{Text}': {error}");
            Assert.That(command, Is.InstanceOf<ConnectPortsCommand>());

            return (ConnectPortsCommand) command!;

        }

        #endregion

        #region (private) ParseDisconnect(Text)

        private static DisconnectPortsCommand ParseDisconnect(String Text)
        {

            var success = CommandParser.TryParse(Text, out var command, out var error);

            Assert.That(success, Is.True, $"Could not parse '{Text}': {error}");
            Assert.That(command, Is.InstanceOf<DisconnectPortsCommand>());

            return (DisconnectPortsCommand) command!;

        }

        #endregion

        #region (private) ParseError     (Text)

        private static CommandError ParseError(String Text)
        {

            var success = CommandParser.TryParse(Text, out var command, out var error);

            Assert.That(success, Is.False, $"'{Text}' should not be parsable, but was parsed as '{command}'!");
            Assert.That(error.Message, Is.Not.Empty, "A parser error must have a message!");

            return error;

        }

        #endregion


        #region ConnectPorts

        [Test]
        public void ConnectPorts_WithTwoFixedPorts()
        {

            var command = ParseConnect("ConnectPorts([20000, 30000], maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports.Count,      Is.EqualTo(2));
                Assert.That(command.Ports[0].Port,    Is.EqualTo(IPPort.Parse(20000)));
                Assert.That(command.Ports[1].Port,    Is.EqualTo(IPPort.Parse(30000)));
                Assert.That(command.Ports[0].IsRandom, Is.False);
                Assert.That(command.Description,      Is.EqualTo("maintenance"));
                Assert.That(command.Profile,          Is.Null, "An unspecified transfer profile must stay unspecified!");
            });

        }

        [Test]
        public void ConnectPorts_WithRandomFirstPort()
        {

            var command = ParseConnect("ConnectPorts([?,30000], maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports[0].IsRandom, Is.True);
                Assert.That(command.Ports[1].Port,     Is.EqualTo(IPPort.Parse(30000)));
            });

        }

        [Test]
        public void ConnectPorts_WithRandomSecondPort()
        {

            var command = ParseConnect("ConnectPorts([20000, ?], maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports[0].Port,     Is.EqualTo(IPPort.Parse(20000)));
                Assert.That(command.Ports[1].IsRandom, Is.True);
            });

        }

        [Test]
        public void ConnectPorts_WithTwoRandomPorts()
        {

            var command = ParseConnect("ConnectPorts([?,?], maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports.Count,       Is.EqualTo(2));
                Assert.That(command.Ports[0].IsRandom, Is.True);
                Assert.That(command.Ports[1].IsRandom, Is.True);
            });

        }

        [Test]
        public void ConnectPorts_WithThreePorts_IsAChat()
        {

            var command = ParseConnect("ConnectPorts([?,?,?], maintenance)");

            Assert.That(command.Ports.Count, Is.EqualTo(3));

        }

        [Test]
        public void ConnectPorts_IsCaseInsensitive()
        {

            var command = ParseConnect("connectPORTS([?,?], maintenance)");

            Assert.That(command.CommandName, Is.EqualTo("ConnectPorts"));

        }

        [Test]
        public void ConnectPorts_IgnoresWhitespace()
        {

            var command = ParseConnect("   ConnectPorts (  [ 20000 ,  ? ] ,  maintenance )   ");

            Assert.Multiple(() => {
                Assert.That(command.Ports[0].Port,     Is.EqualTo(IPPort.Parse(20000)));
                Assert.That(command.Ports[1].IsRandom, Is.True);
                Assert.That(command.Description,       Is.EqualTo("maintenance"));
            });

        }

        [Test]
        public void ConnectPorts_KeepsSpecialCharactersWithinTheDescription()
        {

            var command = ParseConnect("ConnectPorts([?,?], $ecret-42_%&/{}=!?)");

            Assert.That(command.Description, Is.EqualTo("$ecret-42_%&/{}=!?"));

        }

        [Test]
        public void ConnectPorts_WithoutADescription()
        {

            var command = ParseConnect("ConnectPorts([?,?])");

            Assert.Multiple(() => {
                Assert.That(command.Ports.Count,  Is.EqualTo(2));
                Assert.That(command.Description,  Is.Null, "A description is optional!");
                Assert.That(command.Profile,      Is.Null);
            });

        }

        [Test]
        public void ConnectPorts_ShowsTheDescriptionWithinToString()
        {

            var command = ParseConnect("ConnectPorts([20000, ?], \"For the nightly backup\", Bulk)");

            Assert.That(command.ToString(), Is.EqualTo("ConnectPorts([20000, ?], \"For the nightly backup\", Bulk)"));

        }

        #endregion

        #region ConnectPorts with a quoted description

        [Test]
        public void ConnectPorts_WithAQuotedDescription()
        {

            var command = ParseConnect("ConnectPorts([?,?], \"SSH rendezvous for maintenance work\")");

            Assert.Multiple(() => {
                Assert.That(command.Description, Is.EqualTo("SSH rendezvous for maintenance work"));
                Assert.That(command.Profile,     Is.Null);
            });

        }

        [Test]
        public void ConnectPorts_WithAQuotedDescriptionAndAProfile()
        {

            var command = ParseConnect("ConnectPorts([?,?], \"Alice and Bob, chatting\", Interactive)");

            Assert.Multiple(() => {
                Assert.That(command.Description, Is.EqualTo("Alice and Bob, chatting"), "A quoted description may contain commas!");
                Assert.That(command.Profile,     Is.EqualTo(TransferProfile.Interactive));
            });

        }

        [Test]
        public void ConnectPorts_WithAnEscapedQuoteWithinTheDescription()
        {

            var command = ParseConnect("ConnectPorts([?,?], \"The \\\"temporary\\\" tunnel\")");

            Assert.That(command.Description, Is.EqualTo("The \"temporary\" tunnel"));

        }

        [Test]
        public void ConnectPorts_WithAnUnclosedQuote_Fails()
        {

            var error = ParseError("ConnectPorts([?,?], \"never ending)");

            Assert.That(error.Code, Is.EqualTo(ResponseCode.InvalidSyntax));

        }

        [Test]
        public void ConnectPorts_WithASingleUnquotedProfile_IsAProfile()
        {

            var command = ParseConnect("ConnectPorts([?,?], Bulk)");

            Assert.Multiple(() => {
                Assert.That(command.Profile,     Is.EqualTo(TransferProfile.Bulk));
                Assert.That(command.Description, Is.Null, "An unquoted profile name is a profile, not a description!");
            });

        }

        [Test]
        public void ConnectPorts_WithASingleQuotedProfile_IsADescription()
        {

            var command = ParseConnect("ConnectPorts([?,?], \"Bulk\")");

            Assert.Multiple(() => {
                Assert.That(command.Description, Is.EqualTo("Bulk"), "Quoting says 'this is a description'!");
                Assert.That(command.Profile,     Is.Null);
            });

        }

        #endregion

        #region ConnectPorts with the echo flag

        [Test]
        [TestCase("Echo")]
        [TestCase("echo")]
        [TestCase("ECHO")]
        [TestCase("EchoToSender")]
        public void ConnectPorts_WithTheEchoFlag(String Text)
        {

            var command = ParseConnect($"ConnectPorts([?,?,?], {Text})");

            Assert.Multiple(() => {
                Assert.That(command.EchoToSender, Is.True);
                Assert.That(command.Description,  Is.Null, "The echo flag is not a description!");
                Assert.That(command.Profile,      Is.Null);
            });

        }

        [Test]
        public void ConnectPorts_WithoutTheEchoFlag_DoesNotEcho()
        {

            Assert.That(ParseConnect("ConnectPorts([?,?,?])").      EchoToSender, Is.False, "The echo must be off by default!");
            Assert.That(ParseConnect("ConnectPorts([?,?,?], NoEcho)").EchoToSender, Is.False);

        }

        [Test]
        public void ConnectPorts_WithDescriptionProfileAndEcho_InAnyOrder()
        {

            var expected = new[] {
                               "ConnectPorts([?,?,?], \"The Friday chat\", Interactive, Echo)",
                               "ConnectPorts([?,?,?], Interactive, \"The Friday chat\", Echo)",
                               "ConnectPorts([?,?,?], Echo, Interactive, \"The Friday chat\")",
                               "ConnectPorts([?,?,?], Echo, \"The Friday chat\", Interactive)"
                           };

            foreach (var text in expected)
            {

                var command = ParseConnect(text);

                Assert.Multiple(() => {
                    Assert.That(command.Description,  Is.EqualTo("The Friday chat"), text);
                    Assert.That(command.Profile,      Is.EqualTo(TransferProfile.Interactive), text);
                    Assert.That(command.EchoToSender, Is.True, text);
                });

            }

        }

        [Test]
        public void ConnectPorts_WithAQuotedEcho_IsADescription()
        {

            var command = ParseConnect("ConnectPorts([?,?], \"Echo\")");

            Assert.Multiple(() => {
                Assert.That(command.Description,  Is.EqualTo("Echo"));
                Assert.That(command.EchoToSender, Is.False);
            });

        }

        [Test]
        public void ConnectPorts_WithTheEchoFlagTwice_Fails()
        {

            var error = ParseError("ConnectPorts([?,?], Echo, NoEcho)");

            Assert.That(error.Code, Is.EqualTo(ResponseCode.InvalidSyntax));

        }

        [Test]
        public void ConnectPorts_ShowsTheEchoFlagWithinToString()
        {

            var command = ParseConnect("ConnectPorts([?,?,?], \"The Friday chat\", Interactive, Echo)");

            Assert.That(command.ToString(), Is.EqualTo("ConnectPorts([?, ?, ?], \"The Friday chat\", Interactive, Echo)"));

        }

        #endregion

        #region ConnectPorts with transfer profiles

        [Test]
        [TestCase("Balanced",     TransferProfile.Balanced)]
        [TestCase("default",      TransferProfile.Balanced)]
        [TestCase("Interactive",  TransferProfile.Interactive)]
        [TestCase("interactive",  TransferProfile.Interactive)]
        [TestCase("low-latency",  TransferProfile.Interactive)]
        [TestCase("LOW_LATENCY",  TransferProfile.Interactive)]
        [TestCase("chat",         TransferProfile.Interactive)]
        [TestCase("SSH",          TransferProfile.Interactive)]
        [TestCase("Bulk",         TransferProfile.Bulk)]
        [TestCase("bulk-transfer",TransferProfile.Bulk)]
        [TestCase("throughput",   TransferProfile.Bulk)]
        [TestCase("backup",       TransferProfile.Bulk)]
        public void ConnectPorts_WithTransferProfile(String Text, TransferProfile Expected)
        {

            var command = ParseConnect($"ConnectPorts([?,?], maintenance, {Text})");

            Assert.That(command.Profile, Is.EqualTo(Expected));

        }

        [Test]
        public void ConnectPorts_WithUnknownTransferProfile_Fails()
        {

            var error = ParseError("ConnectPorts([?,?], maintenance, Telepathy)");

            Assert.Multiple(() => {
                Assert.That(error.Code,    Is.EqualTo(ResponseCode.InvalidSyntax));
                Assert.That(error.Message, Does.Contain("Telepathy"));
            });

        }

        #endregion

        #region DisconnectPorts

        [Test]
        public void DisconnectPorts_WithTwoPorts()
        {

            var command = ParseDisconnect("DisconnectPorts(20000, 30000, maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports, Is.EqualTo(new[] { IPPort.Parse(20000), IPPort.Parse(30000) }));
                Assert.That(command.Description, Is.EqualTo("maintenance"));
            });

        }

        [Test]
        public void DisconnectPorts_WithASinglePort()
        {

            var command = ParseDisconnect("DisconnectPorts(20000, maintenance)");

            Assert.Multiple(() => {
                Assert.That(command.Ports,  Is.EqualTo(new[] { IPPort.Parse(20000) }));
                Assert.That(command.Description, Is.EqualTo("maintenance"));
            });

        }

        [Test]
        public void DisconnectPorts_WithABracketedPortList()
        {

            var command = ParseDisconnect("DisconnectPorts([20000, 30000], maintenance)");

            Assert.That(command.Ports, Is.EqualTo(new[] { IPPort.Parse(20000), IPPort.Parse(30000) }));

        }

        [Test]
        public void DisconnectPorts_WithoutADescription()
        {

            var command = ParseDisconnect("DisconnectPorts(20000)");

            Assert.Multiple(() => {
                Assert.That(command.Ports,        Is.EqualTo(new[] { IPPort.Parse(20000) }));
                Assert.That(command.Description,  Is.Null);
            });

        }

        [Test]
        public void DisconnectPorts_WithOnlyNumbers_AreAllPorts()
        {

            // Every unquoted number is a TCP port now, no argument is reserved
            // for anything else any more.
            var command = ParseDisconnect("DisconnectPorts(20000, 30000, 12345)");

            Assert.Multiple(() => {
                Assert.That(command.Ports,        Is.EqualTo(new[] { IPPort.Parse(20000), IPPort.Parse(30000), IPPort.Parse(12345) }));
                Assert.That(command.Description,  Is.Null);
            });

        }

        [Test]
        public void DisconnectPorts_WithAQuotedNumericDescription()
        {

            var command = ParseDisconnect("DisconnectPorts(20000, 30000, \"12345\")");

            Assert.Multiple(() => {
                Assert.That(command.Ports,        Is.EqualTo(new[] { IPPort.Parse(20000), IPPort.Parse(30000) }));
                Assert.That(command.Description,  Is.EqualTo("12345"), "Quoting tells a description from a port!");
            });

        }

        [Test]
        public void DisconnectPorts_ShowsTheDescriptionWithinToString()
        {

            var command = ParseDisconnect("DisconnectPorts(20000, 30000, \"Maintenance is done\")");

            Assert.That(command.ToString(), Is.EqualTo("DisconnectPorts([20000, 30000], \"Maintenance is done\")"));

        }

        #endregion

        #region Invalid commands

        [Test]
        public void UnknownCommand_Fails()
        {

            var error = ParseError("ReticulateSplines([?,?], maintenance)");

            Assert.That(error.Code, Is.EqualTo(ResponseCode.UnknownCommand));

        }

        [Test]
        [TestCase("",                                    Description = "Empty")]
        [TestCase("   ",                                 Description = "Whitespace only")]
        [TestCase("ConnectPorts",                        Description = "No arguments at all")]
        [TestCase("ConnectPorts([?,?]",                  Description = "Missing closing parenthesis")]
        [TestCase("ConnectPorts([?,?, maintenance)",          Description = "Missing closing bracket")]
        [TestCase("ConnectPorts([?,?],)",                Description = "Empty argument")]
        [TestCase("ConnectPorts([?], maintenance)",           Description = "A rendezvous needs at least two ports")]
        [TestCase("ConnectPorts([], maintenance)",            Description = "No ports at all")]
        [TestCase("ConnectPorts(20000, 30000, maintenance)",  Description = "ConnectPorts requires a bracketed port list")]
        [TestCase("ConnectPorts([20000, 20000], maintenance)",Description = "Duplicate ports")]
        [TestCase("ConnectPorts([0, 30000], maintenance)",    Description = "TCP port zero")]
        [TestCase("ConnectPorts([70000, 30000], maintenance)",Description = "TCP port above 65535")]
        [TestCase("ConnectPorts([-1, 30000], maintenance)",   Description = "Negative TCP port")]
        [TestCase("ConnectPorts([2e4, 30000], maintenance)",  Description = "Not a number")]
        [TestCase("ConnectPorts([?,?], my description)", Description = "Whitespace within an unquoted description")]
        [TestCase("ConnectPorts([?,?], maintenance) extra",   Description = "Trailing text")]
        [TestCase("ConnectPorts([?,?], maintenance, Bulk, x)",Description = "Too many arguments")]
        [TestCase("ConnectPorts([?,?,], maintenance)",        Description = "Trailing comma within the port list")]
        [TestCase("DisconnectPorts(maintenance)",             Description = "No ports at all")]
        [TestCase("DisconnectPorts(20000, a, b)",        Description = "Too many arguments after the ports")]
        [TestCase("DisconnectPorts(?, maintenance)",          Description = "Random ports can not be disconnected")]
        [TestCase("DisconnectPorts([?], maintenance)",        Description = "Random ports can not be disconnected")]
        [TestCase("DisconnectPorts(20000, 20000, maintenance)", Description = "Duplicate ports")]
        public void InvalidCommand_Fails(String Text)
        {

            var error = ParseError(Text);

            Assert.That(error.Code, Is.AnyOf(ResponseCode.InvalidSyntax,
                                             ResponseCode.UnknownCommand));

        }

        [Test]
        public void TooLongDescription_Fails()
        {

            var error = ParseError($"ConnectPorts([?,?], {new String('x', RendezvousCommand.MaxDescriptionLength + 1)})");

            Assert.Multiple(() => {
                Assert.That(error.Code,    Is.EqualTo(ResponseCode.InvalidSyntax));
                Assert.That(error.Message, Does.Contain(RendezvousCommand.MaxDescriptionLength.ToString()));
            });

        }

        [Test]
        public void MaximumLengthDescription_IsAccepted()
        {

            var description  = new String('x', RendezvousCommand.MaxDescriptionLength);
            var command      = ParseConnect($"ConnectPorts([?,?], {description})");

            Assert.That(command.Description, Is.EqualTo(description));

        }

        #endregion

    }

}
