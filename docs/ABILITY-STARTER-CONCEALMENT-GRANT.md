# Starter Concealment grant rank

Concealment (503) grants 1137 through a while-loaded function-2 modifier with no separate endpoints or scale. The existing function-2 value rule uses the learned parent rank. The grant has a level cap of 50 and uses the same qualified data shape as the Sneak grant.

The baseline sent rank 0 for 1137 on ordinary Archive reload. The live Concealment activation then fell back to character level for its Stealth modifier. The corrected grant carries the learned rank in the existing Archive record and loaded membership. With character level 19 and Concealment trained to 18, the existing 1137 activation applies Stealth +18.

Use `ability-parity-starter-concealment-rank` to check both records. Unload and reload 503 normally. Select Rifles and activate the granted Concealment action if it appears in Actions. Record any gap in Actions visibility, then use `?stealth conceal` with 503 loaded to sample the modifier separately if needed. The disposable uses the ordinary Archive change and full self-activation request. Stock UI visibility remains open.

Concealed Shot armor piercing also remains open. The shipped client loads its GOB flag into modifier byte +0x18 at 62520cc9. The tooltip paths at 621eefcc and 621e96c5 display term 0x7a00025f. The reviewed health modifier slice does not establish how this flag changes authoritative resistance inputs.
