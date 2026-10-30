import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chooseVersions,parseVersion} from '../.github/versions.mjs';
const r=(tag_name,draft=false,prerelease=true)=>({tag_name,draft,prerelease});
assert.deepEqual(chooseVersions([r('v1.5.6'),r('admin-v1.5.5-build.17')]),{admin:'1.5.7',public:'1.0.1',legacyBridge:true});
assert.deepEqual(chooseVersions([r('admin-v1.5.7'),r('admin-v1.5.7-build.18')]),{admin:'1.5.8',public:'1.0.1',legacyBridge:true});
assert.equal(chooseVersions([r('admin-v1.5.9'),r('admin-v1.5.10')]).admin,'1.5.11');
assert.equal(chooseVersions([r('public-v1.0.1',false,false)]).public,'1.0.2');
assert.equal(chooseVersions([r('public-v1.0.8',true,false),r('public-v1.0.9',false,true)]).public,'1.0.10');
assert.equal(chooseVersions([],['public-v1.0.3']).public,'1.0.4');
assert.equal(chooseVersions([r('candidate-v1.0.1-build.20',true)]).public,'1.0.1');
assert.equal(chooseVersions([],['admin-v1.6.4']).admin,'1.6.5');
assert.equal(chooseVersions([r('admin-v1.7.0',true)]).admin,'1.7.1');
assert.equal(chooseVersions([r('admin-v1.5.7')],['admin-v1.5.8-build.20']).admin,'1.5.9');
assert.equal(parseVersion('1.2.3-beta'),null);assert.equal(parseVersion('1.02.3'),null);assert.equal(parseVersion('1.2.9999999999999999999'),null);
for(const path of ['.github/choose-versions.mjs','.github/store-build.mjs']){
 const source=fs.readFileSync(path,'utf8');assert.ok(!source.includes('admin-v1.5.5-build.'));
}
console.log('Semantic version increments, legacy migration, draft/public isolation and reserved tags verified.');
