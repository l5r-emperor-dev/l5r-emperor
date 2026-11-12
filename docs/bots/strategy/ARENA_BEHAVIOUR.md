# Arena behaviour: what the bots do, and which of it loses games

This is the measured behaviour of Conan, the Contender and Mendon in logged games, read for one purpose: to say which bot behaviour is good, which is weak, and how large each weakness is. It supplies the symptoms cited in `RULE_FAMILIES.md` and the **behaviour profile** that the strategy brief (`../CONAN_STRATEGY_BRIEF.md`, steps 1 and 6) tracks beside win rate.

The full tables are in `arena-tables/` (Q1 to Q9, one file per question). The scripts that produce them are in `tools/bots/behaviour/`.

## Samples and limits

| Player | Games | Source |
| --- | --- | --- |
| Conan | 1,091 | Conan-best against the frozen Contender, blocks 7800, 7900 and 8100 |
| Contender (vs Conan) | 1,091 | The other seat of the same games |
| Contender (mirror) | 2,912 seats | Contender against Contender, block 8300 A/A |
| Mendon | 7 | His practice replays, all wins against bots |
| Practice bot | 7 | The other seat of those games |

Read every number here with four limits in mind:

1. **The tables are observational.** A behaviour that is more common in lost games may be a result of losing. Each finding below is a symptom and a hypothesis; the arena test of the rule is the evidence.
2. **Mendon's sample is seven games.** His rows are counts, never percentages, and they describe wins against weak opponents.
3. **The parser estimates.** Force before a battle is rebuilt from the turn header, attachments and numbered bonuses; bowed state is known only at each turn header; a log event names a clan and the seat is taken from the command it follows. The parser's self-check (`l5rlog.py <logs>`) reports how often its estimate matches the engine's resolution line.
4. **"Paper margin"** means committed Force minus the defender's unbowed Force at home minus Province Strength, before any action. It is the estimate a Force-counting bot makes.

## The behaviour profile

One row per measure. "Good" is the direction a stronger player moves it, judged from the rules, from Mendon's games and from which bot wins.

| Measure | Mendon | Conan | Contender vs Conan | Contender mirror | Good | Table |
| --- | --- | --- | --- | --- | --- | --- |
| Attacks that destroy a Province | 14 of 19 | 72% | 51% | 59% | higher | Q1c |
| Attacks that lose a Personality | 1 of 19 | 13% | 25% | 23% | lower | Q1c |
| Attacks that achieve nothing | 4 of 19 | 13% | 37% | 28% | lower | Q1c |
| Turns with two or more Provinces destroyed, per 100 attacks | 15.8 | 9.7 | 6.4 | 8.0 | higher | Q6d |
| Defended battlefields held with an attacker destroyed | 7 of 12 | 25% | 7% | 20% | higher | Q3b |
| Gift defences: Province lost and defenders destroyed | 0 of 12 | 14% | 32% | 20% | lower | Q3b |
| Unbowed Personalities assigned when attacked | 36 of 67 | 65% | 75% | 77% | fewer, better placed | Q3a |
| A Province lost on the opponent's turn after an all-in attack | 0 of 8 | 34% | 47% | — | lower | Q2a |
| A neutralising card in hand when an undefended Province fell with an unbowed Personality at home | — | 70% | — | — | lower | Q4e |
| Cavalry Personalities held for the Cavalry segment | 1 of 1 | 15% | 80% | 81% | higher | Q5a |
| Gold capacity left unproduced, turns 1 to 8 | 12% | 24% | 29% | 27% | lower | Q7a |
| First purchase is a Personality when behind on Personalities | 9 of 9 | 38% | 42% | 41% | higher | Q7c |
| One-Personality attacks into a held Favor that achieve nothing | 1 of 1 | 75% | 90% | 84% | fewer such attacks | Q8b |

## What Conan already does well

Keep these; a new rule that undoes one of them is a regression even if its own measure improves.

- **Its attacks land.** At every paper margin Conan's attacks destroy a Province more often than the Contender's (for example 54% against 31% at a margin of -9 to -1, and 80% against 49% at 0 to +5; Q1e). The lookahead finds which attacks its hand can carry.
- **It attacks when behind and holds a little when ahead.** With 8 or more Force less than the opponent it attacks on 40% of turns and the Contender on 18%; with 15 or more ahead, 92% against 99% (Q1d).
- **It keeps its army.** At the start of its own turn 7 it has 5.9 Personalities and 23 Force in play; the Contender facing it has 4.0 and 15 (Q7b). Part of this is the result of winning battles, part is losing fewer attackers.
- **It defends to kill more often**: 25% of its defended battlefields end with an attacker destroyed, against 7% for the Contender facing it (Q3b).
- **Attacking is right against these opponents.** Matched on turn, Provinces and Force balance, a Conan turn with an all-in attack ends in a win 80% of the time, a partial attack 84%, no attack 71% (Q2d). A rule that only makes Conan attack less will lose.

## Where the bots are weak, largest first

### 1. Defence (family D)

Of the 1,072 Provinces Conan lost in battle in its lost games (Q9b):

| How the Province fell | Share |
| --- | --- |
| Defended and lost | 31% |
| Declined: an unbowed Personality stayed home | 31% |
| No guard, after its own attack | 25% |
| No guard, and it had not attacked | 14% |

Two opposite errors sit side by side. **Gift defences**: 14% of Conan's defended battlefields lose the Province and the defenders with it (32% for the Contender; Q3b). About half of them are at the last Province, where defending is forced (Q3c); the rest are a choice. **Declined defences with the answer in hand**: in 70% of the 564 cases where a Province fell undefended with an unbowed Personality at home, Conan held at least one neutralising Strategy card (1.7 on average) that it could not play, because no unit of its own was at the battlefield (Q4e). Between 11% and 17% of the undefended battlefields that fell on paper could have been held on paper by the units left at home (Q3e).

A fixed rule cannot fix both. The decision needs a projection of the battle that counts the cards a single defender would unlock.

### 2. Attack selection and shape (family A)

- **Hopeless attacks.** Conan made 325 attacks at a paper margin of -10 or worse; 40% achieved nothing and 27% lost a Personality (Q1e). Some of these are right (they are how it attacks when behind); the Read should tell which.
- **The Favor.** When the defender holds the Imperial Favor, 71% of Conan's attacks have an attacker sent home by it, and 75% of its one-Personality attacks achieve nothing. It still attacks on 76% of those turns (Q8b). Mendon attacked into a held Favor on 1 of 7 such turns.
- **Nothing changes after a reveal.** After an attack that took nothing because the defender used a removal action, Conan attacks again on 78% of turns and commits 72% of its unbowed Personalities, the same as when no removal was shown (75%, 62%; Q8a). Mendon's commitment fell from 95% of his unbowed Personalities to about 60% once the defender had shown removal.
- **The opponent's hand size is ignored.** Conan's attack rate is flat from 0 to 2 cards in the defender's hand to 7 or more (73% and 71%), and the Contender's rises (Q8d).
- **Splitting is under-used.** In every bucket of defender guard and of paper surplus, a split attack destroyed more Provinces than a one-Province attack (for Conan with a surplus of 0 to 9: 1.29 against 0.79 per attack) at a similar rate of losses (Q6c). Conan splits 40% of eligible attacks. Mendon split 5 of 9 and took 8 of the 10 Provinces targeted (Q6b). This comparison is confounded by which positions invite a split; it marks where to look, and the shape is one the Read should offer whenever each group can win alone.

### 3. The counter-attack (family C)

After an all-in attack Conan loses at least one Province on the opponent's next turn 34% of the time, against 24% after no attack (Q2a). When the Force left unbowed at home is under a quarter of the opponent's unbowed Force, it is attacked 93% of the time and loses a Province 66% of the time; at or above the opponent's Force, 26% and 5% (Q2c). In lost games, 30% of the Provinces it lost fell on the turn after its own all-in attack, with 0.9 unbowed Personalities at home (Q9d).

The price is real and so is the gain (section above, Q2d). 19% of Conan's losses are games where it traded Provinces with its army bowed from its own attack, and 18% are races lost by one Province (Q9f). These are the games where weighing the counter-attack would change the result.

### 4. Cavalry (family B)

Conan assigns 85% of its Cavalry Personalities in the Infantry segment, where the defender sees them before choosing its defence; the Contender holds 80% back (Q5a). Where Cavalry is held back, about 60% of the battlefields it goes to have no defender, the defender answers with its own Cavalry in 0 to 2% of cases, and the Province falls 61 to 72% of the time when undefended against 28 to 40% when defended (Q5b). With two or more unbowed Cavalry Personalities, Conan's attacks take 1.03 Provinces when it uses the Cavalry segment and 0.89 when it does not (Q5c). Piloting the all-Cavalry deck, the Contender wins 69% of its mirror games against 49% with other decks (Q5d-pilot).

Neither bot plans the fork Mendon describes: an Infantry assignment chosen to make the defender commit, then Cavalry sent where the defence is not. And no bot defends against it: the defender's reply in the Cavalry defence segment is almost never used.

### 5. Inside a battle (family E)

The battle is decided by actions more than by Force. When the attacker used more neutralising actions than the defender the Province fell 45 to 58% of the time; when the defender used more, 11 to 21% (Q4d). A battlefield that falls on paper by 3 or more is held by the defender 36 to 63% of the time, and one that holds on paper by 3 or more falls 11 to 22% (Q4c). About half of the losers of military games end the game with a neutralising card still in hand (Q4e-end).

### 6. Dynasty (family F)

- **Gold goes unused.** The bots leave 24 to 29% of their Gold capacity unproduced over turns 1 to 8; Mendon left 12% (Q7a). By turn 7 a bot has about 25 Gold of capacity and produces 14.
- **They keep buying Holdings anyway.** On turns 6 to 8 the bots buy about one Holding a turn. The share of turns with a Holding bought does not fall as last turn's unused Gold rises: 60% after 0 to 2 unused, 69% after 10 or more (Conan, Q7e).
- **The unused Gold is rarely a missed Personality**: an affordable Personality was left in the row on 2 to 3% of turns (Q7d). The Gold has nothing to buy, which says the late Holdings should have been something else earlier, or that Gold belongs in the hand's abilities.
- **Holdings first when behind.** When behind on Personalities with both a Personality and a Holding affordable, a bot's first purchase is a Holding 54 to 58% of the time; Mendon bought the Personality first 9 times of 9 (Q7c). Forcing this order unconditionally lost 41 games (`bodyfirst`), so the condition matters: the threat next turn, not the count of Personalities.

### 7. Route and posture (family G)

Conan loses 11% of its lost games to Honor, 23 of those 32 to one deck (Q9a, Q9e). After the opponent passed 30 Honor with about three Provinces left, Conan attacked on 60% of its decisions and all-in on 28%. Mendon's attack rate falls to nothing after turn 8 in the games he won by Honor or by drain (Q1a); the bots attack on 56 to 77% of turns at every stage of every game.

### How Conan's losses divide (Q9f)

| Class | Share of 291 losses |
| --- | --- |
| Overrun: defences beaten or declined, took one Province or none | 27% |
| Traded Provinces: most fell with its army bowed from its own attack | 19% |
| Lost the Province race by one | 18% |
| No army at home and not attacking (outbuilt or wiped) | 12% |
| Honor race lost | 11% |
| Defences beaten or declined, took two | 8% |
| Mixed, own dishonor | 5% |

## Reproducing the tables

```
python3 tools/bots/behaviour/q1_attack_timing.py <logs-dir>...   # likewise q2 to q9, one script per table file
```

Log directories come from a tournament run with `L5R_TOURN_LOG=<dir>` (`tools/bots/par.sh` sets it to `artifacts/bots/logs-<name>`). The directory name tells the parser who is at each seat; see the header of `tools/bots/behaviour/l5rlog.py` for the convention and for the `dir=ChallengerLabel,ChampionLabel` override. `digest.py <log>` prints one game turn by turn as the parser understood it, for checking the parser against a raw log.
