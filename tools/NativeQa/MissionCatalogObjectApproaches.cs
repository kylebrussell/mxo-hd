using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using HeadlessClient;
using hds;

namespace NativeQa;

internal static class MissionCatalogObjectApproaches
{
    internal sealed record Approach(ushort ObjectId, string Kind, double[]? Centre, NavPoint? Stand, byte? Yaw)
    {
        public string? Preview { get; init; }

        private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public string Describe()
        {
            string prefix = $"Mission object {ObjectId} {Kind}";
            if (Stand is not NavPoint point)
            {
                string near = Centre == null ? "" : " near " + string.Join(",", Centre.Select(Number));
                return prefix + ": no NAV-valid stand point found" + near + ".";
            }

            var nav = point.location;
            string xyz = string.Join(",", point.Position.Select(Number));
            string args = string.Join(" ", point.Position.Select(Number));
            return $"{prefix}: stand {xyz} face {Yaw:X2} (yaw hex). "
                + $"Assisted: ?tp {args} {nav.sector:X} {nav.region:X} {nav.polygon:X} - {Yaw:X2}"
                + (Preview == null ? "" : " " + Preview);
        }
    }

    internal static Approach[] Find(MissionProgress.SavedState state, string root)
    {
        if (state.Objects.Count == 0) return Array.Empty<Approach>();

        var kinds = Directory.EnumerateFiles(Path.Combine(root, "data", "missions"), "*.xml")
            .Select(file => XElement.Load(file))
            .First(mission => (int?)mission.Attribute("id") == state.Mission)
            .Elements("phase").ElementAt(state.Phase).Elements("object")
            .ToDictionary(obj => (ushort)(int)obj.Attribute("id")!, obj => (string?)obj.Attribute("kind") ?? "Furniture");
        var street = MervFollowupStreetEntry.ForSite(state);
        return state.Objects.OrderBy(obj => obj.Id).Select(obj =>
        {
            string kind = kinds.GetValueOrDefault(obj.Id, "Prop");
            if (obj.Spot == null) return new Approach(obj.Id, kind, null, null, null);
            ushort district = obj.Spot.metrId;
            double[] centre;
            NavPoint stand;
            if (obj.OpenFloor is { } placement)
            {
                centre = placement.Centre;
                if (TryStand(district, placement.Standing, out stand))
                    return Facing(obj.Id, kind, centre, stand);
                return new Approach(obj.Id, kind, centre, null, null);
            }

            if (street is not null && street.Raw.GetProperty("Phase").GetInt32() == state.Phase)
            {
                foreach (string key in new[] { "FurnitureRoutes", "BoundRoutes" })
                {
                    if (!street.Raw.TryGetProperty(key, out var envelope)) continue;
                    foreach (var route in envelope.GetProperty("Routes").EnumerateArray())
                    {
                        if (route.GetProperty("ObjectId").GetUInt16() != obj.Id) continue;
                        centre = Vector(route.GetProperty("NativePosition"));
                        return TryStand(district, Vector(route.GetProperty("Standing")), out stand)
                            ? Facing(obj.Id, kind, centre, stand)
                            : new Approach(obj.Id, kind, centre, null, null);
                    }
                }
            }

            var pose = MissionObjectPoses.For(new MissionObjectState { spot = obj.Spot, goid = obj.Goid });
            centre = new[] { pose.X, pose.Y, pose.Z };
            return TryNearby(obj, centre, street?.Floor.Standing, out stand)
                ? Facing(obj.Id, kind, centre, stand)
                : new Approach(obj.Id, kind, centre, null, null);
        }).Select(approach => WithPirateBroadcastPreview(state, approach)).ToArray();
    }

    private static Approach WithPirateBroadcastPreview(MissionProgress.SavedState state, Approach approach)
    {
        if (state.Mission != 210203 || state.Phase != 0 || approach.ObjectId != 1 || approach.Centre is not { } centre)
            return approach;
        var saved = state.Objects.Single(obj => obj.Id == approach.ObjectId);
        if (saved.Spot?.mxoStaticId != 249561936 || saved.Goid != 326)
            return approach;

        var source = new MissionObjectState { def = new MissionObjectDef { kind = "Phone" }, spot = saved.Spot, goid = saved.Goid };
        var phone = MissionObjectPoses.ForPresentation(source);
        double[] preview = { 9572, 2095, 6893 };
        double dx = preview[0] - centre[0], dz = preview[2] - centre[2];
        double reach = MissionRuntime.ObjectReachUnits;
        if (phone.X != 9301 || Math.Abs(phone.Y - 2230.140896) > 0.001 || phone.Z != 6845
            || dx * dx + dz * dz <= reach * reach
            || !TryStand(saved.Spot.metrId, preview, out var view))
            return approach;

        byte yaw = MobAggro.YawTowards(view.x, view.z, phone.X, phone.Z);
        return approach with
        {
            Preview = "Before triggering the goal, look at the top of the cardboard box on the gray cabinet by the door. "
                + $"Preview: ?tp 9572 2095 6893 {view.location.sector:X} {view.location.region:X} {view.location.polygon:X} - {yaw:X2}"
        };
    }

    private static double[] Vector(JsonElement vector) => vector.EnumerateArray().Select(v => v.GetDouble()).ToArray();

    private static Approach Facing(ushort id, string kind, double[] centre, NavPoint stand) =>
        new(id, kind, centre, stand, MobAggro.YawTowards(stand.x, stand.z, centre[0], centre[2]));

    private static bool TryStand(ushort district, double[] position, out NavPoint stand)
    {
        stand = default;
        // Validate the same representation ?tp sends to the client.
        double x = (float)Math.Round(position[0], 3), y = (float)Math.Round(position[1], 3), z = (float)Math.Round(position[2], 3);
        if (!NavMesh.Locate(district, x, y, z, out var nav) || Math.Abs(y - (nav.floorY + 95)) > 1)
            return false;
        stand = new NavPoint { x = x, y = y, z = z, location = nav };
        return true;
    }

    private static bool TryNearby(MissionProgress.SavedObject obj, double[] centre, double[]? arrival, out NavPoint stand)
    {
        stand = default;
        const double preferredDistance = 150;
        var raw = obj.Spot;
        ushort district = raw.metrId;
        double expectedY = arrival?[1] ?? raw.pos_y;
        double maxVertical = MissionStaging.IsClickable(obj.Goid) ? 200 : 300;
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            double x = centre[0] + preferredDistance * Math.Sin(angle), z = centre[2] + preferredDistance * Math.Cos(angle);
            if (!NavMesh.Project(district, x, expectedY, z, out var candidate, maxDistance: 65, reachableFrom: arrival)
                || !TryStand(district, candidate.Position, out stand)) continue;

            double dx = stand.x - centre[0], dz = stand.z - centre[2];
            if (stand.location.indoors != raw.exterior && (arrival == null || Math.Abs(stand.y - expectedY) <= 30)
                && dx * dx + dz * dz <= MissionRuntime.ObjectReachUnits * MissionRuntime.ObjectReachUnits
                && Math.Abs(stand.y - centre[1]) <= maxVertical)
                return true;
        }
        return false;
    }
}
