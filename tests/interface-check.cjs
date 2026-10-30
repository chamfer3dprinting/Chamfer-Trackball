const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync('src/Chamfer.Trackball.App/ui.html','utf8');
const scripts=[...html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)];
assert.ok(scripts.length>0);for(const [i,s]of scripts.entries())new vm.Script(s[1],{filename:'ui-'+i+'.js'});
for(const key of ['snapKeys','navigateKeys','panKeys','orbitKeys'])assert.ok(html.includes(key));
assert.ok(html.includes('data-install-integration="blender"'));
assert.ok(html.includes('data-install-integration="fusion"'));
assert.ok(!html.includes('cfg.orbitKeys="ctrl"'));
console.log('Interface scripts parse and include all requested held-key controls.');

for(const key of ['ctIdentify','trackballSwapButtons','trackballSwapWheels','adminConnect','modePanel'])assert.ok(html.includes(key));
assert.ok(html.includes('data-k="trackballSwapWheels"'));
assert.ok(!html.includes('No sign-in needed'));
const png=Buffer.from(fs.readFileSync('src/Chamfer.Trackball.App/logo.base64','utf8'),'base64');
assert.equal(png.subarray(1,4).toString(),'PNG');assert.equal(png.readUInt32BE(16),128);
const ico=fs.readFileSync('src/Chamfer.Trackball.App/logo.ico');assert.equal(ico.readUInt16LE(2),1);assert.ok(ico.readUInt16LE(4)>4);
console.log('Device identification, release authentication and original logo resources verified.');
