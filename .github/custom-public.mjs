import fs from 'node:fs';
import {validateCustomRequest,executableHash,validatePublicRelease} from './custom-public-policy.mjs';
const repo=process.env.GITHUB_REPOSITORY,token=process.env.GH_TOKEN,mode=process.argv[2],file=process.env.CUSTOM_REQUEST_FILE;
if(repo!=='chamfer3dprinting/Chamfer-Trackball'||!token||!file)throw Error('Missing authorized build context');
const base='https://api.github.com/repos/'+repo;
async function api(method,path,body,missing=false){
 for(let attempt=0;attempt<4;attempt++){
  const response=await fetch(base+path,{method,headers:{Authorization:'Bearer '+token,Accept:'application/vnd.github+json','Content-Type':'application/json','User-Agent':'chamfer-custom-public'},body:body===undefined?undefined:JSON.stringify(body)});
  if(missing&&response.status===404)return null;
  if(response.ok)return response.status===204?null:response.json();
  if((method==='GET'||method==='PATCH')&&(response.status>=500||response.status===429)&&attempt<3){await new Promise(r=>setTimeout(r,1000*(attempt+1)));continue;}
  throw Error(method+' '+path+' failed: '+response.status);
 }
}
async function releases(){
 const all=[];for(let page=1;page<=100;page++){const batch=await api('GET','/releases?per_page=100&page='+page);all.push(...batch);if(batch.length<100)return all;}throw Error('Too many releases');
}
async function status(request,state,message,extra={}){
 if(request.action!=='release')return;
 const route='/contents/.github/custom-public-status.json';
 for(let attempt=0;attempt<3;attempt++){
  const previous=await api('GET',route+'?ref=main',undefined,true);
  const value={requestId:request.requestId,version:request.version,state,message,runUrl:'https://github.com/'+repo+'/actions/runs/'+process.env.GITHUB_RUN_ID,...extra};
  try{await api('PUT',route,{message:'Public v'+request.version+': '+state,branch:'main',...(previous?{sha:previous.sha}:{}),content:Buffer.from(JSON.stringify(value,null,2)).toString('base64')});return;}catch(error){if(attempt===2)throw error;}
 }
}
if(mode==='prepare'){
 const request=validateCustomRequest(JSON.parse(fs.readFileSync('.github/custom-public-request.json','utf8')),process.env.GITHUB_SHA);
 await api('GET','/commits/'+request.sourceRevision);
 fs.writeFileSync(file,JSON.stringify(request));
 fs.appendFileSync(process.env.GITHUB_OUTPUT,'version='+request.version+'\nsource='+request.sourceRevision+'\n');
 await status(request,'building','Building and testing public v'+request.version+'…');
 console.log('Chosen public version: v'+request.version+'. Mode: '+request.action);
}else if(mode==='finish'){
 if(fs.existsSync(file)&&process.env.JOB_RESULT!=='success'){const request=JSON.parse(fs.readFileSync(file,'utf8'));await status(request,'error','The public build or verification failed. Open GitHub Actions for details, then retry.');}
}else if(mode==='publish'){
 const request=validateCustomRequest(JSON.parse(fs.readFileSync(file,'utf8')),process.env.GITHUB_SHA);
 const bytes=fs.readFileSync(process.env.CUSTOM_EXE),hash=executableHash(bytes);
 if(request.action==='verify'){console.log(JSON.stringify({verifiedOnly:true,version:request.version,hash}));process.exit(0);}
 let all=await releases();let release=all.find(r=>r.tag_name==='public-v'+request.version);
 if(!release){
  const body={tag_name:'public-v'+request.version,target_commitish:request.sourceRevision,name:'Chamfer Trackball v'+request.version,body:fs.readFileSync('release-notes/public-template.md','utf8')+'\n\nVerified executable SHA-256: '+hash+'\nSource: '+request.sourceRevision,draft:true,prerelease:false,make_latest:'false'};
  try{release=await api('POST','/releases',body);}catch(error){release=(await releases()).find(r=>r.tag_name===body.tag_name);if(!release)throw error;}
 }
 if(release.target_commitish!==request.sourceRevision)throw Error('That version already belongs to another app revision. Choose another version.');
 const existing=release.assets.find(a=>a.name==='ChamferTrackball.exe');
 if(existing)validatePublicRelease(release,request,hash);
 else{
  if(!release.draft||release.assets.length)throw Error('Existing version cannot be replaced');
  const response=await fetch('https://uploads.github.com/repos/'+repo+'/releases/'+release.id+'/assets?name=ChamferTrackball.exe',{method:'POST',headers:{Authorization:'Bearer '+token,'Content-Type':'application/octet-stream','User-Agent':'chamfer-custom-public'},body:bytes});
  if(!response.ok)throw Error('Upload failed: '+response.status);
  if((await response.json()).digest!==hash)throw Error('Uploaded digest differs; release remains a draft');
 }
 release=await api('GET','/releases/'+release.id);validatePublicRelease(release,request,hash);
 if(release.draft||release.prerelease)await api('PATCH','/releases/'+release.id,{draft:false,prerelease:false,make_latest:'true'});
 release=await api('GET','/releases/'+release.id);validatePublicRelease(release,request,hash);
 if(release.draft||release.prerelease)throw Error('Public release was not activated');
 const url='https://github.com/'+repo+'/releases/download/public-v'+request.version+'/ChamferTrackball.exe';
 const download=await fetch(url);if(!download.ok||executableHash(Buffer.from(await download.arrayBuffer()))!==hash)throw Error('Public download verification failed');
 await status(request,'published','Public v'+request.version+' is released.',{url,hash});console.log(JSON.stringify({version:request.version,published:true,url,hash}));
}else throw Error('Unknown custom release step');
