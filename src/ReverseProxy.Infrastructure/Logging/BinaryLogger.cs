using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.Logging.Services;

public sealed class BinaryLogger : IBinaryLogger, IAsyncDisposable
{
    private readonly byte[] _buffer = new byte[8192];
    private int _bufferPosition = 0;

    private readonly string _logsFolder;
    private readonly string _archiveFolder;
    private readonly string _compressedFolder;

    private readonly bool _debugMode;

    private DateTime _currentDate;
    private string _currentLogFilePath;
    private FileStream? _activeFileStream;

    private readonly ConcurrentDictionary<string, ushort> _routeMap = new(StringComparer.OrdinalIgnoreCase);
    private ushort _nextRouteCode = 1;

    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Timer _flushTimer;

    public BinaryLogger(string? baseDirectory = null, bool debugMode = false)
    {
        _debugMode = debugMode;

        var rootPath = baseDirectory 
            ?? Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
            ?? AppContext.BaseDirectory;

        _logsFolder = Path.Combine(rootPath, "Logs");
        _archiveFolder = Path.Combine(_logsFolder, "Archive");
        _compressedFolder = Path.Combine(_logsFolder, "Compressed");

        Directory.CreateDirectory(_logsFolder);
        Directory.CreateDirectory(_archiveFolder);
        Directory.CreateDirectory(_compressedFolder);

        _currentDate = DateTime.UtcNow.Date;
        _currentLogFilePath = GetLogFilePath(_currentDate);

        _flushTimer = new Timer(
            callback: async _ => await SafeFlushAsync(),
            state: null,
            dueTime: TimeSpan.FromSeconds(5),
            period: TimeSpan.FromSeconds(5));
    }

    private string GetLogFilePath(DateTime date) =>
        Path.Combine(_logsFolder, $"requests_log_{date:yyyyMMdd}.bin");

    public async Task LogAsync(RequestLogDto logDto)
    {
        var (ipBytes, isIpv6) = PrepareIp(logDto.IpAddress);
        
        byte flagsByte    = (byte)(isIpv6 ? 1 : 0); 
        ushort routeCode  = PrepareRouteCode(logDto.RoutePath); 
        byte statusByte   = PrepareStatus(logDto.StatusCode);
        ushort duration   = PrepareDuration(logDto.DurationMs);
        ushort payloadKb  = PreparePayloadKb(logDto.ResponseSizeBytes);
        uint dayOffset    = PrepareDayOffset(logDto.DayOffset);

        int recordSize = 1 + ipBytes.Length + 2 + 1 + 2 + 2 + 4;

        await _fileLock.WaitAsync();
        try
        {
            if (DateTime.UtcNow.Date != _currentDate || _bufferPosition + recordSize > _buffer.Length)
            {
                await InternalFlushAsync();
            }

            int startPosition = _bufferPosition;
            WriteRecordToBuffer(flagsByte, ipBytes, routeCode, statusByte, duration, payloadKb, dayOffset);

            int recordLength = _bufferPosition - startPosition;

            if (_debugMode)
            {
                PrintRecordDebug(flagsByte, ipBytes, routeCode, statusByte, duration, payloadKb, dayOffset, startPosition, recordLength);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task FlushAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            await InternalFlushAsync();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SafeFlushAsync()
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BinaryLogger Error] Background flush failed: {ex.Message}");
        }
    }

    private async Task InternalFlushAsync()
    {
        EnsureStreamOpen();

        if (_bufferPosition > 0 && _activeFileStream != null)
        {
            try
            {
                await _activeFileStream.WriteAsync(_buffer.AsMemory(0, _bufferPosition));
                await _activeFileStream.FlushAsync();
                _bufferPosition = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BinaryLogger Error] Failed to flush: {ex.Message}");
            }
        }

        RotateIfNewDay();
    }

    private void EnsureStreamOpen()
    {
        if (_activeFileStream == null)
        {
            _activeFileStream = new FileStream(
                _currentLogFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
        }
    }

    private void RotateIfNewDay()
    {
        var today = DateTime.UtcNow.Date;
        if (today == _currentDate)
            return;

        if (_activeFileStream != null)
        {
            _activeFileStream.Dispose();
            _activeFileStream = null;
        }

        if (File.Exists(_currentLogFilePath))
        {
            var archivedPath = Path.Combine(_archiveFolder, Path.GetFileName(_currentLogFilePath));
            File.Move(_currentLogFilePath, archivedPath, overwrite: true);
            CompressArchiveIfFull();
        }

        _currentDate = today;
        _currentLogFilePath = GetLogFilePath(_currentDate);

        EnsureStreamOpen();
    }

    private void CompressArchiveIfFull()
    {
        var archivedFiles = Directory.GetFiles(_archiveFolder);
        if (archivedFiles.Length < 30)
            return;

        var zipPath = Path.Combine(_compressedFolder, $"logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var file in archivedFiles)
            {
                zip.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        foreach (var file in archivedFiles)
        {
            File.Delete(file);
        }
    }

    public ushort RegisterRoute(string routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath))
            return 0;

        if (_routeMap.TryGetValue(routePath, out var code))
            return code;

        lock (_routeMap)
        {
            if (_routeMap.TryGetValue(routePath, out code))
                return code;

            if (_nextRouteCode == ushort.MaxValue)
                return ushort.MaxValue;

            ushort assignedCode = _nextRouteCode++;
            _routeMap[routePath] = assignedCode;
            return assignedCode;
        }
    }

    private ushort PrepareRouteCode(string? routePath) => RegisterRoute(routePath ?? string.Empty);

    private byte PrepareStatus(int statusCode)
    {
        byte isSuccessBit = (byte)(statusCode is >= 200 and < 400 ? 1 : 0);
        byte statusCodeCategory = (byte)Math.Clamp(statusCode / 10, 0, 127);

        return (byte)(isSuccessBit | (statusCodeCategory << 1));
    }

    private (byte[] Bytes, bool IsIpv6) PrepareIp(string? ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString) || !IPAddress.TryParse(ipString, out var ipAddress))
            return (new byte[4], false);

        if (ipAddress.IsIPv4MappedToIPv6)
            ipAddress = ipAddress.MapToIPv4();

        bool isIpv6 = ipAddress.AddressFamily == AddressFamily.InterNetworkV6;
        return (ipAddress.GetAddressBytes(), isIpv6);
    }

    private ushort PrepareDuration(long durationMs) => (ushort)Math.Min(ushort.MaxValue, durationMs);

    private ushort PreparePayloadKb(long sizeBytes) => (ushort)Math.Min(ushort.MaxValue, sizeBytes / 1024);

    private uint PrepareDayOffset(int dayOffset) => (uint)Math.Max(0, dayOffset);

    private void WriteRecordToBuffer(
        byte flagsByte,
        byte[] ipBytes,
        ushort routeCode,
        byte statusByte,
        ushort duration,
        ushort payloadKb,
        uint dayOffset)
    {
        Span<byte> destination = _buffer.AsSpan(_bufferPosition);

        destination[0] = flagsByte;
        ipBytes.CopyTo(destination[1..]);

        int pos = 1 + ipBytes.Length;

        BinaryPrimitives.WriteUInt16LittleEndian(destination[pos..], routeCode);
        destination[pos + 2] = statusByte;

        BinaryPrimitives.WriteUInt16LittleEndian(destination[(pos + 3)..], duration);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[(pos + 5)..], payloadKb);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[(pos + 7)..], dayOffset);

        _bufferPosition += pos + 11;
    }

    public void PrintRecordDebug(
        byte flagsByte,
        byte[] ipBytes,
        ushort routeCode,
        byte statusByte,
        ushort duration,
        ushort payloadKb,
        uint dayOffset,
        int startPosition,
        int recordLength)
    {
        ReadOnlySpan<byte> recordSpan = _buffer.AsSpan(startPosition, recordLength);
        string hexOutput = Convert.ToHexString(recordSpan);
        string ipString = new IPAddress(ipBytes).ToString();
        bool isIpv6 = (flagsByte & 1) != 0;

        Console.WriteLine("================ [ BINLOG RECORD INSPECTION ] ================");
        Console.WriteLine($"[Flags Byte]   : 0x{flagsByte:X2} (IPv6: {isIpv6})");
        Console.WriteLine($"[IP Address]   : {ipString} (Length: {ipBytes.Length} bytes)");
        Console.WriteLine($"[Route Code]   : {routeCode} (ushort)");
        Console.WriteLine($"[Status Byte]  : 0x{statusByte:X2}");
        Console.WriteLine($"[Duration]     : {duration} ms");
        Console.WriteLine($"[Payload Size] : {payloadKb} KB");
        Console.WriteLine($"[Day Offset]   : {dayOffset} seconds");
        Console.WriteLine("--------------------------------------------------------------");
        Console.WriteLine($"[Hex Dump]     : {hexOutput}");
        Console.WriteLine($"[Record Size]  : {recordLength} Bytes");
        Console.WriteLine("==============================================================\n");
    }

    public async ValueTask DisposeAsync()
    {
        if (_flushTimer != null)
        {
            await _flushTimer.DisposeAsync();
        }

        await FlushAsync();

        if (_activeFileStream != null)
        {
            await _activeFileStream.DisposeAsync();
            _activeFileStream = null;
        }

        _fileLock.Dispose();
    }
}