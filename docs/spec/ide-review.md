# IDE recommendation review

## Standing user requirement
- Always review IDE recommendations for every edited code file, including newly added code and scripts. This applies to every task, not just the column-token PR.
- The user explicitly limited this rule to code. Markdown files, agent notes, documentation, and other non-code files (such as project/build configuration) are exempt from IDE linting. Continue normal manual review, relevant build/config checks, and executable documentation-example checks.
- Prefer Rider MCP lint_files with warning severity (includes suggestions/hints), or get_file_problems with errorsOnly=false.
- Evaluate recommendations rather than applying them mechanically. Consider correctness, readability, .NET target compatibility, AOT, public API needs, allocations, and existing conventions. Apply worthwhile changes, then re-inspect modified files and run checks appropriate to the change.
- Confirm that analysis completed. Retry timed-out/incomplete batches or inspect the affected files individually. Record unsupported/not-in-project files; do not report those as inspection-clean.
- Keep concise validation evidence for the file coverage, changes accepted, and reasons for material recommendations left unchanged. Avoid unrelated churn for existing style preferences.

## Documentation and final review
- The main README is the friendly, benefit-led GitHub/NuGet introduction. Retain its executable-example and exact-package-text checks; these checks are independent of IDE linting.
- Review the complete branch diff from main before final validation. Run IDE inspections on its edited/added code files. Markdown validation/agent files need manual review, not IDE lint attempts or unsupported-file records.
