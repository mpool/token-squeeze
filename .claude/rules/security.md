## Security Rules

When modifying code that handles project names, storage paths, or file deletion:

- **Project names are sanitized (fixed).** `ProjectIndexer.ResolveProjectName` runs `SanitizeName` (strips all non-alphanumeric except `-_. `) and `IndexStore` methods call `PathValidator.ValidateWithinRoot` as a second layer. Both mitigations are in place — maintain them when adding new entry points.

- **PurgeCommand deletion is validated (fixed).** `IndexStore.Delete()` calls `PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir)` before `Directory.Delete`. Safe as-is.

- **`SaveFileFragment` and `RebuildSearchIndex` now validate (fixed).** Both call `ValidateWithinRoot` internally. Maintain this when modifying these methods.

- **Nested .gitignore support is implemented (fixed).** `DirectoryWalker` uses a `gitignoreStack` that picks up `.gitignore` files in every directory during recursion. `SecretDetector` provides a second layer but is filename-only — it never scans file contents.

- **Symlink check ordering.** `DirectoryWalker` reads file bytes before checking `PathValidator.IsSymlinkEscape`. Bytes are discarded on failure (no persistence), but the read still occurs. Move the check before `File.ReadAllBytes` if modifying this area.
