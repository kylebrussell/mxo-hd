"""Offline geometry experiment for free GO548 Boxes. No server/gameplay imports.

The footprint is the measured closed BASE mesh, not animated/actor clearance.
All distances use existing NAV world units. EPS is numerical tolerance only.
"""
from __future__ import annotations

from collections import deque
from dataclasses import dataclass
import math
import struct

EPS = 1e-7
Point = tuple[float, float]
SurfaceId = tuple[int, int, int]


def cross(a: Point, b: Point) -> float:
    return a[0] * b[1] - a[1] * b[0]


def sub(a: Point, b: Point) -> Point:
    return a[0] - b[0], a[1] - b[1]


def area(p: tuple[Point, ...]) -> float:
    return sum(cross(a, b) for a, b in zip(p, p[1:] + p[:1])) / 2


def convex(p: tuple[Point, ...]) -> bool:
    return (len(p) >= 3 and all(math.isfinite(x) for v in p for x in v)
            and area(p) > EPS and all(math.dist(a, b) > EPS for a, b in zip(p, p[1:] + p[:1]))
            and all(cross(sub(b, a), sub(v, a)) >= -EPS
                    for a, b in zip(p, p[1:] + p[:1]) for v in p))


def inside(p: tuple[Point, ...], q: Point) -> bool:
    return all(cross(sub(b, a), sub(q, a)) >= -EPS for a, b in zip(p, p[1:] + p[:1]))


@dataclass(frozen=True)
class Surface:
    id: SurfaceId
    vertices: tuple[Point, ...]  # CCW convex, world coordinates
    plane: tuple[float, float, float]  # y = fx*x + fz*z + c

    @property
    def flat(self) -> bool:
        return self.plane[0] == 0 and self.plane[1] == 0


@dataclass(frozen=True)
class Root:
    surface: SurfaceId
    standing: tuple[float, float, float]
    evidence: str


@dataclass(frozen=True)
class RawPose:
    position: tuple[float, float, float]
    quaternion_hex: str  # preserve original bytes, including f32 rounding


def footprint(raw: RawPose) -> tuple[Point, ...]:
    if len(raw.position) != 3 or not all(math.isfinite(v) for v in raw.position):
        raise ValueError("InvalidRawPosition")
    try:
        q = bytes.fromhex(raw.quaternion_hex)
        if len(q) != 16:
            raise ValueError()
        x, y, z, w = struct.unpack('<4f', q)
    except (ValueError, struct.error):
        raise ValueError("InvalidQuaternion") from None
    if not all(math.isfinite(v) for v in (x, y, z, w)):
        raise ValueError("InvalidQuaternion")
    if x != 0 or z != 0:
        raise ValueError("TiltedQuaternion")
    n = y*y + w*w
    if abs(n - 1) > 1e-5:
        raise ValueError("NonUnitQuaternion")
    # Normalize only the mathematical yaw calculation; never modify raw bytes.
    c, s = (w*w-y*y)/n, 2*y*w/n
    return tuple((c*a+s*b, -s*a+c*b) for a, b in ((-50, -70), (50, -70), (50, 70), (-50, 70)))


def erode(p: tuple[Point, ...], offsets: tuple[Point, ...]) -> tuple[Point, ...]:
    """Exact convex half-plane erosion, no grid or inset fallback."""
    result = p
    for a, b in zip(p, p[1:] + p[:1]):
        e = sub(b, a)
        limit = -min(cross(e, o) for o in offsets)
        def side(v):
            return cross(e, sub(v, a)) - limit
        output = []
        for v, u in zip(result, result[1:] + result[:1]):
            sv, su = side(v), side(u)
            if sv >= -EPS:
                output.append(v)
            if (sv >= 0) != (su >= 0):
                t = sv / (sv-su)
                output.append((v[0] + t*(u[0]-v[0]), v[1] + t*(u[1]-v[1])))
        clean = []
        for v in output:
            if not clean or math.dist(v, clean[-1]) > EPS:
                clean.append(v)
        if len(clean) > 1 and math.dist(clean[0], clean[-1]) <= EPS:
            clean.pop()
        result = tuple(clean)
        if len(result) < 3 or abs(area(result)) <= EPS:
            return ()  # conservative: zero-area feasible sets not supported
    return result


def nearest(p: tuple[Point, ...], q: Point) -> Point:
    if inside(p, q):
        return q
    options = []
    for a, b in zip(p, p[1:] + p[:1]):
        e = sub(b, a)
        if math.hypot(*e) <= EPS:
            continue
        t = max(0., min(1., sum(v*w for v, w in zip(sub(q, a), e)) / sum(v*v for v in e)))
        v = a[0]+t*e[0], a[1]+t*e[1]
        options.append((sum(v*v for v in sub(v, q)), v))
    return min(options)[1]


def portal(a: Surface, b: Surface) -> Point | None:
    """Positive-length collinear shared boundary; no corner/nearby joins."""
    if a.plane[2] != b.plane[2]:
        return None
    for p, q in zip(a.vertices, a.vertices[1:] + a.vertices[:1]):
        e = sub(q, p)
        length = math.hypot(*e)
        for u, v in zip(b.vertices, b.vertices[1:] + b.vertices[:1]):
            if abs(cross(e, sub(u, p))) > EPS*length or abs(cross(e, sub(v, p))) > EPS*length:
                continue
            dot = lambda w: sum(x*y for x, y in zip(sub(w, p), e)) / (length*length)
            lo, hi = max(0., min(dot(u), dot(v))), min(1., max(dot(u), dot(v)))
            if (hi-lo)*length > EPS:
                t = (lo+hi)/2
                return p[0]+t*e[0], p[1]+t*e[1]
    return None


def segment_touches_closed_base(a: Point, b: Point, shape: tuple[Point, ...]) -> bool:
    """Convex segment clipping; contact with the closed base is also blocked."""
    lo, hi = 0., 1.
    for p, q in zip(shape, shape[1:] + shape[:1]):
        edge = sub(q, p)
        tolerance = EPS * max(1., math.hypot(*edge))
        start = cross(edge, sub(a, p)) + tolerance
        change = cross(edge, sub(b, a))
        if change == 0:
            if start < 0:
                return False
        elif change > 0:
            lo = max(lo, -start/change)
        else:
            hi = min(hi, -start/change)
        if lo > hi:
            return False
    return True


def route_avoids_closed_base(route: tuple[Point, ...], shape: tuple[Point, ...]) -> bool:
    # Degenerate segments explicitly validate every point, including a same-
    # polygon root with no portal segments. No radius or alternative packing.
    return (all(not segment_touches_closed_base(p, p, shape) for p in route)
            and all(not segment_touches_closed_base(a, b, shape)
                    for a, b in zip(route, route[1:])))


def plan(surfaces: tuple[Surface, ...], root: Root | None, raw: RawPose) -> dict:
    """Return one geometric placement, or NoPlan. Does not certify use/clearance.

    The explicitly supplied polygon set bounds the experiment. There is no
    auto-nearest floor, dropped root, arbitrary radius, or production mutation.
    """
    def no(reason):
        return dict(status='NoPlan', reason=reason, rawPosition=raw.position,
                    rawQuaternion=raw.quaternion_hex)
    try:
        offsets = footprint(raw)
    except ValueError as e:
        return no(str(e))
    if root is None or not isinstance(root.evidence, str) or not root.evidence.strip():
        return no('MissingExplicitRootEvidence')
    if len(root.standing) != 3 or not all(math.isfinite(v) for v in root.standing):
        return no('InvalidRootPoint')
    if len({s.id for s in surfaces}) != len(surfaces):
        return no('DuplicateSurfaceIdentity')
    if any(len(s.plane) != 3 or not all(math.isfinite(v) for v in s.plane) for s in surfaces):
        return no('MalformedSurface')
    by_id = {s.id: s for s in surfaces}
    start = by_id.get(root.surface)
    if start is None:
        return no('RootSurfaceAbsent')
    if not convex(start.vertices):
        return no('MalformedRootSurface')
    if not start.flat:
        return no('RootIsNotFlat')
    rx, ry, rz = root.standing
    if abs(ry - (start.plane[2]+95)) > EPS or not inside(start.vertices, (rx, rz)):
        return no('RootPointOrStandingHeightMismatch')
    # Uncertain/concave polygons are obstacles, never silently convexified or
    # traversed. An unrelated malformed polygon must not poison a valid root.
    rejected = [s.id for s in surfaces if not convex(s.vertices)]
    flat = sorted((s for s in surfaces if s.flat and s.plane[2] == start.plane[2]
                   and convex(s.vertices)), key=lambda s: s.id)
    previous = {start.id: None}
    portals = {}
    queue = deque([start])
    while queue:
        current = queue.popleft()
        for other in flat:
            if other.id not in previous:
                p = portal(current, other)
                if p is not None:
                    previous[other.id] = current.id
                    portals[other.id] = p
                    queue.append(other)
    candidates = []
    for s in flat:
        if s.id not in previous:
            continue
        feasible = erode(s.vertices, offsets)
        if not feasible:
            continue
        centre = nearest(feasible, (raw.position[0], raw.position[2]))
        shape = tuple((centre[0]+x, centre[1]+z) for x, z in offsets)
        path = [s.id]
        while previous[path[-1]] is not None:
            path.append(previous[path[-1]])
        path.reverse()
        # End at the actual destination ingress, not at a root-projected point
        # whose final segment can cut through the newly placed Box itself.
        route_xz = ((rx, rz),) + tuple(portals[i] for i in path[1:])
        approach = route_xz[-1]
        if not inside(s.vertices, approach) or not route_avoids_closed_base(route_xz, shape):
            continue
        candidates.append((math.dist(centre, (raw.position[0], raw.position[2])), s.id,
                           centre, approach, path, route_xz))
    if not candidates:
        return no('NoPositiveAreaFootprintWithClearIngressRoute')
    distance, sid, centre, approach, path, route_xz = min(candidates)
    floor = start.plane[2]
    chosen = by_id[sid]
    route = [(x, floor+95, z) for x, z in route_xz]
    return dict(status='Plan', rawPosition=raw.position, rawQuaternion=raw.quaternion_hex,
                root=dict(surface=root.surface, standing=root.standing, evidence=root.evidence),
                surface=sid, surfaceY=floor, centre=(centre[0], floor+40, centre[1]),
                surfaceVertices=chosen.vertices, feasibleCentreRegion=erode(chosen.vertices,offsets),
                baseFootprintXZ=tuple((centre[0]+x,centre[1]+z) for x,z in offsets),
                approach=(approach[0], floor+95, approach[1]), displacementXZ=distance,
                approachDistanceXZ=math.dist(approach,centre),
                approachDistance3D=math.sqrt(math.dist(approach,centre)**2+55**2),
                useRangeValidated=False,
                componentPolygons=len(previous), feasiblePolygons=len(candidates),
                excludedUncertainSurfaces=rejected,
                capacity='One Box only; feasible polygons are not a simultaneous allocation count',
                route=route, routePolygons=path,
                witness='NAV point/shared-edge path avoids this closed Box base including boundary contact; no avatar, animation, other scene collision, native walking or use-range certification')
