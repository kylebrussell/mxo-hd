"""Independent test oracle: edge intersections plus even/odd point location.

This deliberately does not import the planner's clipping/inside/route helpers.
"""

def _turn(a,b,c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])


def _on(a,b,p):
    return (abs(_turn(a,b,p)) <= 1e-6
            and min(a[0],b[0])-1e-7 <= p[0] <= max(a[0],b[0])+1e-7
            and min(a[1],b[1])-1e-7 <= p[1] <= max(a[1],b[1])+1e-7)


def _point_in_closed_polygon(p, polygon):
    inside=False
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if _on(a,b,p):return True
        if (a[1] > p[1]) != (b[1] > p[1]):
            x=a[0]+(p[1]-a[1])*(b[0]-a[0])/(b[1]-a[1])
            if p[0] < x:inside=not inside
    return inside


def _edges_meet(a,b,c,d):
    if any((_on(a,b,c),_on(a,b,d),_on(c,d,a),_on(c,d,b))):return True
    return ((_turn(a,b,c)>0) != (_turn(a,b,d)>0)
            and (_turn(c,d,a)>0) != (_turn(c,d,b)>0))


def route_is_clear(route, polygon):
    points=[(p[0],p[2]) for p in route]
    if any(_point_in_closed_polygon(p,polygon) for p in points):return False
    return not any(_edges_meet(a,b,c,d)
        for a,b in zip(points,points[1:])
        for c,d in zip(polygon,polygon[1:]+polygon[:1]))
