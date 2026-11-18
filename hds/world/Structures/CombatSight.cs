using hds.shared;

namespace hds
{
    /// <summary>Combat uses the mapped floor path until collision geometry is available.</summary>
    internal static class CombatSight
    {
        internal static (double X, double Y, double Z) Position(WorldClient player)
        {
            double x=0, y=0, z=0;
            NumericalUtils.LtVector3dToDoubles(player.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return (x, y, z);
        }

        internal static bool Between(WorldClient source, Mob target) =>
            From(source, target.getXPos(), target.getYPos(), target.getZPos());

        internal static bool Between(WorldClient source, WorldClient target)
        {
            var p = Position(target);
            return From(source, p.X, p.Y, p.Z);
        }

        internal static bool From(WorldClient source, double x, double y, double z)
        {
            var p = Position(source);
            return Clear(source, p.X, p.Y, p.Z, x, y, z);
        }

        internal static bool Clear(WorldClient source, double x, double y, double z, double tx, double ty, double tz) =>
            NavMesh.HasSightPath((ushort)source.playerData.getDistrictId(), x, y, z, tx, ty, tz, rejectUnmappedHeightDifference: false);
    }
}
