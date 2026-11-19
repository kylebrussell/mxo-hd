using System;
using System.IO;

namespace hds
{
    /// <summary>Opt-in character UI seed for the disposable NativeQa account.</summary>
    public static class QaCharacterUi
    {
        public const byte DataType = 0x0c;
        public const int ChunkSize = 1000;
        public const int MaxLength = 32000;
        public const string WorkingFile = "fixture-character-ui.cfg";

        internal static byte[] Read(uint accountId, uint characterId)
        {
            if (accountId != 990110 || characterId != 110 ||
                !(Environment.GetEnvironmentVariable("MXO_DB_NAME") ?? "").StartsWith("mxotest_", StringComparison.Ordinal) ||
                !File.Exists(WorkingFile))
                return Array.Empty<byte>();

            using var file = File.OpenRead(WorkingFile);
            if (file.Length is <= 0 or > MaxLength)
                throw new InvalidDataException("Fixture character UI exceeds the stock buffer.");
            byte[] data = new byte[(int)file.Length];
            file.ReadExactly(data);
            return data;
        }
    }
}
