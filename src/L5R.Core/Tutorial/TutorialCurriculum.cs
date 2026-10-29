namespace L5R.Core;

/// <summary>One lesson of the curriculum, built or not. <see cref="Id"/> is the id of the <see cref="TutorialLesson"/> that fills it once it is written. The menu shows every entry (nothing is locked):
/// a built one starts, an unbuilt one is a placeholder. <see cref="Subtitle"/> is what the lesson covers, shown when the entry is hovered or focused (empty when the title says it all).</summary>
public sealed record TutorialEntry(string Id, string Title, string Subtitle)
{
    /// <summary>True once a lesson with this id is in <see cref="TutorialLessons.All"/>.</summary>
    public bool Built => TutorialLessons.All.Any(l => l.Id == Id);
}

public sealed record TutorialTrack(string Id, string Title, TutorialEntry[] Entries);

/// <summary>Every tutorial the game needs, in the order the menu shows them (docs/TUTORIAL_CURRICULUM.md is the readable copy; issue #1035 tracks writing them).
/// Whoever builds one adds its <see cref="TutorialLesson"/> to <see cref="TutorialLessons.All"/> with the same id, and the menu starts it.</summary>
public static class TutorialCurriculum
{
    public static readonly TutorialTrack[] Tracks =
    [
        new("playing", "Playing the Game",
        [
            new("first-turn", "The Basics", "Take a first turn as Lion against Crane: the phases, recruiting, Proclaim, equipping, attacking and destroying a province."),
        ]),
        new("getting-started", "Level Up Your Play",
        [
            new("dynasty-deck", "Events, Regions, and Celestials", "The rest of the Dynasty Deck"),
            new("recruiting", "Recruiting Personalities", "Clan Alignment, Honor Requirement, Recruitment Effects, Overlays"),
            new("spells", "Spells and Shugenja", "Attaching and using spells in the Action and Battle Phases"),
            new("attachments", "Attachments", "Followers, Items and Spells: Equip, Honor Requirement, Weapon and Armor, and the abilities on attachments"),
            new("keywords", "Keywords", "Functional vs Non-functional Keywords"),
            new("traits-actions", "Traits and Actions", "Timing and limitations of Traits and Actions"),
            new("cost-effect", "Cost and Effect", "The difference between paying costs and applying effects"),
            new("special-attacks", "Special Actions - Attacks", "Melee and Ranged Attacks"),
            new("special-duels", "Special Actions - Duels", "Refusability, the Duel flow, and Focus Effects"),
            new("special-dishonor", "Special Actions - Dishonor", "Dishonorable status, Dying Dishonorable, Dishonorable Dead, Rehonor, Seppuku"),
            new("rings", "Rings and Enlightenment", "How to play a ring, How to win an Enlightenment Victory"),
            new("reactions", "Reactions", "Timing, Triggers, Effects"),
            new("imperial-favor", "Imperial Favor", "Lobbying and Using the Imperial Favor"),
        ]),
        new("building", "Building a Deck",
        [
            new("card-library", "Card Library", ""),
            new("deck-builder", "Deck Builder Overview", "How to use the Deck Builder"),
            new("deck-anatomy", "Anatomy of a Deck", "What is required and what is good practice"),
            new("using-your-deck", "Using Your Designed Deck", ""),
            new("using-settings", "Using Settings", ""),
        ]),
    ];

    public static IEnumerable<TutorialEntry> All => Tracks.SelectMany(t => t.Entries);

    /// <summary>The entry the curriculum lists after this lesson (the next track's first when it is a track's last), or null after the final one.</summary>
    public static TutorialEntry? After(string lessonId)
    {
        var all = All.ToArray(); var at = Array.FindIndex(all, e => e.Id == lessonId);
        return at >= 0 && at + 1 < all.Length ? all[at + 1] : null;
    }

    /// <summary>The track a lesson belongs to: where the menu returns to when the lesson ends or is left.</summary>
    public static TutorialTrack? TrackOf(string lessonId) => Tracks.FirstOrDefault(t => t.Entries.Any(e => e.Id == lessonId));
}
