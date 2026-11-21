using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md step 2, the effect descriptors): one card-agnostic reading per ability, checked on the abilities CODE_MAP.md section 5 traced by hand and on the
// shapes the Read must tell apart (send home, bow, destroy, a Force cut, a lock, a duel, late Force, a Province Strength change, a negation).
internal static class EffectDescriptorTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static EffectDescriptor D(string id) => EffectDescriptors.Of(id);
    private static bool Has(EffectDescriptor d, string kind) => d.Conditions.Any(c => c.Kind == kind);

    public static readonly (string, Action)[] Tests =
    [
        ("a send-home reads as SendHome, not Neutral (block-supply, favor-battle, incapacitated; CODE_MAP 5.1)", () =>
        {
            var b = D("block-supply"); Check(b.Class == EffectClass.SendHome && b.Side == TargetSide.Enemy && Has(b, "attacking") && b.Neutralises, $"block-supply: {b}");
            var f = D("favor-battle"); Check(f.Class == EffectClass.SendHome && f.Side == TargetSide.Enemy && Has(f, "attacking"), $"favor-battle: {f}");
            var i = D("incapacitated"); Check(i.Class == EffectClass.SendHome && Has(i, "defending") && i.IsBattle, $"incapacitated: {i}");
        }),
        ("a Force cut carries its number, and the Ranged Attack beside it is not lost (kahoku-exp-attack; CODE_MAP 5.2)", () =>
        {
            var d = D("kahoku-exp-attack");
            Check(d.Class == EffectClass.ForceCut && d.Magnitude == 4 && d.Side == TargetSide.Enemy && d.Target == TargetKind.Personality, $"kahoku: {d}");
            Check(d.Secondary.Contains(EffectClass.Destroy) && Has(d, "force-at-most") && d.Conditions.First(c => c.Kind == "force-at-most").Value == 3, $"kahoku ranged: {d}");
        }),
        ("an Attack with a printed strength is a Destroy limited to that Force; one equal to a stat varies (dairuko-melee, ebizo-attack; CODE_MAP 5.3)", () =>
        {
            var m = D("dairuko-melee"); Check(m.Class == EffectClass.Destroy && m.Magnitude == 8 && !m.MagnitudeVaries && Has(m, "force-at-most") && Has(m, "without-followers"), $"dairuko: {m}");
            var e = D("ebizo-attack"); Check(e.Class == EffectClass.Destroy && e.Magnitude is null && e.MagnitudeVaries, $"ebizo: {e}");
        }),
        ("bow, then destroy an attachment: Bow primary, Destroy secondary, the target without attachments (hiroto-bow)", () =>
        {
            var d = D("hiroto-bow"); Check(d.Class == EffectClass.Bow && d.Secondary.Contains(EffectClass.Destroy) && Has(d, "without-attachments") && d.Side == TargetSide.Enemy, $"hiroto: {d}");
        }),
        ("Force to 0 is a Force cut of everything; -6F and bow keeps both (rikate-exp-weaken, control-the-board-weaken)", () =>
        {
            var r = D("rikate-exp-weaken"); Check(r.Class == EffectClass.ForceCut && r.Magnitude == 99, $"rikate: {r}");
            var c = D("control-the-board-weaken"); Check(c.Class == EffectClass.ForceCut && c.Magnitude == 6 && c.Secondary.Contains(EffectClass.Bow) && c.PerformerKeyword == "Tactician" && c.PerformerUnbowed, $"control: {c}");
        }),
        ("late Force: Tactical Advantage is a repeatable ForceAdd whose amount varies; a printed +3 is fixed (tactical-advantage, crushing-force)", () =>
        {
            var t = D("tactical-advantage"); Check(t.Class == EffectClass.ForceAdd && t.MagnitudeVaries && t.Repeatable && t.DiscardsCard && !t.Neutralises, $"tactical: {t}");
            var c = D("crushing-force"); Check(c.Class == EffectClass.ForceAdd && c.Magnitude == 3 && c.Side == TargetSide.Own && Has(c, "opposed"), $"crushing: {c}");
        }),
        ("a lock, a duel, a negation and a Province Strength change each have their own class (naoto-lock, steel-on-steel-duel, a-close-call-negate, genji-province-lower)", () =>
        {
            var l = D("naoto-lock"); Check(l.Class == EffectClass.Lock && l.Target == TargetKind.Unit && l.Neutralises, $"naoto: {l}");
            var s = D("steel-on-steel-duel"); Check(s.Class == EffectClass.Duel && s.Secondary.Contains(EffectClass.Destroy) && s.PerformerUnbowed && s.PerformerAtBattlefield, $"steel: {s}");
            var n = D("a-close-call-negate"); Check(n.Class == EffectClass.Negate && n.IsReaction && n.Trigger == "before-attack-resolves", $"close call: {n}");
            var g = D("genji-province-lower"); Check(g.Class == EffectClass.ProvinceStrength && g.Magnitude == -3 && g.BowsSource && g.Target == TargetKind.Province, $"genji: {g}");
        }),
        ("a conditional effect says so, and a cost is read from the clause before the colon (deception-revealed-weaken, akodo-guidance-melee)", () =>
        {
            var d = D("deception-revealed-weaken"); Check(d.Class == EffectClass.ForceCut && d.Magnitude == 4 && d.Secondary.Contains(EffectClass.Bow), $"deception: {d}");
            var m = D("akodo-guidance-melee"); Check(m.Class == EffectClass.Destroy && m.Magnitude == 4 && m.DiscardsCard, $"guidance: {m}");
        }),
        ("the traps of the hand check: 'destroyed' in a condition is no Destroy, a negated lock is no Lock, an own unit moved home is a Retreat, 'without attachments' is not an Attachment target (preserving-beauty, incense-mill-cleanse, fubatsu-plate-move, reckless-rush-bow, overcome-adversity)", () =>
        {
            var p = D("preserving-beauty"); Check(p.Class == EffectClass.HonorGain && !p.Has(EffectClass.Destroy), $"preserving: {p}");
            var i = D("incense-mill-cleanse"); Check(i.Class == EffectClass.Negate && !i.Has(EffectClass.Lock), $"incense: {i}");
            var f = D("fubatsu-plate-move"); Check(f.Class == EffectClass.Retreat, $"fubatsu: {f}");
            var r = D("reckless-rush-bow"); Check(r.Class == EffectClass.Bow && r.Target == TargetKind.UnitCard && Has(r, "without-attachments"), $"reckless: {r}");
            var o = D("overcome-adversity"); Check(o.Class == EffectClass.ForceCut && o.MagnitudeVaries && o.Side == TargetSide.Enemy, $"overcome: {o}");
        }),
        ("an ability the catalogue does not know is Unknown with a reason, never a crash", () =>
        {
            var u = EffectDescriptors.Of("no-such-ability"); Check(u.Class == EffectClass.Unknown && u.Why is not null, $"unknown: {u}");
        }),
        ("coverage: no more than 10% of the catalogue's Battle abilities are Unknown", () =>
        {
            var battle = AbilityCatalog.All.Where(kv => kv.Value.Timing is "Battle" or "BattleOrOpen").ToArray();
            var unknown = battle.Where(kv => EffectDescriptors.Of(kv.Key).Class == EffectClass.Unknown).ToArray();
            Console.WriteLine($"  Battle abilities {battle.Length}, unknown {unknown.Length}: {string.Join(", ", unknown.Take(12).Select(kv => kv.Key))}");
            Check(unknown.Length <= battle.Length / 10, $"{unknown.Length} of {battle.Length} Battle abilities are Unknown");
        })
    ];
}
