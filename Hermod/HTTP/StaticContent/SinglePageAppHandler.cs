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
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// How a single-page application is delivered.
    /// </summary>
    public sealed record SinglePageAppOptions
    {

        #region (static) DefaultAssetExtensions

        /// <summary>
        /// What the last segment of a URL may end in for the request to be
        /// taken for a file of the bundle rather than for a page of the
        /// application. See <see cref="AssetExtensions"/>.
        /// </summary>
        /// <remarks>
        /// Generous about what a bundle ships, and deliberately silent about a
        /// handful of extensions that would otherwise be right: ".zip", ".mov",
        /// ".app", ".dev", ".page" and ".box" are all top-level domains, and a
        /// path segment ending in one of those is far more often a name than a
        /// file - "/chats/alice@example.zip" is a conversation.
        ///
        /// The cost of each mistake is not the same, which is what this list is
        /// weighted for. A kind of file that is missing here and goes missing
        /// on the server answers with the stub instead of a 404 - untidy. A
        /// kind of file that is here by mistake turns every page URL ending in
        /// it into a 404 - broken.
        /// </remarks>
        public static readonly IReadOnlySet<String> DefaultAssetExtensions =
            new HashSet<String>(StringComparer.OrdinalIgnoreCase) {

                // Code and data
                "js", "mjs", "cjs", "css", "map", "json", "wasm", "xml", "xsl",
                "csv", "txt", "webmanifest", "manifest", "atom", "rss",

                // Documents of the bundle itself
                "html", "htm", "pdf",

                // Pictures
                "png", "jpg", "jpeg", "gif", "webp", "avif", "bmp", "svg", "ico", "cur",

                // Fonts
                "woff", "woff2", "ttf", "otf", "eot",

                // Sound and moving pictures
                "mp4", "m4v", "webm", "ogv", "ogg", "oga", "mp3", "m4a",
                "wav", "opus", "flac", "aac"

            };

        #endregion


        /// <summary>
        /// The application stub within the bundle.
        /// </summary>
        public String                 IndexFile                 { get; init; } = "index.html";

        /// <summary>
        /// Which URLs are taken for files of the bundle when the bundle does
        /// not have them: those whose last segment ends in a dot and one of
        /// these extensions. Everything else is a page of the application and
        /// gets the stub.
        /// </summary>
        /// <remarks>
        /// The point of the distinction is that a missing asset must be a real
        /// 404 rather than the stub with status 200 - otherwise a mistyped
        /// script tag hands the browser HTML to execute, and a deployment that
        /// forgot half the bundle looks healthy.
        ///
        /// Guessing from the extension is not perfect and cannot be: a URL is
        /// not obliged to say what it is. It is, however, much closer than
        /// "the last segment contains a dot", which this replaced - that test
        /// called every version number, every e-mail address and every domain
        /// name in a path a file. Set this where a bundle ships something
        /// unusual, or where a page URL of the application ends in one of the
        /// extensions above.
        /// </remarks>
        public IReadOnlySet<String>   AssetExtensions           { get; init; } = DefaultAssetExtensions;

        /// <summary>
        /// An optional transformation of the stub, e.g. for replacing {{placeholders}}
        /// with values only the server knows.
        /// </summary>
        public Func<String, String>?  IndexTransform            { get; init; }

        /// <summary>
        /// The security related header fields to send.
        /// </summary>
        public SecurityHeaderOptions  SecurityHeaders           { get; init; } = SecurityHeaderOptions.Default;

        /// <summary>
        /// Whether to offer Brotli and gzip for compressible content.
        /// </summary>
        public Boolean                Compression               { get; init; } = true;

        /// <summary>
        /// Files smaller than this are sent as they are.
        /// </summary>
        public Int32                  MinimumCompressionSize    { get; init; } = 1024;

        /// <summary>
        /// The Cache-Control for the application stub. It references hashed
        /// assets, so it should be revalidated on every navigation.
        /// </summary>
        public String                 IndexCacheControl         { get; init; } = "no-cache";

        /// <summary>
        /// The Cache-Control for files whose name carries a content hash.
        /// </summary>
        public String                 HashedAssetCacheControl   { get; init; } = "public, max-age=31536000, immutable";

        /// <summary>
        /// The Cache-Control for all other files of an immutable bundle.
        /// </summary>
        public String                 DefaultCacheControl       { get; init; } = "public, max-age=3600";

    }


    /// <summary>
    /// Extension methods for mapping a single-page application onto an HTTP API.
    /// </summary>
    public static class SinglePageApplicationExtensions
    {

        #region MapSinglePageApplication(this HTTPAPI, Content, Options = null)

        /// <summary>
        /// Serve a bundler-style single-page application from the root of the
        /// given HTTP API: files of the bundle are delivered as they are, every
        /// other URL gets the application stub with status 200 (so that deep
        /// links and reloads work), and a URL that names a file the bundle does
        /// not have gets a real 404 - see
        /// <see cref="SinglePageAppOptions.AssetExtensions"/> for where that
        /// line is drawn.
        /// </summary>
        /// <param name="HTTPAPI">The HTTP API, normally the one registered at "/".</param>
        /// <param name="Content">Where the bundle comes from.</param>
        /// <param name="Options">Delivery options; defaults when null.</param>
        public static SinglePageAppHandler MapSinglePageApplication(this HTTPAPI           HTTPAPI,
                                                                    IStaticContentSource   Content,
                                                                    SinglePageAppOptions?  Options   = null)
        {

            var handler   = new SinglePageAppHandler(Content, Options);
            var catchAll  = HTTPPath.Root + $"{{{SinglePageAppHandler.PathParameter}..}}";

            // Literal segments always win over the catch-all within the router,
            // so other handlers registered on the same HTTPAPI keep working.
            // Unknown paths below them end up here, though; an API is better
            // off in its own HTTPAPI, which the server dispatches to first.
            foreach (var method in new[] { HTTPMethod.GET, HTTPMethod.HEAD })
            {
                HTTPAPI.AddHandler(HTTPPath.Root, handler.HandleAsync, HTTPMethod: method);
                HTTPAPI.AddHandler(catchAll,      handler.HandleAsync, HTTPMethod: method);
            }

            return handler;

        }

        #endregion

    }


    /// <summary>
    /// The request handler behind MapSinglePageApplication: static files with
    /// entity tags, conditional requests, content coding and caching policy,
    /// plus the fallback to the application stub.
    /// </summary>
    public sealed partial class SinglePageAppHandler
    {

        #region Data

        /// <summary>
        /// The name of the catch-all URL parameter.
        /// </summary>
        public const String PathParameter = "path";

        // Bundler-style hashed file names, e.g. "assets/app.3f9c1a2b4c5d6e7f8a9b.js"
        [GeneratedRegex(@"\.[0-9a-f]{8,}\.[A-Za-z0-9]+$")]
        private static partial Regex HashedFileName();

        private const Int32 MaxCachedEncodings  = 256;
        private const Int32 MaxCachedIndexes    = 16;

        private readonly ConcurrentDictionary<String, Byte[]>      encodedCache  = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<String, StaticFile>  indexCache    = new(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// Where the bundle comes from.
        /// </summary>
        public IStaticContentSource  Content    { get; }

        /// <summary>
        /// The delivery options.
        /// </summary>
        public SinglePageAppOptions  Options    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new single-page application handler.
        /// </summary>
        /// <param name="Content">Where the bundle comes from.</param>
        /// <param name="Options">Delivery options; defaults when null.</param>
        public SinglePageAppHandler(IStaticContentSource   Content,
                                    SinglePageAppOptions?  Options   = null)
        {
            this.Content  = Content;
            this.Options  = Options ?? new SinglePageAppOptions();
        }

        #endregion


        #region HandleAsync(Request)

        /// <summary>
        /// Handle a GET or HEAD request.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        public Task<HTTPResponse> HandleAsync(HTTPRequest Request)
        {

            var path = Request.TryGetURLParameter(PathParameter)?.Trim('/') ?? "";

            // 1) A file of the bundle?  ("assets/app.3f9c1a.js", "favicon.svg")
            //    The stub itself is always delivered transformed and revalidated.
            if (path.Length > 0 && Content.TryGet(path, out var file))
                return Task.FromResult(
                           String.Equals(path, Options.IndexFile, StringComparison.Ordinal)
                               ? IndexResponse(Request, file)
                               : Deliver(Request, file, CacheControlFor(file), IsDocument: false)
                       );

            // 2) A page URL (the last segment does not end in an extension the
            //    bundle would ship) => the application stub
            if (!LooksLikeAnAsset(path))
            {

                if (Content.TryGet(Options.IndexFile, out var index))
                    return Task.FromResult(IndexResponse(Request, index));

                return Task.FromResult(
                           ErrorResponse(
                               Request,
                               HTTPStatusCode.InternalServerError,
                               $"The single-page application stub '{Options.IndexFile}' is missing in {Content.Description}!"
                           )
                       );

            }

            // 3) A missing asset => a real 404, never the stub with status 200
            return Task.FromResult(
                       ErrorResponse(
                           Request,
                           HTTPStatusCode.NotFound,
                           $"'{Request.Path}' was not found!"
                       )
                   );

        }

        #endregion

        #region (private) LooksLikeAnAsset(Path)

        /// <summary>
        /// Whether this path asks for a file of the bundle - which decides what
        /// a path the bundle does not have is answered with: a 404 for a file,
        /// the application stub for a page.
        /// </summary>
        /// <remarks>
        /// A leading dot is not an extension: "/.well-known/x" names a
        /// directory, and a segment that is nothing but ".gitignore" is a name
        /// as much as a type. A trailing dot is not one either.
        /// </remarks>
        private Boolean LooksLikeAnAsset(String Path)
        {

            var lastSegment  = Path[(Path.LastIndexOf('/') + 1)..];
            var dot          = lastSegment.LastIndexOf('.');

            return dot > 0 &&
                   dot < lastSegment.Length - 1 &&
                   Options.AssetExtensions.Contains(lastSegment[(dot + 1)..]);

        }

        #endregion


        #region (private) IndexResponse(Request, Source)

        private HTTPResponse IndexResponse(HTTPRequest  Request,
                                           StaticFile   Source)
        {

            // The transformation is deterministic, so the result is cached by
            // the entity tag of its source; a rebuilt stub gets a new entry.
            if (indexCache.Count > MaxCachedIndexes)
                indexCache.Clear();

            var index = indexCache.GetOrAdd(Source.ETag, _ => Transform(Source));

            return Deliver(Request, index, Options.IndexCacheControl, IsDocument: true);

        }

        private StaticFile Transform(StaticFile Source)
        {

            if (Options.IndexTransform is null)
                return Source;

            var html = Options.IndexTransform(Encoding.UTF8.GetString(Source.Content));

            return StaticFile.Create(Source.RelativePath, Encoding.UTF8.GetBytes(html));

        }

        #endregion

        #region (private) Deliver(Request, File, CacheControl, IsDocument)

        /// <summary>
        /// The common delivery path: pick a content coding, answer 304 when the
        /// client already has this representation, otherwise send it with the
        /// caching and security header fields.
        /// </summary>
        private HTTPResponse Deliver(HTTPRequest  Request,
                                     StaticFile   File,
                                     String       CacheControl,
                                     Boolean      IsDocument)
        {

            var compressible  = Options.Compression && ContentNegotiation.IsCompressible(File.ContentType);
            var coding        = compressible && File.Content.Length >= Options.MinimumCompressionSize
                                    ? ContentNegotiation.SelectContentCoding(Request.AcceptEncoding)
                                    : null;

            var body          = coding is null
                                    ? File.Content
                                    : Encoded(File, coding);

            // Not worth it: fall back to the identity representation.
            if (coding is not null && body.Length >= File.Content.Length)
            {
                coding  = null;
                body    = File.Content;
            }

            var etag = ContentNegotiation.ETagForCoding(File.ETag, coding);

            if (ContentNegotiation.IfNoneMatchMatches(Request.IfNoneMatch, etag))
            {

                var notModified = new HTTPResponse.Builder(Request) {
                                      HTTPStatusCode  = HTTPStatusCode.NotModified,
                                      ETag            = etag,
                                      CacheControl    = CacheControl,
                                      Vary            = compressible ? "Accept-Encoding" : null
                                  };

                return notModified.WithCommonSecurityHeaders(Options.SecurityHeaders).AsImmutable;

            }

            var response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = File.ContentType,
                               Content         = body,
                               ETag            = etag,
                               CacheControl    = CacheControl,
                               Vary            = compressible ? "Accept-Encoding" : null
                           };

            if (coding is not null)
                response.SetHeaderField("Content-Encoding", coding);

            if (IsDocument)
                response.WithDocumentSecurityHeaders(Options.SecurityHeaders);
            else
                response.WithCommonSecurityHeaders(Options.SecurityHeaders);

            return response.AsImmutable;

        }

        #endregion

        #region (private) Encoded(File, Coding)

        /// <summary>
        /// The Brotli or gzip representation of a file, computed once per
        /// content: the cache key is the entity tag, so a changed file on
        /// disk in development mode gets a fresh entry.
        /// </summary>
        private Byte[] Encoded(StaticFile  File,
                               String      Coding)
        {

            if (encodedCache.Count > MaxCachedEncodings)
                encodedCache.Clear();

            return encodedCache.GetOrAdd(
                       $"{File.ETag}|{Coding}",
                       _ => HTTPContentCoding.Encode(File.Content, Coding)
                   );

        }

        #endregion

        #region (private) ErrorResponse(Request, StatusCode, Message)

        private HTTPResponse ErrorResponse(HTTPRequest     Request,
                                           HTTPStatusCode  StatusCode,
                                           String          Message)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = StatusCode,
                   ContentType     = HTTPContentType.Text.PLAIN,
                   Content         = Encoding.UTF8.GetBytes($"{StatusCode.Code} {StatusCode.Name}: {Message}"),
                   CacheControl    = "no-cache"
               }.WithCommonSecurityHeaders(Options.SecurityHeaders).AsImmutable;

        #endregion

        #region (private) CacheControlFor(File)

        private String CacheControlFor(StaticFile File)
        {

            if (!Content.IsImmutable)
                return "no-cache";

            if (HashedFileName().IsMatch(File.RelativePath))
                return Options.HashedAssetCacheControl;

            return Options.DefaultCacheControl;

        }

        #endregion

    }

}
