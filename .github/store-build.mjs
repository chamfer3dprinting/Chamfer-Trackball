import fs from 'node:fs';
import crypto from 'node:crypto';
const repo=process.env.GITHUB_REPOSITORY,token=process.env.GH_TOKEN,build=process.env.BUILD_NUMBER,sha=process.env.SOURCE_REVISION;
if(!/^\d+$/.test(build)||!/^\w{40}$/.test(sha))throw Error('Invalid build identity');
async function api(method,route,body,raw=false){
 const url=route.startsWith('https://')?route:'https://api.github.com/repos/'+repo+route;
 const r=await fetch(url,{method,headers:{Authorization:'Bearer '+token,Accept:'application/vnd.github+json','User-Agent':'chamfer-verified-build',...(raw?{'Content-Type':'application/octet-stream'}:{'Content-Type':'application/json'})},body:body?(raw?body:JSON.stringify(body)):undefined});
 if(!r.ok)throw Error(method+' '+route+' '+r.status+' '+await r.text());return r.status===204?null:r.json();
}
const head=await api('GET','/git/ref/heads/main');if(head.object.sha!==sha){console.log('Newer source is being verified; skip older build.');process.exit(0);}
const publicVersion=process.env.PUBLIC_VERSION;if(!/^\d+\.\d+\.\d+$/.test(publicVersion))throw Error('Invalid public candidate version');
const notes=fs.readFileSync('release-notes/admin-v1.5.5.md','utf8').replaceAll('v1.0.1','v'+publicVersion);
async function store(tag,title,path,name,draft){
 const bytes=fs.readFileSync(path),digest='sha256:'+crypto.createHash('sha256').update(bytes).digest('hex');
 if(bytes[0]!==77||bytes[1]!==90)throw Error('Missing Windows executable');
 const rel=await api('POST','/releases',{tag_name:tag,target_commitish:sha,name:title,body:notes+'\nSource: '+sha+'\nBuild: '+build,draft,prerelease:!draft,make_latest:'false'});
 const asset=await api('POST','https://uploads.github.com/repos/'+repo+'/releases/'+rel.id+'/assets?name='+encodeURIComponent(name),bytes,true);
 if(asset.digest!==digest)throw Error('GitHub digest mismatch: '+name);
 console.log(JSON.stringify({tag,draft,name,size:bytes.length,digest}));
}
// Never activate the public channel as a side effect of an Admin update.
await store('candidate-v'+publicVersion+'-build.'+build,'Unpublished public v'+publicVersion+' · build '+build,process.env.PUBLIC_EXE,'ChamferTrackball.exe',true);
await store('admin-v1.5.5-build.'+build,'Chamfer Trackball Admin v1.5.5 · build '+build,process.env.ADMIN_EXE,'ChamferTrackball-Admin.exe',false);
