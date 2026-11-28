using System;
using System.Collections.Generic;
using System.IO;

namespace hds.auth
{
    /// <summary>
    /// Reassembles length-prefixed auth TCP frames across arbitrary TCP read boundaries.
    /// Length prefix rule (matching AuthHandler.FrameAuthMessage / getOpCode):
    ///   - first byte &lt; 0x80: 1-byte length (0 = empty keepalive/trailer frame, skipped)
    ///   - first byte &gt;= 0x80: 2-byte big-endian length with the 0x8000 flag masked off
    /// Complete frames are returned with their length prefix intact because AuthHandler reads offsets from byte 0.
    /// </summary>
    public sealed class AuthFrameBuffer
    {
        public const int MaxFrameLength = 4096;

        private readonly int _maxFrameLength;
        private byte[] _buffer = Array.Empty<byte>();
        private int _count;

        public AuthFrameBuffer() : this(MaxFrameLength)
        {
        }

        public AuthFrameBuffer(int maxFrameLength)
        {
            if (maxFrameLength < 2)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
            _maxFrameLength = maxFrameLength;
        }

        public int MaxAllowedFrameLength => _maxFrameLength;
        public int BufferedBytes => _count;

        public void Append(byte[] source, int offset, int size, List<byte[]> frames)
        {
            if (size <= 0) return;
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (offset < 0 || offset + size > source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (_count + size > _maxFrameLength * 2)
                throw new InvalidDataException("Stream buffer limit exceeded.");

            EnsureCapacity(_count + size);
            Buffer.BlockCopy(source, offset, _buffer, _count, size);
            _count += size;

            int consumed = 0;
            while (consumed < _count)
            {
                byte first = _buffer[consumed];
                int headerLen;
                int bodyLen;
                if (first < 0x80)
                {
                    headerLen = 1;
                    bodyLen = first;
                }
                else
                {
                    if (_count - consumed < 2) break;
                    headerLen = 2;
                    bodyLen = ((first & 0x7F) << 8) | _buffer[consumed + 1];
                }

                if (bodyLen == 0)
                {
                    consumed += headerLen;
                    continue;
                }

                int totalLen = headerLen + bodyLen;
                if (totalLen > _maxFrameLength)
                    throw new InvalidDataException("Frame exceeds maximum allowed length (" + totalLen + ").");

                if (_count - consumed < totalLen) break;

                byte[] frame = new byte[totalLen];
                Buffer.BlockCopy(_buffer, consumed, frame, 0, totalLen);
                frames.Add(frame);
                consumed += totalLen;
            }

            if (consumed > 0)
            {
                int remaining = _count - consumed;
                if (remaining > 0)
                    Buffer.BlockCopy(_buffer, consumed, _buffer, 0, remaining);
                _count = remaining;
            }
        }

        private void EnsureCapacity(int needed)
        {
            if (_buffer.Length >= needed) return;
            int next = _buffer.Length == 0 ? Math.Max(256, needed) : Math.Max(_buffer.Length * 2, needed);
            Array.Resize(ref _buffer, next);
        }
    }
}
