import crypto from 'node:crypto';
function ensure(pass,message){if(!pass)throw Error(message);}
export function validateRequest(r){
 ensure(r.action==='publish'&&/^\d+\.\d+\.\d+$/.test(r.version),'Invalid public version/action');
 ensure(r.candidateTag.startsWith('candidate-v'+r.version+'-build.')&&/^candidate-v\d+\.\d+\.\d+-build\.\d+$/.test(r.candidateTag),'Candidate does not match public version');
 ensure(/^[a-f0-9]{40}$/.test(r.sourceRevision)&&/^[a-f0-9]{64}$/.test(r.sha256),'Missing pinned source/checksum');
 for(const key of ['candidateReleaseId','assetId','verifiedRunId'])ensure(Number.isSafeInteger(r[key])&&r[key]>0,'Invalid '+key);
}
export function validateRun(run,r,repo){
 ensure(run.repository?.full_name===repo&&run.head_sha===r.sourceRevision&&run.conclusion==='success'&&run.status==='completed'
  &&run.path==='.github/workflows/build-source.yml'&&run.event==='push'&&run.head_branch==='main','Candidate source has no matching successful Windows verification');
}
export function validateCandidate(candidate,r){
 ensure(candidate.id===r.candidateReleaseId&&candidate.tag_name===r.candidateTag&&candidate.target_commitish===r.sourceRevision,'Candidate identity changed');
 const asset=candidate.assets.find(a=>a.id===r.assetId&&a.name==='ChamferTrackball.exe');
 ensure(asset?.digest==='sha256:'+r.sha256,'Candidate asset checksum changed');return asset;
}
export function verifyBytes(bytes,r){
 ensure(bytes.length>2&&bytes[0]===77&&bytes[1]===90,'Not a Windows executable');
 ensure(crypto.createHash('sha256').update(bytes).digest('hex')===r.sha256,'Executable checksum does not match the verified candidate');
}
export function verifyPublicAsset(release,r){
 ensure(release.tag_name==='public-v'+r.version&&release.target_commitish===r.sourceRevision,'Public release version/source differs');
 ensure(release.assets.length===1,'Public release must contain only its public executable');
 const asset=release.assets.find(a=>a.name==='ChamferTrackball.exe');
 ensure(asset?.digest==='sha256:'+r.sha256,'Public release executable differs');return asset;
}
