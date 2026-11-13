# DataMine (map7 / retail construct 17): partial shipped interactions

This package loads **6,105 known-safe native static keys** and explicitly binds the eight shipped Archivist Proxy returners. It omits all variants of 6,855 known rotated/conflicting keys and the **whole sector34**, whose required exterior SID is missing. Its unknown exterior count remains unknown; this is not a complete district or a data-node implementation.

## Resource evidence and strict omissions

The client METR declares 59 sectors. Available resources comprise 58 SID, 59 SIDI and59 SIDG files. They yield 13,219 known references / 12,960 known keys. There are 6,821 keys touched by nonzero prefab rotations and 175 conflicting keys (141 overlap), giving 6,855 omitted keys. All remaining 6,105 keys are unambiguous rotation0 placements. Every variant of an omitted key stays absent; no first-variant or guessed rotation policy is used. Coordinates retain the established city CSV +145Y convention, not a new native pivot claim.

Sector34 `dataminene_3` declares exterior SID resource `0x04000800` in METR. That RID is absent from the packed index, its expected path is absent from packed paths and loose files, and its available SIDI/SIDG companions do not establish an exterior object count. The exporter refuses by default. The explicit `--omit-missing-sectors` option omits the **whole** sector, including its companions; their hashes and the METR declaration remain in [the audit](../research/world-data/datamine-static-audit.json). Unsupported rotations independently require `--omit-unproven-rotations`. Neither option silently enables the other.

Existing NAV7 already contains all 59 sectors/42 prefabs (version3); a new read-only export matches after decompression. No NAV replacement is needed. [Resource evidence](../research/world-data/datamine-resource-evidence.json) records the59 source NAV hashes, GOB proxy fields and shipped tooltip. HDL has NextID2 but zero hardlines; cooked COD has zero Hardlines, 6 TransportGroups and 120 TransportSpaces. NETS has zero CNET networks. No hardline, subway or elevator mapping is invented.

The 18 existing map7 captured NPC rows lie inside the sector X/Z footprint and remain unchanged. Their elevations/completeness/native visibility are not established here. No DataNode606 or DatamineTap47276–47279 appears in the available known static placements. Missing/omitted data prevents a claim that the retail district had no nodes; this package invents none.

Reproduce from the repository root using read-only client access:

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 7 --omit-missing-sectors --omit-unproven-rotations
python research/world-data/datamine_resources.py <client-data-tools> <MxO-directory>
python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv <temporary-output> 7
```

The 12/13/17 static exports also reproduce unchanged. Omitting either required DataMine option must refuse before writing outputs.

## Actual generic returners and authority

GOB 47267 `Generic_ArchivistBase_Returner` has schema 3602, ArchivistOrganization 0, explicit ExecuteRule 1000 (`GeneralSuccess`) and MaxDistance 1000. Its shipped details string `0x04000339` says it returns operatives to the city and instructs double-click/right-click Use. It differs from the organization-specific returners' rule 9007; no organization restriction is added.

All eight exterior placements are unambiguous. The table uses the existing CSV view position; player standing height for the ground check is CSV Y−50. NAV floor differs from the source ground (CSV Y−145) by 0 except 1.418 units on the slope at 20/23 and−3 at 23/12. No proxy is relocated.

| Sector / static ID | CSV view position X,Y,Z | NAV sector / region / polygon |
|---|---|---|
|8/1|−31200,1545,16450|8/2/4|
|8/3|−37050,535,4100|8/3/102|
|20/23|5950,395,12650|20/32/55|
|23/12|38550,108,−4350|23/2/5|
|28/1230|13550,645,−17200|28/94/2|
|36/1|−16150,1545,43800|36/2/3|
|41/3576|23200,13745,27550|41/15/365|
|44/2|6200,4945,−24150|44/24/5|

CSV alone does not render a proxy. The existing PvpArchives model renderer explicitly emits prototype 47267 with native related-static ID, position and source quaternion, tracking a client-owned view generation. The binding loader requires the complete exact eight-key set, supported schema and source navigation floors; invalid/incomplete bindings refuse. Existing generated fallback returners remain available even if loading fails.

Use requires the current created view object, matching source binding/entity, map7, live on-world state, existing alive/control/combat eligibility and distance≤1000. The existing RPC spawn-counter guard still applies. Unknown, deleted, forged, remote and cross-world views cannot invoke the new return. A completed return clears on-world state before another attempt. Native model appearance and radial acceptance still require Claude's client check; sent/decoded03 is not proof of rendering.

## Existing server policies retained, not retail reconstruction

Normal book 47270 (Zion),47268 (Machines) and47269 (Merovingian) have PvPID 7 and inclusive level 1–100. `rshooks.LookupCID` maps this to retail construct 17; the saved district/nav index remains 7. The normal sender uses its unchanged sourced 180-second state cycle.

`LookupConstructData` lacks an exact DataMine arrival. The existing OURS nearest-floor fallback near(−500,2100,500) resolves to(−430,3895,500), NAV4/26/0. It is unchanged. **The existing generated map7 returner is always retained**, because a route from that fallback roof to the actual proxies is not established. This package adds no conditional suppression based on a claimed native render or guessed route.

Actual generic proxy Use reuses the existing exact session-origin return. A session without an origin uses the existing explicit OURS Mara fallback(23828,495,13984). Shared WorldID0/position0 GOB defaults do not justify literal tutorial-world0 travel. No other archive arrival, organization gate or return behavior changes.

## Claude-only native one-pass

Use discovered `datamine` profile. It seeds the normal Zion book in bag1, empties launcher106 and starts at the actual Zion city sender(111600,745,−6130), without dev/GM/forced RNG. Equip the book through normal UI, inspect its info/Actions, stand near the sender and wait the ordinary 180-second cycle. Check the actual DataMine world, terrain/walk/nav and the retained fallback returner. Inspect available safe doors/panels without claiming omitted interiors or working elevator destinations.

Try a genuinely reachable sourced Archivist Proxy and its actual double-click/right-click Use. Record model appearance, floor/reach problems and missing routes; do not substitute an injected position jump or chat for a native walk. A proxy reached through normal play should return to the exact city session origin. If the partial geometry blocks the route, report that limitation. No hardline/Loading Area return claim is made.

The separate `datamine-source-proxy` diagnostic profile starts at the verified 8/1 standing point(−31200,1495,16450), without dev/GM/RNG or a synthetic city-origin session. Check the actual 47267 model and real radial Use returning to the existing OURS no-origin Mara fallback. This diagnostic spawn proves no native walk, normal book arrival or exact retail destination; main `datamine` remains the normal-cycle profile.

## Automated validation

Release full suite: 4,261 PASS; final scoped 16 world/archive tests PASS after limiting generic dispatch to map7. Python 12 audit/link/policy tests PASS. Both NativeQa configurations build; all four read-only mode/configuration plans pass with dev/GM/forced rolls disabled. Current ignored audit MD/CSV/coverage reproduce exactly with CRLF; tracked parity CSV/historical snapshots and captured NPC data are unchanged.

The actual-wire fixture passed 25.0 seconds/exit0/peak1 disposable server, with success raw output saved before normal disposal. It ran frozen 6678e0afa0e01a6bbbdeb4bc2dc6b92fdb911164 / Release hds SHA256 D05E10E78185337EB7746F7F908884AD419B0EB108B718C9B24E9395634CA114. The ordinary 180-second cycle was already running and reached its next departure phase during the test; no clock override or accelerated timer was used. The fixture mounts 47270, waits the normal sender cycle, checks map7/retail17, observes eight native 47267/static-bound03 records and source quaternions using ordinary position records, rejects invalid handles/range/worlds and checks source-proxy return to the exact session origin. A separate organization uses the generic proxy and explicit no-origin Mara fallback. Fixture position records prove view/use protocol, **not native walking, model appearance, node locations or complete district parity**.


### Current master refresh

Master5cc17819 (Archive01/Medium02 packages included) is merged. The original DataMine runtime, actual bindings, fixture, both modes, CSV and audits are byte-identical to the frozen wire-tested source. Master DataLoader bytes are preserved plus one map7 line; the shared exporter unions4/7/12/13/15/17 while retaining the exact missing-SID whole-sector and rotation omission blocks. All six CSV/audit outputs reproduce exactly.32 focused world/archive tests, both NativeQa configurations and all four read-only mode/configuration plans pass; current ignored three-artifact CRLF roundtrip/policy also pass. Only command-line SDK-supported hardlink copy properties were used for the incremental builds; no project, OS or user settings changed. No new full suite or fresh server was started for these upstream-only imports.

The subsequent masterc37f5e84/PR164 union also preserves all DataMine runtime/binding/fixture/mode/data blobs exactly. Master loader bytes+one7 line and exporter4/7/12/13/15/16/17 reproduce exactly;36 focused tests and both incremental hardlink-copy builds PASS, with all four read-only plans/provenance PASS. The upstream shared Rotation seed defaults0, preserving this fixture’s existing inputs. A further optional AbilityAudit build was stopped before launch by the700MiB disk guard; no ability source/data changed, previous audit proof is retained and remote CI will generate the current bundle. No fresh/full repeat or cleanup was performed.

## Native failure repair candidate (native acceptance still pending)

Claude's 18:51 native check on e8c2ae1 reached DataMine through the normal book cycle and walked successfully, but reported two missing region-object exceptions and a client crash during the generated returner's trip to Richland. Server-side return committed and relog reached the sender. The source-proxy diagnostic was not run; neither body rendering nor a successful native return is established by headless wire checks.

The two requested keys, 0x01b00004 and 0x00c00005, have no authoritative loaded source object. DataLoader's legacy lookup returns an empty placeholder; RegionHandler previously decoded its null type. Region requests now reject truncated payloads and unavailable types before decoding. They create no invented object or reply. This preserves the existing unsupported-object no-reply policy; no retail negative-object response has been recovered. The client constructor621b3f00 writes opcode80c9, ObjIDu32 and ObjPos3xf32; producer621b4640 copies the existing object's ID and position. That client-supplied position cannot replace missing source data.

A separate, proven reset-lifecycle gap allowed queued old-world03 objects to be resent after the client had dropped that world: RESETDONE cleared viewMan and sent RPCs but left ObjectMessagesQueue. The reset boundary now discards old sent and unsent object messages before new-world setup. New messages queued afterward survive; unsent RPC behavior stays unchanged. This is a reasoned return-safety correction, **not proof that it caused or fixes the native crash**. Source-bound proxies still use the existing renderer and fallback; no model, RSI, pose, relocation or demand-binding policy is invented.

`datamine-native-return-regression` exercises normal47270 mount and the unchanged180-second cycle, sends both missing requests, uses the generated normal-arrival38271 returner, completes the full city reset/load/spawn, checks exact origin and equipped book, logs out normally, waits the ordinary reservation cleanup, relogs on the same disposable server and waits for a second normal departure. Its explicit eight-minute test budget accommodates two real cycles; runtime timing is unchanged. Passing this test cannot establish native render or crash acceptance.

Claude one-pass: main `datamine` profile, ordinary book equip/cycle; inspect arrival without assuming label means body; use generated returner and confirm Richland loads without native crash; check book still equipped, relog at sender and wait a full cycle. Separately `datamine-source-proxy`: walk at least2m away from the diagnostic start before judging the actual47267 model, then ordinary Details/Use. Direct diagnostic placement is not a walk-route or normal-arrival claim. Keep the prior native failure explicit until these native checks pass.

The reset repair additionally fences outgoing preparation and final encryption/socket dispatch with an independent per-client comm epoch. It begins on the valid decrypted RESETDONE bit **before** publishing incoming headers; PSS remains the protocol acknowledgement bitmap, not a world ID. Normal incoming PSS/Cseq/ACK publication uses the same short gate. Pending reset blocks outgoing preparation, including sent-marker changes, until counter/queue/view reset and ordinary setup finish. Combat/trade/life cleanup and setup execute outside the transport gate. Matching-epoch resume and flush-finally release prevent an old rejected flush from stranding new setup; new flush requests remain pending. Archive renderer allocation/update/deletion also validates its captured epoch, world generation, map and on-world state before committing, preventing a stale archive visibility pass from recreating an old view after the reset. These are bounded known producer/transport protections, not a claim that every session race or the native crash is resolved.

Deterministic barrier units cover a prepared old batch rejected before it can use reset counters; an old captured flush unable to mark a new creation sent or let ACK0 remove it; pending setup isolation; and the actual WorldClient flush claim releasing so TickFlush encrypts and dispatches new03 plus newRPC to a disposable loopback socket. Archive commit units cover stale epoch, pending reset, world-generation and map changes. No production timing/RNG or broad RPC clearing changes.

### Authoritative failed native trace

The retained failed session is Temp/mxo-headless/f19e7264051e. ServerLog SHA25664e066fd94378480f874321f80e1f8a16abf893abe7d0b4c519528fc11de5590; PacketLog SHA25677716469f91d67e22655b624ad2b8cfd5e80b0a532da8cc3e05f596afcfaa521. At18:45:56, ServerLog1064 records fallbackUse,1067 commits exact origin(111600,745,-6130),1076 records8108. PacketLog1764 receives reset42;1769 sends new-counter81a9+06slums_barrens_full.metr. The60 server datagrams before the18:47:50 relog contain no03 and receive no client reply. The entire captured session contains only fallback38271 creations (PacketLog1403/3635), no47267 creation. Thus this trace does **not** identify stale03 or proactive source47267 views as the observed crash cause.

The reported non-refiring sender is also not a demonstrated server eligibility failure: relog's inventory response (DebugLog2772) retains47270 in106; ServerLog1304 spawns city at18:47:50,1580 saves map7 at18:47:58,1589 receives8108,2063 spawns map7,2099/2123 repeat missing requests. Server-side cycle re-entry occurred; native visual acceptance remains separate and unproven. No book/equipment/cycle policy change is justified.
