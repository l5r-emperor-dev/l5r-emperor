namespace L5R.Core;

/// <summary>A card that starts in play at home, face up (a Border Keep, a bowed Bamboo Harvesters). <paramref name="Attachments"/> are cards already attached to it (a Personality's Item);
/// <paramref name="ChiToken"/> is a token on it that changes its Chi by that amount (a -1C token).</summary>
internal sealed record TutorialHomeCard(string DefinitionId, bool Bowed = false, string[]? Attachments = null, int ChiToken = 0);

/// <summary>One side of a lesson's table. The deck is the pinned deck of <see cref="Clan"/> (<c>content/published/lion-crane-v1.json</c>) unless <see cref="Deck"/> names another; the cards in
/// <see cref="DynastyTop"/> and <see cref="FateTop"/> are put on top in that order, whether or not the deck holds them. A catalogue card outside the pinned decks takes the place of the deck's last card.
/// <see cref="DynastyDiscard"/> starts in the Dynasty discard pile; each of <see cref="Regions"/> is attached to that Province (counted from 0).</summary>
internal sealed record TutorialSide(string Clan, string StrongholdId, string[] DynastyTop, string[] FateTop, TutorialHomeCard[] Home, int? Honor = null, PublishedDeck? Deck = null,
    string[]? DynastyDiscard = null, (int Province, string DefinitionId)[]? Regions = null);

/// <summary>The table a lesson starts from. Provinces are filled and face down, hands are empty and the match stops before the first turn (<see cref="MatchEngine.TutorialBeginTurn"/>);
/// a lesson that needs more (a Ring already in play, a destroyed Province, a hand) does it from its steps' <see cref="TutorialStep.Then"/>.
/// <see cref="You"/> is the seat the player takes; <see cref="FirstPlayer"/> is whoever goes first, 0 being you. <see cref="TurnsTaken"/> starts the match that many turns in for each player (so first-turn rules
/// no longer apply); <see cref="NoEndDraw"/> makes the End phase draw nothing, so hands stay as the lesson set them.</summary>
internal sealed record TutorialSetup(TutorialSide You, TutorialSide Foe, int FirstPlayer = 0, int TurnsTaken = 0, bool NoEndDraw = false);

/// <summary>What the opponent does when a step lets it play: which Personalities it recruits with the Clan Discount, which cards it recruits at full cost (a Holding, whatever is free),
/// and the abilities it uses on a target (<c>(ability id, definition id of the card it targets)</c>). Anything else it declines or passes.
/// <see cref="AttackProvince"/>: it declares an attack on its own turn, sends every Personality it has at home against that Province of yours and fights that battle. <see cref="Performers"/>: for an ability
/// several of its cards could perform (one granted by its Stronghold), the card (by definition id) that performs it, matched by position with <see cref="Abilities"/>.</summary>
internal sealed record TutorialFoeScript(string[] RecruitWithDiscount, string[] Recruit, (string Ability, string Target)[] Abilities, FoePurchase[]? Purchases = null, string[]? Discard = null,
    int? AttackProvince = null, string[]? Performers = null)
{
    public static readonly TutorialFoeScript Passive = new([], [], []);
}

/// <summary>One recruit the opponent makes, in order, with exactly the Clan Discount / Clan Allegiance it uses, paid by exactly these cards (by definition id), and whether it Proclaims.
/// When a script has <see cref="TutorialFoeScript.Purchases"/> they replace the plain <c>RecruitWithDiscount</c> / <c>Recruit</c> lists.</summary>
internal sealed record FoePurchase(string DefinitionId, bool ClanDiscount, bool ClanAllegiance, string[] PaidBy, bool Proclaim = false);

/// <summary>A lesson: where it starts, what it says and what the opponent does. Every lesson stands alone (nothing is locked behind another), so each builds its own table.</summary>
/// <param name="ProvincesStep">The step that lays out the Provinces; the notes before it are read on an empty cloth. Zero when the table is shown from the start.</param>
/// <param name="Opening">What is done to the table before the first note shows (draw the hand, start the turn), so a lesson can open in the middle of a turn.</param>
internal sealed record TutorialLesson(string Id, string Title, TutorialSetup Setup, TutorialStep[] Steps, TutorialFoeScript Foe, int ProvincesStep = 0, Action<TutorialContext>? Opening = null);

/// <summary>Every lesson, in the order the menu suggests. The menu never locks one.</summary>
internal static class TutorialLessons
{
    public static readonly TutorialLesson[] All = [TutorialLessonOne.Lesson, TutorialLessonDynasty.Lesson, TutorialLessonRecruiting.Lesson, TutorialLessonSpells.Lesson, TutorialLessonAttachments.Lesson, TutorialLessonKeywords.Lesson, TutorialLessonKeywords2.Lesson];
    public static TutorialLesson Default => All[0];
    public static TutorialLesson Get(string id) => All.Single(l => l.Id == id);
}
