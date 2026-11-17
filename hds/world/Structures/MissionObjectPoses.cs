using System;
namespace hds
{
    /// <summary>One effective fixed pose for every admitted object consumer; untouched objects retain the existing facade.</summary>
    public static class MissionObjectPoses
    {
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
                return MissionComputerAnchors.For(state);
            }
            var pose=state.OpenFloor;if(state.def==null||!pose.Basic(state.spot,state.goid,state.def.id))throw new InvalidOperationException("InvalidOpenFloorPose");
            var centre=pose.Centre;return new MissionComputerAnchors.Pose(centre[0],centre[1],centre[2],pose.Quaternion);
        }
    }
}
