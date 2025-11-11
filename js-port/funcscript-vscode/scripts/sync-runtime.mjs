import { execSync } from 'node:child_process';
import { mkdirSync, copyFileSync, unlinkSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const extensionRoot = path.resolve(__dirname, '..');
const runtimeSource = path.resolve(extensionRoot, '../funcscript-js');
const runtimeDir = path.join(extensionRoot, 'runtime');
const targetName = 'funcscript-runtime.tgz';

function run(command, cwd) {
  return execSync(command, {
    cwd,
    stdio: ['ignore', 'pipe', 'inherit']
  }).toString().trim();
}

mkdirSync(runtimeDir, { recursive: true });
const packOutput = run('npm pack --silent', runtimeSource).split('\n').pop();
const packedPath = path.join(runtimeSource, packOutput);
const targetPath = path.join(runtimeDir, targetName);
copyFileSync(packedPath, targetPath);
unlinkSync(packedPath);
console.log(`Synced FuncScript runtime -> runtime/${targetName}`);
if (!existsSync(targetPath)) {
  throw new Error('Failed to copy runtime tarball.');
}
