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

function getPluginDir() {
  return path.join(getClaudeConfigDir(), "plugins", PLUGIN_NAME);
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

function registerPlugin(pluginDir) {
  const registryPath = path.join(getClaudeConfigDir(), "plugins", "installed_plugins.json");
  const pluginKey = `${PLUGIN_NAME}@npm`;

  let registry = { version: 2, plugins: {} };
  if (fs.existsSync(registryPath)) {
    try {
      registry = JSON.parse(fs.readFileSync(registryPath, "utf-8"));
    } catch {
      // Corrupted file — start fresh but preserve version
      registry = { version: 2, plugins: {} };
    }
  }

  // Read version from the plugin manifest we just copied
  let pluginVersion = "1.0.0";
  const manifestPath = path.join(pluginDir, ".claude-plugin", "plugin.json");
  if (fs.existsSync(manifestPath)) {
    try {
      const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf-8"));
      pluginVersion = manifest.version || pluginVersion;
    } catch { /* use default */ }
  }

  const now = new Date().toISOString();
  registry.plugins[pluginKey] = [
    {
      scope: "user",
      installPath: pluginDir,
      version: pluginVersion,
      installedAt: now,
      lastUpdated: now,
    },
  ];

  fs.writeFileSync(registryPath, JSON.stringify(registry, null, 2));
  console.log("Plugin registered in Claude Code.");
}

async function install() {
  const platformKey = getPlatformKey();
  const platformInfo = PLATFORMS[platformKey];

  if (!platformInfo) {
    console.error(`Unsupported platform: ${platformKey}`);
    console.error(`Supported: ${Object.keys(PLATFORMS).join(", ")}`);
    process.exit(1);
  }

  const pluginDir = getPluginDir();
  console.log(`Installing ${PLUGIN_NAME} to ${pluginDir}`);

  // Find the plugin source directory (bundled in the npm package)
  const packageRoot = path.resolve(__dirname, "..");
  const pluginSrc = path.join(packageRoot, "plugin");

  // Clean previous install
  if (fs.existsSync(pluginDir)) {
    fs.rmSync(pluginDir, { recursive: true });
  }

  // Copy plugin files (skills, hooks, scripts, settings, .claude-plugin)
  const dirs = [".claude-plugin", "skills", "hooks", "scripts"];
  for (const dir of dirs) {
    const src = path.join(pluginSrc, dir);
    if (fs.existsSync(src)) {
      copyDirSync(src, path.join(pluginDir, dir));
    }
  }

  // Copy settings.json
  const settingsSrc = path.join(pluginSrc, "settings.json");
  if (fs.existsSync(settingsSrc)) {
    fs.copyFileSync(settingsSrc, path.join(pluginDir, "settings.json"));
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

  // Register plugin in Claude Code's installed_plugins.json
  registerPlugin(pluginDir);

  console.log(`\n${PLUGIN_NAME} installed successfully.`);
  console.log("Restart Claude Code to activate the plugin.");
  console.log("\nAvailable skills:");
  console.log("  /token-squeeze:index    - Index a directory");
  console.log("  /token-squeeze:outline  - Show symbols in a file");
  console.log("  /token-squeeze:extract  - Get full source of a symbol");
  console.log("  /token-squeeze:find     - Search symbols");
  console.log("  /token-squeeze:list     - List indexed projects");
  console.log("  /token-squeeze:purge    - Remove an index");
}

install().catch((err) => {
  console.error(`Installation failed: ${err.message}`);
  process.exit(1);
});
