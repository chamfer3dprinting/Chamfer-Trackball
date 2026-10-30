import fs from 'node:fs';
import {validateRequest,validateRun,validateCandidate,verifyBytes,verifyPublicAsset} from './public-release-policy.mjs';
const repo=process.env.GITHUB_REPOSITORY,token=process.env.GH_TOKEN;
if(repo!=='chamfer3dprinting/Chamfer-Trackball'||!token)throw Error('Missing authorized repository context');
const request=JSON.parse(fs.readFileSync('.github/public-release.json','utf8'));validateRequest(request);
const base='https://api.github.com/repos/'+repo;
async function api(method,path,body,allowMissing=false){
 const attempts=method==='POST'?1:4;
 for(let attempt=0;attempt<attempts;attempt++){
  const response=await fetch(base+path,{method,headers:{Authorization:'Bearer '+token,Accept:'application/vnd.github+json','User-Agent':'chamfer-public-publisher','Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
  if(allowMissing&&response.status===404)return null;
  if(response.ok)return response.status===204?null:response.json();
  if((response.status>=500||response.status===429)&&attempt+1<attempts){await new Promise(resolve=>setTimeout(resolve,1000*(attempt+1)));continue;}
  throw Error(method+' '+path+' failed: '+response.status);
 }
}
const run=await api('GET','/actions/runs/'+request.verifiedRunId);validateRun(run,request,repo);
const candidate=await api('GET','/releases/'+request.candidateReleaseId);validateCandidate(candidate,request);
const download=await fetch(base+'/releases/assets/'+request.assetId,{headers:{Authorization:'Bearer '+token,Accept:'application/octet-stream','User-Agent':'chamfer-public-publisher'}});
if(!download.ok)throw Error('Could not download verified candidate: '+download.status);
const bytes=Buffer.from(await download.arrayBuffer());verifyBytes(bytes,request);
const tag='public-v'+request.version;
// Draft releases may not resolve through /releases/tags before the tag exists.
const matches=[];
for(let page=1;page<=100;page++){
 const batch=await api('GET','/releases?per_page=100&page='+page);
 matches.push(...batch.filter(r=>r.tag_name===tag));
 if(batch.length<100)break;
 if(page===100)throw Error('Too many releases to inspect safely');
}
if(matches.length>1)throw Error('Multiple drafts use this public version; review before publishing');
let release=matches[0]||null;
if(release){
 // An existing version always owns its original bytes. Never clobber an asset.
 if(release.assets.some(a=>a.name==='ChamferTrackball.exe'))verifyPublicAsset(release,request);
 else if(!release.draft)throw Error('Existing published version has no matching executable');
 if(release.target_commitish!==request.sourceRevision)throw Error('Existing version belongs to another source revision');
}else{
 const notes=fs.readFileSync('release-notes/public-v'+request.version+'.md','utf8');
 release=await api('POST','/releases',{tag_name:tag,target_commitish:request.sourceRevision,name:'Chamfer Trackball v'+request.version,body:notes+'\n\nVerified executable SHA-256: sha256:'+request.sha256+'\nSource: '+request.sourceRevision,draft:true,prerelease:false,make_latest:'false'});
}
if(!release.assets.some(a=>a.name==='ChamferTrackball.exe')){
 const upload=await fetch('https://uploads.github.com/repos/'+repo+'/releases/'+release.id+'/assets?name=ChamferTrackball.exe',{method:'POST',headers:{Authorization:'Bearer '+token,'Content-Type':'application/octet-stream','User-Agent':'chamfer-public-publisher'},body:bytes});
 if(!upload.ok)throw Error('Could not upload public executable: '+upload.status);
 const saved=await upload.json();if(saved.digest!=='sha256:'+request.sha256)throw Error('Uploaded checksum differs; release remains a draft');
}
release=await api('GET','/releases/'+release.id);verifyPublicAsset(release,request);
if(release.draft||release.prerelease)release=await api('PATCH','/releases/'+release.id,{draft:false,prerelease:false,make_latest:'true'});
release=await api('GET','/releases/'+release.id);const asset=verifyPublicAsset(release,request);
if(release.draft||release.prerelease)throw Error('Public release was not activated');
const url='https://github.com/'+repo+'/releases/download/'+tag+'/ChamferTrackball.exe';
if(asset.browser_download_url!==url)throw Error('Unexpected public download URL');
let publicDownload;
for(let attempt=0;attempt<6;attempt++){
 publicDownload=await fetch(url);
 if(publicDownload.ok)break;
 if(attempt<5)await new Promise(resolve=>setTimeout(resolve,2000));
}
if(!publicDownload.ok)throw Error('Public download unavailable: '+publicDownload.status);
verifyBytes(Buffer.from(await publicDownload.arrayBuffer()),request);
console.log(JSON.stringify({version:request.version,public:true,release:release.html_url,download:url,sha256:request.sha256,verifiedRun:request.verifiedRunId}));
