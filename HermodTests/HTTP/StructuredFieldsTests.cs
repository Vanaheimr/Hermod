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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9651, Structured Field Values for HTTP.
    ///
    /// Most of these are the specification's own examples. The rest are the
    /// places where being liberal would make two implementations disagree about
    /// what a field said - a trailing comma, a sixteenth digit, an upper-case
    /// hexadecimal escape - and where this parser therefore refuses instead.
    ///
    /// The round-trip tests matter more than they look: RFC 9421 message
    /// signatures cover a signature base that both ends re-serialize, so a
    /// serializer that differs from its parser by one space is a verification
    /// that fails for reasons nobody can see on the wire.
    /// </summary>
    [TestFixture]
    public class StructuredFieldsTests
    {

        #region Lists

        #region AListOfTokens()

        [Test]
        public void AListOfTokens()
        {

            Assert.That(StructuredFields.TryParseList("sugar, tea, rum", out var members), Is.True);

            Assert.That(
                members!.Cast<SFItem>().Select(item => item.Value.AsToken),
                Is.EqualTo(new[] { "sugar", "tea", "rum" })
            );

        }

        #endregion

        #region AnInnerListCarriesItemsAndParametersOfItsOwn()

        [Test]
        public void AnInnerListCarriesItemsAndParametersOfItsOwn()
        {

            Assert.That(StructuredFields.TryParseList("(\"foo\"; a=1;b=2);lvl=5, (\"bar\" \"baz\");lvl=1", out var members), Is.True);

            var first   = (SFInnerList) members![0];
            var second  = (SFInnerList) members![1];

            Assert.Multiple(() => {

                Assert.That(members!.Count,                           Is.EqualTo(2));

                Assert.That(first.Items.Count,                        Is.EqualTo(1));
                Assert.That(first.Items[0].Value.AsString,            Is.EqualTo("foo"));
                Assert.That(first.Items[0].Parameters.Count,          Is.EqualTo(2));
                Assert.That(first.Parameters.TryGet("lvl", out var lvl) ? lvl.AsInteger : -1,
                                                                      Is.EqualTo(5));

                Assert.That(second.Items.Select(item => item.Value.AsString),
                                                                      Is.EqualTo(new[] { "bar", "baz" }));

            });

        }

        #endregion

        #region AnEmptyInnerListIsStillAMember()

        [Test]
        public void AnEmptyInnerListIsStillAMember()
        {

            Assert.That(StructuredFields.TryParseList("(\"foo\" \"bar\"), (\"baz\"), (\"bat\" \"one\"), ()", out var members), Is.True);

            Assert.Multiple(() => {
                Assert.That(members!.Count,                        Is.EqualTo(4));
                Assert.That(((SFInnerList) members![3]).Items,     Is.Empty);
            });

        }

        #endregion

        #region AnEmptyFieldIsAnEmptyList()

        /// <summary>
        /// Section 4.2.1 step 3: no structured data is not a parse failure, it
        /// is an empty list. A field that is present and empty says nothing,
        /// which is different from saying it is malformed.
        /// </summary>
        [Test]
        public void AnEmptyFieldIsAnEmptyList()
        {

            Assert.That(StructuredFields.TryParseList("", out var members), Is.True);
            Assert.That(members,                                            Is.Empty);

        }

        #endregion

        #region ATrailingCommaFails()

        [Test]
        public void ATrailingCommaFails()
        {

            Assert.Multiple(() => {
                Assert.That(StructuredFields.TryParseList("sugar, tea,",       out _), Is.False);
                Assert.That(StructuredFields.TryParseDictionary("a=1, b=2,",   out _), Is.False);
            });

        }

        #endregion

        #region SomethingAfterTheValueFails()

        [Test]
        public void SomethingAfterTheValueFails()
        {

            Assert.Multiple(() => {
                Assert.That(StructuredFields.TryParseList("sugar tea",  out _), Is.False);
                Assert.That(StructuredFields.TryParseItem("42 43",      out _), Is.False);
            });

        }

        #endregion

        #region TheTopLevelToleratesSurroundingSpaces()

        [Test]
        public void TheTopLevelToleratesSurroundingSpaces()
        {

            Assert.That(StructuredFields.TryParseList("   sugar,\ttea   ", out var members), Is.True);
            Assert.That(members!.Count,                                                     Is.EqualTo(2));

        }

        #endregion

        #endregion

        #region Dictionaries

        #region ADictionaryOfMixedValues()

        [Test]
        public void ADictionaryOfMixedValues()
        {

            Assert.That(StructuredFields.TryParseDictionary("en=\"Applepie\", da=:w4ZibGV0w6ZydGU=:", out var dictionary), Is.True);

            Assert.Multiple(() => {

                Assert.That(dictionary!.TryGetItem("en", out var en) ? en.AsString : null,
                            Is.EqualTo("Applepie"));

                Assert.That(dictionary!.TryGetItem("da", out var da) ? Encoding.UTF8.GetString(da.AsByteSequence) : null,
                            Is.EqualTo("Æbletærte"));

            });

        }

        #endregion

        #region AKeyWithoutAValueIsTrue()

        [Test]
        public void AKeyWithoutAValueIsTrue()
        {

            Assert.That(StructuredFields.TryParseDictionary("a=?0, b, c; foo=bar", out var dictionary), Is.True);

            Assert.Multiple(() => {

                Assert.That(dictionary!.Count, Is.EqualTo(3));

                Assert.That(dictionary!.TryGetItem("a", out var a) && !a.AsBoolean,  Is.True);
                Assert.That(dictionary!.TryGetItem("b", out var b) &&  b.AsBoolean,  Is.True);
                Assert.That(dictionary!.TryGetItem("c", out var c) &&  c.AsBoolean,  Is.True);

                Assert.That(dictionary!.TryGet("c", out var member), Is.True);
                Assert.That(member!.Parameters.TryGet("foo", out var foo) ? foo.AsToken : null,
                            Is.EqualTo("bar"));

            });

        }

        #endregion

        #region ARepeatedKeyKeepsItsPlaceAndTakesTheLastValue()

        /// <summary>
        /// Section 4.2.2: the ordered map is set, not appended to. Anything
        /// else would make "a=1, b=2, a=3" serialize back in a different order
        /// than it arrived.
        /// </summary>
        [Test]
        public void ARepeatedKeyKeepsItsPlaceAndTakesTheLastValue()
        {

            Assert.That(StructuredFields.TryParseDictionary("a=1, b=2, a=3", out var dictionary), Is.True);

            Assert.Multiple(() => {
                Assert.That(dictionary!.Keys,                                             Is.EqualTo(new[] { "a", "b" }));
                Assert.That(dictionary!.TryGetItem("a", out var a) ? a.AsInteger : -1,    Is.EqualTo(3));
                Assert.That(dictionary!.ToString(),                                       Is.EqualTo("a=3, b=2"));
            });

        }

        #endregion

        #region ADictionaryMayHoldInnerLists()

        [Test]
        public void ADictionaryMayHoldInnerLists()
        {

            Assert.That(StructuredFields.TryParseDictionary("rating=1.5, feelings=(joy sadness)", out var dictionary), Is.True);

            Assert.Multiple(() => {

                Assert.That(dictionary!.TryGetItem("rating", out var rating) ? rating.AsDecimal : 0m,
                            Is.EqualTo(1.5m));

                Assert.That(dictionary!.TryGet("feelings", out var feelings), Is.True);

                Assert.That(((SFInnerList) feelings!).Items.Select(item => item.Value.AsToken),
                            Is.EqualTo(new[] { "joy", "sadness" }));

            });

        }

        #endregion

        #region AnUpperCaseKeyIsASyntaxError()

        /// <summary>
        /// Section 3.1.2: a key is lower case. Accepting "A=1" and folding it
        /// would be a kindness that changes what the field said.
        /// </summary>
        [Test]
        public void AnUpperCaseKeyIsASyntaxError()
        {

            Assert.Multiple(() => {
                Assert.That(StructuredFields.TryParseDictionary("A=1",       out _), Is.False);
                Assert.That(StructuredFields.TryParseItem("42;Foo=bar",      out _), Is.False);
                Assert.That(StructuredFields.IsKey("A"),                             Is.False);
                Assert.That(StructuredFields.IsKey("a*1.-_"),                        Is.True);
            });

        }

        #endregion

        #endregion

        #region Bare items

        #region IntegersStopAtFifteenDigits()

        [Test]
        public void IntegersStopAtFifteenDigits()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("42").         Value.AsInteger,  Is.EqualTo(42));
                Assert.That(StructuredFields.ParseItem("-42").        Value.AsInteger,  Is.EqualTo(-42));
                Assert.That(StructuredFields.ParseItem("999999999999999").Value.AsInteger,
                                                                                        Is.EqualTo(999_999_999_999_999));

                // Sixteen digits fails rather than saturating: a recipient that
                // clamps has silently changed the number.
                Assert.That(StructuredFields.TryParseItem("1000000000000000", out _),   Is.False);
                Assert.That(StructuredFields.TryParseItem("-1000000000000000", out _),  Is.False);

            });

        }

        #endregion

        #region DecimalsKeepThreeFractionalDigits()

        [Test]
        public void DecimalsKeepThreeFractionalDigits()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("4.5").   Value.AsDecimal,  Is.EqualTo(4.5m));
                Assert.That(StructuredFields.ParseItem("-1.334").Value.AsDecimal,  Is.EqualTo(-1.334m));

                Assert.That(StructuredFields.TryParseItem("1.2345",           out _), Is.False);  // four fractional digits
                Assert.That(StructuredFields.TryParseItem("1234567890123.0",  out _), Is.False);  // thirteen integer digits
                Assert.That(StructuredFields.TryParseItem("1.",               out _), Is.False);  // nothing after the point

            });

        }

        #endregion

        #region ADecimalAlwaysShowsAFractionalDigit()

        /// <summary>
        /// Section 4.1.5 step 8: without it, a decimal would read back as an
        /// integer, and the two are different types in this grammar.
        /// </summary>
        [Test]
        public void ADecimalAlwaysShowsAFractionalDigit()
        {

            Assert.Multiple(() => {
                Assert.That(SFBareItem.Decimal(1m).      ToString(),  Is.EqualTo("1.0"));
                Assert.That(SFBareItem.Decimal(-1m).     ToString(),  Is.EqualTo("-1.0"));
                Assert.That(SFBareItem.Decimal(1.100m).  ToString(),  Is.EqualTo("1.1"));
            });

        }

        #endregion

        #region ADecimalIsRoundedTowardsTheEvenDigit()

        /// <summary>
        /// Section 4.1.5 step 2: ties go to the even value, not away from zero.
        /// </summary>
        [Test]
        public void ADecimalIsRoundedTowardsTheEvenDigit()
        {

            Assert.Multiple(() => {
                Assert.That(SFBareItem.Decimal(0.0025m).ToString(),  Is.EqualTo("0.002"));
                Assert.That(SFBareItem.Decimal(0.0035m).ToString(),  Is.EqualTo("0.004"));
            });

        }

        #endregion

        #region StringsHoldPrintableASCIIAndTwoEscapes()

        [Test]
        public void StringsHoldPrintableASCIIAndTwoEscapes()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("\"hello world\"").Value.AsString,      Is.EqualTo("hello world"));
                Assert.That(StructuredFields.ParseItem("\"say \\\"hi\\\"\"").Value.AsString,   Is.EqualTo("say \"hi\""));
                Assert.That(StructuredFields.ParseItem("\"back\\\\slash\"").Value.AsString,    Is.EqualTo("back\\slash"));

                Assert.That(StructuredFields.TryParseItem("\"unterminated",  out _),  Is.False);
                Assert.That(StructuredFields.TryParseItem("\"tab\there\"",   out _),  Is.False);  // a control character
                Assert.That(StructuredFields.TryParseItem("\"\\n\"",         out _),  Is.False);  // not an escape here

            });

        }

        #endregion

        #region TokensAreNotStrings()

        [Test]
        public void TokensAreNotStrings()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("foo123/456").Value.Type,     Is.EqualTo(SFType.Token));
                Assert.That(StructuredFields.ParseItem("foo123/456").Value.AsToken,  Is.EqualTo("foo123/456"));
                Assert.That(StructuredFields.ParseItem("*").         Value.AsToken,  Is.EqualTo("*"));
                Assert.That(StructuredFields.ParseItem("\"foo\"").   Value.Type,     Is.EqualTo(SFType.String));

                // A token may not begin with a digit - that is where the number
                // parser takes over, and "1foo" is a number followed by rubbish.
                Assert.That(StructuredFields.TryParseItem("1foo", out _),            Is.False);

            });

        }

        #endregion

        #region ByteSequencesAreBase64BetweenColons()

        [Test]
        public void ByteSequencesAreBase64BetweenColons()
        {

            var item = StructuredFields.ParseItem(":cHJldGVuZCB0aGlzIGlzIGJpbmFyeSBjb250ZW50Lg==:");

            Assert.Multiple(() => {

                Assert.That(Encoding.ASCII.GetString(item.Value.AsByteSequence),
                            Is.EqualTo("pretend this is binary content."));

                Assert.That(StructuredFields.ParseItem("::").Value.AsByteSequence,  Is.Empty);
                Assert.That(StructuredFields.TryParseItem(":nonsense!:",   out _),  Is.False);
                Assert.That(StructuredFields.TryParseItem(":unterminated", out _),  Is.False);

            });

        }

        #endregion

        #region BooleansAreOneCharacter()

        [Test]
        public void BooleansAreOneCharacter()
        {

            Assert.Multiple(() => {
                Assert.That(StructuredFields.ParseItem("?1").Value.AsBoolean,  Is.True);
                Assert.That(StructuredFields.ParseItem("?0").Value.AsBoolean,  Is.False);
                Assert.That(StructuredFields.TryParseItem("?2",    out _),     Is.False);
                Assert.That(StructuredFields.TryParseItem("?true", out _),     Is.False);
            });

        }

        #endregion

        #region DatesAreIntegerSecondsAndNothingElse()

        /// <summary>
        /// Section 4.2.9: a date is an sf-integer behind an "@". "@1659578233.0"
        /// is not a date with a redundant fraction; it is not a date.
        /// </summary>
        [Test]
        public void DatesAreIntegerSecondsAndNothingElse()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("@1659578233").Value.AsDate,
                            Is.EqualTo(DateTimeOffset.Parse("2022-08-04T01:57:13Z")));

                Assert.That(StructuredFields.ParseItem("@-1").Value.AsDateSeconds,  Is.EqualTo(-1));

                Assert.That(StructuredFields.TryParseItem("@1659578233.0", out _),  Is.False);
                Assert.That(StructuredFields.TryParseItem("@",             out _),  Is.False);

            });

        }

        #endregion

        #region ADateBeyondTheCalendarStillParses()

        /// <summary>
        /// An sf-date reaches about 31 million years, which DateTimeOffset does
        /// not. The item keeps the seconds it was given; only the conversion
        /// refuses. Clamping it at parse time would turn a field this
        /// implementation cannot represent into one it silently misreports.
        /// </summary>
        [Test]
        public void ADateBeyondTheCalendarStillParses()
        {

            var item = StructuredFields.ParseItem("@999999999999999");

            Assert.Multiple(() => {
                Assert.That(item.Value.AsDateSeconds,  Is.EqualTo(999_999_999_999_999));
                Assert.That(() => item.Value.AsDate,   Throws.TypeOf<ArgumentOutOfRangeException>());
            });

        }

        #endregion

        #region DisplayStringsArePercentEncodedUTF8()

        [Test]
        public void DisplayStringsArePercentEncodedUTF8()
        {

            Assert.Multiple(() => {

                Assert.That(StructuredFields.ParseItem("%\"f%c3%bc%c3%9f\"").Value.AsDisplayString,
                            Is.EqualTo("füß"));

                Assert.That(SFBareItem.DisplayString("füß").ToString(),
                            Is.EqualTo("%\"f%c3%bc%c3%9f\""));

                // Upper-case hexadecimal is a second way to write the same
                // string, and a canonical form may not have two.
                Assert.That(StructuredFields.TryParseItem("%\"f%C3%BC\"",  out _),  Is.False);

                // A lone continuation byte is not UTF-8. Producing U+FFFD here
                // would hand the caller a string the sender never sent.
                Assert.That(StructuredFields.TryParseItem("%\"%c3\"",      out _),  Is.False);

                Assert.That(StructuredFields.TryParseItem("%\"%zz\"",      out _),  Is.False);

            });

        }

        #endregion

        #endregion

        #region Round trips

        #region SerializingReproducesTheField()

        /// <summary>
        /// Byte for byte, for every canonical form. This is the property RFC
        /// 9421 depends on.
        ///
        /// Canonical, not merely valid: the specification's own example writes
        /// ("foo"; a=1;b=2) with a space after the semicolon, which parses and
        /// then serializes back without it. A round trip is only an identity
        /// for what Section 4.1 would have written.
        /// </summary>
        [TestCase("sugar, tea, rum")]
        [TestCase("(\"foo\";a=1;b=2);lvl=5, (\"bar\" \"baz\");lvl=1")]
        [TestCase("a=?0, b, c;foo=bar")]
        [TestCase("rating=1.5, feelings=(joy sadness)")]
        [TestCase("en=\"Applepie\", da=:w4ZibGV0w6ZydGU=:")]
        [TestCase("@1659578233;tz=utc")]
        [TestCase("%\"f%c3%bc%c3%9f\"")]
        [TestCase("()")]
        public void SerializingReproducesTheField(String Text)
        {

            if (StructuredFields.TryParseDictionary(Text, out var dictionary) && dictionary.Count > 0)
            {
                Assert.That(dictionary.ToString(), Is.EqualTo(Text));
                return;
            }

            Assert.That(StructuredFields.TryParseList(Text, out var members), Is.True, Text);
            Assert.That(StructuredFields.Serialize(members!), Is.EqualTo(Text));

        }

        #endregion

        #region ATrueParameterIsWrittenWithoutItsValue()

        /// <summary>
        /// Section 4.1.1.2: ";b" and ";b=?1" are the same parameter, and only
        /// the shorter one is canonical.
        /// </summary>
        [Test]
        public void ATrueParameterIsWrittenWithoutItsValue()
        {

            Assert.Multiple(() => {
                Assert.That(StructuredFields.ParseItem("a;b=?1").ToString(),  Is.EqualTo("a;b"));
                Assert.That(StructuredFields.ParseItem("a;b=?0").ToString(),  Is.EqualTo("a;b=?0"));
            });

        }

        #endregion

        #region AFieldCanBeBuiltRatherThanParsed()

        [Test]
        public void AFieldCanBeBuiltRatherThanParsed()
        {

            var dictionary = new SFDictionary().
                                 Set("sha-256", SFBareItem.ByteSequence([ 1, 2, 3 ])).
                                 Set("want",    new SFInnerList(
                                                    [ new SFItem(SFBareItem.Token("sha-512")) ],
                                                    new SFParameters().Set("q", SFBareItem.Decimal(0.3m))
                                                ));

            Assert.That(dictionary.ToString(), Is.EqualTo("sha-256=:AQID:, want=(sha-512);q=0.3"));

        }

        #endregion

        #endregion

    }

}
