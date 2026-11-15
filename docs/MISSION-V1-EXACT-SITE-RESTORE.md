# Preserve a V1 mission site after relog

Legacy world CSV data can contain several poses for the same static door key. V1 restoration previously selected the first world/sector/key match, so an earned 101210 carry relog changed its site door from 54200/2945/-16700 to 53200/2945/-17700 while keeping the original waypoint and exit.

Restoration now matches the complete captured raw door using the existing source fingerprint: world, sector, key, GO/type, static ID, exterior flag, XYZ, rotation and quaternion. It neither moves the door nor rewrites waypoints, exits or mission progress. If the captured row is missing, another pose cannot replace it.

V1 has no cluster fingerprint. When multiple clusters share the identical raw door, their existing first-match order remains; this change does not claim complete V2 identity or native room/accessibility validation. The retained 86da run earned 101208 and 101209 before exposing this 101210 restore mismatch. Existing V1 persistence coverage and a verbatim earned checkpoint exercise the correction.
