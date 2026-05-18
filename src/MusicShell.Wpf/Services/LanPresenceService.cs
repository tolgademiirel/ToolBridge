using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MusicShell.Infrastructure;

namespace MusicShell.Services;

public sealed class LanPresenceService : IDisposable
{
    public const int PresencePort = 47892;
    private const string AppName = "ToolBridge";
    private const int MaxPresencePacketBytes = 8192;
    private const int MaxPresenceTextLength = 120;
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OfflineSendTimeout = TimeSpan.FromMilliseconds(600);

    private readonly string _instanceId;
    private readonly string _displayName;
    private readonly string _machineName;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _syncRoot = new();
    private UdpClient? _udpClient;
    private Task? _receiveTask;
    private Task? _broadcastTask;
    private bool _isTransferReceiveEnabled;
    private bool _isStarted;

    public LanPresenceService(string instanceId, string displayName, bool isTransferReceiveEnabled)
    {
        _instanceId = NormalizePresenceText(string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId, 128);
        _displayName = NormalizePresenceText(string.IsNullOrWhiteSpace(displayName) ? Environment.MachineName : displayName, MaxPresenceTextLength);
        _machineName = NormalizePresenceText(string.IsNullOrWhiteSpace(Environment.MachineName) ? "Unknown" : Environment.MachineName, MaxPresenceTextLength);
        _isTransferReceiveEnabled = isTransferReceiveEnabled;
    }

    public event EventHandler<LanPresenceUser>? UserOnline;
    public event EventHandler<string>? UserOffline;

    public string InstanceId => _instanceId;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_isStarted)
            {
                return;
            }

            _udpClient = CreateUdpClient();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_shutdown.Token));
            _broadcastTask = Task.Run(() => BroadcastLoopAsync(_shutdown.Token));
            _isStarted = true;
        }
    }

    public void UpdateStatus(bool isTransferReceiveEnabled)
    {
        _isTransferReceiveEnabled = isTransferReceiveEnabled;
        if (!_shutdown.IsCancellationRequested)
        {
            _ = Task.Run(() => SendPresenceAsync("online", CancellationToken.None));
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    using var offlineCts = new CancellationTokenSource(OfflineSendTimeout);
                    var offlineTask = SendPresenceAsync("offline", offlineCts.Token);
                    if (!offlineTask.Wait(OfflineSendTimeout))
                    {
                        AppLogger.Log("LAN presence offline paketi zaman aşımına uğradı; kapatma devam ediyor.");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogException("LAN presence offline paketi gönderilemedi", ex);
                }

                _shutdown.Cancel();
            }
        }
        finally
        {
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.LogException("LAN presence UDP kapatılamadı", ex);
            }

            _shutdown.Dispose();
        }
    }

    private static UdpClient CreateUdpClient()
    {
        var client = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true,
            MulticastLoopback = false
        };

        client.Client.ExclusiveAddressUse = false;
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, PresencePort));
        return client;
    }

    private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
    {
        await SendPresenceAsync("online", cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(BroadcastInterval, cancellationToken).ConfigureAwait(false);
                await SendPresenceAsync("online", cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLogger.LogException("LAN presence yayın döngüsü hatası", ex);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var client = _udpClient;
        if (client is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                HandleIncomingPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLogger.LogException("LAN presence dinleme hatası", ex);
            }
        }
    }

    private async Task SendPresenceAsync(string messageType, CancellationToken cancellationToken)
    {
        var client = _udpClient;
        if (client is null)
        {
            return;
        }

        var message = new PresenceMessage
        {
            App = AppName,
            Type = messageType,
            InstanceId = _instanceId,
            DisplayName = _displayName,
            UserName = NormalizePresenceText(Environment.UserName ?? string.Empty, MaxPresenceTextLength),
            MachineName = _machineName,
            IsTransferReceiveEnabled = _isTransferReceiveEnabled,
            SentAtUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(message);
        var payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length == 0 || payload.Length > MaxPresencePacketBytes)
        {
            AppLogger.Log($"LAN presence paketi boyut limiti dışında: {payload.Length} bayt.");
            return;
        }

        foreach (var endpoint in GetBroadcastEndpoints())
        {
            try
            {
                await client.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.LogException($"LAN presence paketi gönderilemedi: {endpoint}", ex);
            }
        }
    }

    private void HandleIncomingPacket(byte[] payload, IPEndPoint remoteEndPoint)
    {
        try
        {
            if (payload.Length == 0 || payload.Length > MaxPresencePacketBytes)
            {
                return;
            }

            var json = Encoding.UTF8.GetString(payload);
            var message = JsonSerializer.Deserialize<PresenceMessage>(json);
            if (message is null || !string.Equals(message.App, AppName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(message.InstanceId) || message.InstanceId.Length > 128 ||
                string.Equals(message.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(message.Type, "offline", StringComparison.OrdinalIgnoreCase))
            {
                UserOffline?.Invoke(this, message.InstanceId);
                return;
            }

            if (!string.Equals(message.Type, "online", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var userName = NormalizePresenceText(message.UserName, MaxPresenceTextLength);
            var machineName = NormalizePresenceText(message.MachineName, MaxPresenceTextLength);
            var displayName = string.IsNullOrWhiteSpace(message.DisplayName)
                ? BuildFallbackDisplayName(userName, machineName)
                : NormalizePresenceText(message.DisplayName, MaxPresenceTextLength);

            UserOnline?.Invoke(this, new LanPresenceUser(
                NormalizePresenceText(message.InstanceId, 128),
                displayName,
                userName,
                machineName,
                remoteEndPoint.Address.ToString(),
                message.IsTransferReceiveEnabled,
                DateTime.Now));
        }
        catch (Exception ex)
        {
            AppLogger.LogException("LAN presence paketi çözümlenemedi", ex);
        }
    }

    private static IEnumerable<IPEndPoint> GetBroadcastEndpoints()
    {
        var endpoints = new List<IPEndPoint>
        {
            new(IPAddress.Broadcast, PresencePort)
        };

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                foreach (var addressInfo in properties.UnicastAddresses)
                {
                    if (addressInfo.Address.AddressFamily != AddressFamily.InterNetwork || addressInfo.IPv4Mask is null)
                    {
                        continue;
                    }

                    var broadcastAddress = GetBroadcastAddress(addressInfo.Address, addressInfo.IPv4Mask);
                    if (broadcastAddress is not null)
                    {
                        endpoints.Add(new IPEndPoint(broadcastAddress, PresencePort));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("LAN broadcast adresleri alınamadı", ex);
        }

        return endpoints
            .GroupBy(endpoint => endpoint.Address.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IPAddress? GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        if (addressBytes.Length != maskBytes.Length)
        {
            return null;
        }

        var broadcastBytes = new byte[addressBytes.Length];
        for (var i = 0; i < addressBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcastBytes);
    }

    private static string BuildFallbackDisplayName(string? userName, string? machineName)
    {
        var user = NormalizePresenceText(userName, MaxPresenceTextLength);
        var machine = NormalizePresenceText(machineName, MaxPresenceTextLength);

        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(machine))
        {
            return $"{user} ({machine})";
        }

        return !string.IsNullOrWhiteSpace(user)
            ? user
            : string.IsNullOrWhiteSpace(machine) ? "Ağ kullanıcısı" : machine;
    }

    private static string NormalizePresenceText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = new string(value
            .Where(ch => !char.IsControl(ch))
            .ToArray())
            .Trim();

        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        return cleaned[..maxLength];
    }

    private sealed class PresenceMessage
    {
        public string App { get; set; } = AppName;
        public string Type { get; set; } = "online";
        public string InstanceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public bool IsTransferReceiveEnabled { get; set; } = true;
        public DateTime SentAtUtc { get; set; }
    }
}

public sealed record LanPresenceUser(
    string InstanceId,
    string DisplayName,
    string UserName,
    string MachineName,
    string IpAddress,
    bool IsTransferReceiveEnabled,
    DateTime LastSeen);
