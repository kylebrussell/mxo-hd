using System;
namespace hds
{
    /// <summary>One effective fixed pose for every admitted object consumer; untouched objects retain the existing facade.</summary>
    public static class MissionObjectPoses
    {
        public static bool TryAuditedBinding(StaticWorldObject raw, ushort goid, out MissionComputerAnchors.Pose pose)
        {
            pose = null;
            return raw != null && (goid == 3 || goid == MissionSites.PhoneGoId) && MissionSites.Goid(raw) == goid &&
                MissionOpenFloorAnchors.TryCurrent(raw, out pose, out _);
        }

        public static bool TryBoundStanding(ushort district, ushort goid, MissionComputerAnchors.Pose pose,
            double[] arrival, out NavPoint standing)
        {
            standing = default;
            if (pose == null || (goid != 3 && goid != MissionSites.PhoneGoId) || arrival?.Length != 3 ||
                !NavMesh.Locate(district, arrival[0], arrival[1], arrival[2], out var root) || !root.indoors)
                return false;
            double reach = goid == MissionSites.PhoneGoId ? MissionRuntime.ObjectReachUnits : 400;
            if (Math.Abs(pose.Y - (root.floorY + 95)) > (goid == MissionSites.PhoneGoId ? 300 : 400))
                return false;
            if (!NavMesh.Project(district, pose.X, root.floorY + 95, pose.Z, out var point, reach, arrival) ||
                root.sector != point.location.sector || root.region != point.location.region ||
                Math.Abs(root.floorY - point.location.floorY) > 0.01)
                return false;
            if (goid == 3 && MissionOpenFloorProfiles.TryUsable(goid, out var profile) &&
                !MissionOpenFloorUsableRange.Test(profile, new OpenFloorPoint(point.x, point.y, point.z),
                    new OpenFloorPoint(pose.X, pose.Y, pose.Z), new OpenFloorOrientation(Convert.ToHexString(pose.Orientation))).Within)
                return false;
            standing = point;
            return true;
        }

        internal static bool BoundSourceCanReach(MissionSite site, StaticWorldObject raw, bool checkStanding = false)
        {
            ushort goid = MissionSites.Goid(raw);
            if ((goid != 3 && goid != MissionSites.PhoneGoId) ||
                !MissionOpenFloorAnchors.TryCurrent(raw, out var pose, out _, out int placement) ||
                !NavMesh.TryPrefabRegion(site.metrId, raw.sectorID, placement, out int region))
                return true;

            // The source prefab is part of the audited instance row. Check it before projecting
            // a phone's shorter proximity range or doing any whole-phase placement work.
            if (MissionStreetCoverage.TryBoundArrivalSite(site, out var measured))
                return raw.sectorID == site.elevatorDoor.sectorID && region == measured.Node.Region &&
                    (!checkStanding || TryBoundStanding(site.metrId, goid, pose, measured.Standing, out _));

            var door = MissionElevatorPoses.Floor(site, null);
            var entry = MissionOpenFloorEntry.Calculate(site.metrId, door, site.CentreX, site.CentreZ,
                "bound source admission at ordinary car arrival");
            if (entry.Root == null || raw.sectorID != entry.Root.Surface.Sector || region != entry.Root.Surface.Region)
                return false;
            var root = entry.Root.Standing;
            return !checkStanding || TryBoundStanding(site.metrId, goid, pose, new[] { root.X, root.Y, root.Z }, out _);
        }

        public static MissionComputerAnchors.Pose For(MissionObjectState state)
        {
            if(state?.OpenFloor==null)
            {
                if(MissionBoundObjectPoses.Supported(state))
                {
                    if(MissionBoundObjectPoses.TryCurrent(state,out var bound,out string refusal))return bound;
                    throw new MissionCheckpointException("Bound326 pose refused: "+refusal);
                }
                if(state?.FurniturePresentation!=null)return state.FurniturePresentation();
                // A bound Computer or desk phone owns a real retail prop. Its legacy CSV position
                // can be on another part of the floor, even though its native static key is correct.
                if (TryAuditedBinding(state?.spot, state?.goid ?? 0, out var native))
                    return native;
                return MissionComputerAnchors.For(state);
            }
            var pose=state.OpenFloor;if(state.def==null||!pose.Basic(state.spot,state.goid,state.def.id))throw new InvalidOperationException("InvalidOpenFloorPose");
            var centre=pose.Centre;return new MissionComputerAnchors.Pose(centre[0],centre[1],centre[2],pose.Quaternion);
        }

        public static MissionComputerAnchors.Pose ForPresentation(MissionObjectState state)
        {
            var pose = For(state);
            if (state.PresentationGoid == state.goid || !MissionBoundFurnitureRange.Supports(state.goid)
                || MissionSites.Goid(state.spot) != state.goid)
                return pose;

            // Place a free desk phone on the supporting furniture so its model clears the source.
            // GO 1401 Model6's authored local Y half-extent is 4.40453577 world units.
            const double phoneHalfHeight = 4.40453577041626;
            double top;
            if (MissionBoundFurnitureRange.TryBounds(state, out var bounds, out _))
                top = bounds.Maximum[1];
            else
            {
                // Legacy static CSV rows include +145 Y over the model's floor-level origin.
                const double staticOriginOffset = 145;
                double height = state.goid == 326 ? 160 : 2 * MissionBoundFurniturePoses.ModelHalf(state.goid)[1];
                top = state.spot.pos_y - staticOriginOffset + height;
            }

            return new MissionComputerAnchors.Pose(pose.X, top + phoneHalfHeight, pose.Z, pose.Orientation);
        }
    }
}
