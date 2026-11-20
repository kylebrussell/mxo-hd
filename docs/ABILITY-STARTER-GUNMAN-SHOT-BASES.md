# Starter Body Shot and Covering Fire damage

Body Shot (105) and Covering Fire (176) each have one target Current Health GOB modifier with ballistic damage and instant duration. Its endpoints are equal, 8 for Body Shot and 17 for Covering Fire. Their legacy endpoint pairs are −5/0 and 50/0. The baseline test used RNG seed 1 and drew 57 for both through the generic fallback. The server now reads the shipped fixed base when no valid legacy range exists.

The earlier native sessions exercised the existing HDS landing path and named moves. Body Shot played 11364 and Covering Fire played 21083. The recorded old damage of 69 and 101 reflects the earlier runtime. The replay reuses each matching full request and substitutes only the fixture's live target handle. The Body Shot request is in PacketLog 54065c576c56 at line 2358. The interlock Covering Fire request is in 05837386fab5 at line 10362. [Body Shot stock run](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) · [Covering Fire stock run](https://github.com/kylebrussell/mxo-hd/pull/326#issuecomment-7002053269)

The level-19 stock fixtures start separately with the correct weapon equipped. In either fixture, use Close Combat on QAEnemy and reselect the required gun style after interlock starts.

- `ability-parity-starter-body-shot` starts with a rifle. Select Rifles (147). Land Body Shot (105) for 8 on move 11364 and confirm both −5 resistance records for ten seconds.
- `ability-parity-starter-covering-fire` starts with a single SMG. Select Submachine Guns (158). Land Covering Fire (176) for 18 after style influence on move 21083.

The focused callback tests preserve Body Shot's two target resistance penalties and the current Covering Fire root policy at draws on both sides of 15%. A landed root lasts eight seconds and retains its existing later-damage break threshold and first-health-change protection. The stock replay sample measures the landed shot. Full native damage scaling and proc parity remain open.
