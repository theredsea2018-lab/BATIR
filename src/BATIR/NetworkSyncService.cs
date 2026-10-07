using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace BATIR;

internal static class NetworkSyncService
{
    public const int Port = 47821;

    public static async Task ReceiveBackupAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var magic = new byte[4]; await ReadExactlyAsync(stream, magic, cancellationToken); if (Encoding.ASCII.GetString(magic) != "BAT1") throw new InvalidDataException("پروتکل همگام‌سازی BATIR معتبر نیست.");
            var expectedHash = new byte[32]; await ReadExactlyAsync(stream, expectedHash, cancellationToken);
            var expectedMac = new byte[32]; await ReadExactlyAsync(stream, expectedMac, cancellationToken);
            var length = await ReadInt64Async(stream, cancellationToken);
            if (length <= 0 || length > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("حجم فایل پشتیبان نامعتبر است.");
            var temp = targetPath + ".incoming";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                await CopyExactlyAsync(stream, file, length, cancellationToken);
            }
            if (!Database.VerifyBackup(temp))
            {
                File.Delete(temp);
                throw new InvalidDataException("پشتیبان دریافتی از شبکه معتبر نیست.");
            }
            var actualHash = HexToBytes(Sha256(temp));
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash) || !CryptographicOperations.FixedTimeEquals(expectedMac, ComputeMac(actualHash)))
            { File.Delete(temp); throw new InvalidDataException("صحت یا کلید پشتیبان شبکه تأیید نشد."); }
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(temp, targetPath);
        }
        finally
        {
            listener.Stop();
        }
    }

    public static async Task SendBackupAsync(string host, string backupPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath)) throw new FileNotFoundException("فایل پشتیبان پیدا نشد.", backupPath);
        if (!Database.VerifyBackup(backupPath)) throw new InvalidDataException("پشتیبان قبل از ارسال معتبر نیست.");
        using var client = new TcpClient();
        await client.ConnectAsync(host, Port);
        using var stream = client.GetStream();
        var info = new FileInfo(backupPath);
        var hash = HexToBytes(Sha256(backupPath));
        var mac = ComputeMac(hash);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("BAT1"), 0, 4, cancellationToken);
        await stream.WriteAsync(hash, 0, hash.Length, cancellationToken);
        await stream.WriteAsync(mac, 0, mac.Length, cancellationToken);
        await WriteInt64Async(stream, info.Length, cancellationToken);
        using var file = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        await file.CopyToAsync(stream, 65536, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    static byte[] ComputeMac(byte[] data)
    {
        var key = Database.Query("SELECT Value FROM Settings WHERE Key='NetworkSyncKey' LIMIT 1");
        var secret = key.Rows.Count > 0 ? Convert.ToString(key.Rows[0]["Value"]) : null;
        if (string.IsNullOrWhiteSpace(secret)) secret = "BATIR-LAN-DEFAULT-CHANGE-ME";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(data);
    }

    public static string Sha256(string path)
    {
        using var sha = SHA256.Create(); using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static async Task<long> ReadInt64Async(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[8]; await ReadExactlyAsync(stream, buffer, ct); return BitConverter.ToInt64(buffer, 0);
    }

    static async Task WriteInt64Async(Stream stream, long value, CancellationToken ct)
    {
        var buffer = BitConverter.GetBytes(value); await stream.WriteAsync(buffer, 0, buffer.Length, ct);
    }

    static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
            if (read == 0) throw new EndOfStreamException(); offset += read;
        }
    }

    static async Task CopyExactlyAsync(Stream source, Stream destination, long length, CancellationToken ct)
    {
        var buffer = new byte[65536]; long remaining = length;
        while (remaining > 0)
        {
            var wanted = (int)Math.Min(buffer.Length, remaining);
            var read = await source.ReadAsync(buffer, 0, wanted, ct);
            if (read == 0) throw new EndOfStreamException();
            await destination.WriteAsync(buffer, 0, read, ct); remaining -= read;
        }
    }
}
