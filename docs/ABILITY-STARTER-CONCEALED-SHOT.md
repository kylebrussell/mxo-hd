# Starter Concealed Shot damage

Concealed Shot (504) has one shipped instantaneous ballistic Current Health damage modifier with fixed endpoints 22 and 22. Its old legacy row has 4/0. A baseline run with RNG seed 1 drew 57 from the generic fallback. The server now reads the fixed 22 when a valid legacy range is absent. The GOB's Armor Piercing field remains open for separate evidence.

The level-19 profile uses 42/49 memory. It loads Rifles (147), Concealment (503), Concealed Shot and their explicit prerequisites. Concealment needs Rifles trained to 16 and the shot needs Concealment trained to 18. The visible Sniper discipline has a higher requirement and stays unloaded. The fixture replays ordinary Archive unload and reload for 504 and its parent 503. It verifies removal and restoration of the live Concealment grant, activates it, then fires while concealed and outside interlock.

In `ability-parity-starter-concealed-shot`, use the rifle equipped at login, select Rifles, activate Concealment and shoot QAEnemy at 4 m. PR410 confirmed the ordinary 504 press, six-second cast, 23 damage, and 45-second reuse after fixture-assisted Concealment. The updated replay expects a rank-19 1137 grant. Test the 1137 Actions entry separately in `ability-parity-starter-concealment-rank`. [PR410](https://github.com/kylebrussell/mxo-hd/pull/410).

The existing Sniper tests cover the separate 30% marker, 0.1-second delay, four-second Stun and immunity. This damage change preserves the existing landing and callback path. Full damage scaling, armor piercing and stock display remain open.
