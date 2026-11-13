# Bot decision code map

Read from the code on 2026-11-12 (branch `claude/1206-conan-tuning` at `a3da5ac3`). Paths are relative to the repository root; `TO.X.cs` means `src/L5R.AI/TrainingOpponent.X.cs`. Nothing was built or run; the traces in section 5 were done by hand and with a Python copy of the regexes.

Size: `src/L5R.AI` is 4,687 lines in 27 files. The bot is one static partial class, `TrainingOpponent`, plus `BotContext` (memory and switches).

## 1. The decision pipeline

Every decision is one stateless call: `TrainingOpponent.Choose(view, actions, memory)` (`TrainingOpponent.cs:20-44`). Callers: the client (`src/L5R.Client/Main.cs:206`, `Main.Practice.cs:21`), the arena (`tests/L5R.AI.Tests/BotTournament.cs:134-149`) and Conan's own playouts. Duels in progress go to the separate `DuelOpponent.Choose` (`DuelOpponent.cs:7`) in the client.

Steps inside `Choose`:

1. `memory.Observe(view)` (line 26), and `NoteBattleActions` only when planner triage is on (27).
2. Build `Ctx` (47-57): view, both `PlayerView`s, `BotProfile.Assess` (own Honor tier), `Odds = BattlePolicy.Assess(view)` (current battlefield only), and `Plan = PlanFor(x)` when the planner is on (30-31).
3. For each enabled `ActionOption`: `Prepare` fills in the command (targets, assignments, Gold) or returns null to drop it (32-34).
4. `Score` (272): `Contender2Score(TriageScore(PlanScore(Adjust(ScoreCore))))`.
5. Sort by score, then type, card id, ability id (35-37). The top one is played.
6. Conan only: `LookAhead` may replace the top choice (40). Then `Remember`, `NoteUse` (41-42).

The layers, innermost first:

| Layer | Where | Role |
|---|---|---|
| `ScoreCore` | `TrainingOpponent.cs:274-448` | Champion base score per action type. `PassScore` = 10. |
| `Adjust` | `TO.Battle.cs:13-64` | Champion policies in fixed order: `DuelReply`, `LosingChallenge`, `HonorAwareScore`, `PrudenceScore`, `PhaseUseAdjust`, `UnopposedAdjust`, `AttackerAdjust`, negation, `EquipScore`. |
| `PlanScore` | `TO.Planner.cs:267-284` | Contender: purchase slate ranking; attack and reserve only with `pattack` / `reserve`. |
| `TriageScore` | `TO.Triage.cs:58-72` | Contender: stop spending once the battle is won; printed abilities first (+12). |
| `Contender2Score` | `TO.Contender2.cs:31-180` | Named rules, first match wins. Skipped entirely when `Rules` is empty (Champion). |

How a rule acts:

- **Veto**: `Math.Min(score, BarredScore)`; `BarredScore` is 9, one below a pass, so the bot passes instead. `Contender2Bar` (`TO.Contender2.cs:502-592`) is a list of such vetoes for `useAbility` / `playStrategy` / `equip`, called at line 144.
- **Promote**: `Math.Max(score, N)` (for example `presence` to 130 at line 162) or a fixed return (`aggro` 85).
- **Rewrite**: `Prepare` replaces `command.Assignments` after `PrepareCore`, in this order: `presence` (`TrainingOpponent.cs:74-75`), `deter` (77-78), `guard` (81-90).
- **Remove**: `PrepareCore` returns null (`favoraware`, line 147).

A rule is a string in `BotContext.Rules`. `Rule(x, "name")` tests it and `Knob(x, "name", default)` reads a `name=value` entry (`TO.Contender2.cs:14-22`). There is no registry, no ordering table and no fire counter: order is the textual order of `if` statements in `Contender2Rules` (44-180) and `Contender2Bar`.

Tiers:

- **Champion** = `new BotContext()`: flag defaults at `BotContext.cs:124-191`; no rules.
- **Contender** = `BotContext.Contender()` (`BotContext.cs:52-68`): planner and triage on, plus `TunedRules` (line 73): `econ, presence, lobby, shinden, openbow, ownprov, selflock, surereact, selfboost, nostraighten, provaim, spreadwide, conf, selfplayer, presdepth, pdh=100, keepup, nofoeboost, raid, pdstrong, defwin, nobowgift`.
- **Conan** = Contender + `look` (`BotContext.cs:75-76`). **Hard** adds `lkDef, lkVar, lkPlan` (79-80); the client's Hard level uses `Hard()` (`BotLevel.cs:11`).

## 2. Decision types

"Contender" lists only what the tuned defaults add to the Champion column. "Conan" lists what is searched.

| Decision | Champion | Contender (tuned) | Conan / Hard | Estimates used |
|---|---|---|---|---|
| Declare attack (yes/no) | `DeclareScore` (`TO.Policy.cs:181-193`): attack if `PlanAttack` finds a plan; score `90 + force − ArmyForce(opp home)` | `raid` raises to 85 when the opponent has no unbowed defender (`TO.Contender2.cs:166-175`) | Hard: `PlanAttack` search over `AttackShapes` + "no attack" (`TO.Lookahead.cs:135-206`) | `AttackPlanner.Plan`, `ArmyForce`, own `HandForceBonuses` |
| Assign attackers | `PrepareCore` (`TrainingOpponent.cs:137-156`): `PlanAttack` then `HoldCavalry`; `HonorAttackOf`; trades | off by default: `lean*`, `maxraze`, `favoraware`, `aggro`, `deter`, `guard`, `hostileatk`, `lethal`, `pattack` | Hard: `FollowAttackPlan` (152-170) | as above; `EstimateAttack` only with `pattack` |
| Cavalry manoeuvre (own) | `HoldCavalry` (`TO.Defense.cs:173-175`): own Cavalry withheld until the Cavalry segment, then assigned by the same planner | none | none | none specific |
| Assign defence | `CavalryLastStandPlan ?? CavalryDefensePlan`, `LastStandProvince`, `InformedDefenseProvince` (`TrainingOpponent.cs:157-164`); scores 362-369 | `presence` + `pdstrong`, `defwin`, `presdepth` rewrite (`TO.Contender2.cs:443-491`) and score 130 | Hard: `lkDef` + `DefenceShapes` (212-229) | `InformedDefenseScore`, `DefenderRemovals`, `ForceAfterRemovals`, `HandRemovals`, `EnemyRemovalSources` |
| Battle action and target | `AbilityScore` → `GenericAbilityScore` (`TO.Policy.cs:19-88`); targets in `PrepareCore` 198-216 (`PickAttackTarget`, `PickBowTarget`, `Rank` in `TO.Complete.cs:98-112`) | `TriageScore`; `conf` (`ConfidenceScore`, 355-367); bars `selfboost`, `nostraighten`, `provaim`, `ownprov`, `selflock`, `nofoeboost`, `nobowgift`, `keepup`; `spreadwide` targeting | Battle phase is always searched (`Searched`, 119-128): top `lkK` = 3 candidates | `BattlePolicy.Odds`, `ThreatValue`, `Confidence`, `DecisiveThreat` |
| Reactions | `AbilityScore` 40-51: negation 1500, harms opponent card 1400, else a weighted roll | `surereact` (always react at rank Help or better) | searched only inside Battle | `AbilityIntent` only |
| Favor: lobby | `AbilityValue` (`TrainingOpponent.cs:450-471`), `FavorTakeScore` (`TO.Policy.cs:97-105`), `FavorLobbyWanted` (`TO.Spec818.cs:97-103`) | `lobby` bar (keep `lobbyN` = 2 unbowed at home, `TO.Contender2.cs:530`) | no | Honor comparison, average unit Force |
| Favor: use | `favor-battle` scored `300 + UnitForceOf(target)`; `FavorSpendScore` (112-118) | `lobbycycle`, `favorhold`, `cycle` exist, all off | Battle only | none |
| Action-phase Open / Limited | `GenericAbilityScore`; `PrudenceScore` (early economy, `TO.Prudence.cs:48-65`); `PhaseUseAdjust` holds combat-only effects (`TO.PhaseChoice.cs:23-34`) | `openbow`, `shinden`, `selfplayer`, `selfboost` | `lkAct` off | none about the coming battle |
| Attachments | `EquipScore`, `AttachmentValue` (`TO.Battle.cs:124-150`); target = unbowed, highest Force (`TrainingOpponent.cs:225`) | `spreadwide` (228-230) | no | none |
| Dynasty purchases | `ScoreCore` 308-323; `HoldingPurchaseScore`, `CatchUpRecruitBonus` (`TO.Spec818.cs:44-52`) | planner `Slate` (`TO.Planner.cs:159-177`) via `CardValue` (84-111); `econ` overrides for fair-priced Holdings (`TO.Contender2.cs:111-123`) | `lkBuy` off | `IntentFor` (total Force), Gold `Capacity` / `Ready` |
| Discards | hand: lowest `HandValue` (166-171); Province: `DiscardProvinceScore` (`TO.Spec818.cs:68-75`) | `econ` discardProvince | no | none |
| Duels | `LosingChallenge` bars a challenge with a lower stat (`TO.Challenge.cs:9-19`); `DuelReply` (31-39); focus play in `DuelOpponent` | `duelodds`, `duelplay` off | no | printed duel stat on the table only |
| Seppuku | `SeppukuScore` (`TO.Seppuku.cs:19-33`) | none | no | Honor thresholds, `PlanAttack` |

## 3. What the bot knows and remembers

**In the view** (`src/L5R.Core/Models.cs`; built in `MatchQueries.cs:33-67`):

- `MatchView` (1593-1642): turn, phase, priority, `BattleStep`, `Battlefields`, `FavorPlayerId`, `PendingDuel`, `LastDuel`, `ResolutionArea`, `BattleActions` (current or latest battle only), `ShownCards` (whole game, append-only) and `Events` (whole game `GameEvent[]`).
- Opponent `PlayerView` (1517-1534): Honor, `Home`, `Army`, `Provinces` (face-down cards masked), `HandCount` (hand itself empty), full `Discard` and `RemovedFromGame`, `DeadCount`, `HandLimit`. No deck sizes.
- `CardView` (1451-1476): effective `Force` / `Chi`, `UnitForce`, `Bowed`, `ActionLocked`, `Modifiers`, `GrantedAbilities` with uses left, and since #1219 `SpentAbilityIds` (1475): printed abilities used up this turn, for both seats.
- `AbilityDefinition` (`CardAbilities.cs:58-163`): timing, label text, target category, costs, `IssuesDuel`, `Move`, uses per turn. The effect itself is engine code keyed by ability id and is not machine-readable.

**In `BotContext` memory**:

- Seen cards: every public card name with a count (`Observe`, `BotContext.cs:206-216`); `ExpectedHiddenCopies` = 3 − seen, or 1 − seen for Unique (230-231). Not keyed by owner, turn or zone.
- `_spent`: Battle actions taken this turn, keyed by performer **name** and ability id, read from `view.BattleActions` (32-40).
- Per-turn flags: lobbied, took Favor, recruited, Border Keep refresh, uses of an ability, engine rejections (196-273).
- Planner: current `TurnPlan`, `PlanKey`, `PlanHistory`, `PlanLog` (111-122).
- Conan: `Sampler`, `PlannedAttack`, `SeenHonor` (opponent Honor at the first search of each turn), `LookLog`, `ThinkMs` (83-96).
- Own deck: `Archetype` (173) and `DeckAveragePersonalHonor` (194), when the caller supplies them.

**Not tracked about the opponent**:

- `MatchView.Events` and `ShownCards` are never read. There is no record of the opponent's past attacks, defences, purchases, or the tricks and removal it has shown, other than name counts.
- The hand is modelled as a count; nothing types it by effect or uses `ExpectedHiddenCopies` in an estimate.
- No opponent archetype: `DeckArchetype.Classify` reads the bot's own decklist only (`DeckArchetype.cs:32-55`).
- Opponent Gold appears only in `EnemyTricks` (`gold/2` cap).
- `SpentAbilityIds` has exactly one reader, `BattlePolicy.UnusedBattleActions` (`BattlePolicy.cs:138-139`). Every Contender rule still uses the name-keyed `SpentThisTurn`, which conflates same-named cards, covers Battle actions only, and is empty in playouts (fresh memories).

Disagreement: comments at `BotContext.cs:30` and `TO.Triage.cs:10` say spent abilities are not in the view. They have been since #1219.

## 4. Estimators and classifiers

| Name | Where | Question answered | Side / zone | Ignores |
|---|---|---|---|---|
| `ArmyForce(cards)` | `TrainingOpponent.cs:478` | Sum of effective Force of **unbowed** Personalities and Followers | any set | abilities, hands |
| `UnitForceOf` | `TO.Spec818.cs:93-94` | `UnitForce`, or Force + attachments; **bowed or not** | any | bowed state |
| `TotalForce` | `TO.Planner.cs:80` | All Force at home and in armies, bowed or not | either | everything else |
| `BattlePolicy.Odds` | `BattlePolicy.cs:13-68` | Outcome tier at the current battlefield from Force and Province Strength | board | hands |
| `AbilityIntent.Of` | `AbilityIntent.cs:77-104` | One `Rank` (Neutral, Help, Penalize, Bow, Destroy), one `Side`, flags | any ability | magnitude, conditions; see section 5 |
| `IsThreatening` / `IsThreat` | `BattlePolicy.cs:92-97` | Ability destroys, bows or moves a non-own card; unbowed Personality printing one | enemy in play | whether it is spent, payable or in range |
| `ThreatValue` | `TO.Triage.cs:21-33` | Target priority: Force + ability weight + keyword bumps; ×0.1 if bowed | enemy in play | hand |
| `HandTricks` / `ChooseTricks` | `TO.Estimator.cs:32-61` | Force the bot's **own** hand can add, by Gold | own hand | negation |
| `EnemyTricks` | `TO.Estimator.cs:64-70` | Count of tricks in the **opponent's hand**: `min(4, ceil(HandCount × share), gold/2)`; share = Strategy share of its discard if 3+ seen, else 0.4 | opponent hand | what the tricks do |
| `EstimateAttack` | `TO.Estimator.cs:73-88` | Whether an army razes a Province: enemy = `ArmyForce(opp home)`; swing = `(EnemyTricks + min(3, IsThreat count)) × 3` | attacker; used only with `pattack` or by `guard` | counter-attack, Cavalry |
| `HandRemovals(x, performers)` | `TO.Contender2.cs:184-189` | How many of the bot's units the **opponent's hand** removes at one battle: `min(2 × performers, ceil(HandCount × share))` | opponent hand | Gold, type of removal, seen copies |
| `EnemyRemovalSources` | `TO.Contender2.cs:329-333` | Enemy units with an unspent threatening printed ability | opponent in play | costs, targets |
| `DefenderRemovals` | `TO.Contender2.cs:405-423` | The bot's **own** removals usable on attackers: full (send home, bow, destroy) or `−N F`, with Gold cost; one per card; Favor counts as a free full removal | own hand + own in play | performer needs, negation; defence only |
| `ForceAfterRemovals` | `TO.Contender2.cs:426-437` | Attacking Force left after the affordable removals each hit the largest unit | used when defending | opponent's replies |
| `PresencePlan` (`presence`, `defwin`, `presdepth`) | `TO.Contender2.cs:443-491` | Fewest defenders so the Province holds after own removals; `defwin` adds until Force wins outright; `presdepth` adds until `1 + EnemyRemovalSources + pdh% × HandRemovals` defenders | defender | Province-strength cuts, move-ins |
| `Confidence` (`conf`) | `TO.Contender2.cs:336-344` | `sources − inPlay − cuth/100 × HandRemovals`: own offered removals reaching the deciding unit, minus enemy cut-offs | current battle | negation, Gold |
| `DecisiveThreat` | `TO.Contender2.cs:287-293` | Largest enemy unit whose removal raises the outcome tier | current battle | |
| `HostileDefenseHolds` / `HostileAttackHolds` | `TO.Contender2.cs:246-275` | Does a defence (or attack) still win or save after `HandRemovals` strips its strongest units | both; rules off | in-play enemy abilities |
| `Guard` (`guard`) | `TO.Estimator.cs:143-158` | Units to keep home so the opponent's whole board Force cannot raze the weakest own Province; only with `guardN` (2) or fewer Provinces | own home; off | opponent hand, purchases, Cavalry |
| `AttackPlanner.Plan` | `AttackPlanner.cs:21-61` | Most Provinces attackable with every army above `Strength + defenderForce` (#818: above `Max(Strength, defenderForce)`); own hand bonus to the neediest army | attacker | counts all enemy home Force against **each** army; no enemy hand, abilities, Cavalry or counter-attack |
| `DefensePlanner.Plan` | `DefensePlanner.cs:20-53` | Blind defence against possible Cavalry: an army "reaches" when `10×force + cards×threat >= 10×threat` | defender | what the cards are |
| `CavalryDefensePlan` | `TO.Defense.cs:28-51` | Used only when the attacker sent **no** infantry; threat = `PossibleCavalryForce` (all unbowed home Cavalry) | defender | `cavblind` (off) subtracts `HandRemovals` |
| `CavalryReach` / `CavalryLastStandPlan` | `TO.Defense.cs:59-158` | Max Provinces enemy Cavalry could still destroy; exhaustive defence when all could fall | defender | hands |
| `InformedDefenseScore` | `TO.Defense.cs:271-285` | 95 if defenders beat attackers, 70 if they save the Province, else 0 | defender, board Force | hands, abilities |
| `Contested` (`econctx`) / `Threatened` | `TO.Contender2.cs:380-397` | Opponent's total Force beats mine plus my weakest Province, or a buyable Personality closes my razing gap | board; off | hands, Holdings, turn order |
| `IntentFor` | `TO.Planner.cs:66-77` | Develop / Defend / Attack from `TotalForce` comparison | board | everything else |
| `Slate` / `CardValue` | `TO.Planner.cs:84-177` | Best payable subset of the Province row; Holding value by efficiency × intent, Personality by Force, Honor, "threat 20" for a threatening ability | own row | opponent's board except through intent |
| `econ` | `TO.Contender2.cs:111-123` | Buy fair-priced Holdings first while Holdings < `econN` (8) and own turns taken ≤ `econT` (4); score `SlateBonus + 50 + 5 × (production − cost)` | own | opponent entirely |
| `raid` / `Undefended` | `TO.Contender2.cs:166-175, 237` | Attack whenever each army beats Strength and no unbowed defender exists | attacker | counter-attack |
| `DeckArchetype` | `DeckArchetype.cs:7-55` | Own deck's win route by card-text density | own decklist | opponent |
| Lookahead `Material` | `TO.Lookahead.cs:303-318` | Playout value: Provinces ×12, unit cost + Force, Honor ×1.5, hand ×1.5, Holdings | both | default playout ends with this turn's attack |

## 5. Reading an ability card-agnostically

The only inputs are `AbilityDefinition.Label` (free text), `.Target` (a category string such as `enemy-personality`, `attacking`, `attack`, `none`), `.Timing`, `.Costs`, `.IssuesDuel`, `.Move`, and `CardDefinition.Effect` for two legacy tags (`bow-small`, `force-three`; `StrategyIntent`, `TO.Battle.cs:205-210`).

`AbilityIntent.Of` takes the text after the **last colon** as the effect (`EffectText`, 47-51), derives `Side` from the target prefix (83-87), and picks one rank by precedence (90-94):

```
if (isAttack || Destroy.IsMatch(effect)) rank = EffectRank.Destroy;
else if (Bow.IsMatch(effect) && side != TargetSide.Own) rank = EffectRank.Bow;
else if (Penalize.IsMatch(effect) && side != TargetSide.Own) rank = EffectRank.Penalize;
else if (Help.IsMatch(effect) || side == TargetSide.Own) rank = EffectRank.Help;
```

Anything finer is re-derived by separate regexes scattered across files: `SendHome` (`BattlePolicy.cs:74`), `ForceBonus` `\+\s?(\d+)\s?F` (`TO.Battle.cs:259`), `MinusForce`, `RemovalWords`, `StraightenText` (`TO.Contender2.cs:495-499`), `LowersStrength` (`TO.UnopposedUse.cs:11`), `AttackX` (`BattlePolicy.cs:76`), `CreatedForce` (`TO.Spec818.cs:109`), the phase regexes (`TO.PhaseChoice.cs:15-20`). A few ability ids are hard-coded (`lobby`, `favor-battle`, `favor-cycle`, Border Keep; `TrainingOpponent.cs:450-471`).

Traced examples (labels from `src/L5R.Core/CardAbilities.cs`):

1. `block-supply` (line 208), "Move an attacking unit home bowed", target `attacking`. Side = Any. `Bow` (`\bbow\b`) does not match "bowed"; `Penalize` needs "move him/her/it/them/a target home" and does not match "Move an attacking unit home". **Rank = Neutral**. `SendsHome` does match, so `IsThreatening` is true and `DefenderRemovals` counts it as a full removal. The same card is "nothing" to `GenericAbilityScore` and "full removal" to `presence`. `incapacitated` (207) and `favor-battle` (217) also rank Neutral; the Favor is rescued only by its hard-coded id.
2. `kahoku-exp-attack` (421), "Target an enemy Personality: Give him -4F. Ranged 3 Attack", target `enemy-personality`. Side = Enemy, rank = Penalize (the `-4`). The Ranged Attack is invisible: `isAttack` requires target `attack`, and `AttackStrength` returns null for any other target (`BattlePolicy.cs:115`). `DefenderRemovals` yields a partial removal of 4.
3. `ebizo-attack` (468), "Ranged Attack with strength equal to Ebizo's Force", target `attack`. Rank = Destroy. `KnownAttackStrength` (`TO.Honor.cs:68-80`) finds no number, and its "equal to his/her/its/the performer's Force" pattern does not match a name, so strength is unknown and the code assumes the Attack kills (`kills = strength is not int reach || ...`, line 40).

Reliability: side is sound (it comes from the engine's target category). Rank is right for plain "bow", "destroy" and "−N F" wording and wrong or Neutral for several send-home phrasings. There is no magnitude, no condition (performer keyword, opposed or unopposed, target limits), and no distinction between send home, Force cut, duel, move-in, straighten or Province-strength cut inside `AbilityIntent`.

## 6. Adding and testing a rule

**Add**: put the name in a `Rule(x, "name")` test at the right point of `Contender2Rules` / `Contender2Bar` (score rules) or `Prepare` / `PrepareCore` (rewrites); tune with `Knob`. Names are parsed only by `Rules.Contains` and by `BotContext.Contender` for a handful that map to flags (`BotContext.cs:54-68`). To make it default, add it to `TunedRules` (73).

**Worked example**, commit c3e871fd (`guard`, `hgift`, `cavblind`, `lobbycycle`; 51 lines in four files):

- `hgift` is one veto at `TO.Contender2.cs:154-156`, reusing `HostileDefenseHolds`.
- `lobbycycle` is one line in `Contender2Bar` (590).
- `cavblind` edits the unit list inside `CavalryDefensePlan` (`TO.Defense.cs:43-49`).
- `guard` needed an estimator (`TO.Estimator.cs:143-158`) and a rewrite hook in `Prepare` (`TrainingOpponent.cs:81-90`). Its first version sat in `PlannerAssignment` and changed nothing, because the Contender's `pattack` is off (experiment note, `docs/bots/experiments/2026-11-10-conan-vs-contender.md:324`).
- The commit added **no tests**. All four were screened neutral and left off (same note, 315-326).

**Regression tests**: `tests/L5R.AI.Tests`, custom runner (`Program.cs:274-295`, `tests/TestRunner.cs`; `--filter TEXT`, `--list`). Style: reshape `MatchEngine.CreateStarterMatch().GetView(0)` with `with`, hand-build `ActionOption[]`, call `TrainingOpponent.Choose`, assert on the command. Helpers in `BattlePolicyTests.cs` (`Home` 31, `Person` 202, `Unit` 203, `Battle` 211, `Pass` 228, `Use` 229). Short models: `InformedDefenseTests.cs:29-43`, `PlannerTests.cs:18-68`, `CavalryDefenseTests.cs:54-101`. No test passes a rule name to `Contender(rules: [...])` except the empty list at `PlannerTests.cs:151`.

**The instruments of the strategy brief, step 1 (2026-11-13, branch `claude/1206-conan-instruments`)**: `ConanPositions.cs` (`--filter conan-positions`, `L5R_POS_ARMS`) re-decides Mendon's positions from `docs/bots/replays/` for any rule set; `BattleRecords.cs` (`L5R_TOURN_BATTLES=<dir>`, read by `tools/bots/battles.py`) writes one record per declared attack with each estimator's number and the real result, using the harness-only `TrainingOpponent.DiagnoseAttack`; `tools/bots/behaviour/profile.py` prints the behaviour profile. Step 2's `EffectDescriptors.Of(abilityId)` (`src/L5R.AI/EffectDescriptor.cs`) is the one card-agnostic reading of an ability (class, magnitude, side, target kind, conditions, costs, performer, timing); `--filter descriptor-audit` (`L5R_DESC_OUT`) is its coverage audit. Section 5 below describes the regexes that preceded it; none of the decision code reads the descriptors yet. Step 2's Read (`src/L5R.AI/Read/`: `Ledger`, `Belief`, `Exchange`, `Read`) is built in shadow under rule `read` (`BotContext.NoteRead`, logged to `BotContext.ReadLog`) and written into the battle records (`readAtDeclaration`, `readUndefended`, `readAtDefence`); `Exchange.Need` and `Exchange.Have` are the brief's `need` and `have`. `Threat.Of` / `Read.ThreatNow()` (`Read/Threat.cs`) is section 4.5's `threat(home)`: their straightened army's worst attack against my unbowed home under my best defence, in Mendon's tiers; `ThreatRecords.cs` (`L5R_TOURN_THREATS=<dir>`, read by `tools/bots/threats.py`) writes it per turn with what the opponent's turn then did. `Questions.Of(read, trend)` (`Read/Questions.cs`) answers section 4.5: `Opportunity` (family A's candidates against the defender's best reply, scored `gain − weight(posture) × (exposure − exposure(hold))` through `Calibration`), `Clocks` (turns to each route from `BotContext.History`'s rates) and `Posture` (family G's rule); `BotContext.CurrentAnswers` holds the last, logged with its replanning cause. Nothing decides on it.

**`human-compare`** (`HumanReplayCompare.cs`): replays an exported human game and asks the bot before each seat-0 command. Env: `L5R_HUMAN_REPLAY` (required), `L5R_HUMAN_OUT`, `L5R_HUMAN_BOT` (any `BotTournament.Arms` key, default contender), `L5R_HUMAN_PROBE` / `_PROBE_BOT` / `_PROBE_PLAY`, `L5R_HUMAN_DUMP`. Output: per-turn blocks, one line per human command, the bot's answer marked `[DIFF]`, a summary by phase and command type, and the bot's plan log.

**Arena** (`tools/bots`; all need the Release build, `--no-build`):

- `run_arm.sh <name> <arm> <seed-start> [seeds] [rules]`: one arm; prints the "Challenger won" line; writes `artifacts/bots/<name>.{json,md,out}` and `logs-<name>/`.
- `par.sh <seed-start> <seeds> <baseline> <jobs> name=rules ...`: baseline with `$TUNED`, then variants in parallel; prints and appends the paired comparison to `artifacts/bots/summary.txt`. It always runs arm `c2`, whose rule list **replaces** `TunedRules`.
- `compare_tournament.py`: games, win %, gained, lost, net, p-values, per deck, on games whose result changed.
- `sprt.py`: games, LLR, bounds, verdict (H1 / H0 / continue).
- `think_times.py`: searches and median / p90 / p99 / max ms.
- `analyze_logs.py`: command counts and top abilities in won and lost games.
- `economy.py`: Holdings and Personalities bought and discarded, first raze turn, P(win | razed first).

Disagreements: `docs/bots/CONTENDER.md` result tables predate the tuned rules. `docs/bots/CONAN.md` writes `par.sh ... "conan=<rules>,look"`; that works because `c2` with `look` in its list searches, not because the `conan` arm runs. The tournament `.md` title says "vs champion" whatever `L5R_TOURN_OPP` is.

## 7. Gaps against a threat and opportunity assessment

**Missing outright**

1. **No assessment object.** Each rule recomputes its own number from the view. Nothing is carried between decisions except name counts and per-turn flags, and nothing updates on a reveal: `Events`, `ShownCards` and `SpentAbilityIds` are unused (one reader for the last).
2. **No effect descriptor.** One rank per ability, no magnitudes, and send-home text that classifies as Neutral (section 5). "Can I neutralise this defender" cannot be asked of a card in general; each rule asks it with its own regex.
3. **Attack declaration does not consider the counter-attack.** `DeclareScore`, `AttackPlanner`, `raid` and `aggro` compare own Force with the opponent's unbowed home Force and Province Strength. Only `guard`, `deter` and `lean*` leave units home, and all are off; Hard's `lkPlan` playout stops at the end of this turn's attack unless `lkFar` (off).
4. **The attacker never counts its own removal.** `DefenderRemovals` / `ForceAfterRemovals` are wired only into the defending `presence` plan. An attack that works only after sending a defender home is never found; an attack is planned on Force plus own Force bonuses.
5. **The opponent's hand is a count times a share.** `EnemyTricks` and `HandRemovals` ignore what the opponent has shown, the copies left (`ExpectedHiddenCopies` exists and is unused here), its Gold (except one cap) and the kind of removal.
6. **Purchases ignore threat by default.** Tuned `econ` looks at turn number and Holding count; planner intent is a total-Force comparison. `econctx` / `Contested`, `threatbody`, `firstbody`, `bodyfirst`, `rdef` / `roff`, `lkBuy` are all off, and none estimates the opponent's next attack (its Gold, row, Cavalry, hand).
7. **Cavalry forcing is not modelled as a plan.** Own Cavalry is simply withheld to the Cavalry segment. Enemy Cavalry is the sum of all unbowed home Cavalry, used only when no infantry was sent or when every Province could fall.
8. **No route enumeration for "can this Province be taken".** Defence checks Force against Force + Strength. Province-strength cuts, move-ins, straightens and negation are not counted against the defender; kill-the-attackers is `defwin` on board Force only.

**One-sided estimators worth mirroring**: `DefenderRemovals` (own only), `EnemyRemovalSources` (enemy in play only, current battlefield), `HandTricks` (own) vs `EnemyTricks` (count only), `DeckArchetype` (own deck).

**Reusable pieces**

- Removal model: `Removal`, `DefenderRemovals`, `ForceAfterRemovals`, `HitWeight`, `RemovesUnit`, `DecisiveThreat`.
- Battle arithmetic: `BattlePolicy.Odds` and `Tier`, `BattleEstimate` / `EstimateAttack`, `AttackPlanner`, `LeanPlan`, `CavalryReach`.
- Counter-attack seeds: `Guard`, `DeterPlan`, `Contested`, `Threatened`.
- Rebuild-on-change cache: `TurnPlan` + `PlanKey` (`TO.Planner.cs:25-58`) is the natural home for an assessment, with `PlanLog` for explanation.
- Observation: `BotContext.Observe`, `ExpectedHiddenCopies`, `UnusedBattleActions`.
- Search scaffolding: `AttackShapes`, `DefenceShapes`, `Playout`, `Material`.
