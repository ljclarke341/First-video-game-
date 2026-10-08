// Produces the CrazyGames submission build: dist/crazygames/ and a ZIP.
//
// The only difference from the Play build is the SDK script tag injected
// into index.html. Keeping it out of www/ means the Capacitor build never
// carries a dependency on an external script it cannot reach.
//
//   node tools/build-crazygames.js
import { cp, mkdir, readFile, writeFile, rm, readdir, stat } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const run = promisify(execFile);
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(ROOT, 'www');
const OUT = join(ROOT, 'dist', 'crazygames');
const ZIP = join(ROOT, 'dist', 'chroma-rush-crazygames.zip');

// The flag marks the build target explicitly. Without it, an SDK that fails
// to load (adblock, CDN hiccup) would make the game fall back to the AdMob
// backend and show its dev ad placeholder to real players.
const SDK_TAG =
  '<script>window.__CG_BUILD = true;</script>\n'
  + '<script src="https://sdk.crazygames.com/crazygames-sdk-v3.js"></script>';

async function dirSize(dir) {
  let total = 0;
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, entry.name);
    total += entry.isDirectory() ? await dirSize(p) : (await stat(p)).size;
  }
  return total;
}

await rm(join(ROOT, 'dist'), { recursive: true, force: true });
await mkdir(OUT, { recursive: true });
await cp(SRC, OUT, { recursive: true });

// Inject the SDK ahead of the game module so window.CrazyGames exists by the
// time platform.js runs its detection.
const indexPath = join(OUT, 'index.html');
let html = await readFile(indexPath, 'utf8');
if (!html.includes('crazygames-sdk')) {
  html = html.replace('<script type="module" src="js/main.js"></script>',
                      `${SDK_TAG}\n<script type="module" src="js/main.js"></script>`);
  await writeFile(indexPath, html);
}

// The manifest points at Capacitor-only concerns; the portal has no use for it.
await rm(join(OUT, 'manifest.webmanifest'), { force: true });
html = (await readFile(indexPath, 'utf8'))
  .replace(/\n\s*<link rel="manifest"[^>]*>/, '');
await writeFile(indexPath, html);

// Drop the local-testing ad placeholder. It is unreachable on this build,
// but it is markup that names another ad network and a reviewer reading the
// DOM should not find it.
html = (await readFile(indexPath, 'utf8'))
  .replace(/\n\s*<!-- Simulated ad[\s\S]*?<\/div>\s*<\/div>\n/, '\n');
await writeFile(indexPath, html);

// Replace the AdMob backend with a stub. It never runs on this build (the
// __CG_BUILD flag pins the CrazyGames backend), but shipping a file full of
// another ad network's unit IDs invites a reviewer to fail the "no external
// ads" check over dead code.
await writeFile(join(OUT, 'js', 'ads.js'), `// AdMob backend, stubbed out of the CrazyGames build.
// The portal serves every ad here; see crazygames.js.
export const initAds = async () => {};
export const showRewarded = async () => false;
export const maybeShowInterstitial = async () => {};
export const isAdFree = () => false;
`);

// Flatten js/ into the root. CrazyGames' uploader takes loose files dragged
// into a drop zone rather than an archive, so a build with no subfolders
// removes any chance of the structure arriving wrong.
const jsDir = join(OUT, 'js');
for (const name of await readdir(jsDir)) {
  await cp(join(jsDir, name), join(OUT, name));
}
await rm(jsDir, { recursive: true, force: true });

// The modules import each other with './x.js', which still resolves at the
// root; only the entry point's path in the HTML has to change.
html = (await readFile(indexPath, 'utf8')).replace('src="js/main.js"', 'src="main.js"');
await writeFile(indexPath, html);

try {
  await run('zip', ['-qr', ZIP, '.'], { cwd: OUT });
} catch {
  console.error('zip not found - the folder at dist/crazygames is still valid; '
                + 'compress it yourself with index.html at the archive root.');
}

const bytes = await dirSize(OUT);
console.log(`built  ${OUT}`);
console.log(`zip    ${ZIP}`);
console.log(`size   ${(bytes / 1024).toFixed(1)} KB unpacked`);
