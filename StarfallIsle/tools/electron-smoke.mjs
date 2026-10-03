// Electron duman testi: gercek masaustu kabugu acilir, Steam olmadan yerel
// moda duser, oyun yuklenir ve "hazir" sinyali gelir. GPU/ekran yoksa
// xvfb-run kullanir.
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { ROOT } from './lib.mjs';

const electron = path.join(ROOT, 'node_modules', '.bin', 'electron');
const args = ['.', '--smoke-test', '--no-sandbox', '--disable-dev-shm-usage'];
let cmd = electron;
let cmdArgs = args;
if (!process.env.DISPLAY && process.platform === 'linux') {
  cmd = 'xvfb-run';
  cmdArgs = ['-a', '--server-args=-screen 0 1280x720x24', electron, ...args];
}
const r = spawnSync(cmd, cmdArgs, { cwd: ROOT, encoding: 'utf8', timeout: 180000, env: { ...process.env, ELECTRON_ENABLE_LOGGING: '0' } });
const out = `${r.stdout || ''}\n${r.stderr || ''}`;
const ok = r.status === 0 && out.includes('SMOKE_OK');
const line = out.split('\n').find((l) => l.startsWith('SMOKE_OK')) || '';
const errs = out.split('\n').filter((l) => l.startsWith('RENDERER_ERROR'));
console.log(ok ? `OK  ${line}` : `FAIL cikis=${r.status}\n${out.slice(-3000)}`);
if (errs.length) console.log(errs.join('\n'));
process.exit(ok && !errs.length ? 0 : 1);
