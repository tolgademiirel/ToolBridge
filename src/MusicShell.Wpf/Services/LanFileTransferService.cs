using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MusicShell.Infrastructure;
using MusicShell.Models;

namespace MusicShell.Services;

public sealed class LanFileTransferService : IDisposable
{
    public const int TransferPort = 47893;
    private const string ProtocolAppName = "ToolBridge.Transfer";
    private const int ProtocolVersion = 1;
    private const int MaxHeaderBytes = 1024 * 1024;
    private const int BufferSize = 1024 * 1024;

    private readonly string _localPresenceId;
    private readonly string _localDisplayName;
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private Task? _acceptTask;

    public LanFileTransferService(string localPresenceId, string localDisplayName)
    {
        _localPresenceId = localPresenceId?.Trim() ?? string.Empty;
        _localDisplayName = localDisplayName?.Trim() ?? string.Empty;
    }

    public event EventHandler<LanFileTransferReceivedEventArgs>? TransferReceived;

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _listener = new TcpListener(IPAddress.Any, TransferPort);
        _listener.Start();
        _acceptTask = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
    }

    public async Task SendTransferAsync(
        string remoteIpAddress,
        LanFileTransferRequest request,
        Action<TransferFileItem, int>? progressChanged,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteIpAddress))
        {
            throw new InvalidOperationException("Alıcının IP adresi bulunamadı.");
        }

        if (request.Files.Count == 0)
        {
            throw new InvalidOperationException("Gönderilecek dosya yok.");
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        using var client = new TcpClient(AddressFamily.InterNetwork);
        client.NoDelay = true;

        await client.ConnectAsync(remoteIpAddress.Trim(), TransferPort, linkedCancellation.Token);
        await using var stream = client.GetStream();

        var header = new TransferHeader
        {
            App = ProtocolAppName,
            Version = ProtocolVersion,
            SenderPresenceId = request.SenderPresenceId,
            SenderName = request.SenderName,
            RecipientPresenceId = request.RecipientPresenceId,
            RecipientName = request.RecipientName,
            CreatedAtUtc = DateTime.UtcNow,
            Files = request.Files.Select(file => new TransferFileHeader
            {
                FileName = SanitizeFileName(file.FileName),
                SizeBytes = file.SizeBytes,
                Sha256 = file.SourceChecksum?.Trim() ?? string.Empty
            }).ToList()
        };

        await WriteJsonFrameAsync(stream, header, linkedCancellation.Token);

        var buffer = new byte[BufferSize];
        foreach (var file in request.Files)
        {
            linkedCancellation.Token.ThrowIfCancellationRequested();
            if (!File.Exists(file.FullPath))
            {
                throw new FileNotFoundException($"Dosya bulunamadı: {file.FileName}", file.FullPath);
            }

            long sentBytes = 0;
            await using var sourceStream = new FileStream(
                file.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                BufferSize,
                useAsync: true);

            int read;
            while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCancellation.Token)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), linkedCancellation.Token);
                sentBytes += read;
                var progress = file.SizeBytes <= 0
                    ? 100
                    : Math.Clamp((int)Math.Round(sentBytes * 100d / file.SizeBytes), 0, 99);
                progressChanged?.Invoke(file, progress);
            }

            progressChanged?.Invoke(file, 100);
        }

        await stream.FlushAsync(linkedCancellation.Token);

        var response = await ReadJsonFrameAsync<TransferResponse>(stream, linkedCancellation.Token);
        if (response is null || !response.Success)
        {
            throw new IOException(response?.Message ?? "Alıcı transferi kabul etmedi veya yanıt veremedi.");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var listener = _listener;
        if (listener is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
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
                AppLogger.LogException("LAN dosya transferi dinleme hatası", ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stagingFolder = string.Empty;

        try
        {
            using (client)
            {
                client.NoDelay = true;
                await using var stream = client.GetStream();
                var header = await ReadJsonFrameAsync<TransferHeader>(stream, cancellationToken);
                ValidateHeader(header);

                stagingFolder = CreateStagingFolder();
                var receivedFiles = new List<LanFileTransferReceivedFile>();

                foreach (var fileHeader in header!.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var safeFileName = SanitizeFileName(fileHeader.FileName);
                    var stagedPath = GetUniqueFilePath(stagingFolder, safeFileName);
                    var checksum = await ReceiveFileAsync(stream, stagedPath, fileHeader.SizeBytes, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(fileHeader.Sha256) &&
                        !string.Equals(fileHeader.Sha256, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException($"Checksum doğrulaması başarısız: {safeFileName}");
                    }

                    receivedFiles.Add(new LanFileTransferReceivedFile(
                        safeFileName,
                        stagedPath,
                        fileHeader.SizeBytes,
                        checksum));
                }

                TransferReceived?.Invoke(this, new LanFileTransferReceivedEventArgs(
                    header.SenderPresenceId?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(header.SenderName) ? "Ağ kullanıcısı" : header.SenderName.Trim(),
                    header.RecipientPresenceId?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(header.RecipientName) ? _localDisplayName : header.RecipientName.Trim(),
                    stagingFolder,
                    receivedFiles));

                await WriteJsonFrameAsync(stream, new TransferResponse(true, "Transfer alıcı bilgisayara ulaştı."), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("LAN dosya transferi alınamadı", ex);

            try
            {
                if (client.Connected)
                {
                    await using var stream = client.GetStream();
                    await WriteJsonFrameAsync(stream, new TransferResponse(false, ex.Message), CancellationToken.None);
                }
            }
            catch
            {
                // Yanıt gönderilemezse bağlantı kapanır; gönderici tarafında bağlantı hatası gösterilir.
            }

            if (!string.IsNullOrWhiteSpace(stagingFolder))
            {
                TryDeleteDirectory(stagingFolder);
            }
        }
    }

    private void ValidateHeader(TransferHeader? header)
    {
        if (header is null ||
            !string.Equals(header.App, ProtocolAppName, StringComparison.Ordinal) ||
            header.Version != ProtocolVersion)
        {
            throw new InvalidDataException("Geçersiz ToolBridge transfer paketi.");
        }

        if (!string.IsNullOrWhiteSpace(header.RecipientPresenceId) &&
            !string.Equals(header.RecipientPresenceId, _localPresenceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Transfer paketi bu bilgisayara ait değil.");
        }

        if (header.Files.Count == 0)
        {
            throw new InvalidDataException("Transfer paketinde dosya yok.");
        }

        if (header.Files.Any(file => file.SizeBytes < 0 || string.IsNullOrWhiteSpace(file.FileName)))
        {
            throw new InvalidDataException("Transfer paketinde geçersiz dosya bilgisi var.");
        }
    }

    private static async Task<string> ReceiveFileAsync(Stream stream, string destinationPath, long sizeBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        long remainingBytes = sizeBytes;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);

        await using var destinationStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            BufferSize,
            useAsync: true);
        using var sha256 = SHA256.Create();

        while (remainingBytes > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readLength = (int)Math.Min(buffer.Length, remainingBytes);
            var read = await stream.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken);
            if (read <= 0)
            {
                throw new EndOfStreamException("Transfer bağlantısı dosya tamamlanmadan kapandı.");
            }

            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha256.TransformBlock(buffer, 0, read, null, 0);
            remainingBytes -= read;
        }

        await destinationStream.FlushAsync(cancellationToken);
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
    }

    private static async Task WriteJsonFrameAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        var payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length <= 0 || payload.Length > MaxHeaderBytes)
        {
            throw new InvalidDataException("Transfer başlığı boyut limiti dışında.");
        }

        var lengthBytes = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(lengthBytes.AsMemory(0, lengthBytes.Length), cancellationToken);
        await stream.WriteAsync(payload.AsMemory(0, payload.Length), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<T?> ReadJsonFrameAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = await ReadExactAsync(stream, sizeof(int), cancellationToken);
        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length <= 0 || length > MaxHeaderBytes)
        {
            throw new InvalidDataException("Transfer başlığı okunamadı veya çok büyük.");
        }

        var payload = await ReadExactAsync(stream, length, cancellationToken);
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(payload));
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read <= 0)
            {
                throw new EndOfStreamException("Transfer bağlantısı beklenenden erken kapandı.");
            }

            offset += read;
        }

        return buffer;
    }

    private static string CreateStagingFolder()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ToolBridge",
            "incoming-staging",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        var path = Path.Combine(folder, safeFileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var name = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var index = 1;
        do
        {
            path = Path.Combine(folder, $"{name} ({index++}){extension}");
        }
        while (File.Exists(path));

        return path;
    }

    private static string SanitizeFileName(string? fileName)
    {
        var value = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "transfer-file";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim();
    }

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch
        {
            // Staging klasörü kilitliyse sonraki temizlikte silinebilir.
        }
    }

    public void Dispose()
    {
        try
        {
            _shutdown.Cancel();
            _listener?.Stop();
        }
        catch
        {
            // Kapanış sırasında socket zaten kapanmış olabilir.
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private sealed class TransferHeader
    {
        public string App { get; set; } = ProtocolAppName;
        public int Version { get; set; } = ProtocolVersion;
        public string SenderPresenceId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string RecipientPresenceId { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<TransferFileHeader> Files { get; set; } = new();
    }

    private sealed class TransferFileHeader
    {
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    private sealed record TransferResponse(bool Success, string Message);
}

public sealed class LanFileTransferRequest
{
    public LanFileTransferRequest(
        string senderPresenceId,
        string senderName,
        string recipientPresenceId,
        string recipientName,
        IReadOnlyList<TransferFileItem> files)
    {
        SenderPresenceId = senderPresenceId?.Trim() ?? string.Empty;
        SenderName = senderName?.Trim() ?? string.Empty;
        RecipientPresenceId = recipientPresenceId?.Trim() ?? string.Empty;
        RecipientName = recipientName?.Trim() ?? string.Empty;
        Files = files;
    }

    public string SenderPresenceId { get; }
    public string SenderName { get; }
    public string RecipientPresenceId { get; }
    public string RecipientName { get; }
    public IReadOnlyList<TransferFileItem> Files { get; }
}

public sealed class LanFileTransferReceivedEventArgs : EventArgs
{
    public LanFileTransferReceivedEventArgs(
        string senderPresenceId,
        string senderName,
        string recipientPresenceId,
        string recipientName,
        string stagingFolder,
        IReadOnlyList<LanFileTransferReceivedFile> files)
    {
        SenderPresenceId = senderPresenceId;
        SenderName = senderName;
        RecipientPresenceId = recipientPresenceId;
        RecipientName = recipientName;
        StagingFolder = stagingFolder;
        Files = files;
    }

    public string SenderPresenceId { get; }
    public string SenderName { get; }
    public string RecipientPresenceId { get; }
    public string RecipientName { get; }
    public string StagingFolder { get; }
    public IReadOnlyList<LanFileTransferReceivedFile> Files { get; }
}

public sealed record LanFileTransferReceivedFile(
    string FileName,
    string FullPath,
    long SizeBytes,
    string SourceChecksum);
