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
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A MIME entity to be written: its headers and its body, encoded - text,
    /// an attachment, a multipart of entities, an attached message, or octets
    /// that are written as they are. What MimePart is for reading.
    /// </summary>
    /// <remarks>
    /// Every entity writes 7-bit ASCII with CRLF line breaks (see
    /// <see cref="MimeEncoding"/>), except an attached message that is 8-bit
    /// itself and a raw entity, which are written as they were given.
    /// </remarks>
    public abstract class MimeEntity
    {

        #region Properties

        /// <summary>
        /// Further headers of the entity - Content-Description, Content-Language -
        /// written after the content headers, in their order.
        /// </summary>
        public List<(String Name, String Value)> Headers { get; } = [];

        #endregion


        #region (internal) EntityType / ContentIdOfPart

        /// <summary>
        /// The media type of the entity - type "/" subtype, lower case - or null where it
        /// is not known.
        /// </summary>
        internal virtual String? EntityType
            => null;

        /// <summary>
        /// The Content-ID of the entity, without its angle brackets, or null: the one it
        /// is given, or a Content-ID among its further headers.
        /// </summary>
        internal virtual String? ContentIdOfPart
            => Headers.Where(header => header.Name.Equals("Content-ID", StringComparison.OrdinalIgnoreCase)).
                       Select (header => MimeMessage.ContentIdOf(header.Value)).
                       FirstOrDefault();

        #endregion

        #region (protected abstract) ContentHeaders()

        /// <summary>
        /// The header lines of the content - Content-Type, Content-Transfer-Encoding,
        /// Content-Disposition, Content-ID - folded, without their line breaks.
        /// </summary>
        protected abstract IEnumerable<String> ContentHeaders();

        #endregion

        #region (protected abstract) Body()

        /// <summary>
        /// The body, encoded, ending with a line break.
        /// </summary>
        protected abstract Byte[] Body();

        #endregion

        #region (internal virtual) Write(Output)

        /// <summary>
        /// The entity - headers, an empty line, the body - written to the output.
        /// </summary>
        internal virtual void Write(Stream Output)
        {

            foreach (var line in ContentHeaders())
                WriteLine(Output, line);

            foreach (var (name, value) in Headers)
                WriteLine(Output, MimeEncoding.UnstructuredHeader(name, value));

            Output.Write("\r\n"u8);
            Output.Write(Body());

        }

        internal static void WriteLine(Stream Output, String Line)
        {
            Output.Write(Encoding.ASCII.GetBytes(Line));
            Output.Write("\r\n"u8);
        }

        #endregion

        #region ToBytes()

        /// <summary>
        /// The entity as octets: headers, an empty line, the body.
        /// </summary>
        public Byte[] ToBytes()
        {
            using var output = new MemoryStream();
            Write(output);
            return output.ToArray();
        }

        #endregion

    }


    /// <summary>
    /// Text - text/plain or text/html - in UTF-8: as it is when it is 7-bit and
    /// fit to be sent as it is, quoted-printable otherwise.
    /// </summary>
    /// <param name="Text">The text; its line breaks become CRLF.</param>
    /// <param name="SubType">plain or html.</param>
    public sealed class MimeTextPart(String  Text,
                                     String  SubType = "plain") : MimeEntity
    {

        #region Properties

        /// <summary>
        /// The text.
        /// </summary>
        public String  Text     { get; } = Text;

        /// <summary>
        /// The subtype: plain, html.
        /// </summary>
        public String  SubType  { get; } = SubType;

        #endregion

        private Byte[] Octets
            => Encoding.UTF8.GetBytes(Text);

        internal override String EntityType
            => $"text/{SubType.ToLowerInvariant()}";

        protected override IEnumerable<String> ContentHeaders()
        {

            yield return MimeEncoding.ParameterizedHeader("Content-Type", $"text/{SubType}", [ ("charset", "utf-8") ]);
            yield return $"Content-Transfer-Encoding: {(MimeEncoding.NeedsEncoding(Octets) ? "quoted-printable" : "7bit")}";

        }

        protected override Byte[] Body()
        {

            var octets = Octets;
            var body   = MimeEncoding.NeedsEncoding(octets)
                             ? MimeEncoding.QuotedPrintable(octets)
                             : MimeEncoding.Lines(octets);

            return body.Length == 0 || body[^1] == '\n'
                       ? body
                       : [ .. body, (Byte) '\r', (Byte) '\n' ];

        }

    }


    /// <summary>
    /// An attachment - or an inline picture, with a Content-ID - in base64.
    /// </summary>
    public sealed class MimeAttachment : MimeEntity
    {

        #region Data

        private static readonly Regex MediaType = new (@"^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*$", RegexOptions.CultureInvariant);

        #endregion

        #region Properties

        /// <summary>
        /// The content, as it is.
        /// </summary>
        public ReadOnlyMemory<Byte>  Content      { get; }

        /// <summary>
        /// Its media type - image/png, application/pdf; application/octet-stream
        /// for one that is none.
        /// </summary>
        public String                ContentType  { get; }

        /// <summary>
        /// Its file name, or null.
        /// </summary>
        public String?               FileName     { get; }

        /// <summary>
        /// Whether it is shown in the text rather than kept beside it.
        /// </summary>
        public Boolean               Inline       { get; }

        /// <summary>
        /// The Content-ID an HTML part refers to it by (cid:), without its angle brackets, or null.
        /// </summary>
        public String?               ContentId    { get; }

        #endregion

        #region Constructor(s)

        public MimeAttachment(ReadOnlyMemory<Byte>  Content,
                              String                ContentType,
                              String?               FileName    = null,
                              Boolean               Inline      = false,
                              String?               ContentId   = null)
        {

            this.Content      = Content;
            this.ContentType  = MediaType.IsMatch(ContentType.Trim()) ? ContentType.Trim().ToLowerInvariant() : "application/octet-stream";
            this.FileName     = String.IsNullOrWhiteSpace(FileName) ? null : MimeEncoding.CheckHeaderValue("a file name", FileName.Trim());
            this.Inline       = Inline;
            this.ContentId    = ContentId is null ? null : CheckContentId(ContentId.Trim().Trim('<', '>'));

        }

        #endregion

        internal override String EntityType
            => ContentType;

        internal override String? ContentIdOfPart
            => ContentId ?? base.ContentIdOfPart;

        // RFC 5322 §3.6.4: id-left = dot-atom-text, id-right = dot-atom-text / no-fold-literal.
        private static readonly Regex MsgIdText = new(@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@([A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\[[\x21-\x5A\x5E-\x7E]*\])$");

        /// <summary>
        /// A Content-ID, without its angle brackets: RFC 2045 §7: id := "Content-ID" ":"
        /// msg-id, and RFC 5322 §3.6.4: msg-id = "&lt;" id-left "@" id-right "&gt;", of
        /// printable US-ASCII. What is none is refused - written, it would be none, or
        /// another id than the one a cid: URL refers to.
        /// </summary>
        /// <exception cref="ArgumentException">It is no msg-id.</exception>
        internal static String CheckContentId(String ContentId)
        {

            if (!MsgIdText.IsMatch(ContentId))
                throw new ArgumentException($"The Content-ID \"{ContentId}\" is no msg-id (RFC 5322 §3.6.4): id-left \"@\" id-right, of printable US-ASCII, without white space.", nameof(ContentId));

            return ContentId;

        }

        protected override IEnumerable<String> ContentHeaders()
        {

            yield return MimeEncoding.ParameterizedHeader("Content-Type", ContentType, FileName is null ? [] : [ ("name", FileName) ]);
            yield return "Content-Transfer-Encoding: base64";
            yield return MimeEncoding.ParameterizedHeader("Content-Disposition", Inline ? "inline" : "attachment", FileName is null ? [] : [ ("filename", FileName) ]);

            if (ContentId is not null)
                yield return $"Content-ID: <{ContentId}>";

        }

        protected override Byte[] Body()
            => MimeEncoding.Base64(Content.Span);

    }


    /// <summary>
    /// A multipart - mixed, alternative, related, signed, encrypted - of entities,
    /// with a boundary none of them can hold.
    /// </summary>
    public sealed class MimeMultipart : MimeEntity
    {

        #region Properties

        /// <summary>
        /// The subtype: mixed, alternative, related, signed, encrypted.
        /// </summary>
        public String                                SubType     { get; }

        /// <summary>
        /// The parts, in their order.
        /// </summary>
        public IReadOnlyList<MimeEntity>             Parts       { get; }

        /// <summary>
        /// Parameters of the Content-Type besides the boundary - protocol, micalg - in their order.
        /// </summary>
        public IReadOnlyList<(String Name, String Value)>  Parameters  { get; }

        /// <summary>
        /// The boundary.
        /// </summary>
        public String                                Boundary    { get; }

        #endregion

        #region Constructor(s)

        public MimeMultipart(String                                      SubType,
                             IEnumerable<MimeEntity>                     Parts,
                             IEnumerable<(String Name, String Value)>?   Parameters  = null)
        {

            this.SubType     = SubType.ToLowerInvariant();
            this.Parts       = [ .. Parts ];
            this.Parameters  = [ .. Parameters ?? [] ];
            this.Boundary    = MimeEncoding.NewBoundary();

            if (this.Parts.Count == 0)
                throw new ArgumentException("A multipart without parts is none.");

            // RFC 2387 §3.1: "The type parameter must be specified and its value is the MIME
            // media type of the "root" body part" - the part start names, the first without
            // (§3.2). Given none, it is the root's.
            if (this.SubType == "related" &&
               !this.Parameters.Any(parameter => parameter.Name.Equals("type", StringComparison.OrdinalIgnoreCase)) &&
                RootOf(this.Parts, this.Parameters).EntityType is { } rootType)
            {
                this.Parameters = [ ("type", rootType), .. this.Parameters ];
            }

        }

        #endregion

        // RFC 2387 §3.2: the root is the part whose Content-ID start names, the first without.
        private static MimeEntity RootOf(IReadOnlyList<MimeEntity> Parts, IReadOnlyList<(String Name, String Value)> Parameters)
        {

            var start = Parameters.FirstOrDefault(parameter => parameter.Name.Equals("start", StringComparison.OrdinalIgnoreCase)).Value;
            var id    = start is null ? null : MimeMessage.ContentIdOf(start);

            return (id is null ? null : Parts.FirstOrDefault(part => part.ContentIdOfPart == id)) ?? Parts[0];

        }

        internal override String EntityType
            => $"multipart/{SubType}";

        protected override IEnumerable<String> ContentHeaders()
        {
            // The boundary last: a reader that is lazy about parameters finds it at the end.
            yield return MimeEncoding.ParameterizedHeader("Content-Type", $"multipart/{SubType}", [ .. Parameters, ("boundary", Boundary) ]);
        }

        protected override Byte[] Body()
        {

            using var output  = new MemoryStream();
            var       open    = Encoding.ASCII.GetBytes($"--{Boundary}\r\n");

            foreach (var part in Parts)
            {

                output.Write(open);

                var octets = part.ToBytes();
                output.Write(octets);

                // The line break before a delimiter belongs to the delimiter (RFC 2046
                // §5.1.1): a part that ends with one gets another, so that what it ends
                // with is still its own.
                output.Write("\r\n"u8);

            }

            output.Write(Encoding.ASCII.GetBytes($"--{Boundary}--\r\n"));

            return output.ToArray();

        }

    }


    /// <summary>
    /// An entity given as octets - headers, an empty line, a body - and written as
    /// it is: the part a signature is over, which nothing may touch.
    /// </summary>
    /// <param name="Octets">The entity, as it was signed.</param>
    public sealed class MimeRawEntity(ReadOnlyMemory<Byte> Octets) : MimeEntity
    {

        /// <summary>
        /// The entity.
        /// </summary>
        public ReadOnlyMemory<Byte> Octets { get; } = Octets;

        internal override String EntityType
            => MimeMessage.Parse(Octets).Root.ContentType.MimeType;

        internal override String? ContentIdOfPart
            => MimeMessage.Parse(Octets).Root.ContentId;

        protected override IEnumerable<String> ContentHeaders()
            => [];

        protected override Byte[] Body()
            => Octets.ToArray();

        internal override void Write(Stream Output)
            => Output.Write(Octets.Span);

    }


    /// <summary>
    /// A message attached to a message (message/rfc822) - a forward as attachment.
    /// </summary>
    /// <remarks>
    /// RFC 2046 §5.2.1 allows a message/rfc822 no encoding but 7bit, 8bit and
    /// binary: one that is not 7-bit makes the mail that carries it 8-bit.
    /// </remarks>
    public sealed class MimeAttachedMessage : MimeEntity
    {

        #region Properties

        /// <summary>
        /// The attached message, as it arrived.
        /// </summary>
        public ReadOnlyMemory<Byte>  Message   { get; }

        /// <summary>
        /// Its file name - the subject, with .eml - or null.
        /// </summary>
        public String?               FileName  { get; }

        #endregion

        public MimeAttachedMessage(ReadOnlyMemory<Byte>  Message,
                                   String?               FileName = null)
        {
            this.Message   = Message;
            this.FileName  = String.IsNullOrWhiteSpace(FileName) ? null : MimeEncoding.CheckHeaderValue("a file name", FileName.Trim());
        }

        internal override String EntityType
            => "message/rfc822";

        /// <summary>
        /// Whether the message is 7-bit - and so the mail that carries it.
        /// </summary>
        public Boolean IsSevenBit
        {
            get
            {
                foreach (var octet in Message.Span)
                    if (octet > 127 || octet == 0)
                        return false;
                return true;
            }
        }

        protected override IEnumerable<String> ContentHeaders()
        {
            yield return "Content-Type: message/rfc822";
            yield return $"Content-Transfer-Encoding: {(IsSevenBit ? "7bit" : "8bit")}";
            yield return MimeEncoding.ParameterizedHeader("Content-Disposition", "attachment", FileName is null ? [] : [ ("filename", FileName) ]);
        }

        protected override Byte[] Body()
        {
            var body = MimeEncoding.Lines(Message.Span);
            return body.Length == 0 || body[^1] == '\n' ? body : [ .. body, (Byte) '\r', (Byte) '\n' ];
        }

    }

}
