using System.Text.Json;

namespace L5R.Core;

/// <summary>
/// Issue #888: while one QueryActions call runs, the match does not change, so the Gold payment tokens a payer can name are the same every time they are asked for
/// (one action list asks for them hundreds of times, and each ask walks every registered token hook over every card). They are computed once per payer and subject
/// for the duration of the call and dropped when it ends. Execute and every other path compute them afresh.
/// </summary>
public sealed partial class MatchEngine
{
    private int _queryScopeDepth;
    private Dictionary<(int Player, CardInstance? Card, PaymentKind? Kind, AbilityDefinition? Ability, CardInstance? Wielder), PaymentToken[]>? _paymentTokenMemo;

    /// <summary>Tests only: recompute every remembered token list when it is reused and fail if it differs.</summary>
    [ThreadStatic] internal static bool VerifyQueryScopeThisThread;
    private static readonly bool VerifyQueryScopeEverywhere = Environment.GetEnvironmentVariable("L5R_VERIFY_QUERY_SCOPE") == "1"; // run a whole suite under the check
    internal static bool VerifyQueryScopeForTests { get => VerifyQueryScopeThisThread || VerifyQueryScopeEverywhere; set => VerifyQueryScopeThisThread = value; }

    private ActionOption[] QueryActionsScoped(int playerId)
    {
        // A query may still seed derived data lazily (PrintedHoldsSeeded), so the match is not byte-identical afterwards; what matters is that no Gold payment token
        // changes during the call, and VerifyQueryScopeForTests recomputes every remembered list at each reuse to prove that.
        _queryScopeDepth++;
        try { return QueryActionsUnscoped(playerId); }
        finally { if (--_queryScopeDepth == 0) { _paymentTokenMemo = null; _quoteMemo = null; } }
    }

    // Issue #1206: the same holds for the payment options and sources a fixed Gold quote lists. They depend only on the payer, the amount, the excluded cards, the
    // subject paid for and the match, and in a bot's lookahead playout 93% of the quotes one action list builds repeat an earlier one exactly.
    private Dictionary<object, Array>? _quoteMemo;

    private T[] QueryMemo<T>(object key, Func<T[]> compute)
    {
        if (_queryScopeDepth == 0) return compute();
        var memo = _quoteMemo ??= [];
        if (memo.TryGetValue(key, out var kept))
        {
            if (VerifyQueryScopeForTests && JsonSerializer.Serialize(compute()) != JsonSerializer.Serialize((T[])kept))
                throw new InvalidOperationException($"A remembered Gold quote is stale: {key}.");
            return [.. (T[])kept];
        }
        var computed = compute();
        memo[key] = computed.ToArray();
        return computed;
    }

    private static string ExcludeKey(IReadOnlySet<string>? exclude) => exclude is null or { Count: 0 } ? "" : string.Join(",", exclude.Order(StringComparer.Ordinal));

    private List<PaymentToken> PaymentTokens(int playerId, PaymentSubject? paying)
    {
        if (_queryScopeDepth == 0) return ComputePaymentTokens(playerId, paying);
        var memo = _paymentTokenMemo ??= [];
        var key = (playerId, paying?.Card, paying?.Kind, paying?.Ability, paying?.Wielder);
        if (memo.TryGetValue(key, out var kept))
        {
            if (VerifyQueryScopeForTests)
            {
                var fresh = ComputePaymentTokens(playerId, paying);
                if (fresh.Count != kept.Length || fresh.Zip(kept).Any(pair => pair.First.Id != pair.Second.Id || pair.First.Amount != pair.Second.Amount || pair.First.Label != pair.Second.Label))
                    throw new InvalidOperationException($"A remembered Gold payment token list is stale for player {playerId}: {string.Join(",", kept.Select(t => t.Id))} against {string.Join(",", fresh.Select(t => t.Id))}.");
            }
            return [.. kept];
        }
        var computed = ComputePaymentTokens(playerId, paying);
        memo[key] = [.. computed];
        return computed;
    }
}
