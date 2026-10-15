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

using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// One entity of a MIME message (RFC 2045/2046): its header fields, its type,
    /// its octets as they arrived, and - for a multipart - its parts, or - for a
    /// message/rfc822 - the message it carries.
    /// </summary>
    public sealed class MimePart
    {

        #region Properties

        /// <summary>
        /// Where the part is in its message, numbered as IMAP numbers sections
        /// (RFC 3501 §6.4.5): "1", "2.1", "3.2.1". The top of a multipart message
        /// is "" - it is a container, not something to fetch; a message that is not
        /// a multipart is itself part "1".
        /// </summary>
        public String                   PartId                    { get; }

        /// <summary>
        /// The header fields of this part.
        /// </summary>
        public MimeHeaders              Headers                   { get; }

        /// <summary>
        /// Its Content-Type, or the default where it has none.
        /// </summary>
        public MimeContentType          ContentType               { get; }

        /// <summary>
        /// Its Content-Disposition, or null.
        /// </summary>
        public MimeContentDisposition?  ContentDisposition        { get; }

        /// <summary>
        /// Its Content-Transfer-Encoding, lower case; "7bit" where it has none.
        /// </summary>
        public String                   ContentTransferEncoding   { get; }

        /// <summary>
        /// Its Content-ID, without the angle brackets, or null: what a cid: URL in an
        /// HTML part refers to.
        /// </summary>
        public String?                  ContentId                 { get; }

        /// <summary>
        /// The whole entity as it arrived - header fields and body: what a
        /// multipart/signed signature is made over.
        /// </summary>
        public ReadOnlyMemory<Byte>     Raw                       { get; }

        /// <summary>
        /// The body as it arrived, still transfer-encoded.
        /// </summary>
        public ReadOnlyMemory<Byte>     RawBody                   { get; }

        /// <summary>
        /// The parts of a multipart, in their order; empty otherwise.
        /// </summary>
        public IReadOnlyList<MimePart>  Children                  { get; internal set; } = [];

        /// <summary>
        /// The message a message/rfc822 part carries, or null.
        /// </summary>
        public MimeMessage?             EmbeddedMessage           { get; internal set; }

        /// <summary>
        /// The multipart this part is in, or null at the top.
        /// </summary>
        public MimePart?                Parent                    { get; internal set; }

        /// <summary>
        /// The file name the sender gave it: Content-Disposition's filename, or
        /// Content-Type's name - decoded either way.
        /// </summary>
        public String?                  FileName
            => ContentDisposition?.FileName ?? ContentType.Name;

        /// <summary>
        /// Whether this is a resource an HTML part shows inline - an image of a
        /// multipart/related with a Content-ID - rather than something to list.
        /// </summary>
        public Boolean                  IsInlineResource
            => ContentId is not null &&
               Parent is not null    && Parent.ContentType.Is("multipart/related") &&
               !(ContentDisposition?.IsAttachment ?? false) &&
               !ContentType.Is("text/*");

        /// <summary>
        /// Whether this is part of the machinery of PGP/MIME (RFC 3156) - the
        /// signature of a multipart/signed, the version part of a multipart/encrypted -
        /// rather than something its sender attached.
        /// </summary>
        public Boolean                  IsPgpControl
            => ContentType.Is("application/pgp-signature") ||
               ContentType.Is("application/pgp-encrypted") ||
               Parent is not null && Parent.ContentType.Is("multipart/encrypted");

        /// <summary>
        /// Whether this is something to list as an attachment: what the sender marked
        /// as one, a forwarded message, and anything that is neither text to show nor
        /// a container nor PGP machinery nor an inline resource.
        /// </summary>
        public Boolean                  IsAttachment
        {
            get
            {

                // Containers, PGP machinery and the images an HTML part shows inline.
                if (ContentType.IsMultipart || IsPgpControl || IsInlineResource)
                    return false;

                // What the sender marked as an attachment, and forwarded messages.
                if (ContentDisposition?.IsAttachment ?? false)
                    return true;

                if (ContentType.Is("message/rfc822") || ContentType.Is("message/global"))
                    return true;

                // Text and HTML are to be shown - unless they come with a file name,
                // which is how a .txt or .html is attached.
                if (ContentType.Is("text/plain") || ContentType.Is("text/html"))
                    return FileName is not null;

                // The report parts of a bounce or a read receipt belong to its text.
                if (ContentType.Is("message/*") || ContentType.Is("text/rfc822-headers"))
                    return false;

                // Everything else - images, PDFs, calendars, archives - is one.
                return true;

            }
        }

        #endregion

        #region Constructor(s)

        internal MimePart(String                PartId,
                          MimeHeaders           Headers,
                          MimeContentType       ContentType,
                          ReadOnlyMemory<Byte>  Raw,
                          ReadOnlyMemory<Byte>  RawBody)
        {

            this.PartId                   = PartId;
            this.Headers                  = Headers;
            this.ContentType              = ContentType;
            this.ContentDisposition       = MimeContentDisposition.Parse(Headers.Get("Content-Disposition"));
            this.ContentTransferEncoding  = Headers.Get("Content-Transfer-Encoding")?.Trim().ToLowerInvariant() ?? "7bit";
            this.ContentId                = Headers.Get("Content-ID")?.Trim().Trim('<', '>').Trim();
            this.Raw                      = Raw;
            this.RawBody                  = RawBody;

        }

        #endregion


        #region GetContent() / GetText()

        /// <summary>
        /// The body with its transfer encoding undone: the octets of the image, the
        /// PDF, the text in its charset.
        /// </summary>
        public Byte[] GetContent()
            => TransferEncodings.Decode(RawBody.Span, ContentTransferEncoding);

        /// <summary>
        /// The body as text: its transfer encoding undone, decoded in its charset -
        /// see <see cref="MimeCharsets.Decode"/> for when that is missing or wrong.
        /// </summary>
        public String GetText()
            => MimeCharsets.Decode(GetContent(), ContentType.Charset);

        #endregion

        #region Descendants()

        /// <summary>
        /// This part and every part below it, depth first, in their order - not into
        /// the parts of an embedded message, which is one thing of its own.
        /// </summary>
        public IEnumerable<MimePart> Descendants()
        {

            yield return this;

            foreach (var child in Children)
                foreach (var part in child.Descendants())
                    yield return part;

        }

        #endregion

        #region (override) ToString()

        public override String ToString()
            => $"{(PartId.Length == 0 ? "(top)" : PartId)} {ContentType}{(FileName is not null ? $" \"{FileName}\"" : "")}";

        #endregion

    }


    /// <summary>
    /// A mail as a reader sees it: parsed from its octets into header fields - the
    /// ones a mail program shows decoded - and a tree of MIME parts, with the text
    /// and the HTML to show and the attachments to list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parser reads octets, not text: every part keeps the octets it arrived as
    /// (<see cref="MimePart.Raw"/>), which is what a PGP/MIME signature is made
    /// over, and text is decoded only where it is text - header fields as UTF-8 when
    /// they are and Windows-1252 when they are not, bodies in their charset.
    /// </para>
    /// <para>
    /// It is lenient where mail is broken in practice - a boundary that is never
    /// closed, a header without a colon, LF where CR LF belongs, a Content-Type it
    /// cannot read - and strict where leniency would be an attack: multiparts nest
    /// at most <see cref="MaxDepth"/> deep, and a message has at most
    /// <see cref="MaxParts"/> parts; beyond either, a part is kept as it is and not
    /// looked into.
    /// </para>
    /// <para>
    /// It is the reading half of Hermod's mail model; <see cref="EMail"/> and its
    /// builders are the writing half.
    /// </para>
    /// </remarks>
    public sealed class MimeMessage
    {

        #region Data

        /// <summary>
        /// How deep multiparts and embedded messages nest at most.
        /// </summary>
        public const Int32 MaxDepth  = 32;

        /// <summary>
        /// How many parts a message has at most.
        /// </summary>
        public const Int32 MaxParts  = 2000;

        private static readonly Regex MessageIdPattern = new (@"<([^<>\s]+)>", RegexOptions.CultureInvariant);

        private sealed class ParseState
        {
            public Int32 Parts;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The message as an entity: its header fields are the message's, and it is the
        /// top of the tree of parts.
        /// </summary>
        public MimePart                       Root          { get; }

        /// <summary>
        /// The header fields of the message.
        /// </summary>
        public MimeHeaders                    Headers
            => Root.Headers;

        /// <summary>
        /// The subject, its encoded words decoded; "" when there is none.
        /// </summary>
        public String                         Subject       { get; }

        public IReadOnlyList<MailboxAddress>  From          { get; }
        public IReadOnlyList<MailboxAddress>  Sender        { get; }
        public IReadOnlyList<MailboxAddress>  ReplyTo       { get; }
        public IReadOnlyList<MailboxAddress>  To            { get; }
        public IReadOnlyList<MailboxAddress>  Cc            { get; }
        public IReadOnlyList<MailboxAddress>  Bcc           { get; }

        /// <summary>
        /// The Date: of the message, or null when it has none that can be read.
        /// </summary>
        public DateTimeOffset?                Date          { get; }

        /// <summary>
        /// The Message-ID, without its angle brackets, or null.
        /// </summary>
        public String?                        MessageId     { get; }

        /// <summary>
        /// The In-Reply-To message ids, without their angle brackets.
        /// </summary>
        public IReadOnlyList<String>          InReplyTo     { get; }

        /// <summary>
        /// The References message ids, oldest first, without their angle brackets.
        /// </summary>
        public IReadOnlyList<String>          References    { get; }

        /// <summary>
        /// The List-Id of a mailing list message (RFC 2919), or null.
        /// </summary>
        public ListId?                        ListId        { get; }

        /// <summary>
        /// The plain text to show, or null.
        /// </summary>
        public MimePart?                      TextBody      { get; }

        /// <summary>
        /// The HTML to show, or null.
        /// </summary>
        public MimePart?                      HtmlBody      { get; }

        /// <summary>
        /// The attachments, in their order.
        /// </summary>
        public IReadOnlyList<MimePart>        Attachments   { get; }

        /// <summary>
        /// "encrypted" or "signed" for a PGP/MIME message (RFC 3156), null otherwise.
        /// </summary>
        public String?                        Pgp           { get; }

        #endregion

        #region Constructor(s)

        private MimeMessage(MimePart Root)
        {

            this.Root        = Root;

            var headers      = Root.Headers;

            this.Subject     = headers.GetText("Subject")?.Trim() ?? "";
            this.From        = MailboxAddress.ParseList(headers.Get("From"));
            this.Sender      = MailboxAddress.ParseList(headers.Get("Sender"));
            this.ReplyTo     = MailboxAddress.ParseList(headers.Get("Reply-To"));
            this.To          = MailboxAddress.ParseList(String.Join(", ", headers.GetAll("To")));
            this.Cc          = MailboxAddress.ParseList(String.Join(", ", headers.GetAll("Cc")));
            this.Bcc         = MailboxAddress.ParseList(String.Join(", ", headers.GetAll("Bcc")));
            this.Date        = MimeDate.TryParse(headers.Get("Date"), out var date) ? date : null;
            this.MessageId   = MessageIds(headers.Get("Message-ID")).FirstOrDefault() ?? NullIfEmpty(headers.Get("Message-ID")?.Trim());
            this.InReplyTo   = MessageIds(headers.Get("In-Reply-To"));
            this.References  = MessageIds(headers.Get("References"));
            this.ListId      = headers.Get("List-Id") is { } listId && Mail.ListId.TryParse(listId, out var parsed) ? parsed : null;

            MimePart? text = null, html = null;
            FindBodies(Root, ref text, ref html);

            this.TextBody    = text;
            this.HtmlBody    = html;
            this.Attachments = [.. Root.Descendants().Where(part => part.IsAttachment)];

            this.Pgp         = PgpOf(Root) ?? Root.Children.Select(PgpOf).FirstOrDefault(pgp => pgp is not null);

        }

        #endregion


        #region (static) Parse(Raw)

        /// <summary>
        /// Parse a mail from its octets.
        /// </summary>
        public static MimeMessage Parse(ReadOnlyMemory<Byte> Raw)
            => Parse(Raw, "", 0, new ParseState());

        private static MimeMessage Parse(ReadOnlyMemory<Byte>  Raw,
                                         String                Prefix,
                                         Int32                 Depth,
                                         ParseState            State)
        {

            // The top of a message is part "1" (or Prefix.1) when it is not a multipart,
            // and the container Prefix when it is - what IMAP calls its sections.
            var (headers, body) = SplitEntity(Raw);
            var contentType     = MimeContentType.Parse(headers.Get("Content-Type"), MimeContentType.TextPlain);

            var partId          = contentType.IsMultipart
                                      ? Prefix
                                      : Prefix.Length == 0 ? "1" : $"{Prefix}.1";

            var root            = ParseEntity(Raw, headers, body, contentType, partId, Depth, State);

            return new MimeMessage(root);

        }

        #endregion

        #region (private static) ParseEntity(...)

        private static MimePart ParseEntity(ReadOnlyMemory<Byte>  Raw,
                                            MimeHeaders           Headers,
                                            ReadOnlyMemory<Byte>  Body,
                                            MimeContentType       ContentType,
                                            String                PartId,
                                            Int32                 Depth,
                                            ParseState            State)
        {

            State.Parts++;

            var part = new MimePart(PartId, Headers, ContentType, Raw, Body);

            if (Depth >= MaxDepth || State.Parts > MaxParts)
                return part;

            if (ContentType.IsMultipart && ContentType.Boundary is { Length: > 0 } boundary)
            {

                var childDefault = ContentType.SubType == "digest"
                                       ? MimeContentType.MessageRFC822
                                       : MimeContentType.TextPlain;

                var children     = new List<MimePart>();
                var index        = 0;

                foreach (var slice in SplitMultipart(Body, boundary))
                {

                    if (State.Parts >= MaxParts)
                        break;

                    var (childHeaders, childBody) = SplitEntity(slice);
                    var childType                 = MimeContentType.Parse(childHeaders.Get("Content-Type"), childDefault);
                    var childId                   = PartId.Length == 0 ? $"{++index}" : $"{PartId}.{++index}";

                    var child                     = ParseEntity(slice, childHeaders, childBody, childType, childId, Depth + 1, State);
                    child.Parent                  = part;

                    children.Add(child);

                }

                part.Children = children;

            }

            else if (ContentType.Is("message/rfc822") || ContentType.Is("message/global"))
                part.EmbeddedMessage = Parse(part.GetContent(), PartId, Depth + 1, State);

            return part;

        }

        #endregion

        #region (private static) SplitEntity(Raw)

        /// <summary>
        /// Header block and body: they are separated by the first empty line - CR LF
        /// CR LF, or LF LF where line ends were lost on the way.
        /// </summary>
        private static (MimeHeaders Headers, ReadOnlyMemory<Byte> Body) SplitEntity(ReadOnlyMemory<Byte> Raw)
        {

            var span = Raw.Span;

            // No header fields at all: the entity starts with its empty line.
            if (span.StartsWith("\r\n"u8))
                return (MimeHeaders.Empty, Raw[2..]);

            if (span.StartsWith("\n"u8))
                return (MimeHeaders.Empty, Raw[1..]);

            var crlf  = span.IndexOf("\r\n\r\n"u8);
            var lf    = span.IndexOf("\n\n"u8);

            Int32 headerEnd, bodyStart;

            if (crlf >= 0 && (lf < 0 || crlf <= lf))
            {
                headerEnd = crlf;
                bodyStart = crlf + 4;
            }
            else if (lf >= 0)
            {
                headerEnd = lf;
                bodyStart = lf + 2;
            }
            else
            {
                headerEnd = span.Length;
                bodyStart = span.Length;
            }

            return (MimeHeaders.Parse(MimeCharsets.DecodeUnknown(span[..headerEnd])), Raw[bodyStart..]);

        }

        #endregion

        #region (private static) SplitMultipart(Body, Boundary)

        /// <summary>
        /// The parts of a multipart body (RFC 2046 §5.1.1): what lies between the
        /// delimiter lines "--boundary", up to "--boundary--". The line break before a
        /// delimiter belongs to the delimiter, not to the part. Preamble and epilogue
        /// are no parts; a multipart whose closing delimiter is missing ends at the end.
        /// </summary>
        private static List<ReadOnlyMemory<Byte>> SplitMultipart(ReadOnlyMemory<Byte> Body, String Boundary)
        {

            var parts      = new List<ReadOnlyMemory<Byte>>();
            var span       = Body.Span;
            var delimiter  = System.Text.Encoding.ASCII.GetBytes("--" + Boundary);
            var position   = 0;
            Int32? start   = null;

            while (position <= span.Length)
            {

                var lineBreak  = position < span.Length ? span[position..].IndexOf((Byte) '\n') : -1;
                var lineEnd    = lineBreak < 0 ? span.Length : position + lineBreak;
                var line       = span[position..lineEnd];

                if (line.Length > 0 && line[^1] == '\r')
                    line = line[..^1];

                if (line.StartsWith(delimiter))
                {

                    var rest     = line[delimiter.Length..];
                    var closing  = rest.StartsWith("--"u8);

                    if (closing)
                        rest = rest[2..];

                    if (IsWhiteSpace(rest))
                    {

                        if (start is { } partStart)
                        {

                            var partEnd = position;

                            if (partEnd > partStart && span[partEnd - 1] == '\n')
                                partEnd--;

                            if (partEnd > partStart && span[partEnd - 1] == '\r')
                                partEnd--;

                            parts.Add(Body[partStart..partEnd]);

                        }

                        if (closing)
                            return parts;

                        start = Math.Min(lineEnd + 1, span.Length);

                    }

                }

                if (lineBreak < 0)
                    break;

                position = lineEnd + 1;

            }

            if (start is { } lastStart && lastStart < span.Length)
                parts.Add(Body[lastStart..]);

            return parts;

        }

        private static Boolean IsWhiteSpace(ReadOnlySpan<Byte> Octets)
        {

            foreach (var octet in Octets)
                if (octet is not ((Byte) ' ' or (Byte) '\t'))
                    return false;

            return true;

        }

        #endregion

        #region (private static) FindBodies(Part, ref Text, ref Html)

        /// <summary>
        /// The text and the HTML a mail program would show: of an alternative the last
        /// one of each kind - the sender's preference - of a related its root, of a
        /// signed its content, of anything else the first that is not an attachment.
        /// An encrypted message has none: its content is the ciphertext.
        /// </summary>
        private static void FindBodies(MimePart       Part,
                                       ref MimePart?  Text,
                                       ref MimePart?  Html)
        {

            var type = Part.ContentType;

            if (type.Is("multipart/encrypted"))
                return;

            if (type.Is("multipart/alternative"))
            {

                MimePart? text = null, html = null;

                foreach (var child in Part.Children.Reverse())
                {
                    if (text is not null && html is not null)
                        break;
                    FindBodies(child, ref text, ref html);
                }

                Text ??= text;
                Html ??= html;
                return;

            }

            if (type.Is("multipart/related"))
            {

                var start = type.Parameters.GetValueOrDefault("start")?.Trim().Trim('<', '>');
                var root  = (start is not null ? Part.Children.FirstOrDefault(child => child.ContentId == start) : null)
                                ?? Part.Children.FirstOrDefault();

                if (root is not null)
                    FindBodies(root, ref Text, ref Html);

                return;

            }

            if (type.Is("multipart/signed"))
            {
                if (Part.Children.Count > 0)
                    FindBodies(Part.Children[0], ref Text, ref Html);
                return;
            }

            if (type.IsMultipart)
            {
                foreach (var child in Part.Children)
                {
                    if (Text is not null && Html is not null)
                        break;
                    FindBodies(child, ref Text, ref Html);
                }
                return;
            }

            if (Part.IsAttachment)
                return;

            if (type.Is("text/plain"))
                Text ??= Part;

            else if (type.Is("text/html"))
                Html ??= Part;

        }

        #endregion

        #region (private static) PgpOf(Part)

        private static String? PgpOf(MimePart Part)
        {

            var protocol = Part.ContentType.Parameters.GetValueOrDefault("protocol")?.Trim().ToLowerInvariant();

            if (Part.ContentType.Is("multipart/encrypted") && protocol == "application/pgp-encrypted")
                return "encrypted";

            if (Part.ContentType.Is("multipart/signed")    && protocol == "application/pgp-signature")
                return "signed";

            return null;

        }

        #endregion


        #region FindPart(PartId) / FindByContentId(ContentId)

        /// <summary>
        /// The part with the given id, looking into embedded messages too, or null.
        /// </summary>
        public MimePart? FindPart(String PartId)
        {

            foreach (var part in Root.Descendants())
            {

                if (part.PartId == PartId)
                    return part;

                if (part.EmbeddedMessage is { } embedded &&
                    PartId.StartsWith(part.PartId + ".", StringComparison.Ordinal) &&
                    embedded.FindPart(PartId) is { } inner)
                {
                    return inner;
                }

            }

            return null;

        }

        /// <summary>
        /// The part with the given Content-ID - what cid: refers to - or null.
        /// </summary>
        public MimePart? FindByContentId(String ContentId)
        {
            var id = ContentId.Trim().Trim('<', '>');
            return Root.Descendants().FirstOrDefault(part => part.ContentId is not null && part.ContentId.Equals(id, StringComparison.Ordinal));
        }

        #endregion

        #region (private static) MessageIds(Value) / NullIfEmpty(Text)

        private static IReadOnlyList<String> MessageIds(String? Value)

            => Value is null
                   ? []
                   : [.. MessageIdPattern.Matches(Value).Select(match => match.Groups[1].Value)];

        private static String? NullIfEmpty(String? Text)
            => String.IsNullOrEmpty(Text) ? null : Text;

        #endregion

    }

}
