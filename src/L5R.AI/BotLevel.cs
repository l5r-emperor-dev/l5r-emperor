namespace L5R.AI;

/// <summary>Issue #1214: how hard the practice opponent plays. Easy is the Champion (the bot on main before this), Moderate the Contender (docs/bots/CONTENDER.md), Hard Conan (docs/bots/CONAN.md).</summary>
public enum BotLevel { Easy, Moderate, Hard }

public static class BotLevels
{
    public static string Label(this BotLevel level) => level switch { BotLevel.Easy => "Easy", BotLevel.Moderate => "Moderate", _ => "Hard" };

    public static string Describe(this BotLevel level) => level switch
    {
        BotLevel.Easy => "The Champion: plays sensibly, quickly.",
        BotLevel.Moderate => "The Contender: plans its turn and spends well.",
        _ => "Conan: also plays out its battles ahead. It can take several seconds to decide."
    };

    /// <summary>A fresh memory for one match at this level. Hard only searches once the caller sets <see cref="BotContext.Sampler"/> (see <see cref="NeedsSampler"/>).</summary>
    public static BotContext NewMemory(this BotLevel level) => level switch { BotLevel.Easy => new BotContext(), BotLevel.Moderate => BotContext.Contender(), _ => BotContext.Hard() };

    /// <summary>True when the level plays out candidate moves on redealt copies of the match, so each decision needs a sampler and is slow enough to run off the UI thread.</summary>
    public static bool NeedsSampler(this BotLevel level) => level == BotLevel.Hard;

    public static BotLevel Parse(string? value) => Enum.TryParse<BotLevel>(value, true, out var level) && Enum.IsDefined(level) ? level : BotLevel.Easy;
}
