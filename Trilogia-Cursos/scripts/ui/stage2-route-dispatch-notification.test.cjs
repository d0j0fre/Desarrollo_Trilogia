const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const sourcePath = path.join(__dirname, '../../Proyecto_Final/wwwroot/js/route-dispatch-mail.js');
const source = fs.readFileSync(sourcePath, 'utf8');
const viewSource = fs.readFileSync(path.join(__dirname, '../../Proyecto_Final/Views/RoutesAdmin/Detail.cshtml'), 'utf8');
const ui = require(sourcePath);

function button() {
  return {
    hidden: false, disabled: false, textContent: '', dataset: {}, attrs: {}, handlers: {},
    setAttribute(name, value) { this.attrs[name] = value; },
    addEventListener(name, handler) { this.handlers[name] = handler; },
    click() { return this.handlers.click(); }
  };
}

function fixture(replies) {
  const calls = [];
  const controls = {};
  let document;
  const values = {
    'data-route-id': '10',
    'data-dispatch-id': '4d5c208f-8e9b-4cf0-9ce7-2583b2a15570',
    'data-event-id': '42',
    'data-event-guid': '9ef85c7d-6ab4-431f-a4b7-48707aa196b1',
    'data-notify-eligible': 'true'
  };
  const panel = {
    getAttribute: name => values[name] || null,
    querySelector: selector => controls[selector]
  };
  controls['input[name="__RequestVerificationToken"]'] = { value: 'csrf' };
  controls['[data-route-dispatch-receipt]'] = button();
  controls['[data-route-dispatch-notify]'] = button();
  controls['[data-route-dispatch-notice-status]'] = {
    textContent: '',
    focus() { document.activeElement = this; }
  };
  const toast = { textContent: '', hidden: true };
  document = {
    activeElement: null,
    querySelector: selector => selector === '[data-route-dispatch-toast]' ? toast : null,
    querySelectorAll: () => [panel]
  };
  const fetch = async (url, options) => {
    calls.push({ url, options });
    const reply = replies.shift();
    if (reply instanceof Error) throw reply;
    return { ok: reply.ok !== false, status: reply.status || 200, redirected: false, json: async () => reply };
  };
  ui.createController({ document, fetch, config: { enabled: true, receiptUrl: '/receipt', notifyUrl: '/notify' } }).init();
  return { calls, controls, toast, document };
}

test('only an owned None or Pending receipt enables manual continuation', async () => {
  for (const state of ['NoRecipient', 'AlreadyRequested', 'Sending', 'Unknown', 'SmtpAccepted', 'invalid'])
    assert.equal(ui.canContinue(state, true), false, state);
  assert.equal(ui.canContinue('None', true), true);
  assert.equal(ui.canContinue('Pending', true), true);
  assert.equal(ui.canContinue('None', false), false);

  const f = fixture([{ ok: true, state: 'None' }, { ok: true, state: 'SmtpAccepted' }]);
  assert.equal(f.controls['[data-route-dispatch-notify]'].hidden, true);
  f.controls['[data-route-dispatch-receipt]'].click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  assert.equal(f.controls['[data-route-dispatch-notify]'].hidden, false);
  f.document.activeElement = f.controls['[data-route-dispatch-notify]'];
  f.controls['[data-route-dispatch-notify]'].click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  assert.deepEqual(f.calls.map(x => x.url), ['/receipt', '/notify']);
  assert.match(f.calls[1].options.body, /rutaId=10/);
  assert.match(f.calls[1].options.body, /dispatchId=4d5c208f-8e9b-4cf0-9ce7-2583b2a15570/);
  assert.match(f.calls[1].options.body, /eventId=42/);
  assert.match(f.calls[1].options.body, /eventGuid=9ef85c7d-6ab4-431f-a4b7-48707aa196b1/);
  assert.match(f.calls[1].options.body, /__RequestVerificationToken=csrf/);
  assert.doesNotMatch(f.calls[1].options.body, /OperationId|recipient|CustomerEmail|html|proof/i);
  assert.equal(f.controls['[data-route-dispatch-notify]'].hidden, true);
  assert.equal(f.document.activeElement, f.controls['[data-route-dispatch-notice-status]']);
  assert.match(viewSource, /data-route-dispatch-notice-status tabindex="-1"/);
});

test('unknown or opaque failure never exposes another send', async () => {
  const f = fixture([{ ok: true, state: 'Unknown' }]);
  f.controls['[data-route-dispatch-receipt]'].click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  assert.equal(f.controls['[data-route-dispatch-notify]'].hidden, true);
  assert.match(f.toast.textContent, /no se reintentará automáticamente/i);
  assert.match(source, /response\.status === 401 \|\| response\.status === 403 \|\| response\.status === 404/);
  assert.doesNotMatch(source, /localStorage|sessionStorage|indexedDB|randomUUID|Math\.random/);
});

test('failed manual send moves focus away before hiding the action', async () => {
  const f = fixture([{ ok: true, state: 'None' }, new Error('network')]);
  f.controls['[data-route-dispatch-receipt]'].click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  const send = f.controls['[data-route-dispatch-notify]'];
  assert.equal(send.hidden, false);
  f.document.activeElement = send;
  send.click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  assert.equal(send.hidden, true);
  assert.equal(f.document.activeElement, f.controls['[data-route-dispatch-notice-status]']);
  assert.match(f.controls['[data-route-dispatch-notice-status]'].textContent, /No se confirmó el resultado/);
});

test('invalid selectors fail closed before fetch', async () => {
  const f = fixture([{ ok: true, state: 'None' }]);
  // Deliberately remove the CSRF value after initialization.
  f.controls['input[name="__RequestVerificationToken"]'].value = '';
  f.controls['[data-route-dispatch-receipt]'].click();
  await new Promise(setImmediate); await new Promise(setImmediate);
  assert.equal(f.calls.length, 0);
  assert.equal(f.controls['[data-route-dispatch-notify]'].hidden, true);
});
