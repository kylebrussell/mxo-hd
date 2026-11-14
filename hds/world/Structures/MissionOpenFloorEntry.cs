using System;

namespace hds
{
    /// <summary>Read-only calculation of the existing Ride/NearestOnMesh entry for isolated open-floor admission.
    /// A strict open-floor root additionally requires final Locate and exact standing height; no teleport is performed.</summary>
    internal static class MissionOpenFloorEntry
    {
        internal sealed class Result
        {
            public MissionOpenFloorResolver.Position Intended { get; }
            public MissionOpenFloorResolver.Position Published { get; }
            public bool CarPlaceNear { get; }
            public bool FallbackUsed { get; }
            public bool NearestFound { get; }
            public MissionOpenFloorResolver.Root Root { get; }
            public string Refusal { get; }
            internal Result(MissionOpenFloorResolver.Position intended,MissionOpenFloorResolver.Position published,bool car,bool fallback,bool nearest,
                MissionOpenFloorResolver.Root root,string refusal)
            {Intended=intended;Published=published;CarPlaceNear=car;FallbackUsed=fallback;NearestFound=nearest;Root=root;Refusal=refusal;}
        }
        internal static Result Calculate(ushort district,MissionOpenFloorResolver.Position door,double centreX,double centreZ,string evidence)
        {
            return Calculate(door,centreX,centreZ,evidence,
                p=>PacketsUtils.PlaceNear(district,p.X,p.Y,p.Z,out _),
                p=>NavMesh.Locate(district,p.X,p.Y,p.Z,out NavLocation at)?(NavLocation?)at:null);
        }
        internal static Result Calculate(MissionOpenFloorResolver.Position door,double centreX,double centreZ,string evidence,
            Func<MissionOpenFloorResolver.Position,bool> placeNear,Func<MissionOpenFloorResolver.Position,NavLocation?> locate)
        {
            if(!double.IsFinite(door.X)||!double.IsFinite(door.Y)||!double.IsFinite(door.Z)||!double.IsFinite(centreX)||!double.IsFinite(centreZ)||
                placeNear==null||locate==null||string.IsNullOrWhiteSpace(evidence))
                return new Result(default,default,false,false,false,null,"InvalidEntryInput");
            double dx=centreX-door.X,dz=centreZ-door.Z,length=Math.Max(1,Math.Sqrt(dx*dx+dz*dz));
            double x=door.X+dx/length*200,y=door.Y-50,z=door.Z+dz/length*200;
            var intended=new MissionOpenFloorResolver.Position(x,y,z);
            if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z))
                return new Result(intended,intended,false,false,false,null,"InvalidEntryArithmetic");
            bool car=placeNear(new MissionOpenFloorResolver.Position(door.X,y,door.Z)),fallback=car&&!locate(intended).HasValue,found=false;
            if(fallback)
            {
                for(int radius=50;radius<=400&&!found;radius+=50)for(int step=0;step<16;step++)
                {
                    double angle=step*Math.PI/8,px=door.X+radius*Math.Cos(angle),pz=door.Z+radius*Math.Sin(angle);
                    if(locate(new MissionOpenFloorResolver.Position(px,y,pz)).HasValue) {x=px;z=pz;found=true;break;}
                }
                if(!found) {x=door.X;z=door.Z;}
            }
            var published=new MissionOpenFloorResolver.Position(x,y,z);NavLocation? final=locate(published);
            string refusal=!final.HasValue?"EntryLocateFailed":!double.IsFinite(final.Value.floorY)?"EntryNonfiniteFloor":Math.Abs(y-(final.Value.floorY+95))>MissionOpenFloorResolver.Epsilon?"EntryStandingHeightMismatch":null;
            var root=refusal==null?new MissionOpenFloorResolver.Root(new MissionOpenFloorResolver.Id(final.Value.sector,final.Value.region,final.Value.polygon),published,evidence):null;
            return new Result(intended,published,car,fallback,found,root,refusal);
        }
    }
}
