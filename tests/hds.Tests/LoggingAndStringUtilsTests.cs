using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using hds;

namespace hds.Tests;

public class LoggingAndStringUtilsTests
{
    [Fact]
    public async Task ConcurrentFileAppendsDropZeroLines()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "mxo-log-test-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            const int threads = 8;
            const int linesPerThread = 100;
            Task[] writers = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
            {
                for (int i = 0; i < linesPerThread; i++)
                {
                    Output.EnqueueFileAppend(tempFile, $"t{t}-line{i}\n");
                }
            })).ToArray();

            await Task.WhenAll(writers);
            Assert.True(Output.FlushLogs(5000));

            string[] lines = await File.ReadAllLinesAsync(tempFile);
            Assert.Equal(threads * linesPerThread, lines.Length);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task FileAppendRetriesThroughTransientReaderShareLock()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "mxo-log-retry-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, "initial\n");
            // Hold a FileShare.Read handle briefly (simulating File.ReadAllText/Select-String on Windows),
            // then release it so the writer's retry succeeds instead of dropping the entry.
            using var gate = new ManualResetEventSlim(false);
            Task readerTask = Task.Run(() =>
            {
                using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                gate.Set();
                Thread.Sleep(25);
            });

            gate.Wait();
            Output.EnqueueFileAppend(tempFile, "after-reader\n");
            await readerTask;
            Assert.True(Output.FlushLogs(5000));

            string content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("initial", content);
            Assert.Contains("after-reader", content);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void ConvertByteToReadablePacketMatchesLegacy32ByteRowFormat()
    {
        Assert.Equal("\r\n", Output.ConvertByteToReadablePacket(Array.Empty<byte>()));

        byte[] small = { 0x01, 0x0a, 0xff };
        Assert.Equal("01 0a ff \r\n\r\n", Output.ConvertByteToReadablePacket(small));

        byte[] multi = Enumerable.Range(0, 35).Select(i => (byte)i).ToArray();
        string formatted = Output.ConvertByteToReadablePacket(multi);
        string[] parts = formatted.Split("\r\n");
        Assert.Equal(4, parts.Length); // row 0 (32 bytes), row 1 (3 bytes), blank line, trailing empty
        Assert.Equal(32 * 3, parts[0].Length);
        Assert.StartsWith("00 01 02 ", parts[0]);
        Assert.Equal("20 21 22 ", parts[1]);
        Assert.Equal("", parts[2]);
    }

    [Fact]
    public void StringUtilsConversionsMatchExactLegacyOutput()
    {
        byte[] sample = { 0x00, 0x0f, 0xa5, 0xff };
        Assert.Equal("00 0f a5 ff ", StringUtils.bytesToString(sample));
        Assert.Equal("00 0f ", StringUtils.bytesToString(sample, 2));
        Assert.Equal("000fa5ff", StringUtils.bytesToString_NS(sample));
        Assert.Equal("", StringUtils.bytesToString(Array.Empty<byte>()));
        Assert.Equal("", StringUtils.bytesToString_NS(Array.Empty<byte>()));

        Assert.Equal(sample, StringUtils.hexStringToBytes("000fa5ff"));
        Assert.Equal(sample, StringUtils.hexStringToBytes("000FA5FF"));
        Assert.Empty(StringUtils.hexStringToBytes(""));

        byte[] nameWithNull = { (byte)'N', (byte)'e', (byte)'o', 0, (byte)'X', (byte)'Y' };
        Assert.Equal("Neo", StringUtils.charBytesToString_NZ(nameWithNull));
        Assert.Equal("Neo\0XY", StringUtils.charBytesToString(nameWithNull));
        Assert.Equal("", StringUtils.charBytesToString_NZ(new byte[] { 0, (byte)'A' }));
        Assert.Equal("ABC", StringUtils.charBytesToString_NZ(new byte[] { (byte)'A', (byte)'B', (byte)'C' }));

        Assert.Equal("a1b2c3", StringUtils.removeWhitespaceChars(" a1 \t b2\r\nc3 "));
    }
}
