# IDE recommendation review

## Standing user requirement
- Always review IDE recommendations for every edited file, not just the column-token PR.
- Prefer Rider MCP lint_files with warning severity (includes suggestions/hints), or get_file_problems with errorsOnly=false. Include added files, project files, documentation, and agent notes in the review scope.
- Evaluate recommendations rather than applying them mechanically. Consider correctness, readability, .NET target compatibility, AOT, public API needs, allocations, and existing conventions. Apply worthwhile changes, then re-inspect modified files and run checks appropriate to the change.
- Confirm that analysis completed. Retry timed-out/incomplete batches or inspect the affected files individually. Record unsupported/not-in-project files; do not report those as inspection-clean.
- Keep concise validation evidence for the file coverage, changes accepted, and reasons for material recommendations left unchanged. Avoid unrelated churn for existing style preferences.

## Current token PR
- The user also requested a friendly, benefit-led README refresh before this PR is ready. The main README is the packaged NuGet introduction, so retain its executable example and exact-package-text checks.
- Inspect the complete branch diff from main and all new files before final validation. New validation/agent files must also be checked or recorded as unsupported.
