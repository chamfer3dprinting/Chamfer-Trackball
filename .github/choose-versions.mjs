import fs from 'node:fs';
import {chooseVersions} from './versions.mjs';
const repo=process.env.GITHUB_REPOSITORY;
if(!repo||!process.env.GH_TOKEN||!process.env.GITHUB_ENV)throw Error('Missing build environment');
async function pages(route){
 const all=[];
 for(let page=1;;page++){
  const r=await fetch('https://api.github.com/repos/'+repo+'/'+route+'?per_page=100&page='+page,{headers:{Authorization:'Bearer '+process.env.GH_TOKEN,Accept:'application/vnd.github+json','User-Agent':'chamfer-version-selection'}});
  if(!r.ok)throw Error('Could not read '+route+': '+r.status);
  const batch=await r.json();all.push(...batch);if(batch.length<100)return all;
 }
}
const releases=await pages('releases'),tags=await pages('tags');
const next=chooseVersions(releases,tags.map(t=>t.name));
fs.appendFileSync(process.env.GITHUB_ENV,'ADMIN_VERSION='+next.admin+'\nPUBLIC_VERSION='+next.public+'\nLEGACY_BRIDGE='+next.legacyBridge+'\n');
console.log('Admin v'+next.admin+'; unpublished public candidate v'+next.public+'. Legacy updater bridge: '+next.legacyBridge);
