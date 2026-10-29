'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const {Live}=require('../../Proyecto_Final/wwwroot/js/stage2/chat-v2-live.js');
function fixture({start=async()=>{}}={}){
 const calls=[],handlers={},statuses=[];let invalidations=0;
 const connection={state:'Connected',on:(n,f)=>handlers[n]=f,onreconnecting:f=>handlers.reconnecting=f,
  onreconnected:f=>handlers.reconnected=f,onclose:f=>handlers.close=f,start,
  invoke:async(...args)=>calls.push(args),stop:async()=>calls.push(['stop'])};
 const live=new Live(()=>connection,s=>statuses.push(s),()=>invalidations++);
 return {live,calls,handlers,statuses,get invalidations(){return invalidations;}};
}
test('only a valid P selector is sent, never identity, message or authentication material',async()=>{
 const f=fixture();await f.live.select('P',4);await f.live.select('P',4);
 assert.deepEqual(f.calls,[['Subscribe','P',4]]);
 await f.live.select('C',4);assert.deepEqual(f.calls.at(-1),['Unsubscribe']);
});
test('a late connection start subscribes only to the latest selection',async()=>{
 let release;const started=new Promise(r=>release=r);const f=fixture({start:()=>started});
 const first=f.live.select('P',1);await new Promise(setImmediate);const second=f.live.select('P',2);release();
 await first;await second;assert.deepEqual(f.calls,[['Subscribe','P',2]]);
});
test('logout during connection start never sends a late Subscribe',async()=>{
 let release;const f=fixture({start:()=>new Promise(r=>release=r)});
 const pending=f.live.select('P',1);await new Promise(setImmediate);await f.live.stop();release();await pending;
 assert.deepEqual(f.calls,[['stop']]);
});
test('only zero-argument invalidation triggers an authorized HTTP refresh',async()=>{
 const f=fixture();await f.live.select('P',1);f.handlers.Invalidate();f.handlers.Invalidate({content:'ignored'});
 assert.equal(f.invalidations,1);await f.live.stop();f.handlers.Invalidate();assert.equal(f.invalidations,1);
});
test('reconnect resubscribes to current resource and does not send a message',async()=>{
 const f=fixture();await f.live.select('P',1);f.handlers.reconnecting();f.handlers.reconnected();await f.live.chain;
 assert.deepEqual(f.calls,[['Subscribe','P',1],['Subscribe','P',1]]);
});
test('invalid or disabled selection never opens a connection',async()=>{
 const f=fixture();for(const id of [0,-1,2147483648,'1',null])await f.live.select('P',id);
 await f.live.select('D',4);assert.deepEqual(f.calls,[]);
});
test('transport failure retains HTTP polling and waits for explicit retry',async()=>{
 let attempts=0;const f=fixture({start:async()=>{attempts++;throw new Error('unavailable');}});
 await f.live.select('P',1);await f.live.select('P',1);assert.equal(attempts,1);
 assert.match(f.statuses.at(-1),/periódica/);assert.deepEqual(f.calls,[]);
});
