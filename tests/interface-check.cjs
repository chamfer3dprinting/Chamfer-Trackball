const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync('src/Chamfer.Trackball.App/ui.html','utf8');
const scripts=[...html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)];
assert.ok(scripts.length>0);for(const [i,s]of scripts.entries())new vm.Script(s[1],{filename:'ui-'+i+'.js'});
for(const key of ['snapKeys','navigateKeys','panKeys','orbitKeys'])assert.ok(html.includes(key));
assert.ok(html.includes('data-install-integration="blender"'));
assert.ok(html.includes('data-install-integration="fusion"'));
assert.ok(!html.includes('cfg.orbitKeys="ctrl"'));
console.log('Interface scripts parse and include all requested held-key controls.');

const pollAt=html.indexOf('const ACTIVE_STATUS_POLL_MS=50;');
assert.ok(pollAt>=0,'active status polling must be capped at 20 Hz');
const pollEnd=html.indexOf('/* ---------- live ball picture ---------- */',pollAt);
const pollSource=html.slice(pollAt,pollEnd);
assert.ok(!pollSource.includes('requestAnimationFrame(poll)'),'status polling must not run on every display frame');
assert.ok(pollSource.includes('page==="home"||page==="controls"?ACTIVE_STATUS_POLL_MS:250'));
const controller=fs.readFileSync('src/Chamfer.Trackball.App/Controller.cs','utf8');
assert.ok(controller.includes('System.Windows.Forms.Timer timer=new(){Interval=16}'),'native sensor processing must retain its 16 ms timer');
console.log('Maximized UI polling is capped at 20 Hz; native sensor processing remains at 16 ms.');

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

const inputDriver=fs.readFileSync('src/Chamfer.Trackball.App/InputDriver.cs','utf8');
const hookPump=fs.readFileSync('src/Chamfer.Trackball.App/HookPump.cs','utf8');
const hookCallbacks=inputDriver.slice(inputDriver.indexOf('    nint OnKey('),inputDriver.indexOf('    protected override void WndProc'));
assert.ok(!hookCallbacks.includes('Win32.Key('),'global hooks must queue keyboard injection');
assert.ok(!hookCallbacks.includes('KeysChanged?.Invoke('),'global hooks must queue controller callbacks');
assert.ok(!hookCallbacks.includes('FailOpen('),'global hooks must queue recovery work');
assert.ok(hookPump.includes('Application.Run()')&&hookPump.includes('thread.Start()'),'global hooks need their own running message pump');
console.log('Global input hooks hand off keyboard injection, controller work and recovery outside their callbacks.');

const identificationStart=html.indexOf('document.addEventListener("click",async e=>{',html.indexOf('/* ---------- controls page'));
const identificationEnd=html.indexOf('async function devicePoll',identificationStart);
assert.ok(identificationStart>0&&identificationEnd>identificationStart);
(async()=>{
 let handler;const calls=[];
 const context={document:{addEventListener:(name,fn)=>{handler=fn}},call:async name=>calls.push(name),toast:()=>{}};
 vm.runInNewContext(html.slice(identificationStart,identificationEnd),context);
 await handler({target:{id:'homeIdentifyTrackball'}});await handler({target:{id:'homeCancelIdentify'}});
 assert.deepEqual(calls,['ctIdentify','ctCancelIdentify']);
 assert.ok(html.includes('id="homeDeviceSetup"')&&html.includes('s.deviceReady'));
 console.log('Home identification and cancellation reach the input driver and setup readiness is visible.');
})().catch(e=>{console.error(e);process.exitCode=1;});

const shortcutRecorderStart=html.indexOf('function recordShortcut(target){');
const shortcutRecorderEnd=html.indexOf('const MOD_CODES=',shortcutRecorderStart);
assert.ok(shortcutRecorderStart>0&&shortcutRecorderEnd>shortcutRecorderStart);
(async()=>{
 let accept;const messages=[],saves=[];
 const context={recording:null,cfg:{orbitKeys:'shift',panKeys:'ctrl+shift',zoomKeys:'none',snapKeys:'alt',navigateKeys:'backtick'},modal:()=>new Promise(resolve=>accept=resolve),toast:text=>messages.push(text),renderBinds:()=>{},renderCheat:()=>{},save:()=>saves.push(true),holdLabel:value=>value};
 vm.runInNewContext(html.slice(shortcutRecorderStart,shortcutRecorderEnd),context);
 context.recordShortcut('hold:zoomKeys');context.recording.combo='Ctrl';accept(true);await Promise.resolve();await Promise.resolve();
 assert.equal(context.cfg.zoomKeys,'ctrl');assert.equal(saves.length,1);
 context.recordShortcut('hold:orbitKeys');context.recording.combo='Ctrl';accept(true);await Promise.resolve();await Promise.resolve();
 assert.equal(context.cfg.orbitKeys,'shift');assert.equal(saves.length,1);assert.ok(messages.some(text=>text.includes('Zoom')));
 assert.ok(html.includes('up, down, left or right to switch to a neighboring cube face')&&html.includes('estimates knob motion')&&!html.includes('align to a face, edge or corner'));
 console.log('Ctrl can be recorded for Zoom, Shift Orbit is preserved, and snapping describes only the six faces.');
})().catch(e=>{console.error(e);process.exitCode=1;});

const helperStart=html.indexOf('const PROGRAM_FIELDS='),helperEnd=html.indexOf('function progOpts',helperStart);
assert.ok(helperStart>0&&helperEnd>helperStart);
const programConfig={programs:[{name:'3D modelling',gestures:{up:{type:'keys',keys:'F'}}}],activeProgram:0,pointerSpeed:17,snapKeys:'alt',navigateKeys:'win',orbitKeys:'shift',panKeys:'ctrl+shift',zoomKeys:'ctrl',glide:77,smooth:false,orbitSens:73,panSens:68,zoomSens:28,ball:'pointer',circleZoom:true,circleSize:60,buttons:{back:{action:'panToggle'}},modeButtons:{},combos:[{buttons:[3,4],bind:{action:'lockRotate'}}]};
const helpers=new Function('cfg','S',html.slice(helperStart,helperEnd)+';return {snapshotProgram,loadProgramFields,programDefault,fields:PROGRAM_FIELDS,feel:FEEL_FIELDS};')(programConfig,{defaults:{}});
const duplicate=helpers.snapshotProgram('Copy');
assert.equal(duplicate.pointerSpeed,17);assert.equal(duplicate.feel.glide,77);assert.equal(duplicate.feel.smooth,false);
assert.equal(duplicate.navigateKeys,'win');assert.equal(duplicate.snapKeys,'alt');assert.equal(duplicate.circleSize,60);
assert.equal(duplicate.gestures.up.keys,'F');assert.equal(duplicate.combos[0].bind.action,'lockRotate');
duplicate.buttons.back.action='normal';assert.equal(programConfig.buttons.back.action,'panToggle');
programConfig.pointerSpeed=2;programConfig.glide=1;programConfig.navigateKeys='none';helpers.loadProgramFields(duplicate);
assert.equal(programConfig.pointerSpeed,17);assert.equal(programConfig.glide,77);assert.equal(programConfig.navigateKeys,'win');
const coreStore=fs.readFileSync('src/Chamfer.Trackball.Core/ConfigStore.cs','utf8');
for(const field of helpers.fields)assert.ok(coreStore.includes('"'+field+'"'));
assert.ok(html.includes('const p=snapshotProgram();'));
assert.ok(html.includes('choice==="current"?snapshotProgram()'));
console.log('Added, duplicated and remaining programs carry full speed, glide, gestures, combinations and held controls without aliasing.');

const keyboardStart=html.indexOf('const KEY_ROWS='),keyboardEnd=html.indexOf('/* cheat sheet on Home */',keyboardStart);
assert.ok(keyboardStart>0&&keyboardEnd>keyboardStart);
const keys=[16,17,18,91].map(vk=>({dataset:{vk:String(vk)},classList:{down:false,contains(){return this.down},toggle(name,value){this.down=value}}}));
const keyboardContext={cfg:{orbitKeys:'shift',panKeys:'ctrl+shift',zoomKeys:'ctrl',snapKeys:'alt',navigateKeys:'win'},$:()=>({innerHTML:''}),$$:()=>keys,esc:s=>s,liveText:()=>{},MODEN:{Navigate:'Navigate'}};
vm.runInNewContext(html.slice(keyboardStart,keyboardEnd),keyboardContext);
keyboardContext.updateKeyboard({keys:[91,18],appState:'cad',mode:'Navigate'});
assert.deepEqual(keys.map(key=>key.classList.down),[false,false,true,true]);
keyboardContext.updateKeyboard({keys:[],appState:'cad',mode:'Idle'});assert.ok(keys.every(key=>!key.classList.down));
assert.ok(html.includes('id="liveKeyboard"'));
assert.ok(controller.includes('["keys"]=KeysState()')&&controller.includes('PointerGain'));
assert.ok(controller.includes('pointer.Move(x,y,PointerGain)')&&controller.includes('pointer.Tick('));
assert.ok(inputDriver.includes('if(k is 91 or 92||Keys.Contains(91))usedDeferred.Add(k)'));
console.log('Physical keyboard display lights held Windows/Alt and clears on release; speed reaches real pointer delivery.');

const inputConstructor=inputDriver.slice(inputDriver.indexOf('    public InputDriver(){'),inputDriver.indexOf('    bool RouteKey('));
assert.ok(!inputConstructor.includes('Win32.GetAsyncKeyState('),'startup must not treat aggregate/injected OS state as physical keyboard holds');
console.log('Keyboard starts with observed physical edges, so an inherited Backtick cannot block every held control.');

assert.ok(html.includes('.slider:has(input[data-k="pointerSpeed"]){grid-template-columns:90px 1fr 165px}'),'effective DPI value needs readable space at the normal app width');


assert.ok(html.includes('call("ctProgramDelete",cfg.activeProgram)'),'program deletion must update stored programs and preset references together');
assert.ok(html.includes('p.defaultKind=p.defaultKind||p.name;p.name=n'));
for(const id of ['savedPresetsCard','presetSelect','presetApply','presetRename','presetDelete'])assert.ok(html.includes('id="'+id+'"'));
const presetStart=html.indexOf('async function handlePreset('),presetEnd=html.indexOf('function progOpts(',presetStart);
assert.ok(presetStart>0&&presetEnd>presetStart);
(async()=>{
 const calls=[];let ask='Fine CAD';const cfg={activeProgram:0,programs:[{name:'3D modelling'}],presets:[]};
 const context={cfg,$:()=>({value:'0'}),askText:async()=>ask,modal:async()=>true,doSave:async()=>calls.push(['save']),call:async(name,...args)=>{calls.push([name,...args]);return context.cfg},renderAll:()=>{},toast:()=>{}};
 vm.runInNewContext(html.slice(presetStart,presetEnd),context);
 const button=id=>({id,hasAttribute:name=>name==='data-savepreset'&&id==='save'});
 await context.handlePreset(button('save'));assert.deepEqual(calls,[['save'],['ctPresetSave','Fine CAD',false]]);
 context.cfg.presets=[{name:'Fine CAD'}];calls.length=0;await context.handlePreset(button('save'));assert.deepEqual(calls,[['save'],['ctPresetSave','Fine CAD',true]]);
 ask='Detail view';calls.length=0;await context.handlePreset(button('presetRename'));assert.deepEqual(calls,[['save'],['ctPresetRename',0,'Detail view']]);
 calls.length=0;await context.handlePreset(button('presetApply'));assert.deepEqual(calls,[['save'],['ctPresetApply',0]]);
 calls.length=0;await context.handlePreset(button('presetDelete'));assert.deepEqual(calls,[['save'],['ctPresetDelete',0]]);
 ask=null;calls.length=0;await context.handlePreset(button('save'));assert.equal(calls.length,0);
 console.log('Save, explicit overwrite, apply, rename and delete use the preset bridge after saving current program edits; cancellation changes nothing.');
})().catch(error=>{console.error(error);process.exitCode=1;});

const speedStart=html.indexOf('function speedGain('),speedEnd=html.indexOf('function valLabel(',speedStart);
const speedContext={};vm.runInNewContext(html.slice(speedStart,speedEnd),speedContext);
assert.equal(speedContext.speedLabel(0),'0%');assert.equal(speedContext.speedLabel(1),'1.0%');assert.equal(speedContext.speedLabel(50),'100%');assert.equal(speedContext.speedLabel(100),'400%');
assert.ok(Math.abs(speedContext.speedGain(4)-.0132571137)<.000001);
for(const key of ['orbitSens','panSens','zoomSens'])assert.ok(html.includes('min="0" max="100" data-k="'+key+'"'));
assert.ok(controller.includes('MotionPolicy.Scale(output,x,y,w,OrbitGain,PanGain,ZoomGain)'));
assert.ok(controller.includes('MotionPolicy.SnapGain(')&&controller.includes('server.Move(native,output,x,y,w,input.Keys.Contains(18),nativeGain)'));
assert.ok(html.includes('id="activeSpeeds"'));
console.log('3D slider labels span 0 to 400 percent and use the same widened curve as actual motion; snap gestures keep raw movement.');

const faceFeedbackStart=html.indexOf('function updateFaceTurn('),faceFeedbackEnd=html.indexOf('/* cheat sheet on Home */',faceFeedbackStart);
assert.ok(faceFeedbackStart>0&&faceFeedbackEnd>faceFeedbackStart);
const faceMessages={},faceContext={cfg:{snapKeys:'alt'},holdLabel:key=>key,liveText:(selector,text)=>faceMessages[selector]=text};
vm.runInNewContext(html.slice(faceFeedbackStart,faceFeedbackEnd),faceContext);
for(const message of ['Roll up/down/left/right to switch faces','Clockwise 90°','Counterclockwise 90°','Face turns need a native 3D connection.']){
 faceContext.updateFaceTurn({snapTurn:{message}});
 assert.equal(faceMessages['#homeFaceTurn'],'Face orientation: '+message);
 assert.equal(faceMessages['#controlFaceTurn'],faceMessages['#homeFaceTurn']);
}
faceContext.cfg.snapKeys='shift+q';faceContext.updateFaceTurn({});
assert.ok(faceMessages['#homeFaceTurn'].includes('shift+q'));
faceContext.cfg.snapKeys='none';faceContext.updateFaceTurn({});
assert.ok(faceMessages['#homeFaceTurn'].includes('Assign Snap keys'));
console.log('Face orientation feedback shows the live curve/turn state and the actual saved shortcut.');

for(const key of ['twistKeys','twistSens','twistAxis','invertTwist'])assert.ok(html.includes(key));
const knobFeedback={};const knobContext={cfg:{snapKeys:'alt'},holdLabel:key=>key,liveText:(selector,text)=>knobFeedback[selector]=text};
vm.runInNewContext(html.slice(faceFeedbackStart,faceFeedbackEnd),knobContext);
knobContext.updateFaceTurn({knobActive:true,knobDegrees:30});
assert.equal(knobFeedback['#homeFaceTurn'],'Twist: Clockwise 30° · next step 0%');
knobContext.updateFaceTurn({knobActive:true,knobDegrees:-90});
assert.equal(knobFeedback['#homeFaceTurn'],'Twist: Anticlockwise 90° · next step 0%');
const rotationStart=html.indexOf('function mul('),rotationEnd=html.indexOf('function padReset(',rotationStart);
const rotationContext={pad:{rot:[1,0,0,0,1,0,0,0,1]},Math};
vm.runInNewContext(html.slice(rotationStart,rotationEnd),rotationContext);
rotationContext.rotRoll(-Math.PI/2);
assert.ok(Math.abs(rotationContext.pad.rot[0])<.00001&&rotationContext.pad.rot[1]>.9999&&rotationContext.pad.rot[8]===1);
assert.ok(controller.includes('KnobTwist.CameraDelta(')&&controller.includes('float TwistGain=>'));
assert.ok(controller.includes('!knobHoldActive&&ConfigStore.B(cfg,"smooth",true)'));
console.log('Customizable held Twist, independent speed/direction, immediate stop, live feedback and the orientation preview are wired.');

assert.ok(controller.includes('twistSteps=knobSteps.Add(delta)'));
assert.ok(html.includes('neighboring cube face')&&html.includes('90° at a time'));
assert.ok(!html.includes('Curl the ball path into a clockwise'));
knobContext.updateFaceTurn({knobActive:true,knobDegrees:0,knobProgress:46});
assert.equal(knobFeedback['#homeFaceTurn'],'Twist: turn the ball for a 90° step · next step 46%');
console.log('Alt only selects neighboring faces; held Twist reports 90-degree steps and progress.');

assert.ok(html.includes('data-k="altTwistEstimate"')&&html.includes("Uncertain motion selects faces"));
console.log("Experimental Alt-only estimation is configurable independently from the optional dedicated Twist hold.");

assert.ok(controller.includes('["snapLabel"]=snap==null?"":SnapLabels.For('));
assert.ok(html.includes('s.snapLabel||MODEN[s.mode]'));
console.log('Home and the floating guide receive a distinct prominent label for face snapping versus estimated twist.');
