import { test } from 'node:test';
import assert from 'node:assert/strict';
import { GraphHistory, sameTopology } from '../src/advanced/history.ts';

const node = (id, kind) => ({ id, kind, label: id, deviceId: kind === 'bus' ? null : id, position: { x: 0, y: 0 }, enabled: true, muted: false, gainDb: 0, compressorEnabled: false, compressorThresholdDb: -12, compressorRatio: 4, limiterEnabled: true });
const base = () => ({ schemaVersion: 2, name: 'Mix', nodes: [node('s','capture'),node('b','bus'),node('o','output')], edges: [{id:'in',source:'s',target:'b',gainDb:0,muted:false},{id:'out',source:'b',target:'o',gainDb:0,muted:false}] });
const change = (g, id, patch) => ({ ...g, nodes: g.nodes.map(n => n.id === id ? {...n,...patch} : n) });

test('running undo and redo preserve routing while reverting gain and mute', () => {
  const h = new GraphHistory(), a = base(), b = change(a,'b',{gainDb:-6,muted:true}); h.record(a,b);
  const undone = h.undo(b,true); assert.ok(sameTopology(b,undone)); assert.equal(undone.nodes[1].gainDb,0); assert.equal(undone.nodes[1].muted,false);
  assert.deepEqual(h.redo(undone,true),b);
});
test('running undo skips structure, keeps new nodes and preserves unrelated later fields', () => {
  const h = new GraphHistory(), a = base(), b = change(a,'b',{gainDb:-6}); h.record(a,b);
  const c = {...b,nodes:[...b.nodes,node('extra','capture')]}; h.record(b,c);
  const external = change(c,'b',{label:'renamed'}), result = h.undo(external,true);
  assert.equal(result.nodes.length,4); assert.equal(result.nodes[1].gainDb,0); assert.equal(result.nodes[1].label,'renamed');
  assert.equal(h.canUndo(result,true),false); assert.equal(h.canUndo(result,false),true);
});
test('one pointer gesture produces one undo entry despite multiple changes', () => {
  const h = new GraphHistory(), a = base(); h.begin(); let current = a;
  for(const gainDb of [-1,-3,-6,-9]) { const next = change(current,'b',{gainDb}); h.record(current,next); current = next; }
  h.end(); assert.deepEqual(h.undo(current,true),a); assert.equal(h.canUndo(a,true),false);
});
test('restoring a removed primary send preserves send ordering', () => {
  const h = new GraphHistory(), a = base(), b = {...a,edges:a.edges.slice(1)}; h.record(a,b);
  assert.deepEqual(h.undo(b,false),a); assert.equal(h.canRedo(a,true),false);
});
test('activation and device changes cannot be undone while running', () => {
  for(const patch of [{enabled:false},{deviceId:'new'}]) {
    const h = new GraphHistory(), a = base(), b = change(a,'s',patch); h.record(a,b);
    assert.equal(h.undo(b,true),null); assert.deepEqual(h.undo(b,false),a);
  }
});
test('history cannot target removed nodes or reused edge identities', () => {
  const h = new GraphHistory(), a = base(), b = {...a,edges:a.edges.map(e=>e.id==='in'?{...e,muted:true}:e)}; h.record(a,b);
  assert.equal(h.canUndo({...b,edges:b.edges.map(e=>({...e,target:'elsewhere'}))},true),false);
  const nodeHistory = new GraphHistory(), changed = change(a,'s',{gainDb:-3}); nodeHistory.record(a,changed);
  assert.equal(nodeHistory.canUndo({...changed,nodes:changed.nodes.filter(n=>n.id!=='s')},true),false);
  assert.equal(nodeHistory.canUndo(change(changed,'s',{deviceId:'replacement'}),true),false);
});
