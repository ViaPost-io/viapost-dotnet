# Contributing

1. Never commit API keys or customer data.
2. Add one failing behavioral test first (Red), implement the minimum (Green), then refactor.
3. Run the complete verification commands from `README.md`.
4. Update `CHANGELOG.md` for user-visible changes.
5. Never update `openapi.yaml` without recording the source commit and expected SHA in the drift
   checker.

Mutating operations must remain non-retryable. Contract changes require compatibility review.
