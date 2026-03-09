#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const os = require("os");
const https = require("https");

const PLUGIN_NAME = "token-squeeze";
const GITHUB_REPO = "mpool/token-squeeze";

const PLATFORMS = {
  "win32-x64": { asset: "TokenSqueeze-win-x64.zip", binary: "TokenSqueeze.exe" },
  "darwin-arm64": { asset: "TokenSqueeze-osx-arm64.tar.gz", binary: "TokenSqueeze" },
  "darwin-x64": { asset: "TokenSqueeze-osx-arm64.tar.gz", binary: "TokenSqueeze" },
};

function getClaudeConfigDir() {
  if (process.env.CLAUDE_CONFIG_DIR) return process.env.CLAUDE_CONFIG_DIR;
  return path.join(os.homedir(), ".claude");
}

const MARKETPLACE_NAME = "token-squeeze-npm";
const PLUGIN_KEY = `${PLUGIN_NAME}@${MARKETPLACE_NAME}`;

function getPluginDir(version) {
  return path.join(getClaudeConfigDir(), "plugins", "cache", MARKETPLACE_NAME, PLUGIN_NAME, version);
}

function getMarketplaceDir() {
  return path.join(getClaudeConfigDir(), "plugins", "marketplaces", MARKETPLACE_NAME);
}

function getPluginVersion(pluginSrc) {
  const manifestPath = path.join(pluginSrc, ".claude-plugin", "plugin.json");
  if (fs.existsSync(manifestPath)) {
    try {
      const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf-8"));
      return manifest.version || "1.0.0";
    } catch { /* fallback */ }
  }
  return "1.0.0";
}

function getPlatformKey() {
  return `${process.platform}-${process.arch}`;
}

function copyDirSync(src, dest) {
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirSync(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}

function followRedirects(url, callback) {
  https.get(url, { headers: { "User-Agent": "token-squeeze-installer" } }, (res) => {
    if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
      followRedirects(res.headers.location, callback);
    } else {
      callback(res);
    }
  });
}

function downloadFile(url, destPath) {
  return new Promise((resolve, reject) => {
    followRedirects(url, (res) => {
      if (res.statusCode !== 200) {
        reject(new Error(`Download failed: HTTP ${res.statusCode}`));
        return;
      }
      const file = fs.createWriteStream(destPath);
      res.pipe(file);
      file.on("finish", () => { file.close(); resolve(); });
      file.on("error", reject);
    });
  });
}

function getLatestReleaseUrl(assetName) {
  return new Promise((resolve, reject) => {
    const url = `https://api.github.com/repos/${GITHUB_REPO}/releases/latest`;
    https.get(url, { headers: { "User-Agent": "token-squeeze-installer" } }, (res) => {
      let data = "";
      res.on("data", (chunk) => { data += chunk; });
      res.on("end", () => {
        if (res.statusCode !== 200) {
          reject(new Error(`GitHub API error: HTTP ${res.statusCode}. Have you created a release?`));
          return;
        }
        try {
          const release = JSON.parse(data);
          const asset = release.assets.find((a) => a.name === assetName);
          if (!asset) {
            reject(new Error(
              `Asset "${assetName}" not found in release ${release.tag_name}.\n` +
              `Available assets: ${release.assets.map((a) => a.name).join(", ") || "(none)"}`
            ));
            return;
          }
          resolve({ url: asset.browser_download_url, version: release.tag_name });
        } catch (e) {
          reject(new Error(`Failed to parse GitHub response: ${e.message}`));
        }
      });
    }).on("error", reject);
  });
}

async function extractArchive(archivePath, destDir, platform) {
  const { execSync } = require("child_process");
  fs.mkdirSync(destDir, { recursive: true });

  if (platform === "win32") {
    // Use PowerShell to extract zip
    execSync(
      `powershell -Command "Expand-Archive -Path '${archivePath}' -DestinationPath '${destDir}' -Force"`,
      { stdio: "pipe" }
    );
  } else {
    execSync(`tar -xzf "${archivePath}" -C "${destDir}"`, { stdio: "pipe" });
  }
}

function createMarketplace(pluginVersion) {
  const marketplaceDir = getMarketplaceDir();
  const manifestDir = path.join(marketplaceDir, ".claude-plugin");
  fs.mkdirSync(manifestDir, { recursive: true });

  const marketplace = {
    "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
    name: MARKETPLACE_NAME,
    metadata: {
      description: "TokenSqueeze plugin marketplace (installed via npm)",
      version: "1.0.0",
    },
    owner: {
      name: "Max Pool",
    },
    plugins: [
      {
        name: PLUGIN_NAME,
        source: `./plugins/${PLUGIN_NAME}`,
        description: "Codebase indexing and symbol retrieval for token optimization",
        version: pluginVersion,
        author: { name: "Max Pool" },
        category: "development",
      },
    ],
  };

  fs.writeFileSync(
    path.join(manifestDir, "marketplace.json"),
    JSON.stringify(marketplace, null, 2)
  );

  // Create the plugins/<name> dir as a symlink/copy pointing to the cache
  // The marketplace needs the plugin source at the relative path declared above
  const marketplacePluginDir = path.join(marketplaceDir, "plugins", PLUGIN_NAME);
  fs.mkdirSync(path.join(marketplaceDir, "plugins"), { recursive: true });

  // Remove stale link/dir if present
  if (fs.existsSync(marketplacePluginDir)) {
    fs.rmSync(marketplacePluginDir, { recursive: true });
  }

  // Copy .claude-plugin into marketplace plugin dir so it's discoverable
  const src = path.join(marketplaceDir, "..", "..", "cache", MARKETPLACE_NAME, PLUGIN_NAME);
  // We'll create a minimal stub — just needs .claude-plugin/plugin.json
  const stubManifestDir = path.join(marketplacePluginDir, ".claude-plugin");
  fs.mkdirSync(stubManifestDir, { recursive: true });
  fs.writeFileSync(
    path.join(stubManifestDir, "plugin.json"),
    JSON.stringify({
      name: PLUGIN_NAME,
      version: pluginVersion,
      description: "Codebase indexing and symbol retrieval plugin for Claude Code token optimization",
      author: { name: "Max Pool" },
    }, null, 2)
  );

  // Register in known_marketplaces.json so Claude Code recognizes it
  const knownPath = path.join(getClaudeConfigDir(), "plugins", "known_marketplaces.json");
  let known = {};
  if (fs.existsSync(knownPath)) {
    try {
      known = JSON.parse(fs.readFileSync(knownPath, "utf-8"));
    } catch { /* start fresh */ }
  }

  known[MARKETPLACE_NAME] = {
    source: {
      source: "directory",
      path: marketplaceDir,
    },
    installLocation: marketplaceDir,
    lastUpdated: new Date().toISOString(),
  };

  fs.writeFileSync(knownPath, JSON.stringify(known, null, 2));
  console.log("Local marketplace created.");
}

function registerPlugin(pluginDir, pluginVersion) {
  const configDir = getClaudeConfigDir();

  // 1. Register in installed_plugins.json
  const registryPath = path.join(configDir, "plugins", "installed_plugins.json");
  let registry = { version: 2, plugins: {} };
  if (fs.existsSync(registryPath)) {
    try {
      registry = JSON.parse(fs.readFileSync(registryPath, "utf-8"));
    } catch {
      registry = { version: 2, plugins: {} };
    }
  }

  // Remove legacy key if present
  delete registry.plugins[`${PLUGIN_NAME}@npm`];

  const now = new Date().toISOString();
  registry.plugins[PLUGIN_KEY] = [
    {
      scope: "user",
      installPath: pluginDir,
      version: pluginVersion,
      installedAt: now,
      lastUpdated: now,
    },
  ];

  fs.writeFileSync(registryPath, JSON.stringify(registry, null, 2));

  // 2. Enable in settings.json
  const settingsPath = path.join(configDir, "settings.json");
  let settings = {};
  if (fs.existsSync(settingsPath)) {
    try {
      settings = JSON.parse(fs.readFileSync(settingsPath, "utf-8"));
    } catch { /* start fresh */ }
  }

  if (!settings.enabledPlugins) {
    settings.enabledPlugins = {};
  }
  // Remove legacy key if present
  delete settings.enabledPlugins[`${PLUGIN_NAME}@npm`];
  settings.enabledPlugins[PLUGIN_KEY] = true;

  fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2));
  console.log("Plugin registered and enabled in Claude Code.");
}

async function install() {
  const platformKey = getPlatformKey();
  const platformInfo = PLATFORMS[platformKey];

  if (!platformInfo) {
    console.error(`Unsupported platform: ${platformKey}`);
    console.error(`Supported: ${Object.keys(PLATFORMS).join(", ")}`);
    process.exit(1);
  }

  // Find the plugin source directory (bundled in the npm package)
  const packageRoot = path.resolve(__dirname, "..");
  const pluginSrc = path.join(packageRoot, "plugin");
  const pluginVersion = getPluginVersion(pluginSrc);
  const pluginDir = getPluginDir(pluginVersion);
  console.log(`Installing ${PLUGIN_NAME} v${pluginVersion} to ${pluginDir}`);

  // Clean previous installs (current version, legacy flat dir, old "npm" cache)
  const configDir = getClaudeConfigDir();
  const legacyDirs = [
    pluginDir,
    path.join(configDir, "plugins", PLUGIN_NAME),
    path.join(configDir, "plugins", "cache", "npm", PLUGIN_NAME),
  ];
  for (const dir of legacyDirs) {
    if (fs.existsSync(dir)) {
      fs.rmSync(dir, { recursive: true });
    }
  }

  // Copy plugin files (skills, hooks, scripts, settings, .claude-plugin)
  const dirs = [".claude-plugin", "skills", "hooks", "scripts"];
  for (const dir of dirs) {
    const src = path.join(pluginSrc, dir);
    if (fs.existsSync(src)) {
      copyDirSync(src, path.join(pluginDir, dir));
    }
  }

  // Copy root-level plugin files
  const rootFiles = ["settings.json", "mcp-server.js", ".mcp.json"];
  for (const file of rootFiles) {
    const src = path.join(pluginSrc, file);
    if (fs.existsSync(src)) {
      fs.copyFileSync(src, path.join(pluginDir, file));
    }
  }

  console.log("Plugin files copied.");

  // Download platform binary from GitHub releases
  console.log(`Downloading ${platformInfo.asset} from latest release...`);

  try {
    const { url, version } = await getLatestReleaseUrl(platformInfo.asset);
    console.log(`Found release ${version}`);

    const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "token-squeeze-"));
    const archivePath = path.join(tmpDir, platformInfo.asset);

    await downloadFile(url, archivePath);
    console.log("Download complete. Extracting...");

    const binDir = path.join(pluginDir, "bin", platformKey === "win32-x64" ? "win-x64" : "osx-arm64");
    await extractArchive(archivePath, binDir, process.platform);

    // Set executable permission on non-Windows
    if (process.platform !== "win32") {
      const binaryPath = path.join(binDir, platformInfo.binary);
      fs.chmodSync(binaryPath, 0o755);
    }

    // Cleanup temp
    fs.rmSync(tmpDir, { recursive: true });
    console.log("Binary installed.");
  } catch (err) {
    console.error(`\nFailed to download binary: ${err.message}`);
    console.error("\nThe plugin files have been installed, but you need the binary.");
    console.error("You can build it manually with the .NET 9 SDK:");
    console.error("  git clone https://github.com/mpool/token-squeeze.git");
    console.error("  cd token-squeeze && ./plugin/build.sh");
    console.error(`  cp plugin/bin/<platform>/* ${path.join(pluginDir, "bin", "<platform>")}/`);
  }

  // Create local marketplace and register plugin
  createMarketplace(pluginVersion);
  registerPlugin(pluginDir, pluginVersion);

  console.log(`\n${PLUGIN_NAME} installed successfully.`);
  console.log("Restart Claude Code to activate the plugin.");
  console.log("\nMCP tools (available automatically):");
  console.log("  token_squeeze_list      - List indexed projects");
  console.log("  token_squeeze_outline   - Show symbols in a file");
  console.log("  token_squeeze_extract   - Get full source of a symbol");
  console.log("  token_squeeze_find      - Search symbols");
  console.log("\nSkills (slash commands):");
  console.log("  /token-squeeze:index    - Index a directory");
  console.log("  /token-squeeze:savings  - Estimate token savings");
  console.log("  /token-squeeze:purge    - Remove an index");
}

install().catch((err) => {
  console.error(`Installation failed: ${err.message}`);
  process.exit(1);
});
