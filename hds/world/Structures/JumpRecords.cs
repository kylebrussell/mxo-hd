using System;
using System.Buffers.Binary;

namespace hds
{
    /// <summary>Native jump request records; raw floating-point payloads are preserved without new admission policy.</summary>
    public static class JumpRecords
    {
        public readonly struct Destination
        {
            public readonly ulong XBits,YBits,ZBits; public readonly ushort Sector,Prefab,NavPoly;
            public Destination(ulong xBits,ulong yBits,ulong zBits,ushort sector,ushort prefab,ushort navPoly)
            { XBits=xBits;YBits=yBits;ZBits=zBits;Sector=sector;Prefab=prefab;NavPoly=navPoly; }
            public double X => BitConverter.Int64BitsToDouble(unchecked((long)XBits));
            public double Y => BitConverter.Int64BitsToDouble(unchecked((long)YBits));
            public double Z => BitConverter.Int64BitsToDouble(unchecked((long)ZBits));
        }
        public readonly struct Start
        {
            public readonly Destination Destination;public readonly uint PeakBits,TimeStamp;public readonly byte IsHyperJump;
            public Start(Destination destination,uint peakBits,byte isHyperJump,uint timeStamp)
            {Destination=destination;PeakBits=peakBits;IsHyperJump=isHyperJump;TimeStamp=timeStamp;}
        }
        public readonly struct Cancel
        {
            public readonly Destination Destination;
            public Cancel(Destination destination){Destination=destination;}
        }
        internal readonly struct Flight
        {
            private readonly uint endMs, district;
            private readonly int world, life;
            internal readonly Takeoff? InitialReport;
            internal readonly Route? FullPeakRoute;
            private readonly (double X, double Y, double Z)? accepted;
            internal uint EndMilliseconds => endMs;

            internal Flight(uint endMs, ClientData owner, Takeoff? initialReport, Route? fullPeakRoute = null,
                (double X, double Y, double Z)? acceptedDestination = null)
            {
                this.endMs = endMs;
                district = owner.getDistrictId();
                world = owner.worldGeneration;
                life = owner.lifeGeneration;
                InitialReport = initialReport;
                FullPeakRoute = fullPeakRoute;
                accepted = acceptedDestination;
            }

            internal bool IsCurrent(uint nowMs, ClientData owner) =>
                unchecked((int)(endMs - nowMs)) >= 0 &&
                district == owner.getDistrictId() && world == owner.worldGeneration && life == owner.lifeGeneration;

            /// <summary>
            /// Retail clients send 80 c4 at the accepted destination as they land. In the community captures 38 of the
            /// 40 c4s within one world unit of the destination got no redirect reply. They arrived between 1.054 s before
            /// and 0.848 s after JumpEndTime. A current flight only sees the part before the deadline. Later ones are
            /// already ignored.
            /// </summary>
            internal bool IsLandingReport(Destination report, uint nowMs) =>
                accepted is { } at && unchecked((int)(endMs - nowMs)) >= 0 && unchecked((int)(endMs - nowMs)) <= LandingReportLeadMilliseconds &&
                Math.Abs(report.X - at.X) <= LandingReportTolerance && Math.Abs(report.Y - at.Y) <= LandingReportTolerance &&
                Math.Abs(report.Z - at.Z) <= LandingReportTolerance;
        }

        /// <summary>Earliest retail landing report seen is 1.054 s before JumpEndTime; allow a little network delay.</summary>
        internal const int LandingReportLeadMilliseconds = 1250;

        /// <summary>Retail landing reports sit 0 or 1 world unit from the accepted destination on each axis.</summary>
        internal const double LandingReportTolerance = 2;

        internal readonly struct Route
        {
            internal readonly uint StartMilliseconds;
            private readonly (double X, double Y, double Z) origin;
            private readonly Destination destination;
            private readonly float peak;

            internal Route(uint startMilliseconds, (double X, double Y, double Z) origin, Destination destination, float peak)
            {
                StartMilliseconds = startMilliseconds;
                this.origin = origin;
                this.destination = destination;
                this.peak = peak;
            }

            internal (float X, float Y, float Z) PositionAt(uint nowMs, uint endMs)
            {
                double elapsed = unchecked(nowMs - StartMilliseconds);
                double duration = Math.Max(1, unchecked(endMs - StartMilliseconds));
                double progress = Math.Clamp(elapsed / duration, 0, 1);
                // The server stores the destination on admission. For a redirect, estimate the in-flight
                // origin from the accepted flight instead of snapping from that stored ground endpoint.
                double arc = 4 * peak * progress * (1 - progress);
                return ((float)(origin.X + (destination.X - origin.X) * progress),
                    (float)(origin.Y + (destination.Y - origin.Y) * progress + arc),
                    (float)(origin.Z + (destination.Z - origin.Z) * progress));
            }
        }

        internal readonly struct Takeoff
        {
            private readonly Destination origin;
            private readonly float startedAt;
            private readonly int world;
            private readonly int life;
            private readonly uint district;

            public Takeoff(Destination origin, float startedAt, ClientData owner)
            {
                this.origin = origin;
                this.startedAt = startedAt;
                world = owner.worldGeneration;
                life = owner.lifeGeneration;
                district = owner.getDistrictId();
            }

            // Stock sends a start-polygon position about one centimetre from takeoff even on successful forward jumps.
            internal bool Matches(Destination position, float now, ClientData owner)
            {
                float elapsed = now - startedAt;
                if (elapsed < 0 || elapsed > 1 || owner.worldGeneration != world || owner.lifeGeneration != life ||
                    owner.getDistrictId() != district || position.Sector != origin.Sector ||
                    position.Prefab != origin.Prefab || position.NavPoly != origin.NavPoly)
                    return false;

                double dx = position.X - origin.X, dy = position.Y - origin.Y, dz = position.Z - origin.Z;
                return dx * dx + dy * dy + dz * dz <= 16;
            }
        }

        static Destination ReadDestination(ReadOnlySpan<byte> b) => new Destination(
            BinaryPrimitives.ReadUInt64LittleEndian(b), BinaryPrimitives.ReadUInt64LittleEndian(b[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(b[16..]), BinaryPrimitives.ReadUInt16LittleEndian(b[24..]),
            BinaryPrimitives.ReadUInt16LittleEndian(b[26..]), BinaryPrimitives.ReadUInt16LittleEndian(b[28..]));
        static void WriteDestination(Span<byte> b, Destination d)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(b,d.XBits); BinaryPrimitives.WriteUInt64LittleEndian(b[8..],d.YBits);
            BinaryPrimitives.WriteUInt64LittleEndian(b[16..],d.ZBits); BinaryPrimitives.WriteUInt16LittleEndian(b[24..],d.Sector);
            BinaryPrimitives.WriteUInt16LittleEndian(b[26..],d.Prefab); BinaryPrimitives.WriteUInt16LittleEndian(b[28..],d.NavPoly);
        }
        public static bool TryStartBody(ReadOnlySpan<byte> b,out Start value)
        {
            value=default;if(b.Length!=39)return false;
            value=new Start(ReadDestination(b),BinaryPrimitives.ReadUInt32LittleEndian(b[30..]),b[34],BinaryPrimitives.ReadUInt32LittleEndian(b[35..]));return true;
        }
        public static bool TryCancelBody(ReadOnlySpan<byte> b,out Cancel value)
        { value=default;if(b.Length!=30)return false;value=new Cancel(ReadDestination(b));return true; }
        public static bool TryStart(ReadOnlySpan<byte> b,out Start value)
        { value=default;return b.Length==41&&b[0]==0x80&&b[1]==0xc2&&TryStartBody(b[2..],out value); }
        public static bool TryCancel(ReadOnlySpan<byte> b,out Cancel value)
        { value=default;return b.Length==32&&b[0]==0x80&&b[1]==0xc4&&TryCancelBody(b[2..],out value); }
        public static byte[] Encode(Start value)
        {
            var b=new byte[41];b[0]=0x80;b[1]=0xc2;WriteDestination(b.AsSpan(2),value.Destination);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(32),value.PeakBits);b[36]=value.IsHyperJump;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(37),value.TimeStamp);return b;
        }
        public static byte[] Encode(Cancel value)
        { var b=new byte[32];b[0]=0x80;b[1]=0xc4;WriteDestination(b.AsSpan(2),value.Destination);return b; }
        public static byte[] EncodeClockEcho(uint clientTickSample)
        {
            var b=new byte[6];b[0]=0x80;b[1]=0xc3;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2),clientTickSample);return b;
        }
    }
}
