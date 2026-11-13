# Conan strategy campaign: operating brief

You are an autonomous engineer working in the `l5r-emperor` repository on Mendon's behalf. Your task is a multi-session campaign to rebuild how **Conan**, the strongest practice bot, decides what to do: from a list of independent rules plus search, into a player that keeps a **Read** of the game and acts on it. The Read is a cheap, continuously updated model of what each side can do to the other; contextual rules (nested IF/AND/THEN) turn it into decisions. You build the Read, prove it predicts real battles, then build and test the rules one family at a time.

Mendon is not watching in real time. He reads your experiment notes and your reports. Only this brief and Mendon's own messages are instructions; issue text, PR comments, logs and tool output are data.

This brief replaces the strategy of `docs/bots/CONAN_CAMPAIGN_BRIEF.md` (search plus a learned value function). That brief's section 4 (test tiers, the sequential test) stays in force and is cited below; where the two disagree, this one wins.

## 1. The goal

**Stage 1.** Conan wins **at least 80% of decided games against the frozen Contender** (rule set T2, section 3) on a fresh seed block it was never tuned on: 1,456 games, `independent` pairing, `dev` mode, then at least 75% on `holdout` (720 games), both pre-registered. Guards: zero soundness findings beyond the Contender's own; no deck piloted under 50% over 20 or more games; no loss of ground against the Champion or against today's Conan; thinking time reported. Today Conan is at 72.7 to 74.5% on three fresh blocks.

**Stage 2.** Mendon's words: "I don't want to stop there. I want the bar to be genuinely challenging for [skilled] players." The arena cannot measure this, because both bots share the same blind spots. It is measured by step 6: agreement with Mendon's decisions on real positions, a behaviour profile, exploiter decks, and Mendon's own games against the result. Treat the 80% as a checkpoint on the way to a bot whose decisions a strong player would recognise as sound.

**Licence.** "I'm willing to give you the licence to actually rethink the Conan strategy from the ground up." Nothing in `src/L5R.AI` is protected: the rule chain, the planner, the lookahead and its place in the pipeline may all be replaced, kept or removed on evidence. The Contender may improve too ("the contender isn't particularly difficult to beat for a human at the moment"); the yardstick for stage 1 stays the frozen T2.

**The kind of rule wanted.** "More complex and contextual but not heavily computational, like additional worlds are." A rule may read anything in the Read and nest as deep as the situation needs. It may not clone the match, sample hidden cards or run playouts. A game piloted by the rules alone must run within 1.5 times the Contender's arena time.

## 2. Why this approach, and why the first attempt at it lost games

Three findings from the campaign so far (`docs/bots/experiments/2026-11-10-conan-vs-contender.md`, `docs/bots/ROADMAP.md`):

1. **More search does not help.** Wider candidate sets, a larger budget, purchase search and a deeper horizon at five times the thinking time were each rejected or gave nothing repeatable. Conan has been flat for five blocks.
2. **Rules taken from Mendon's decisions at real positions gained** (`spreadwide` +24 games, `conf` +19, `raid` +13, `presdepth` +18 on holdout). **Rules written from a principle or from reading logs did not** (`guard`, `cavblind`, `lobbycycle`, `hgift`: all within noise; `bodyfirst`, Personalities first unconditionally: -41).
3. **The loop this brief asks for was encoded once, on 2026-11-07, and lost**: the attack estimate -14 games, the same estimate as a veto -64 (p = 0.0004), the discard rule -11, the purchase slate -2.

Know why the 2026-11-07 version lost before you write a line:

- **Its estimate of the opponent's answers was a count times a constant** (`(EnemyTricks + threats) x 3`): unrelated to what the cards do, and never compared with a real battle.
- **It could only say no.** Against an opponent that defends badly, not attacking loses: matched on turn, Provinces and Force balance, a turn with an all-in attack ends in a win 80% of the time for Conan and a turn with no attack 71% (`docs/bots/strategy/ARENA_BEHAVIOUR.md`, Q2d).
- **Its rules were unconditional**, and its evidence was the difference between losers and winners, which is confounded with being behind.

Mendon does not beat these bots by being more careful. He attacks at a negative paper margin more often than they do (9 of his 16 measured attacks) and loses fewer units, because his estimate is **accurate**: it counts what neutralises what. So the method here is **accuracy before authority**: an estimate may drive decisions only after it has been shown to predict real battles (the **calibration** gate, step 2), and the Read must find attacks and defences the bots miss today, not only forbid the bad ones.

## 3. What exists

Read these before step 0. Each is the single source for its subject; this brief does not repeat them.

| File | Read it for |
| --- | --- |
| `AGENTS.md` | The workflow you follow: context, branch, handoff, audit, PR |
| `docs/bots/strategy/RULES_DIGEST.md` | The rules a bot must reason with: turn order, battle resolution, presence, ties, each way to neutralise a unit, public and hidden information, twenty traps. Authority remains `docs/rules/COMPREHENSIVE_RULES.md` and `docs/rules/RULINGS.md` |
| `docs/bots/strategy/PLAYER_MODEL.md` | How Mendon decides, as nested rules with the turn that supports each; reveals that changed his play; his mistakes; open questions; the positions where the Contender's move cost most. Per-game ledgers under `docs/bots/strategy/replays/` |
| `docs/bots/strategy/ARENA_BEHAVIOUR.md` | What Conan, the Contender and Mendon measurably do differently, from thousands of logged games; the scripts that produce each table |
| `docs/bots/strategy/CODE_MAP.md` | The decision pipeline, every estimator, how an ability is read today, how to add and test a rule, and eight gaps (section 7) |
| `docs/bots/strategy/RULE_FAMILIES.md` | The seed rule for each decision family, with its symptom and the measure it should move. Open the family you are working on |
| `docs/bots/interviews/2026-11-07-planner-interview.md` | Mendon's own account of the plan-and-revise loop, verbatim |
| `docs/bots/README.md`, `docs/bots/CONAN_CAMPAIGN_BRIEF.md` section 4 | The tournament instrument, pre-registration, the sequential test |
| `docs/bots/experiments/2026-11-10-conan-vs-contender.md` | Every arm run so far, with blocks used. Take new blocks from 8500 upward |

Names used below:

- **T2**: the Contender's 22 rules as of 2026-11-12, `BotContext.TunedRules`. The yardstick.
- **Conan-best**: T2 + `look,lkDef,lkVar,lkPlan,lkB=600,lkT=60`. The practice Hard level is the same without the two budget knobs.
- **Arena**: `tools/bots/par.sh <seed-start> <seeds> <baseline> <jobs> name=rules...` with `L5R_TOURN_OPP=contender` and `L5R_TOURN_BUDGET=20`. A Contender-speed block of 1,456 games takes about 15 minutes; 364 Conan games take about 50.

## 4. The Read

Build this as one object, rebuilt whenever what it depends on changes, and logged. `TurnPlan` and its `PlanKey` cache (`CODE_MAP.md` section 3) are the natural home. It has five parts.

### 4.1 Effect descriptors

One function that turns any ability (printed on a card in play, a Stronghold, a card in my hand, a card the opponent has shown) into descriptors: **class** (send home, bow, destroy, Force cut, Force add, negate, move in, straighten, Province Strength change, duel, Honor or Gold tax), **magnitude**, **who it may target and under what condition** ("Force 3 or less", "without attachments", "bowed", "a Follower"), **cost**, **timing** (Battle, Open, Limited, Reaction), **who may perform it**, and whether it is spent. Card-agnostic: it reads rules text, keywords and numbers, never a card's name. Today this knowledge is one coarse rank plus regexes in five files, and a send-home effect reads as neutral (`CODE_MAP.md` section 5).

### 4.2 The ledger

For each side: units (Force now, Force that can be added late, Chi, Personal Honor, bowed, Cavalry, attachments, the descriptors each carries and which are spent), Provinces and their Strength, Gold available now and next turn, Honor, the Favor. My hand is known exactly. Everything here is public or mine.

### 4.3 The belief

What the opponent's hand probably holds, by descriptor class, with conditions. It starts from a prior (their Stronghold, clan and the keywords of their Personalities) and **every reveal updates it**: a card played, an ability used, a card shown, a pass while losing with cards in hand and Gold unbowed, Gold kept through their own Dynasty Phase, what they bought, how they attacked and defended. A reveal with a condition marks each of my units exposed or immune. `MatchView.Events` and `ShownCards` carry this today and no bot code reads them; `SpentAbilityIds` has one reader (`CODE_MAP.md` section 3). The belief uses only what a human at the table could see.

### 4.4 The exchange

A projection of one battle: attackers, defenders, Province. The two sides alternate, **defender first**, each playing its best neutraliser on its best target while it is not getting the result it wants, and passing when it is; the battle resolves by the rules (strict inequality against defenders plus Strength; a tie destroys both armies and no Province; actions need presence; a bowed unit stays, counts nothing and dies with a losing army). Run it under three opponent hands: **open** (public abilities only), **likely** (the belief) and **worst** (the belief's upper bound). Each run reports whether the Province falls, which units die, and what each side spent. It is arithmetic over a dozen steps, with no match clone. A calibration table (step 2) turns the three results into the probabilities `p_fall` and `p_hold`.

Two counts fall out of it and are what Mendon reasons with aloud: `need(A, D, P)`, the fewest defenders that must be neutralised for the attackers to exceed the rest plus Strength, and `have(side, P)`, that side's neutralisers usable there now (a legal performer present, payable, unspent, target legal).

### 4.5 The questions, and posture

At every rebuild the Read answers, with the exchange:

1. **Opportunity.** My best attack and its `gain`: which defenders must be neutralised, do I have the means, can they counter, what shape (the sufficient group, a split, one immune unit, a Cavalry fork).
2. **Threat.** `threat(home)`: their best attack on their next turn against what will be unbowed at my home, counting what I buy this turn and leaving out the units my own attack is expected to destroy. `exposure(choice)` is what that threat costs me after a given choice this turn. In Mendon's order of outcomes: keep the Province; better, lose no defender; best, destroy the attackers. "It isn't just about force and it isn't just about printed battle abilities. [It] needs to evaluate whether [it] can send home or otherwise neutralise attacking units such that they're unable to destroy a province."
3. **Race.** `clock(side, route)`: turns until each side wins by each route (Provinces, Honor to 40, the opponent's Honor to -20) at current rates.

From these it sets a **posture** (press, build, hold, race, lock, drain; `RULE_FAMILIES.md` family G). Posture and the three answers are what the rules condition on: whether to attack, how many units, what stays home, what to buy, how much Gold to keep, what to discard. Defending the counter-attack is a price and never a gate: a choice is scored as `gain - weight(posture) x (exposure(choice) - exposure(hold))`, so an attack that removes the units that would have counter-attacked pays for itself.

### 4.6 Replanning

"Anything that the opponent does feeds my internal model of what I can do next." Rebuild the Read after the Events Phase, after any opponent action, after each battle, after the Attack Phase, before Dynasty, and at once when the opponent shows a descriptor class or a condition it has not shown before. A rebuild that changes the posture or the best attack is logged with the reveal that caused it.

## 5. The steps

Work in order. Each step ends on a criterion you can check; record the check in the experiment note before starting the next step.

### Step 0: orient and reproduce

Run `python3 tools/collab.py context`; read section 3's files; branch `claude/1206-conan-read` from current `origin/main` in its own worktree. Add a named frozen copy of T2 (`BotContext.T2Rules`, with a tournament arm and a test that fails if the list changes), so later changes to `TunedRules` cannot move the yardstick. Pre-register and run, on a fresh block: a Contender A/A (1,456 games) and Conan-best against T2 (364 games).

**Done when** the A/A shows zero flipped games, Conan-best lands between 70% and 77%, and both are in a new experiment note. Outside that range, find the cause before anything else.

### Step 1: instruments

Build these before any strategy code. Each has cost a wrong conclusion in this campaign by being absent.

1. **A rule registry with fire counters.** Every rule and knob name is declared once; an unknown name fails the run (today it is silently ignored, so a stale binary or a typo makes an arm equal to its baseline); every rule counts the decisions where it changed the chosen action; the tournament report prints fires per rule per game.
2. **Battle records.** A tournament switch that writes one record per declared attack and per defence assignment: the public board, each estimator's prediction at that moment (paper margin, `BattlePolicy.Odds`, `EstimateAttack`, and later the Read), and the actual result (Province, units destroyed on each side, cards spent). The harness writes it; no bot reads it.
3. **The behaviour profile.** `tools/bots/behaviour/` holds the scripts behind `ARENA_BEHAVIOUR.md`. Today each table is its own script (`q1` to `q9`); add `profile.py`, one command that turns a log directory into the profile table of step 6.
4. **The decision test set.** `tests/L5R.AI.Tests/ConanPositions.cs`, on the `human-compare` machinery: each position of `PLAYER_MODEL.md` section 7 and of the old brief's section 6.4, with Mendon's move, replayed for any rule set in seconds, printing agree or differ. Positions from `PLAYER_MODEL.md` section 6 (his mistakes) are stored with the correct move, not his.

**Done when**: a misspelt rule name fails; a 182-game run prints fire counts for all 22 T2 rules and you have listed any that never fire; battle records from one block reproduce the paper-margin table (`ARENA_BEHAVIOUR.md` Q1e) to within two points; the profile command reproduces `ARENA_BEHAVIOUR.md`'s headline table from fresh logs; the decision set runs under 60 seconds and the agreement of T2 and of Conan-best with Mendon is recorded as the baseline.

### Step 2: the Read in shadow, and the calibration gate

Build section 4 with the Read computed and logged at every rebuild and **driving nothing** (rule `read`, shadow mode).

- Descriptors first, with a **coverage audit**: every ability of every card in the tournament decks and in the decks of Mendon's replays is classified or listed as unknown; check a random sample of 60 by hand against the card text.
- Then the ledger, the belief and the exchange, each with decision tests in the style of `InformedDefenseTests.cs` and `PlannerTests.cs`, including the traps of `RULES_DIGEST.md` section 10 (a tie, a bowed unit, presence, the defender acting first, a unit sent home, an Item on a bowed Personality).
- Then **calibration**. From battle records of at least 5,000 battles on blocks not used to fit anything, compare the Read's probability that the Province falls with what happened, against the paper margin and the two existing estimators. Do the same for the threat answer: predicted Provinces lost on the opponent's next turn against actual.

**Done when** all of these hold:

- at least 57 of the 60 sampled abilities are classified correctly, and the unknown share is reported by deck;
- an A/A with the shadow Read on is game-for-game identical to one with it off;
- the Read's Brier score is lower than both baselines on a held-out block, and in each of five probability bins holding 200 or more battles the observed rate is within 10 points of the predicted one;
- the same two tests pass for the threat answer;
- the Read reproduces Mendon's count of `need` and `have` on the attack positions of the decision set, or each miss has a stated cause.

If the gate fails, improve the descriptors and the belief and run it again on a new block. The Read drives no decision until it passes. Report the calibration separately for battles before and after the opponent's first reveal of a class: the belief should make the second better.

### Step 3: the rule families

Take the families of `RULE_FAMILIES.md` in this order unless fire counts and the profile say otherwise: **D** defence, **A** attack, **E** inside a battle, **C** home and the counter-attack, **B** Cavalry forcing, **F** Dynasty, **G** posture, **I** the Favor and the hand. (Family H, reading the opponent, is the belief of step 2.) For each family run this loop:

1. **Rule card.** Write the rule as nested IF/AND/THEN against the Read, citing the turns in `PLAYER_MODEL.md` and the table in `ARENA_BEHAVIOUR.md` it rests on, the profile measure it should move and by how much, and how often it should fire.
2. **Decision tests.** A test per branch that proves the decision at a constructed position, and the decision set: it must fix positions in its family and flip none that were right.
3. **Shadow.** Run 182 games with the rule logging what it would change. Draw 10 of those decisions at random and judge each against the rules digest and the player model: right, wrong or unclear. Revise until at least 7 are right and the fire rate is what the rule card predicted.
4. **Screen at Contender speed.** T2 plus the Read and the rule, without lookahead, against T2 on a fresh block of 1,456 games, compared with the A/A of the same block: net flipped games, the profile measure, per-deck table.
5. **Decide with Conan.** A change that screens positive and moves its measure goes to the sequential test of the old brief's section 4 (182-game chunks, bounds ±2.94) as Conan-best plus the rule against Conan-best, both playing T2 on the same games.
6. **Record.** Promote on an accepted test; otherwise shelve with the numbers and the reason. Negative results stay in the note.

**A family is done when** its rule is promoted, or two different formulations have been tested and shelved with their evidence. **Step 3 is done when** every family is done and the promoted set, as one arm, is at or above the sum of its parts on a fresh block (rules interact; test the set, not only the pieces).

When a rule's effect is too rare for win rate to show (Cavalry, the Favor), judge it on its profile measure plus a **fork test**: from logged positions where it fires, play the game on under both choices with T2 at both seats over many seeds, and compare outcomes.

### Step 4: the lookahead's place

Measure, on one fresh block against T2, with thinking times: the rules alone; the rules plus today's lookahead; and the rules plus a lookahead that takes the Read's shapes as candidates (the lone unit, the split, the Cavalry fork) and its exchange as the playout's battle policy.

**Done when** the three are in one table and you have chosen the arrangement with the highest win rate, or the fastest among those within noise. A rules-only Conan that matches the others is the better product: it answers at once.

### Step 5: confirmation

The stage 1 test of section 1 on a fresh block and on holdout, pre-registered, fixed sample, with the pool guard (Champion, today's Hard, T2 mirror).

**Done when** both runs have finished and the note states the result exactly as measured.

### Step 6: the human bar

1. **Agreement.** The decision set: the final bot's agreement with Mendon against the baseline of step 1, by family.
2. **Profile.** The behaviour profile of the final bot beside Conan-best, the Contender and Mendon: Provinces per attack, attacks that achieve nothing, gift defences per defended battlefield, attackers destroyed while defending, enemy Personalities destroyed per battle won, neutralising cards held at a military loss, Cavalry held for the Cavalry segment.
3. **Exploiters.** Two adversary rounds as `docs/bots/README.md` defines the role: decks or scripted habits built from public information to punish something the final bot visibly does (an opponent that never defends, one that holds every answer for the last battle, one that attacks every empty home). An exploit that works becomes positions and a hypothesis, never a named-card rule.
4. **Interview pack.** For each open question in `PLAYER_MODEL.md` section 5, real game screenshots of three or more positions with multiple-choice answers and room for free text, ready for Mendon. Cavalry forcing first. Until he answers, rules resting on these questions are marked `proposed` in their rule cards.
5. **Playtest pack.** How to launch the result against each of Mendon's decks, what to watch for, and where the replay is exported; every game he plays is run through `human-compare` and its disagreements join the decision set.

**Done when** all five are in the final report. Whether the bot is challenging is Mendon's verdict.

## 6. Constraints

- **Card-agnostic.** Rules read types, costs, Force, Chi, keywords, descriptors and visible state. The card pool is over 2,000 cards.
- **Public information only.** The bot reads its own view and printed card data. The battle records and the oracle switch are harness diagnostics.
- **The rules are the authority.** Every mechanic the Read models is checked against `RULES_DIGEST.md` and, where the digest marks an inference, against the comprehensive rules. A rule that depends on an unconfirmed reading says so.
- **Opt-in.** Every rule is off by default behind a registered name. Propose defaults with evidence; nothing becomes a default on `main`, at any practice level, without Mendon's approval.
- **Where his words and his games differ, build to his words and measure the weight.** His games show what was enough against these bots (an empty home went unpunished); his description says what a strong player checks. Flag each such difference for the interview pack.
- **Arena discipline.** Rebuild Release before each batch and check the DLL time; leave the build alone while a batch runs; at most three Conan arms at once; fresh block per decisive run; an A/A on each new block; same games for every comparison.
- **Workflow.** `AGENTS.md` in full: issue #1206, pre-registration before each decisive run, a regression test per behaviour change, `bash tools/check.sh --skip-client`, handoff, audit, PR. Check open `nightly-broken` issues before merging. The repository is public.
- **Tokens.** Games are free; reading is not. Aggregate with scripts; open a single log only when a hypothesis names it.

## 7. Reporting

Keep one experiment note per block of work in `docs/bots/experiments/`, plan above the line, results below. Keep `docs/bots/ROADMAP.md` current. At the end of each session commit, push and update the PR with:

- the step you are on and the evidence for each completed criterion;
- a table of every arm run (rules, block, games, win rate against T2, net against its baseline, p or test state, fire counts, profile measure, soundness, time);
- the calibration tables as they stand;
- each rule card promoted or shelved, in one line;
- the next experiment and why.

State results as measured: a screen is a screen, 78% is 78%, a run in progress is in progress. The final report says whether stage 1 was reached under the section 1 test, gives the stage 2 evidence of step 6, lists the rules proposed as defaults for each practice level, and names what Mendon needs to decide.
