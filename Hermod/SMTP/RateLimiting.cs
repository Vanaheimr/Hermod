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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#region Configuration

public sealed record RateLimitConfig
{
    /// <summary>Maximum concurrent connections total</summary>
    public int      MaxTotalConnections         { get; init; } = 100;

    /// <summary>Maximum concurrent connections per IP</summary>
    public int      MaxConnectionsPerIp         { get; init; } = 10;

    /// <summary>Maximum new connections per IP per minute</summary>
    public int      MaxConnectionsPerIpPerMinute{ get; init; } = 30;

    /// <summary>Maximum AUTH attempts per IP per hour</summary>
    public int      MaxAuthAttemptsPerIpPerHour { get; init; } = 10;

    /// <summary>Maximum RCPT TO commands per session</summary>
    public int      MaxRcptPerSession           { get; init; } = 100;

    /// <summary>Maximum messages per authenticated session</summary>
    public int      MaxMessagesPerSession       { get; init; } = 100;

    /// <summary>Maximum messages per IP per hour (unauthenticated)</summary>
    public int      MaxMessagesPerIpPerHour     { get; init; } = 50;

    /// <summary>Maximum invalid commands before disconnect</summary>
    public int      MaxInvalidCommands          { get; init; } = 5;

    /// <summary>Delay after failed AUTH (milliseconds)</summary>
    public int      AuthFailDelayMs             { get; init; } = 3000;

    /// <summary>IPs that bypass rate limiting</summary>
    public HashSet<string> WhitelistedIps       { get; init; } = ["127.0.0.1", "::1"];

    /// <summary>IPs that are always blocked</summary>
    public HashSet<string> BlacklistedIps       { get; init; } = [];

}

#endregion

#region Rate Limit Result

public enum RateLimitResult
{
    Allowed,
    TooManyConnections,
    TooManyConnectionsPerIp,
    ConnectionRateExceeded,
    AuthRateExceeded,
    MessageRateExceeded,
    Blacklisted
}

#endregion

#region Connection Tracker

/// <summary>
/// Tracks connections and enforces rate limits.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class ConnectionTracker : IDisposable
{

    private readonly RateLimitConfig _config;
    private readonly ILogger         _logger;

    // Current connections
    private int _totalConnections;
    private readonly ConcurrentDictionary<String, Int32>          _connectionsPerIp   = new();

    // Rate tracking with sliding windows
    private readonly ConcurrentDictionary<String, SlidingWindow>  _connectionRates    = new();
    private readonly ConcurrentDictionary<String, SlidingWindow>  _authAttempts       = new();
    private readonly ConcurrentDictionary<String, SlidingWindow>  _messageRates       = new();

    // Cleanup timer
    private readonly Timer _cleanupTimer;

    public ConnectionTracker(RateLimitConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
        
        // Cleanup old entries every 5 minutes
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public int TotalConnections => _totalConnections;
    public int GetConnectionCount(System.Net.IPAddress ip) => _connectionsPerIp.GetValueOrDefault(ip.ToString(), 0);

    /// <summary>
    /// Check if a new connection is allowed
    /// </summary>
    public RateLimitResult CanConnect(System.Net.IPAddress ip)
    {

        var ipStr = ip.ToString();

        // Check blacklist
        if (_config.BlacklistedIps.Contains(ipStr))
        {
            _logger.Log(LogLevel.Warning, $"Blacklisted IP rejected: {ipStr}");
            return RateLimitResult.Blacklisted;
        }

        // Check whitelist (bypass all limits)
        if (_config.WhitelistedIps.Contains(ipStr))
        {
            return RateLimitResult.Allowed;
        }

        // Check total connections
        if (_totalConnections >= _config.MaxTotalConnections)
        {
            _logger.Log(LogLevel.Warning, $"Max total connections reached ({_totalConnections})");
            return RateLimitResult.TooManyConnections;
        }

        // Check per-IP connections
        var ipConnections = _connectionsPerIp.GetValueOrDefault(ipStr, 0);
        if (ipConnections >= _config.MaxConnectionsPerIp)
        {
            _logger.Log(LogLevel.Warning, $"Max connections per IP reached for {ipStr} ({ipConnections})");
            return RateLimitResult.TooManyConnectionsPerIp;
        }

        // Check connection rate
        var window = _connectionRates.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromMinutes(1)));
        if (window.Count >= _config.MaxConnectionsPerIpPerMinute)
        {
            _logger.Log(LogLevel.Warning, $"Connection rate exceeded for {ipStr}");
            return RateLimitResult.ConnectionRateExceeded;
        }

        return RateLimitResult.Allowed;

    }

    /// <summary>
    /// Register a new connection
    /// </summary>
    public void RegisterConnection(System.Net.IPAddress ip)
    {

        var ipStr = ip.ToString();

        Interlocked.Increment(ref _totalConnections);
        _connectionsPerIp.AddOrUpdate(ipStr, 1, (_, count) => count + 1);

        var window = _connectionRates.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromMinutes(1)));
        window.Increment();

    }

    /// <summary>
    /// Unregister a closed connection
    /// </summary>
    public void UnregisterConnection(System.Net.IPAddress ip)
    {

        var ipStr = ip.ToString();

        Interlocked.Decrement(ref _totalConnections);
        _connectionsPerIp.AddOrUpdate(ipStr, 0, (_, count) => Math.Max(0, count - 1));

    }

    /// <summary>
    /// Check if AUTH attempt is allowed
    /// </summary>
    public bool CanAttemptAuth(System.Net.IPAddress ip)
    {

        var ipStr = ip.ToString();

        if (_config.WhitelistedIps.Contains(ipStr))
            return true;

        var window = _authAttempts.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromHours(1)));
        return window.Count < _config.MaxAuthAttemptsPerIpPerHour;

    }

    /// <summary>
    /// Record a failed AUTH attempt
    /// </summary>
    public void RecordAuthFailure(System.Net.IPAddress ip)
    {

        var ipStr = ip.ToString();
        var window = _authAttempts.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromHours(1)));
        window.Increment();

        _logger.Log(LogLevel.Warning, $"AUTH failure from {ipStr} (attempt {window.Count}/{_config.MaxAuthAttemptsPerIpPerHour})");

    }

    /// <summary>
    /// Check if sending a message is allowed (for unauthenticated senders)
    /// </summary>
    public bool CanSendMessage(System.Net.IPAddress ip, bool isAuthenticated)
    {

        if (isAuthenticated)
            return true; // Authenticated users have per-session limits only

        var ipStr = ip.ToString();

        if (_config.WhitelistedIps.Contains(ipStr))
            return true;

        var window = _messageRates.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromHours(1)));

        return window.Count < _config.MaxMessagesPerIpPerHour;

    }

    /// <summary>
    /// Record a sent message
    /// </summary>
    public void RecordMessage(System.Net.IPAddress ip)
    {
        var ipStr = ip.ToString();
        var window = _messageRates.GetOrAdd(ipStr, _ => new SlidingWindow(TimeSpan.FromHours(1)));
        window.Increment();
    }

    private void Cleanup()
    {

        var now = Timestamp.Now;

        // Clean up expired windows
        foreach (var kvp in _connectionRates)
        {
            if (kvp.Value.IsExpired(now))
                _connectionRates.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in _authAttempts)
        {
            if (kvp.Value.IsExpired(now))
                _authAttempts.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in _messageRates)
        {
            if (kvp.Value.IsExpired(now))
                _messageRates.TryRemove(kvp.Key, out _);
        }

        // Clean up IPs with 0 connections
        foreach (var kvp in _connectionsPerIp)
        {
            if (kvp.Value == 0)
                _connectionsPerIp.TryRemove(kvp.Key, out _);
        }

    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

}

#endregion

#region Sliding Window

/// <summary>
/// Simple sliding window counter for rate limiting
/// </summary>
public sealed class SlidingWindow
{

    private readonly TimeSpan                         _windowSize;
    private readonly ConcurrentQueue<DateTimeOffset>  _timestamps   = new();
    private          DateTimeOffset                   _lastAccess   = Timestamp.Now;

    public SlidingWindow(TimeSpan windowSize)
    {
        _windowSize = windowSize;
    }

    public int Count
    {
        get
        {
            Prune();
            return _timestamps.Count;
        }
    }

    public void Increment()
    {
        Prune();
        _timestamps.Enqueue(Timestamp.Now);
        _lastAccess = Timestamp.Now;
    }

    public Boolean IsExpired(DateTimeOffset now)
    {
        return now - _lastAccess > _windowSize * 2;
    }

    private void Prune()
    {

        var cutoff = Timestamp.Now - _windowSize;

        while (_timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _timestamps.TryDequeue(out _);
        }

    }

}

#endregion

#region Session Counters

/// <summary>
/// Per-session counters for rate limiting
/// </summary>
public sealed class SessionCounters
{

    public Int32  InvalidCommands    { get; set; }
    public Int32  Messages           { get; set; }
    public Int32  Recipients         { get; set; }
    public Int32  AuthAttempts       { get; set; }

    public void Reset()
    {
        // Keep InvalidCommands and AuthAttempts across transactions
        Messages   = 0;
        Recipients = 0;
    }

}

#endregion
