# Starter Concealed Shot damage

Concealed Shot (504) has one shipped instantaneous ballistic Current Health damage modifier with fixed endpoints 22 and 22. Its old legacy row has 4/0. A baseline run with RNG seed 1 drew 57 from the generic fallback. The server now reads the fixed 22 when a valid legacy range is absent. The GOB's Armor Piercing field remains open for separate evidence.

The level-19 profile uses 42/49 memory. It loads Rifles (147), Concealment (503), Concealed Shot and their explicit prerequisites. Concealment needs Rifles trained to 16 and the shot needs Concealment trained to 18. The visible Sniper discipline has a higher requirement and stays unloaded. The fixture replays ordinary Archive unload and reload for 504 and its parent 503. It verifies removal and restoration of the live Concealment grant, activates it, then fires while concealed and outside interlock.

In `ability-parity-starter-concealed-shot`, use the rifle equipped at login, select Rifles, activate Concealment and shoot QAEnemy at 4 m. The disposable uses the selected, out-of-combat rifle request from PacketLog 05837386fab5 line 5004, substituting 504 and the live target handle. A native press of 504 remains to be observed. If the stock Actions list lacks the granted Concealment action, record that before using the fixture's `?stealth conceal` fallback.

The existing Sniper tests cover the separate 30% marker, 0.1-second delay, four-second Stun and immunity. This damage change preserves the existing landing and callback path. Full damage scaling, armor piercing and stock display remain open.
