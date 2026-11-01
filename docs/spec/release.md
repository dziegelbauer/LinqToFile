# Release preparation decisions

## Phase 6 scope
- Prepare and validate a local alpha.6 package. Public publication and release creation are not part of the current authorization.
- Package metadata links the confirmed GitHub repository. Keep Apache-2.0 expression and the unmodified license text. Do not invent individual authors or a copyright holder.
- Separate NuGet-facing README from repository README; its documentation links are absolute.
- Validate package assets/manifest and a fresh external consumer, with the model assembly depending only on the package and the caller depending on the model assembly.
- Execute the exact packaged README C# snippets, plus sync/async, encoding, and Native AOT checks. PowerShell 7 is development tooling only.
- Use relative project paths after changing into the temporary workspace to avoid macOS /var versus /private/var path aliases in the restore graph.
- CI runs the external check on each existing platform. The user confirmed successful CI after commit d8c7030. Phase 6 is complete; hosted results are user-reported, not independently retrieved by the agent.

## Publication decisions deferred
- Public release version, author display metadata, target feed, package ownership, and explicit authorization to publish. The artifact remains a prerelease; current Authors defaults to the project name.
