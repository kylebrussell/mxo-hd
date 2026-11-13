"""Explicit permission for conservative missing-layer export, before any output is opened."""
def require_explicit_omission(kinds, allow_sid=False, allow_static_layers=False):
    if not kinds:
        return
    if allow_static_layers or (allow_sid and all(k == 'SID' for k in kinds)):
        return
    raise ValueError('Required static layer resource missing; strict default refuses export. '
                     'SID-only permission cannot omit SIDI/SIDG; explicit whole-sector omission is required.')
