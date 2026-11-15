"""Native top-level IPI pose math (client.dll6246b0c0/62111670/62046a40).
Only a validated direct sector parent is accepted; +145 server Y is NOT native math.
"""
import math
import struct


def f32(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError('Nonfinite native transform value')
    return struct.unpack('<f', struct.pack('<f', value))[0]


def quarter_turn(point, rotation):
    if rotation not in (0, 1, 2, 3):
        raise ValueError('Unsupported native quarter turn')
    x, y, z = map(f32, point)
    x, z = ((x, z), (z, -x), (-x, -z), (-z, x))[rotation]
    return f32(x), y, f32(z)


def rotation_quaternion(rotation):
    if rotation not in (0, 1, 2, 3):
        raise ValueError('Unsupported native quarter turn')
    h = f32(math.sqrt(.5))
    return ((0., 0., 0., 1.), (0., h, 0., h), (0., 1., 0., 0.), (0., -h, 0., h))[rotation]


def compose_quaternion(parent, local):
    # Native62046a40: parent Hamilton local; one float32 store per output component.
    a, b, c, d = map(f32, parent)
    e, f, g, h = map(f32, local)
    return tuple(map(f32, (d*e+a*h+b*g-c*f, d*f-a*g+b*h+c*e,
                           d*g+a*f-b*e+c*h, d*h-a*e-b*f-c*g)))


def sector_pose(position, quaternion, pivot, rotation, sector_corner, *, parent_kind):
    if parent_kind != 'direct-sector':
        raise ValueError('Unrepresented nested/EPI parent: refuse to flatten')
    if len(pivot) != 3 or any(not isinstance(v, int) or not -32768 <= v <= 32767 for v in pivot):
        raise ValueError('Native pivot must be three signed i16 values')
    #624685b0 stores each signed-i16 *10 as float32.62045e10 stores parent composition;
    #62111670 stores rotated point, then624cf0cb adds/stores translation. No normalization.
    translation = tuple(f32(f32(v*10) + f32(c)) for v, c in zip(pivot, sector_corner))
    if len(translation) != 3:
        raise ValueError('Sector corner must have three components')
    point = tuple(f32(v+t) for v,t in zip(quarter_turn(position, rotation), translation))
    return point, compose_quaternion(rotation_quaternion(rotation), quaternion)
