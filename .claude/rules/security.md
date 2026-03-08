## Security Rules

When modifying code that handles project names, storage paths, or file deletion:

- **Project names are unsanitized.** `ProjectIndexer.ResolveProjectName` passes user input (directory name or `--name`) directly to `StoragePaths.GetProjectDir` via `Path.Combine` — no traversal protection. `PathValidator` exists but is not called on project names. Any new code touching project names must sanitize to alphanumeric + hyphens, or validate with `PathValidator.ValidateWithinRoot`.

- **PurgeCommand deletes recursively without path validation.** `IndexStore.Delete()` calls `Directory.Delete(projectDir, recursive: true)` where `projectDir` is derived from user input. A crafted name could target directories outside `~/.token-squeeze/projects/`. Add `PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir)` before any deletion.

- **Only root-level .gitignore is respected.** `DirectoryWalker` reads only the project root `.gitignore`. Nested `.gitignore` files (common in monorepos) are ignored, potentially indexing secret-containing files. `SecretDetector` provides a second layer but is not comprehensive.
