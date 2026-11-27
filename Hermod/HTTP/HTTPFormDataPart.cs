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

    using System.Text;


    /// <summary>
    /// One part of a multipart/form-data body (RFC 7578, Section 4): a form
    /// field, or a file, under the name of the form field it came from.
    ///
    /// Several parts may share a name, which is how several files of one field
    /// are sent (Section 4.3), and how a field a form repeats arrives; their
    /// order is the form's (Section 5.2).
    /// </summary>
    public sealed class HTTPFormDataPart
    {

        #region Data

        /// <summary>
        /// application/octet-stream as browsers send it for a file of no known
        /// type: without the charset the registered instance carries.
        /// </summary>
        private static readonly HTTPContentType OctetStream = new ("application", "octet-stream", null, null, null);

        #endregion

        #region Properties

        /// <summary>
        /// The name of the form field, the Content-Disposition's "name" parameter
        /// as it was sent. Section 5.1 leaves its encoding open; browsers send
        /// UTF-8, with a CR, an LF and a quotation mark percent-encoded, and that
        /// is not undone here.
        /// </summary>
        public String                                       Name           { get; }

        /// <summary>
        /// The file name, the "filename" parameter as it was sent, when the part
        /// is a file; an empty file name is the one a browser sends for a file
        /// input left empty. Advisory, and not to be stored under as it is: see
        /// <see cref="SafeFileName"/>. "filename*" is ignored: Section 4.2
        /// forbids it.
        /// </summary>
        public String?                                      FileName       { get; }

        /// <summary>
        /// The file name made fit to store under, by the rules of
        /// <see cref="HTTPContentDisposition.SafeFileName"/>, which follow
        /// RFC 6266, Section 4.3, as RFC 7578, Section 4.2 asks: the last path
        /// segment only, no control characters, no character Windows refuses,
        /// no device name. Null when nothing usable is left, or the part is no
        /// file.
        /// </summary>
        public String?                                      SafeFileName
        {
            get
            {

                // HTTPContentDisposition takes no empty file name and none with
                // a control character, and a quoted string may hold a tab.
                var name = FileName is not null
                               ? new String(FileName.Where(character => !Char.IsControl(character)).ToArray())
                               : null;

                return name is { Length: > 0 }
                           ? HTTPContentDisposition.Attachment(name).SafeFileName
                           : null;

            }
        }

        /// <summary>
        /// The part's Content-Type, when it has one. Without one it is
        /// "text/plain" (Section 4.4).
        /// </summary>
        public HTTPContentType?                             ContentType    { get; }

        /// <summary>
        /// Every header field of the part, in the order sent. Section 4.8 makes
        /// any other than Content-Disposition, Content-Type and
        /// Content-Transfer-Encoding something a recipient MUST ignore; they are
        /// kept here for what a log wants, and mean nothing.
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, String>>  Headers        { get; }

        /// <summary>
        /// The content, the octets between the part's header section and the
        /// next delimiter. A Content-Transfer-Encoding, which Section 4.7 has
        /// senders never write, is not undone.
        /// </summary>
        public Byte[]                                       Content        { get; }

        /// <summary>
        /// Whether the part is a file rather than a field.
        /// </summary>
        public Boolean                                      IsFile
            => FileName is not null;

        /// <summary>
        /// The charset its text is in: the Content-Type's, else the form's
        /// "_charset_" field's (Section 4.6), else UTF-8.
        /// </summary>
        public String                                       Charset        { get; }

        /// <summary>
        /// The content as text, in <see cref="Charset"/>. Null when that charset
        /// is not one .NET knows.
        /// </summary>
        public String?                                      Text
        {
            get
            {

                Encoding encoding;

                try
                {
                    encoding = Encoding.GetEncoding(Charset);
                }
                catch (ArgumentException)
                {
                    return null;
                }

                return encoding.GetString(Content);

            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A form field with a text value, sent in UTF-8.
        /// </summary>
        /// <param name="Name">The name of the form field.</param>
        /// <param name="Value">Its value.</param>
        public HTTPFormDataPart(String  Name,
                                String  Value)

            : this(Name,
                   null,
                   null,
                   [],
                   Encoding.UTF8.GetBytes(Value ?? throw new ArgumentNullException(nameof(Value))),
                   "utf-8")

        { }

        /// <summary>
        /// A file.
        /// </summary>
        /// <param name="Name">The name of the form field.</param>
        /// <param name="FileName">The file name.</param>
        /// <param name="Content">The file's octets.</param>
        /// <param name="ContentType">Its media type; application/octet-stream when not given, as Section 4.4 suggests for a type not known.</param>
        public HTTPFormDataPart(String            Name,
                                String            FileName,
                                Byte[]            Content,
                                HTTPContentType?  ContentType   = null)

            : this(Name,
                   FileName ?? throw new ArgumentNullException(nameof(FileName)),
                   ContentType ?? OctetStream,
                   [],
                   Content  ?? throw new ArgumentNullException(nameof(Content)),
                   ContentType?.CharSet ?? "utf-8")

        { }

        internal HTTPFormDataPart(String                                       Name,
                                  String?                                      FileName,
                                  HTTPContentType?                             ContentType,
                                  IReadOnlyList<KeyValuePair<String, String>>  Headers,
                                  Byte[]                                       Content,
                                  String                                       Charset)
        {

            this.Name         = Name ?? throw new ArgumentNullException(nameof(Name));
            this.FileName     = FileName;
            this.ContentType  = ContentType;
            this.Headers      = Headers;
            this.Content      = Content;
            this.Charset      = Charset;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => FileName is not null
                   ? $"{Name}: file '{FileName}', {Content.Length} octets"
                   : $"{Name}: {Content.Length} octets";

        #endregion

    }

}
