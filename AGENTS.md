# Project contribution rules

- Before editing source or project documentation, read [`コーディング規約.md`](コーディング規約.md).
- Before reviewing changes, read [`コーディング規約.md`](コーディング規約.md) and apply the local `レビュー基準.md` when that Git-ignored file exists. If it is absent, the shared coding standard still applies.
- Apply these rules to every new or modified file and every changed class, method, function, and user-facing document. Existing unrelated content is outside the review scope.
- Before reporting a review, inspect the complete changed behavior and its callers, tests, data, and user documentation. Report specific file locations, impact, and the smallest safe correction for each finding.
- For changes to executable behavior, use the applicable CI, unit, integration, PlayMode, and end-to-end checks required by [`コーディング規約.md`](コーディング規約.md). Report what ran and what the evidence does not cover.
