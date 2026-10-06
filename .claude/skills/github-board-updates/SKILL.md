---
name: github-board-updates
description: Keep the GitHub project board (alraiani "aerai-seller-app-project", project 3) in sync with the work — move items between Todo / In Progress / Done and post progress comments on the item's issue recording what was done and why. Use whenever starting work on a board item, finishing a phase or commit for it, changing its scope or hitting a blocker, opening its PR, or finishing it; also when the user says they moved an item.
---

# GitHub board updates

The board at https://github.com/users/alraiani/projects/3 is the team's record of what is being built. Each item's **issue comments are the work log**: anyone reading the issue should be able to tell what was done, in what order, and why, without opening the code. Applies to both the WPF app (`src/`) and the web app (`web/`).

Requires `gh` with the `project` scope (`gh auth status` lists it; if missing, ask the user to run `gh auth refresh -s project`).

## Board facts

| What | Value |
|---|---|
| Repo | `alraiani/aeria-seller-app` |
| Project | owner `alraiani`, number `3`, id `PVT_kwHOALvtt84Bleat` |
| Status field | `PVTSSF_lAHOALvtt84BleatzhkLK1Q` |
| Status options | Todo `f75ad846` · In Progress `47fc9ee4` · Waiting on Review `7a563279` · Done `98236657` |

If a command fails with an unknown id (the board was rebuilt or a column added), refresh them:

```bash
gh project field-list 3 --owner alraiani --format json --jq '.fields[] | select(.name=="Status")'
```

## When to comment

Comment on the item's **issue** at each of these moments — not on every commit:

1. **Start** (item moved to In Progress, by you or the user): the planned approach, phases, and decisions taken with the user, each with its reason.
2. **Milestone**: a phase is committed/pushed. What changed, the commit(s), and why it was done this way.
3. **Change of course**: scope change, blocker, or a deliberately deferred piece (e.g. "UK waits for EU credentials"). What changed and why.
4. **PR opened**: link and a one-line summary.
5. **Done**: summary, PR link, how it was verified, follow-ups (and new issues created for them, if any).

Read existing comments first (`gh issue view <n> --repo alraiani/aeria-seller-app --comments`) so you add to the log instead of repeating it.

## Comment format

Short, linked, skimmable. Never paste large code; never include secrets, tokens, connection strings, credentials, or customer/order data.

```markdown
### <Start | Phase N: name | Change of course | PR opened | Done>

**What:** 1–4 bullets of what was done or decided.
**Why:** the reasoning — constraints, trade-offs, user decisions.
**Commits/PR:** <sha links or PR link>   (omit if none yet)
**Next:** what comes after this.

<sub>Logged by Claude Code</sub>
```

Post it from stdin so formatting survives:

```bash
gh issue comment <n> --repo alraiani/aeria-seller-app --body-file - <<'EOF'
...
EOF
```

To fix your own last comment rather than adding noise: `gh issue comment <n> --repo alraiani/aeria-seller-app --edit-last --body-file -`.

## Moving an item

```bash
# item id for issue <n>
ITEM=$(gh project item-list 3 --owner alraiani --limit 200 --format json \
  --jq '.items[] | select(.content.number==<n>) | .id')
gh project item-edit --id "$ITEM" --project-id PVT_kwHOALvtt84Bleat \
  --field-id PVTSSF_lAHOALvtt84BleatzhkLK1Q --single-select-option-id <option>
```

- **In Progress** when work actually starts (post the Start comment in the same step).
- **Waiting on Review** once the work is finished and its PR is open, waiting to be reviewed and merged.
- **Done** only after the PR is merged, or when the user explicitly says so. Pair it with the Done comment.
- Never move items you aren't working on, and never move an item backwards without telling the user.
- An issue not on the board: `gh project item-add 3 --owner alraiani --url <issue url>`.

## Draft items

Draft items aren't issues and can't take comments. Offer to convert one before starting work (GraphQL `convertProjectV2DraftIssueItemToIssue` with the item id and repository id); if the user declines, append the log to the draft's body instead (`gh project item-edit --id <item> --title ... --body ...`).
