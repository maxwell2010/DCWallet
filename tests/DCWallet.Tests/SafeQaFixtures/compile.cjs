const fs=require('fs'),crypto=require('crypto');
const solc=require(process.argv[2] || 'solc');
const path=__dirname+'/';
const source=fs.readFileSync(path+'SafeQa.sol','utf8');
const input={language:'Solidity',sources:{'SafeQa.sol':{content:source}},settings:{optimizer:{enabled:true,runs:200},evmVersion:'paris',outputSelection:{'*':{'*':['abi','evm.bytecode.object','evm.bytecode.sourceMap','evm.deployedBytecode.object','evm.deployedBytecode.sourceMap']}}}};
const output=JSON.parse(solc.compile(JSON.stringify(input)));
if((output.errors||[]).some(e=>e.severity==='error'))throw Error(JSON.stringify(output.errors));
const sha=v=>crypto.createHash('sha256').update(v).digest('hex');
fs.writeFileSync(path+'SafeQa.compiled.json',JSON.stringify({compiler:solc.version(),settings:input.settings,sourceSha256:sha(Buffer.from(source)),contracts:Object.fromEntries(Object.entries(output.contracts['SafeQa.sol']).map(([name,c])=>[name,{abi:c.abi,creationBytecode:'0x'+c.evm.bytecode.object,runtimeBytecode:'0x'+c.evm.deployedBytecode.object,runtimeSha256:sha(Buffer.from(c.evm.deployedBytecode.object,'hex')),creationSourceMap:c.evm.bytecode.sourceMap,runtimeSourceMap:c.evm.deployedBytecode.sourceMap}]))},null,2));
console.log('Compiler '+solc.version()+'; contracts compiled with pinned Paris EVM');

