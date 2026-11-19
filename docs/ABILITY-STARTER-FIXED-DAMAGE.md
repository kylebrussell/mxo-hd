# Starter fixed ability damage

The shipped health modifiers give Tinkering (36) a fixed viral base of 4 and Hindering Shot (130) a fixed ballistic base of 9. Both legacy rows contain 0/0, which previously reached the generic 50–80 draw. The existing Spinning Backsweep path now shares the same qualification for a single enabled, instant, positive fixed health modifier of the reviewed type. A valid legacy range still takes precedence. Typed influence and resistance still run at their established call sites.

## Stock observations

Use `ability-parity-starter-fixed-damage` on level-11 CSR110 with 36 of 41 memory. The fixture owns both prerequisite chains at rank 10 and supplies a handgun. QAEnemy stands 4 m away.

1. Select QAEnemy, select Coder Style and activate Tinkering. Capture the outgoing 43-byte ability request, 6-second cast bar, viral hit and 30-second reuse.
2. Remain out of interlock, select Handguns and activate Hindering Shot against QAEnemy. Capture the outgoing request, 2-second cast bar, ballistic hit and 8-second own reuse. Keep the four source-specific 8-second modifier adds and removals: Movement Speed −50, Sneak Movement −50, Invisibility Movement −50 and Force Combat −10.

Stock QA on PR389 recorded both 43-byte hotbar requests, the four-point Tinkering hit and nine-point Hindering Shot hit, the cast and reuse timers, and all four Hindering Shot modifiers. See PacketLog d3b7112643d9. The disposable contest draw isolates the Tinkering hit. Hindering Shot's movement consumers and stock choreography remain open, as do Tinkering's other area recipients, geometry and native damage scaling.

## Advanced Tinkering

GOB 514 has one fixed instant viral health modifier worth 6 and an incomplete legacy range. The same fallback now supplies its base damage. Its existing typed damage, target-centred selection, 6-second cast and 30-second reuse still apply.

In `ability-parity-advanced-tinkering`, select Coder Style and QAEnemy at 4 m, then use Advanced Tinkering. Capture the ability request, target damage, casting time and reuse. The level-11 profile uses 21 of 41 memory and loads the explicit prerequisites. The replay takes the complete stock Tinkering request from PR389 and substitutes ability 514 and the live view and spawn IDs. Stock emission for 514 and broader area and scaling remain open.
