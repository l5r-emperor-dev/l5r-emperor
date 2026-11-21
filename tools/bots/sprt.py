#!/usr/bin/env python3
"""Sequential probability ratio test over paired arena games (issue #1206, docs/bots/CONAN_CAMPAIGN_BRIEF.md section 4, Tier 2).

    python3 tools/bots/sprt.py --baseline best-7200.json [best-7201.json ...] --arm arm-7200.json [arm-7201.json ...] [--p1 0.6] [--alpha 0.05] [--beta 0.05]

Only the games whose outcome changed between the arm and the baseline on the same seeds count: G gained, L lost. Under H0 a changed game is a gain with
probability p0 = 0.5 (the arm is no better); under H1 with p1 (default 0.6, about net +10 per 50 changed games). LLR = G ln(p1/p0) + L ln((1-p1)/(1-p0)).
Accept H1 at LLR >= ln((1-beta)/alpha), H0 at LLR <= ln(beta/(1-alpha)); otherwise continue (add the next 182-game chunk, one seed). Analyse only at chunk
boundaries. Python standard library only.
"""
import argparse
import math

from compare_tournament import load, won


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--baseline", nargs="+", required=True)
    ap.add_argument("--arm", nargs="+", required=True)
    ap.add_argument("--p1", type=float, default=0.6)
    ap.add_argument("--alpha", type=float, default=0.05)
    ap.add_argument("--beta", type=float, default=0.05)
    a = ap.parse_args()
    base, arm = load(a.baseline), load(a.arm)
    shared = [k for k in arm if k in base and arm[k]["Winner"] is not None and base[k]["Winner"] is not None]
    gained = sum(1 for k in shared if won(arm[k]) and not won(base[k]))
    lost = sum(1 for k in shared if won(base[k]) and not won(arm[k]))
    p0 = 0.5
    llr = gained * math.log(a.p1 / p0) + lost * math.log((1 - a.p1) / (1 - p0))
    upper, lower = math.log((1 - a.beta) / a.alpha), math.log(a.beta / (1 - a.alpha))
    state = "H1 accepted (arm better)" if llr >= upper else "H0 accepted (arm not better)" if llr <= lower else "continue"
    rate = sum(1 for k in shared if won(arm[k])) / len(shared) if shared else 0
    print(f"games {len(shared)}  arm win {100 * rate:.1f}%  gained {gained}  lost {lost}  net {gained - lost:+d}  LLR {llr:+.2f}  bounds [{lower:.2f}, {upper:.2f}]  -> {state}")


if __name__ == "__main__":
    main()
