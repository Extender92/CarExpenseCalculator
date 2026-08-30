# Issue workflow

## Purpose

GitHub Issues provide a decision-complete work queue for the repository. They do
not replace the product and architecture documents. Each work item links the
documents and existing implementation that define its scope.

## Workflow labels

An issue has exactly one workflow status:

| Label | Meaning |
| --- | --- |
| `status:needs-refinement` | The outcome, scope, dependencies, or acceptance criteria still require decisions. |
| `status:ready` | The issue is decision complete and has no unmet dependencies. |
| `status:in-progress` | A specific agent has been assigned and work has started. |
| `status:blocked` | Work cannot continue because a documented dependency or external decision is unresolved. |

Priorities are ordered `priority:p0`, `priority:p1`, then `priority:p2`.
Area labels identify the primary repository boundaries affected by the work.
The `tracking` label marks milestone overview issues and never represents a
direct implementation assignment.

The ready queue is ordered by the earliest active milestone, then priority, then
the lowest issue number. Only the user or the primary coordinating agent chooses
from this queue. Implementation agents never select issues themselves.

## Assignment and authority

An implementation agent must receive an explicit issue number from the user or
the primary coordinating agent. Before changing anything, the agent must:

1. Read `AGENTS.md`, the complete issue, and every linked repository document.
2. Confirm that the issue has `status:ready` and that every dependency is closed.
3. Inspect the relevant implementation and current Git state.

Assignment of a ready issue authorizes the agent to replace `status:ready` with
`status:in-progress`, create a focused branch, implement and verify the issue,
commit and push the work, and open or update its pull request. It does not
authorize merging the pull request; merges require separate user approval. After
an approved merge, GitHub automatically deletes the remote head branch. The
agent then switches to `main`, pulls with `--ff-only`, verifies that the pull
request is merged and the worktree is clean, and deletes the local head branch.
Use `git branch -d` for a recognized merge. Use `git branch -D` after a squash
merge only when those checks prove that the pull request content is present on
`main`. This verified post-merge cleanup needs no additional approval. Unmerged
branches must not be deleted manually without an explicit user request.

Assignment does not authorize changing the issue's scope, priority, milestone,
dependencies, or acceptance criteria. The primary coordinating agent owns
backlog refinement and decides when blocked work becomes ready.

## Branches and pull requests

Use one issue per branch and pull request. Name the branch with the issue number
and a short slug:

- `feature/<number>-<slug>` for product behavior.
- `fix/<number>-<slug>` for defect corrections.
- `docs/<number>-<slug>` for documentation-only work.
- `chore/<number>-<slug>` for repository and tooling work.

Keep the pull request within the assigned scope. Use `Closes #<number>` only
when every acceptance criterion is satisfied. A pull request is ready for user
review only after all required checks pass and the completion report identifies
the verification performed, warnings, limitations, and any skipped checks.

If work becomes blocked, stop before inventing missing behavior or expanding
scope. Comment on the assigned issue with concrete evidence and notify the
primary coordinating agent, who updates its workflow status and dependencies.

## Milestones and dependencies

Milestones follow the order in `roadmap.md`. A tracking issue summarizes each
milestone and links its work items. Future milestones remain
`status:needs-refinement` until their legal, product, data-source, and technical
decisions are sufficient to create decision-complete work items.

Dependency relationships are written explicitly as `Blocked by #<number>` in
the issue body and linked from the milestone tracker. Closing a dependency does
not automatically make every dependent issue ready. The primary coordinating
agent first verifies that no decisions or dependencies remain, updates the issue
body if needed, and only then applies `status:ready`.
