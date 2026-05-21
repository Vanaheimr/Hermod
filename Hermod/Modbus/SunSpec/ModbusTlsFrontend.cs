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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Modbus/TLS (mbaps) frontend. Terminates TLS, enforces mutual auth against a
/// pinned CA, extracts the SunSpec role from the client cert, applies the RBAC
/// policy, then delegates execution to a pluggable <see cref="IModbusBackend"/>.
///
/// There is NO unencrypted listener. If a peer fails the TLS handshake, doesn't
/// present a valid client cert, or its cert lacks a known role, the connection is
/// dropped before any Modbus byte is forwarded to the backend.
/// </summary>
public sealed class ModbusTlsFrontend : IDisposable
{

    private readonly ModbusTlsFrontendOptions  _opts;
    private readonly IModbusBackendFactory     _backendFactory;
    private readonly AuthorizationPolicy       _authz;
    private readonly ILogger                   _logger;
    private readonly TcpListener               _listener;
    private readonly CertificateBinding        _defaultBinding;
    private readonly Dictionary<String, CertificateBinding> _sniBindings;
    private readonly CancellationTokenSource   _cts = new();

    private long                               _connectionCounter;

    public ModbusTlsFrontend(ModbusTlsFrontendOptions     opts,
                             IModbusBackendFactory        backendFactory,
                             AuthorizationPolicy          authz,
                             ILogger<ModbusTlsFrontend>?  logger   = null)
    {

        _opts               = opts;
        _backendFactory     = backendFactory;
        _authz              = authz;
        _logger             = (ILogger?)logger ?? NullLogger.Instance;
        _defaultBinding     = LoadBinding(null, opts.ServerPfxPath, opts.ServerPfxPassword, opts.CaCertPath);
        _sniBindings        = opts.SNIBindings?.
                                   ToDictionary(
                                       binding => NormalizeServerName(binding.ServerName),
                                       binding => LoadBinding(binding.ServerName, binding.ServerPfxPath, binding.ServerPfxPassword, binding.CaCertPath)
                                   ) ??
                               [];

        if (!_defaultBinding.ServerCertWithKey.HasPrivateKey)
            throw new InvalidOperationException("Server cert PFX has no private key.");

        _listener           = new TcpListener(opts.ListenAddress, opts.ListenPort);

    }

    public async Task RunAsync(CancellationToken externalCt = default)
    {

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt, _cts.Token);

        var ct = linked.Token;

        _listener.Start();
        _logger.LogInformation("mbaps frontend listening on {Endpoint} (TLS-only, mutual auth)",
            $"{_opts.ListenAddress}:{_opts.ListenPort}");
        _logger.LogInformation("server cert: {Subject}", _defaultBinding.ServerCertWithKey.Subject);
        _logger.LogInformation("trusted CA: {Subject}", _defaultBinding.TrustedCa.Subject);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                var connId = Interlocked.Increment(ref _connectionCounter);
                _ = HandleConnectionAsync(tcp, connId, ct);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            _listener.Stop();
        }

    }

    private async Task HandleConnectionAsync(TcpClient tcp, long connId, CancellationToken ct)
    {

        var remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";

        // Outer activity covers the entire connection lifetime - handshake plus request loop.
        using var connActivity = Telemetry.ActivitySource.StartActivity(
            "mbaps.connection", ActivityKind.Server);
        connActivity?.SetTag("net.peer.address", remote);
        connActivity?.SetTag("mbaps.connection_id", connId);

        // Connection-scoped log scope: every log line inside this method
        // automatically carries connection_id and peer.
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["connection_id"] = connId,
            ["peer"]          = remote,
        });

        ServerMetrics.ActiveConnections.Add(1);
        _logger.LogInformation("accept tcp from {Peer}", remote);

        IModbusBackend? backend = null;
        String?         role    = null;

        try
        {

            tcp.NoDelay = true;
            var selectedBinding = _defaultBinding;

            using var net = tcp.GetStream();
            using var ssl = new SslStream(
                                net,
                                leaveInnerStreamOpen:               false,
                                userCertificateValidationCallback:  (sender, certificate, certificateChain, policyErrors) =>
                                                                       ValidateClientCertificate(
                                                                           sender,
                                                                           certificate,
                                                                           certificateChain,
                                                                           policyErrors,
                                                                           selectedBinding.TrustedCa
                                                                       )
            );

            var sslOpts = new SslServerAuthenticationOptions {
                              ServerCertificateSelectionCallback = (sender, serverName) => {
                                                                        selectedBinding = SelectBinding(serverName);
                                                                        return selectedBinding.ServerCertWithKey;
                                                                    },
                              ClientCertificateRequired = true,
                              EnabledSslProtocols       = SslProtocols.Tls12 | SslProtocols.Tls13,
                              CertificateRevocationCheckMode = X509RevocationMode.NoCheck, // demo: no CRL/OCSP infra
                              AllowRenegotiation        = false,                            // mbaps doesn't need it
                              EncryptionPolicy          = EncryptionPolicy.RequireEncryption,
                          };

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(_opts.HandshakeTimeout);

            // ----- TLS handshake (instrumented) -----
            using var handshakeActivity = Telemetry.ActivitySource.StartActivity(
                "mbaps.handshake", ActivityKind.Server);
            var handshakeSw = Stopwatch.StartNew();
            string handshakeOutcome = "ok";
            try
            {
                await ssl.AuthenticateAsServerAsync(sslOpts, handshakeCts.Token).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                handshakeOutcome = "bad_cert";
                handshakeActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "TLS handshake failed: {ExceptionType}", ex.GetType().Name);
                return;
            }
            catch (OperationCanceledException) when (handshakeCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                handshakeOutcome = "timeout";
                handshakeActivity?.SetStatus(ActivityStatusCode.Error, "handshake timeout");
                _logger.LogWarning("TLS handshake timeout after {Timeout}", _opts.HandshakeTimeout);
                return;
            }
            catch (Exception ex)
            {
                handshakeOutcome = "io";
                handshakeActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "TLS handshake failed: {ExceptionType}", ex.GetType().Name);
                return;
            }
            finally
            {
                handshakeSw.Stop();
                var tags = new TagList { { "outcome", handshakeOutcome } };
                ServerMetrics.Handshakes.Add(1, tags);
                ServerMetrics.HandshakeDuration.Record(handshakeSw.Elapsed.TotalSeconds, tags);
            }

            var clientCert = ssl.RemoteCertificate is X509Certificate2 c
                ? c
                : throw new InvalidOperationException("missing client cert after handshake");
            role = RoleExtractor.TryExtractRole(clientCert);

            handshakeActivity?.SetTag("tls.protocol_version", ssl.SslProtocol.ToString());
            handshakeActivity?.SetTag("tls.cipher_suite",     ssl.NegotiatedCipherSuite.ToString());
            handshakeActivity?.SetTag("mbaps.role",           ServerMetrics.RoleLabel(role));
            connActivity?.SetTag("mbaps.role",                ServerMetrics.RoleLabel(role));

            _logger.LogInformation("TLS up: proto={Protocol} cipher={CipherSuite} client={ClientSubject} role={Role}",
                ssl.SslProtocol, ssl.NegotiatedCipherSuite, clientCert.Subject, role ?? "(none)");

            backend = await _backendFactory.CreateAsync(connId, role, ct).ConfigureAwait(false);

            await PumpModbusAsync(ssl, role, backend, connId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            connActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "connection error: {ExceptionType}", ex.GetType().Name);
        }
        finally
        {
            if (backend is not null)
            {
                try { await backend.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogWarning(ex, "backend dispose failed"); }
            }
            try { tcp.Close(); } catch { }
            ServerMetrics.ActiveConnections.Add(-1);
            _logger.LogInformation("closed");
        }
    }

    /// <summary>
    /// Validates the client cert chain against our pinned CA.
    /// </summary>
    private Boolean ValidateClientCertificate(Object            sender,
                                              X509Certificate?  cert,
                                              X509Chain?        _ignoreSystemChain,
                                              SslPolicyErrors   errors,
                                              X509Certificate2  trustedCa)
    {

        if (cert is null)
        {
            _logger.LogWarning("client presented no certificate");
            return false;
        }

        var x = cert as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Cert));

        using var chain = new X509Chain
        {
            ChainPolicy = {
                RevocationMode    = X509RevocationMode.NoCheck,
                TrustMode         = X509ChainTrustMode.CustomRootTrust,
                VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority,
            },
        };
        chain.ChainPolicy.CustomTrustStore.Add(trustedCa);

        if (_ignoreSystemChain is not null)
        {
            foreach (var chainElement in _ignoreSystemChain.ChainElements)
            {
                if (!chainElement.Certificate.Thumbprint.Equals(x.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    chain.ChainPolicy.ExtraStore.Add(chainElement.Certificate);
            }
        }

        if (!chain.Build(x) ||
            !IsAnchoredByTrustedCA(x, chain, trustedCa))
        {
            foreach (var s in chain.ChainStatus)
                _logger.LogWarning("chain status: {Status} {Information}", s.Status, s.StatusInformation?.Trim());
            return false;
        }

        var eku = x.Extensions["2.5.29.37"] as X509EnhancedKeyUsageExtension;
        if (eku is not null && !eku.EnhancedKeyUsages.OfType<System.Security.Cryptography.Oid>()
                .Any(o => o.Value == "1.3.6.1.5.5.7.3.2"))
        {
            _logger.LogWarning("client cert lacks clientAuth EKU (subject={Subject})", x.Subject);
            return false;
        }

        return true;

    }

    private static Boolean IsAnchoredByTrustedCA(X509Certificate2 peerCertificate,
                                                 X509Chain         chain,
                                                 X509Certificate2  trustedCa)
    {

        if (peerCertificate.IssuerName.RawData.SequenceEqual(trustedCa.SubjectName.RawData))
            return true;

        return chain.ChainElements.
                   Cast<X509ChainElement>().
                   Any(chainElement => chainElement.Certificate.Thumbprint.Equals(trustedCa.Thumbprint, StringComparison.OrdinalIgnoreCase));

    }

    /// <summary>
    /// Read frames in a loop, authorize, dispatch to backend, write responses.
    /// </summary>
    private async Task PumpModbusAsync(SslStream          ssl,
                                       String?            role,
                                       IModbusBackend     backend,
                                       long               connId,
                                       CancellationToken  ct)
    {

        ssl.ReadTimeout  = (int)_opts.IdleTimeout.TotalMilliseconds;
        ssl.WriteTimeout = (int)_opts.WriteTimeout.TotalMilliseconds;

        while (!ct.IsCancellationRequested)
        {

            ModbusFrame req;

            try {
                req = await ModbusFrame.ReadAsync(ssl, ct).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                return;
            }
            catch (IOException) {
                return;
            }

            if (req.Pdu.Length == 0)
            {
                _logger.LogWarning("empty PDU");
                return;
            }

            var fc = (ModbusFunctionCodes)req.Pdu[0];
            byte[] respPdu;
            try
            {
                respPdu = await DispatchAsync(fc, req, role, backend, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "dispatch error: {ExceptionType}", ex.GetType().Name);
                respPdu = ModbusPDU.BuildException(fc, ModbusExceptionCode.ServerDeviceFailure);
            }

            var resp  = new ModbusFrame(req.TransactionId, req.UnitId, respPdu);
            var bytes = resp.ToBytes();

            await ssl.WriteAsync(bytes, ct).ConfigureAwait(false);
            await ssl.FlushAsync(ct).       ConfigureAwait(false);

        }

    }

    /// <summary>
    /// Parse the request PDU just enough to evaluate the RBAC policy. If allowed,
    /// hand the original PDU to the backend untouched. Authorization is enforced
    /// here once per request - the backend is intentionally policy-agnostic.
    /// </summary>
    private async Task<byte[]> DispatchAsync(ModbusFunctionCodes  fc,
                                             ModbusFrame          req,
                                             String?              role,
                                             IModbusBackend       backend,
                                             CancellationToken    ct)
    {

        var fcLabel   = $"0x{(byte)fc:X2}";
        var roleLabel = ServerMetrics.RoleLabel(role);

        // Per-request span for tracing. Cheap when no listener is attached.
        using var activity = Telemetry.ActivitySource.StartActivity(
                                 "mbaps.request",
                                 ActivityKind.Server
                             );
        activity?.SetTag("modbus.fc",      fcLabel);
        activity?.SetTag("modbus.unit_id", req.UnitId);
        activity?.SetTag("mbaps.role",     roleLabel);

        var sw = Stopwatch.StartNew();
        string decision = "deny";
        try
        {

            var pdu = req.Pdu;

            if (!TryParseAddressRange(fc, pdu, out var addr, out var qty))
            {
                _logger.LogWarning("malformed PDU: fc={FunctionCode}", fcLabel);
                activity?.SetStatus(ActivityStatusCode.Error, "malformed PDU");
                return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalFunction);
            }

            activity?.SetTag("modbus.address",  addr);
            activity?.SetTag("modbus.quantity", qty);

            var d = _authz.Authorize(role, fc, addr, qty);
            if (!d.Allowed)
            {
                _logger.LogWarning(
                    "RBAC DENY: fc={FunctionCode} addr={Address} qty={Quantity} role={Role} reason={Reason}",
                    fcLabel, addr, qty, role ?? "(none)", d.Reason);
                ServerMetrics.AuthorizationDenials.Add(1, new TagList
                {
                    { "role",   roleLabel },
                    { "fc",     fcLabel   },
                    { "reason", d.Reason ?? "?" },
                });
                activity?.SetTag("mbaps.deny_reason", d.Reason);
                activity?.SetStatus(ActivityStatusCode.Error, d.Reason);
                return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalFunction);
            }

            decision = "allow";
            _logger.LogDebug("RBAC ALLOW: fc={FunctionCode} addr={Address} qty={Quantity} role={Role}",
                fcLabel, addr, qty, role ?? "(none)");

            var resp = await backend.ProcessRequestAsync(req.UnitId, pdu, ct).ConfigureAwait(false);

            return resp;

        }
        finally
        {
            sw.Stop();
            var tags = new TagList {
                { "fc",       fcLabel   },
                { "role",     roleLabel },
                { "decision", decision  },
            };
            ServerMetrics.Requests.Add(1, tags);
            ServerMetrics.RequestDuration.Record(sw.Elapsed.TotalSeconds, tags);
        }
    }

    /// <summary>
    /// Pull the (firstAddress, quantity) pair out of a request PDU for FCs the
    /// policy understands. Returns false for malformed PDUs or unsupported FCs;
    /// the caller turns that into Modbus exception 0x01.
    /// </summary>
    private static bool TryParseAddressRange(ModbusFunctionCodes fc, byte[] pdu, out ushort addr, out ushort qty)
    {

        addr = 0; qty = 0;

        switch (fc)
        {
            case ModbusFunctionCodes.ReadHoldingRegisters:
            case ModbusFunctionCodes.ReadInputRegisters:
            case ModbusFunctionCodes.ReadCoils:
            case ModbusFunctionCodes.ReadDiscreteInputs:
            {
                if (pdu.Length != 5) return false;
                addr = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
                qty  = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
                if (qty == 0) return false;
                return true;
            }

            case ModbusFunctionCodes.WriteSingleRegister:
            case ModbusFunctionCodes.WriteSingleCoil:
            {
                if (pdu.Length != 5) return false;
                addr = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
                qty  = 1;
                return true;
            }

            case ModbusFunctionCodes.WriteMultipleRegisters:
            case ModbusFunctionCodes.WriteMultipleCoils:
            {
                if (pdu.Length < 6) return false;
                addr = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2));
                qty  = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
                var byteCount = pdu[5];
                if (qty == 0) return false;
                // Sanity-check the byte-count for register writes (FC10).
                if (fc == ModbusFunctionCodes.WriteMultipleRegisters &&
                    (byteCount != qty * 2 || pdu.Length != 6 + byteCount))
                    return false;
                return true;
            }

            case ModbusFunctionCodes.ReadWriteMultipleRegisters:
            {
                // Bytes: FC | ReadAddr(2) | ReadQty(2) | WriteAddr(2) | WriteQty(2) | ByteCount(1) | data...
                if (pdu.Length < 10) return false;
                var rqty = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2));
                var waddr = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(5, 2));
                var wqty = BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(7, 2));
                if (rqty == 0 || wqty == 0) return false;
                // Authorize on the WRITE address range - more restrictive of the two.
                addr = waddr;
                qty  = wqty;
                return true;
            }

            default:
                return false;

        }
    }

    private CertificateBinding SelectBinding(String? serverName)
    {

        if (!String.IsNullOrWhiteSpace(serverName) &&
            _sniBindings.TryGetValue(NormalizeServerName(serverName), out var binding))
        {
            _logger.LogDebug("selected SNI binding for {ServerName}: server cert={ServerSubject}, client CA={CaSubject}",
                             serverName,
                             binding.ServerCertWithKey.Subject,
                             binding.TrustedCa.Subject);
            return binding;
        }

        return _defaultBinding;

    }

    private static CertificateBinding LoadBinding(String? serverName,
                                                  String  serverPfxPath,
                                                  String? serverPfxPassword,
                                                  String  caCertPath)
    {

        return new CertificateBinding(
                   serverName,
                   LoadPfx(serverPfxPath, serverPfxPassword),
                   X509CertificateLoader.LoadCertificateFromFile(caCertPath)
               );

    }

    private static String NormalizeServerName(String serverName)

        => serverName.Trim().TrimEnd('.').ToLowerInvariant();

    private static X509Certificate2 LoadPfx(string path, string? password)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(path, password,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private sealed record CertificateBinding(String?           ServerName,
                                             X509Certificate2  ServerCertWithKey,
                                             X509Certificate2  TrustedCa);

    public void Dispose()
    {
        _cts.Cancel();
        _defaultBinding.ServerCertWithKey.Dispose();
        _defaultBinding.TrustedCa.Dispose();
        foreach (var binding in _sniBindings.Values)
        {
            binding.ServerCertWithKey.Dispose();
            binding.TrustedCa.Dispose();
        }
        _cts.Dispose();
    }

}
