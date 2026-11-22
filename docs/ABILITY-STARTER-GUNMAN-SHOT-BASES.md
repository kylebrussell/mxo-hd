# Starter Body Shot and Covering Fire damage

Body Shot (105) and Covering Fire (176) each have one target Current Health GOB modifier with ballistic damage and instant duration. Its endpoints are equal, 8 for Body Shot and 17 for Covering Fire. Their legacy endpoint pairs are −5/0 and 50/0. The baseline test used RNG seed 1 and drew 57 for both through the generic fallback. The server now reads the shipped fixed base when no valid legacy range exists.

The earlier native sessions exercised the existing HDS landing path and named moves. Body Shot played 11364 and Covering Fire played 21083. The recorded old damage of 69 and 101 reflects the earlier runtime. The replay reuses each matching full request and substitutes only the fixture's live target handle. The Body Shot request is in PacketLog 54065c576c56 at line 2358. The interlock Covering Fire request is in 05837386fab5 at line 10362. [Body Shot stock run](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) · [Covering Fire stock run](https://github.com/kylebrussell/mxo-hd/pull/326#issuecomment-7002053269)

The level-19 stock fixtures start separately with the correct weapon equipped. In either fixture, use Close Combat on QAEnemy and reselect the required gun style after interlock starts.

- `ability-parity-starter-body-shot` starts with a rifle. Select Rifles (147). Land Body Shot (105) for 8 on move 11364 and confirm both −5 resistance records for ten seconds.
- `ability-parity-starter-covering-fire` starts with a single SMG. Select Submachine Guns (158). Land Covering Fire (176) for 18 after style influence on move 21083.

The focused callback tests preserve Body Shot's two target resistance penalties and the current Covering Fire root policy at draws on both sides of 15%. A landed root lasts eight seconds and retains its existing later-damage break threshold and first-health-change protection.

### Body Shot source evidence

The one enabled health component in GOB 105 specifies instant ballistic damage and sets both endpoints to 8. It has no value function or scale. The queued NPC and PvP landing paths use that fixed ability base with the caster's ordinary ballistic influence and the recipient's resistance. The regular rifle hit and the queued Body Shot are distinct damage events. The older flat-50 Rifle Body Shot callback is in the unimported old soldier module.

Native PR741 at character level 19 and PR616 at character level 50 with learned rank 19 each measured 8 neutral and 6 with Ballistic Resistance. Both observed move 11364 and the paired −5 target records. The old PR330 hit of 69 came from the earlier runtime's generic damage roll. The headless matrix independently changes character level, learned Body Shot rank, Rifles rank, and two equipped rifles whose tool damage differs. It also checks the real landed health change with added Ballistic Damage and Ballistic Resistance. [PR741 result](https://github.com/kylebrussell/mxo-hd/pull/741#issuecomment-7075743659) · [PR616 result](https://github.com/kylebrussell/mxo-hd/pull/616#issuecomment-7058153028)

Covering Fire retains its broader damage-scaling and proc checks.
