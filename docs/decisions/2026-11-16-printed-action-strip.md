# Printed-action use strip — #1201

Alfredo selected concept A: a numbered strip below the full card/token/restriction assembly, remaining readable when the card bows. The strip preserves the original scan. Each printed action has its own USED / UNUSED status, with full timing and labels in hover and inspection. UNUSED means a use remains, not that timing, costs or other restrictions permit acting now.

Core publishes optional `CardView.PrintedActions` rows with the printed action's execution `AbilityIds`, label, timing and current spent status. Traits and non-action Focus Effects/before-resolution effects are excluded. Display aliases use an explicit, print-checked list: a shared usage key alone is not a print identity (Brawl and Folklore have separate paragraphs sharing a limit). New unverified keys remain separate until their printing is checked. Legacy `SpentAbilityIds` keeps every spent execution branch for existing consumers.

The projection follows existing once-per-game/period/use-count logic and the live Unsafe Passage waiver. It does not change legality, commands, state persistence, seeds or replay input. Both seats see only face-up in-play status; old payloads have no strip. Historical usage is not fabricated for old payloads.

Attachment strips use separate bands below the complete stack, with each strip's hover/selection identifying its card. Expanded stacks retain those bands. Regions clear their Dynasty host and province placard. Preview and persistent-row layouts reserve the corresponding bottom space. Reduced motion keeps the same static status.

Native validation and review acceptance are recorded in the task handoff/PR; automated package checks do not establish Mendon's Windows graphical or rules acceptance.
