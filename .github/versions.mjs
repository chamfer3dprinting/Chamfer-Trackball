const versionPattern=/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
export function parseVersion(value){
 const match=versionPattern.exec(value);if(!match)return null;
 const parts=match.slice(1).map(Number);return parts.every(n=>Number.isSafeInteger(n)&&n<=65534)?parts:null;
}
export function compareVersions(a,b){for(let i=0;i<3;i++){if(a[i]!==b[i])return a[i]-b[i];}return 0;}
function nextPatch(parts){const result=[...parts];result[2]++;if(result[2]>65534)throw Error('Choose a new minor version before exceeding the assembly patch range');return result.join('.');}
export function chooseVersions(releases,tags=[]){
 let admin=[1,5,6],publicVersion=[1,0,1],hasPublic=false,hasSemanticAdmin=false;
 // Include reserved tags so an existing executable/tag is never overwritten,
 // even after a partial upload or a withdrawn release.
 const names=[...tags,...releases.map(r=>r.tag_name)];
 for(const tag of names){
  const match=/^(?:admin-v|v)(\d+\.\d+\.\d+)(?:-build\.\d+)?$/.exec(tag);
  const parsed=match&&parseVersion(match[1]);if(parsed&&compareVersions(parsed,admin)>0)admin=parsed;
  if(/^admin-v\d+\.\d+\.\d+$/.test(tag))hasSemanticAdmin=true;
 }
 // A withdrawn or reserved public tag still owns its original binary/version.
 for(const tag of names){
  const match=/^public-v(\d+\.\d+\.\d+)$/.exec(tag),parsed=match&&parseVersion(match[1]);
  if(parsed&&compareVersions(parsed,publicVersion)>=0){publicVersion=parsed;hasPublic=true;}
 }
 return {admin:nextPatch(admin),public:hasPublic?nextPatch(publicVersion):'1.0.1',legacyBridge:!hasSemanticAdmin||releases.some(r=>/^admin-v\d+\.\d+\.\d+-build\.\d+$/.test(r.tag_name))};
}
