using System;
using System.Buffers.Binary;
using System.Text;

namespace hds
{
    /// <summary>The stock editor sends one narrow string before its chunked 81 96.</summary>
    internal sealed class BackgroundChunkUpload
    {
        internal const int MaxLength = 1024; // characters.background
        private const int ChunkSize = 300;
        private const int LifetimeMs = 30_000;
        private readonly int worldGeneration;
        private readonly long deadline = Environment.TickCount64 + LifetimeMs;
        private byte[] bytes;
        private int written;

        private BackgroundChunkUpload(int worldGeneration)
        {
            this.worldGeneration = worldGeneration;
        }

        internal static BackgroundChunkUpload Start(ReadOnlySpan<byte> body, int worldGeneration)
        {
            // Stock mail uses the wide/multi header and keeps its existing receiver.
            if (body.Length < 11 || body[8] != 0 || body[9] != 0 || body[10] != 0)
                return null;

            var upload = new BackgroundChunkUpload(worldGeneration);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(body);
            uint width = BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
            if (body.Length == 11 && width == ChunkSize && length is >= ChunkSize and <= MaxLength)
                upload.bytes = new byte[length];
            return upload;
        }

        internal void Append(ReadOnlySpan<byte> body, int currentWorld)
        {
            if (bytes == null) return;
            int next = Math.Min(ChunkSize, bytes.Length - written);
            if (!Current(currentWorld) || next == 0 || body.Length != next + 4 ||
                BinaryPrimitives.ReadUInt16LittleEndian(body) != 4 ||
                BinaryPrimitives.ReadUInt16LittleEndian(body[2..]) != next ||
                body[4..].Contains((byte)0))
            {
                bytes = null;
                return;
            }
            body[4..].CopyTo(bytes.AsSpan(written));
            written += next;
        }

        internal string ReadIfComplete(int currentWorld) =>
            bytes != null && written == bytes.Length && Current(currentWorld)
                ? Encoding.Latin1.GetString(bytes)
                : null;

        private bool Current(int currentWorld) => currentWorld == worldGeneration && Environment.TickCount64 <= deadline;
    }
}
