using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace hds
{
    /// <summary>Native 7.6005 records; decoder/consumer addresses and limits in docs/DATA-MINING.md.</summary>
    public static class DataMiningPackets
    {
        public const uint NodeFx = 0x28000462; // DataNode 606 FX/FXID, gameobjects.gob
        public static byte[] Clear() => new byte[] { 0x80, 0xcf };
        public static byte[] Remove(ushort id) => new byte[] { 0x80, 0xce, (byte)id, (byte)(id >> 8) };
        public static byte[] Indicator(byte angle, byte percent) => new byte[] { 0x80, 0xcb, angle, percent };
        public static byte[] Marker(ushort id, uint rezId, double x, double y, double z)
        {
            using var s = new MemoryStream(); using var w = new BinaryWriter(s);
            w.Write((byte)0x80); w.Write((byte)0xcd); w.Write(id); w.Write(rezId);
            w.Write((float)x); w.Write((float)y); w.Write((float)z);
            return s.ToArray();
        }

        public readonly struct Ping
        {
            public readonly sbyte X, Z;
            public readonly byte Relationship;
            public Ping(sbyte x, sbyte z, byte relationship) { X = x; Z = z; Relationship = relationship; }
        }
        /// <summary>Coordinates relative to the recipient, in metres. Skip unrepresentable pings rather than wrap.</summary>
        public static Ping? Relative(double px, double pz, double x, double z, byte relationship)
        {
            double dx = Math.Round((x - px) / 100), dz = Math.Round((z - pz) / 100);
            if (!double.IsFinite(dx) || !double.IsFinite(dz) || dx < -128 || dx > 127 || dz < -128 || dz > 127) return null;
            return new Ping((sbyte)dx, (sbyte)dz, relationship);
        }

        public static byte[] Map(IEnumerable<Ping> positions, byte seconds)
        {
            Ping[] p = positions.Take(128).ToArray(); // ours: bound one RPC well below the datagram limit
            using var s = new MemoryStream(); using var w = new BinaryWriter(s);
            w.Write((byte)0x80); w.Write((byte)0xc5); w.Write((ushort)p.Length); w.Write(seconds);
            foreach (Ping v in p) { w.Write((byte)0); w.Write(v.X); w.Write(v.Z); w.Write(v.Relationship); }
            return s.ToArray();
        }
    }
}
