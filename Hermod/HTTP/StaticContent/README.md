# Static content and single-page applications

Delivery of a static content bundle over the HTTP/1.1 server: the output of a
frontend bundler such as webpack, embedded into an assembly or read from a
directory on disk, with the things a browser expects from a static file server
and that Hermod's HTTP/1.1 core deliberately leaves to the application.

```csharp
var httpServer = await HTTPServer.StartNew(IPv4Address.Parse("127.0.0.1"), IPPort.Parse(8080));

// A JSON API in its own HTTPAPI: the server dispatches to the most specific
// API first, so unknown API paths never reach the SPA fallback.
var api = httpServer.AddHTTPAPI(HTTPPath.Parse("/api"));
api.AddHandler(HTTPPath.Root + "v1/status", HTTPMethod: HTTPMethod.GET, HTTPDelegate: ...);

// The web frontend at "/".
var web = httpServer.AddHTTPAPI();

web.MapSinglePageApplication(
    new EmbeddedContentSource("com.example.Website.HTTPRoot.", typeof(Program).Assembly),
    new SinglePageAppOptions {
        IndexTransform   = html => html.Replace("{{Version}}", version),
        SecurityHeaders  = SecurityHeaderOptions.Default with {
                               StrictTransportSecurity = usesTLS ? SecurityHeaderOptions.DefaultStrictTransportSecurity : null
                           }
    }
);
```

## Routing

`MapSinglePageApplication` registers `GET` and `HEAD` for the API root and for
the catch-all `{path..}`. Every request is answered by one rule:

| Request | Answer |
|---|---|
| a file of the bundle (`/assets/app.<hash>.js`, `/favicon.svg`) | the file |
| the stub itself (`/index.html`) | the stub, transformed |
| any other path without a file extension (`/`, `/devices/42`) | the stub with status 200, so that deep links and reloads work with a client-side router |
| any other path with a file extension (`/assets/missing.js`) | `404 Not Found`, never the stub |

Literal routes on the same `HTTPAPI` still win over the catch-all, but unknown
paths below them end up in the fallback; an API belongs in its own `HTTPAPI`.

## Content sources

- `EmbeddedContentSource(prefix, assembly)`: manifest resources. The URL path
  `assets/app.js` maps onto the resource name `<prefix>assets.app.js`, which
  MSBuild produces for an `EmbeddedResource` with a matching `LogicalName`
  (or by default when the directory names are plain identifiers). Directory
  names must not contain dots. Files are read once and cached.
- `FileSystemContentSource(directory)`: files on disk, read on every request,
  for development with a bundler in watch mode. Resolved paths must stay below
  the root directory; `IsImmutable` is false, so everything is `no-cache`.

Both go through `StaticPath.TryNormalize`, which rejects dot segments,
backslashes and empty segments as a second line of defence behind the
server's request-target validation.

## Headers

| Concern | Behaviour |
|---|---|
| Content type | `HTTPContentType.ForFileName` from the file extension; `application/octet-stream` for unknown ones |
| Entity tags | strong `ETag` from the content (SHA-256); `If-None-Match` answered with `304 Not Modified` (weak comparison, lists, `*`) |
| Cache-Control | stub `no-cache`; file names with a content hash `public, max-age=31536000, immutable`; other files one hour; everything `no-cache` for a mutable source; all three configurable |
| Content coding | text-like content of `MinimumCompressionSize` (1 KiB) or more as Brotli or gzip according to `Accept-Encoding` (quality values, `*`); encoded representations get their own `ETag` (`"…-br"`) and `Vary: Accept-Encoding`; fonts and images stay as they are; encodings are cached per entity tag |
| Security | `X-Content-Type-Options: nosniff` on every response; HTML documents additionally get `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy` and `X-Frame-Options`; `Strict-Transport-Security` only when set, i.e. over TLS |

`ContentNegotiation` and `SecurityHeaderExtensions` are public, so other
handlers can use the same negotiation and the same header set.

The content coding uses `HTTPContentCoding` from the HTTP/2 core, which is
independent of the HTTP version.
