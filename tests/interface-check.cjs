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

for(const key of ['zoomKeys','circleZoom','circleSize','data-preset="ballnav"'])assert.ok(html.includes(key));
assert.ok(!html.includes('Orbit → pan → zoom'));
(async()=>{
 let cfg={combos:[]},firstResolve,sent=[],flashSaved=()=>{},toast=()=>{},renderAll=()=>{},saveTimer=null;
 const call=async(name,value)=>{if(name==='ctState')return {config:cfg};sent.push(structuredClone(value));if(sent.length===1)await new Promise(r=>firstResolve=r);return value;};
 const run=new Function('call','flashSaved','toast','renderAll','get','set',`
 let cfg=get(),saveTimer=null;let savePending=false,saveFlight=null,editVersion=0;
 ${html.slice(html.indexOf('function save(now)'),html.indexOf('function flashSaved',html.indexOf('function save(now)')))}
 return {save:doSave,edit(v){cfg=v;editVersion++;savePending=true},get(){return cfg}};
 `);
 const state=run(call,flashSaved,toast,renderAll,()=>cfg,v=>cfg=v);
 const first=state.save();state.edit({combos:[{buttons:[1,2],bind:{action:'keys',keys:'Alt+Q'}}]});
 const second=state.save();firstResolve();await Promise.all([first,second]);
 assert.equal(sent.length,2);assert.equal(sent[1].combos[0].bind.keys,'Alt+Q');
 assert.equal(state.get().combos[0].buttons.length,2);
 console.log('Rapid combination edits survive an earlier in-flight save.');
})().catch(e=>{console.error(e);process.exitCode=1;});

assert.ok(!html.includes('" · build "+S.build'));
assert.ok(html.includes('$("#sideVer").textContent="v"+S.version'));
const updateStart=html.indexOf('$("#updBtn").onclick='),updateEnd=html.indexOf('/* ---------- admin',updateStart);
assert.ok(updateStart>0&&updateEnd>updateStart);
(async()=>{
 const calls=[],button={dataset:{stage:'available',latest:'1.5.7'}};
 const context={$:()=>button,S:{version:'1.5.5'},doSave:async()=>calls.push('save'),call:async n=>calls.push(n),toast:()=>{},setTimeout:()=>{},refreshUpd:()=>{},goTo:()=>{},modal:()=>{throw Error('Update click must install directly')}};
 vm.runInNewContext(html.slice(updateStart,updateEnd),context);
 await button.onclick();assert.deepEqual(calls,['save','ctUpdInstall']);assert.equal(button.disabled,true);
 calls.length=0;button.dataset.stage='latest';await button.onclick();assert.deepEqual(calls,['ctUpdCheck']);
 console.log('One Update click saves settings and invokes installation; Check again only checks.');
})().catch(e=>{console.error(e);process.exitCode=1;});

assert.ok(html.includes('id="publicVersion"'));assert.ok(html.includes('id="releasePublic"'));
assert.ok(html.includes('adminRequestVersion'));
console.log('Admin has a persistent custom version field and Release for public control.');

const customStart=html.indexOf('$("#releasePublic").onclick='),customEnd=html.indexOf('$("#adRefresh").onclick=',customStart);
(async()=>{
 const field={value:'v9.8.7'},button={},calls=[];
 const context={$:s=>s==='#publicVersion'?field:button,call:async(n,v)=>{calls.push([n,v]);return {connected:true}},modal:async()=>true,toast:()=>{},refreshAdmin:async()=>{}};
 vm.runInNewContext(html.slice(customStart,customEnd),context);await button.onclick();
 assert.ok(calls.some(([n,v])=>n==='adminRequestVersion'&&v==='9.8.7'));
 field.value='1.2';calls.length=0;await button.onclick();assert.equal(calls.length,0);
 console.log('Chosen release version reaches the Admin bridge and invalid versions are rejected.');
})().catch(e=>{console.error(e);process.exitCode=1;});
