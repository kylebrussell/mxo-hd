# Starter fixed ability damage

The shipped health modifiers give Tinkering (36) a fixed viral base of 4 and Hindering Shot (130) a fixed ballistic base of 9. Both legacy rows contain 0/0, which previously reached the generic 50–80 draw. The existing Spinning Backsweep path now shares the same qualification for a single enabled, instant, positive fixed health modifier of the reviewed type. A valid legacy range still takes precedence. Typed influence and resistance still run at their established call sites.

## Stock observations

Use `ability-parity-starter-fixed-damage` on level-11 CSR110 with 36 of 41 memory. The fixture owns both prerequisite chains at rank 10 and supplies a handgun. QAEnemy stands 4 m away.

1. Select QAEnemy, select Coder Style and activate Tinkering. Capture the outgoing 43-byte ability request, 6-second cast bar, viral hit and 30-second reuse.
2. Remain out of interlock, select Handguns and activate Hindering Shot against QAEnemy. Capture the outgoing request, 2-second cast bar, ballistic hit and 8-second own reuse. Keep the four source-specific 8-second modifier adds and removals: Movement Speed −50, Sneak Movement −50, Invisibility Movement −50 and Force Combat −10.

The headless fixture uses the established inline 43-byte 80b9 combat request format, with the live view/spawn handle. Stock emission for these two specific buttons still needs capture. If the client greys out an action or never sends the request, record the available style, weapon, target and interlock state. The disposable viral contest draw isolates Tinkering's landed effect. Hindering Shot's timed record values leave the movement consumers and stock choreography open. Tinkering's other area recipients, geometry and native damage scaling remain separate checks.
