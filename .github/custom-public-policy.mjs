import crypto from 'node:crypto';
export function normalizeVersion(raw){
 const value=String(raw).trim().replace(/^v/i,'');
 if(!/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(value)||value.split('.').some(n=>Number(n)>65534))throw Error('Choose a version like 1.2.3.');
 return value;
}
export function validateCustomRequest(request,currentSource){
 const version=normalizeVersion(request.version);
 if(!['release','verify'].includes(request.action)||!/^[a-f0-9]{32}$/.test(request.requestId))throw Error('Invalid release request');
 const source=request.action==='verify'&&request.sourceRevision==='current'?currentSource:request.sourceRevision;
 if(!/^[a-f0-9]{40}$/.test(source))throw Error('Choose a verified source revision');
 return {...request,version,sourceRevision:source};
}
export function executableHash(bytes){
 if(bytes.length<2||bytes[0]!==77||bytes[1]!==90)throw Error('Not a Windows executable');
 return 'sha256:'+crypto.createHash('sha256').update(bytes).digest('hex');
}
export function validatePublicRelease(release,request,hash){
 if(release.tag_name!=='public-v'+request.version||release.target_commitish!==request.sourceRevision||release.assets.length!==1||release.assets[0].name!=='ChamferTrackball.exe'||release.assets[0].digest!==hash)throw Error('Existing version contains another executable; never overwrite it');
}
