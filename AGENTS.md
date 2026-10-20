# Project contribution rules

- Before editing source or project documentation, read [`コーディング規約.md`](コーディング規約.md).
- Before reviewing changes, read [`コーディング規約.md`](コーディング規約.md) and apply the local `レビュー基準.md` when that Git-ignored file exists. If it is absent, the shared coding standard still applies.
- Apply these rules to every new or modified file and every changed class, method, function, and user-facing document. Existing unrelated content is outside the review scope.
- Before reporting a review, inspect the complete changed behavior and its callers, tests, data, and user documentation. Report specific file locations, impact, and the smallest safe correction for each finding.
- Work on one Issue per branch. Name the branch `feat/<topic>` (or `fix/`, `docs/`, `chore/`) and create it from the branch that already contains the code the work depends on. Do not add unrelated Issue work to an existing feature branch.
- When the work is finished, push the branch and open a pull request whose base is that parent branch. Describe the Issue, the behavior change, what ran, and what was not verified.
- Merge the pull request only after every CI check passes and the review finds no unresolved defect or standard violation. If a check was not run or failed, report the cause and do not merge without the user's approval.
- Do not commit generated or local files (`bin/`, `obj/`, `Library/`, `ProjectSettings` files that were not intentionally changed, `レビュー基準.md`). Every new `.cs`, `.asmdef`, `.unity`, or `.json` file under `Assets/KadokaShipBattler` needs its `.meta` file.
- When a new CI project is added under `Tools/CI/`, exclude its folder from `Tools/CI/GameplayChecks.csproj` and add its step to `.github/workflows/unity-ci.yml`.
- For changes to executable behavior, use the applicable CI, unit, integration, PlayMode, and end-to-end checks required by [`コーディング規約.md`](コーディング規約.md). Report what ran and what the evidence does not cover.
