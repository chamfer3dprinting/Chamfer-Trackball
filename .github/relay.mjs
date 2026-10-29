// Release relay: the owner's Admin app posts a signed note ("release 1.2.7" or
// "withdraw 1.2.7") to a ntfy.sh topic. This script runs every few minutes, keeps only
// notes signed by the Admin app's key, and makes the public releases match.
// Anyone can read the notes; only the Admin app can sign them.
import crypto from 'node:crypto';

const TOPIC = 'chamfer-trackball-rel-a09b0844bad13fced4806a0d';
const PUBLIC_KEY = 'c62d09050bb5c67e42ebb0dd5de29e9fc9e3ee5c1305b9d7cd5a7d7a0a03168c'; // Ed25519, hex
const REPO = process.env.GITHUB_REPOSITORY;
const TOKEN = process.env.GH_TOKEN;
const USER_ASSET = 'ChamferTrackball.exe';

const key = crypto.createPublicKey({ key: { kty: 'OKP', crv: 'Ed25519', x: Buffer.from(PUBLIC_KEY, 'hex').toString('base64url') }, format: 'jwk' });

async function gh(method, path, body, raw) {
  const r = await fetch(path.startsWith('http') ? path : `https://api.github.com/repos/${REPO}${path}`, {
    method, headers: { Authorization: `Bearer ${TOKEN}`, Accept: 'application/vnd.github+json', 'User-Agent': 'chamfer-relay', ...(raw ? { 'Content-Type': 'application/octet-stream' } : {}) },
    body: raw ? body : body ? JSON.stringify(body) : undefined,
  });
  if (r.status === 404) return null;
  if (!r.ok) throw new Error(`${method} ${path}: ${r.status} ${await r.text()}`);
  return r.status === 204 ? {} : r.json();
}

const res = await fetch(`https://ntfy.sh/${TOPIC}/json?poll=1&since=12h`);
if (!res.ok) throw new Error('ntfy ' + res.status);
const notes = [];
for (const line of (await res.text()).split('\n')) {
  if (!line.trim()) continue;
  let m; try { m = JSON.parse(line); } catch { continue; }
  if (m.event !== 'message' || typeof m.message !== 'string') continue;
  const [p, s] = m.message.trim().split('.');
  if (!p || !s) continue;
  const payload = Buffer.from(p, 'base64url');
  if (!crypto.verify(null, payload, key, Buffer.from(s, 'base64url'))) { console.log('ignored unsigned note', m.id); continue; }
  let n; try { n = JSON.parse(payload); } catch { continue; }
  if (!['release', 'withdraw'].includes(n.a) || !/^\d+\.\d+\.\d+$/.test(n.v) || Math.abs(n.t - m.time) > 300) { console.log('ignored note', m.id); continue; }
  notes.push(n);
}
notes.sort((a, b) => a.t - b.t);
const want = {};
for (const n of notes) want[n.v] = n.a; // the newest note per version wins
console.log('wanted:', JSON.stringify(want));

for (const [v, a] of Object.entries(want)) {
  const ptag = `public-v${v}`;
  const pub = await gh('GET', `/releases/tags/${ptag}`);
  if (a === 'withdraw') {
    if (pub) { await gh('DELETE', `/releases/${pub.id}`); await gh('DELETE', `/git/refs/tags/${ptag}`); console.log('withdrew', v); }
    continue;
  }
  if (pub && pub.assets.some(x => x.name === USER_ASSET)) continue; // already public
  const priv = await gh('GET', `/releases/tags/v${v}`);
  const asset = priv && priv.assets.find(x => x.name === USER_ASSET);
  if (!asset) { console.log('no user app in v' + v); continue; }
  const bin = Buffer.from(await (await fetch(asset.browser_download_url)).arrayBuffer());
  if (bin.subarray(0, 2).toString() !== 'MZ' || (asset.digest && 'sha256:' + crypto.createHash('sha256').update(bin).digest('hex') !== asset.digest)) throw new Error('v' + v + ' app file check failed');
  const rel = pub || await gh('POST', '/releases', { tag_name: ptag, target_commitish: 'main', name: `Chamfer Trackball v${v}`, body: priv.body || `- Chamfer Trackball v${v}`, prerelease: false, make_latest: 'true' });
  await gh('POST', `https://uploads.github.com/repos/${REPO}/releases/${rel.id}/assets?name=${USER_ASSET}`, bin, true);
  console.log('released', v);
}
