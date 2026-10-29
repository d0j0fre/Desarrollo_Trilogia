'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const {Controller}=require('../../Proyecto_Final/wwwroot/js/stage2/chat-v2-continuity.js');
const source=fs.readFileSync(require.resolve('../../Proyecto_Final/wwwroot/js/stage2/chat-v2.js'),'utf8');
class Option {constructor(text,value){this.text=text;this.value=value;this.disabled=false;}}
class Element {
 constructor(){this.value='';this.options=[];this.handlers={};this.disabled=false;this.hidden=false;this.attrs={};this.textContent='';}
 addEventListener(name,handler){this.handlers[name]=handler;}
 setAttribute(name,value){this.attrs[name]=value;}
 replaceChildren(...nodes){this.options=nodes;this.value=nodes[0]?.value??'';}
 get selectedOptions(){return this.options.filter(o=>o.value===this.value);}
 fire(name){this.handlers[name]?.({preventDefault(){}});}
}
async function fixture({missingSelection=false}={}){
 const nodes={};for(const id of ['status','domain','resource','messages','empty','content','send','retry','refresh','contact','open-button','open','compose'])nodes['#chat-v2-'+id]=new Element();
 nodes['input[name="__RequestVerificationToken"]']={value:'synthetic-antiforgery'};
 nodes['#chat-v2-domain'].replaceChildren(...['P','D','C'].map(v=>new Option(v,v)));
 if(missingSelection){for(const option of nodes['#chat-v2-domain'].options)option.disabled=true;nodes['#chat-v2-domain'].value='';}
 const root={dataset:{apiRoot:'/ChatV2/'},querySelector:id=>nodes[id]};
 const calls=[],owned=[],events={};let controller;
 const window={ChatContinuity:{Controller:class extends Controller {constructor(...args){super(...args);controller=this;}}},addEventListener:(n,f)=>{const prior=events[n];events[n]=()=>{prior?.();f();};}};
 vm.runInNewContext(source,{window,ChatContinuity:window.ChatContinuity,document:{querySelector:()=>root,hidden:false,addEventListener(){}},location:{origin:'http://local.invalid'},
  URL,Option,AbortController,HTMLFormElement:Element,setInterval:()=>1,clearInterval(){},
  fetch:async(url,options)=>{const action=url.pathname.split('/').at(-1);calls.push({action,options,url:url.toString()});let result;
   if(action==='Context')result={contextHandle:'context',internalChat:true,attention:false,management:false,departmentChat:false};
   if(action==='Directory')result=[{userId:2}];if(action==='Own')result=owned;
   if(action==='OpenPrivate'){owned.push({domain:'P',resourceId:7});result={resourceId:7};}
   if(action==='Membership')result={authorized:true,canSend:true};if(action==='History')result={messages:[],nextCursor:null};
   return {ok:true,redirected:false,json:async()=>result};}
 });
 const flush=async()=>{for(let i=0;i<10;i++)await new Promise(setImmediate);};await flush();
 return {nodes,calls,events,controller,flush};
}
test('personal availability exposes authorized contacts and leaves D/C disabled',async()=>{
 const f=await fixture();assert.equal(f.nodes['#chat-v2-contact'].options.length,2);
 assert.equal(f.nodes['#chat-v2-contact'].disabled,false);assert.equal(f.nodes['#chat-v2-open-button'].disabled,true);
 assert.equal(f.nodes['#chat-v2-domain'].options[1].disabled,true);assert.equal(f.nodes['#chat-v2-domain'].options[2].disabled,true);
});

test('a browser with no selected option chooses the first authorized modality before loading',async()=>{
 const f=await fixture({missingSelection:true});
 assert.equal(f.nodes['#chat-v2-domain'].value,'P');
 assert.equal(new URL(f.calls.find(c=>c.action==='Own').url).searchParams.get('domain'),'P');
 assert.equal(f.nodes['#chat-v2-contact'].disabled,false);
});

test('a load failure without an attempted send does not claim an unknown send',()=>{
 const c=new Controller(async()=>{}, {clear(){},state(){}});
 c.fail(new Error('availability'),c.generation);
 assert.equal(c.lastState,'Unavailable');
 c.pending=Object.freeze({requestId:'same',content:'original'});
 c.fail(new Error('response lost'),c.generation);
 assert.equal(c.lastState,'PendingConfirmation');
 assert.equal(c.pending.requestId,'same');assert.equal(c.pending.content,'original');
});
test('opening posts current context and antiforgery, then selects the returned resource',async()=>{
 const f=await fixture();f.nodes['#chat-v2-contact'].value='2';f.nodes['#chat-v2-contact'].fire('change');
 assert.equal(f.nodes['#chat-v2-open-button'].disabled,false);f.nodes['#chat-v2-open'].fire('submit');await f.flush();
 const request=f.calls.find(c=>c.action==='OpenPrivate');assert.equal(request.options.method,'POST');
 assert.equal(request.options.headers['X-CSRF-TOKEN'],'synthetic-antiforgery');
 assert.deepEqual(JSON.parse(request.options.body),{contextHandle:'context',otherUser:2});
 assert.equal(f.controller.resource.id,7);assert.equal(f.controller.resource.domain,'P');
 assert.equal(f.nodes['#chat-v2-open'].attrs['aria-busy'],'false');
});
test('a pending message prevents opening another personal context',async()=>{
 const f=await fixture();f.controller.pending={requestId:'pending'};f.nodes['#chat-v2-contact'].value='2';
 f.nodes['#chat-v2-contact'].fire('change');assert.equal(f.nodes['#chat-v2-open-button'].disabled,true);
 f.nodes['#chat-v2-open'].fire('submit');await f.flush();assert.equal(f.calls.some(c=>c.action==='OpenPrivate'),false);
 assert.equal(f.controller.pending.requestId,'pending');
});
test('logout removes the contact directory and disables opening',async()=>{
 const f=await fixture();f.events.pagehide();assert.equal(f.nodes['#chat-v2-contact'].options.length,1);
 assert.equal(f.nodes['#chat-v2-open-button'].disabled,true);assert.equal(f.controller.context,null);
});
test('opaque resource revocation purges a pending body instead of retaining it',async()=>{
 let allow=true,purged=0;const c=new Controller(async name=>{
  if(name==='context')return {contextHandle:'same'};
  if(name==='membership'){if(!allow){const e=new Error();e.status=404;throw e;}return {canSend:true};}
  if(name==='history')return {messages:[],nextCursor:null};
 },{clear(){},purge(){purged++;},render(){}});
 await c.select('P',7);c.pending={requestId:'original',content:'private synthetic',domain:'P',id:7};allow=false;
 await c.refresh();assert.equal(c.pending,null);assert.equal(c.context,null);assert.equal(c.canSend,false);
 assert.equal(c.lastState,'AccessChanged');assert.equal(purged,1);
});
test('a missing receipt retries only the original UUID after current membership',async()=>{
 const request='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';let receipts=0,sends=0;const c=new Controller(async(name,body)=>{
  if(name==='context')return {contextHandle:'same'};if(name==='membership')return {canSend:true};
  if(name==='history')return {messages:[],nextCursor:null};if(name==='prepare')return {intentBinding:'bound'};
  if(name==='receipt'){receipts++;const e=new Error();e.status=404;throw e;}
  if(name==='send'){sends++;assert.equal(body.requestId,request);return {requestId:request,domain:'P',resourceId:7,
   messageId:'1',streamSequence:'1',content:'synthetic',persistedState:'PERSISTED'};}
 },{clear(){},purge(){},render(){}},{uuid:()=>request});
 await c.select('P',7);c.pending=Object.freeze({contextHandle:'same',domain:'P',id:7,requestId:request,content:'synthetic',intentBinding:'bound'});
 await c.retry();assert.equal(receipts,1);assert.equal(sends,1);assert.equal(c.pending,null);
});
