# Release preparation decisions

## Current publishing authorization
- GitHub rejected ruleset access for this private repository with HTTP 403: GitHub Pro or public visibility is required. No visibility or account-plan change was made. Feature-branch work remains the project convention; the workflow still gates main publication on the full CI matrix.
- GitHub CLI is authenticated as dziegelbauer with repository admin access. Configured and verified the nuget environment, its main-only branch deployment policy, and NUGET_USER=dziegelbauer. The user confirmed personal NuGet ownership and created the NuGet Trusted Publishing policy for this repository, ci.yml, environment nuget, and package LinqToFile with create/version-push scopes. The OIDC exchange and package push succeeded in main run 41349150779.
- The user authorized prototype prerelease publication to nuget.org after successful main CI. Public GitHub release creation is not part of this change.
- All future work uses feature branches and pull requests into main. Publishing and human-documentation restructuring merged through PR #1; allocation-measurement noise fixes merged through PR #2. Their main commits are 87c383f and 5a30854.
- The CI version is the base prerelease plus .ci.<github.run_number>. Reruns retain the version; new runs get distinct versions. Local defaults remain unchanged.
- Publish the Linux validation job's package only after the entire Windows/Linux/macOS matrix succeeds. PR, feature-branch, tag, and manual runs do not publish. Main push events cover all merge strategies; branch rules must enforce PR-only changes.
- Use NuGet Trusted Publishing (NuGet/login@v1) with job-scoped id-token permission, GitHub environment nuget, and the NUGET_USER variable. Account trust and environment setup are external prerequisites. See [publishing setup](maintenance/publishing.md).
- Local checks pass: workflow actionlint validation; version retries/new runs/invalid arguments; warning-free CI-version Release build; all 719 tests; versioned package metadata and clean JIT/Native AOT consumption. Hosted main run 41349150779 passed prepare, all three platform validation jobs, and publish; NuGet accepted LinqToFile 0.1.0-alpha.9.ci.15 at 2026-11-01 20:58:21 UTC. Subsequent public flat-container and package-page checks still returned 404, so public restore availability is not yet confirmed. Do not republish the same immutable version to address indexing delay.
- Two CI allocation checks intermittently counted one-time runtime work. They now warm their paths and use the minimum of three measurements without changing their allocation thresholds or runtime code. Both PR matrices and the final main matrix passed.

## Phase 6 scope
- The phase prepared and validated a local alpha.6 package. Public publication and release creation were outside that phase; the current publishing authorization above now applies.
- Package metadata links the confirmed GitHub repository. Keep Apache-2.0 expression and the unmodified license text. Do not invent individual authors or a copyright holder.
- Separate NuGet-facing README from repository README; its documentation links are absolute.
- Validate package assets/manifest and a fresh external consumer, with the model assembly depending only on the package and the caller depending on the model assembly.
- Execute the exact packaged README C# snippets, plus sync/async, encoding, and Native AOT checks. PowerShell 7 is development tooling only.
- Use relative project paths after changing into the temporary workspace to avoid macOS /var versus /private/var path aliases in the restore graph.
- CI runs the external check on each existing platform. The user confirmed successful CI after commit d8c7030. Phase 6 is complete; hosted results are user-reported, not independently retrieved by the agent.

## Remaining release decisions
- Stable release version and GitHub release creation remain separate decisions. The artifact remains a prerelease; current Authors defaults to the project name. The NuGet publishing profile is provided through NUGET_USER, with ownership configured in its trust policy.
