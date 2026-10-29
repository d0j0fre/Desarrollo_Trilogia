const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const sourcePath = path.join(__dirname, '../../Proyecto_Final/wwwroot/js/driver-delivery-mail.js');
const source = fs.readFileSync(sourcePath, 'utf8');
const ui = require(sourcePath);

test('only None and Pending can expose the manual send action', () => {
  for (const state of ['SmtpAccepted', 'Sending', 'Unknown', 'AlreadyRequested', 'NoEvent', 'unrecognized']) {
    assert.equal(ui.canSend(state), false, state);
  }
  assert.equal(ui.canSend('None'), true);
  assert.equal(ui.canSend('Pending'), true);
});

test('receipt lookup is separate and a same-session None receipt enables one manual request', async () => {
  const calls = [];
  const buttons = {};
  const panel = {
    getAttribute: name => name === 'data-route-order-id' ? '77' : null,
    querySelector: selector => buttons[selector]
  };
  const toast = { textContent: '', className: '', hidden: true };
  const document = { getElementById: () => toast, querySelectorAll: () => [panel] };
  function button() {
    return {
      hidden: false, disabled: false, textContent: '', dataset: {}, attrs: {}, handlers: {},
      setAttribute(name, value) { this.attrs[name] = value; },
      addEventListener(name, handler) { this.handlers[name] = handler; },
      click() { return this.handlers.click(); }
    };
  }
  buttons['[data-delivery-receipt]'] = button();
  buttons['[data-delivery-send]'] = button();
  buttons['[data-delivery-notice-status]'] = { textContent: '', hidden: true };
  const replies = [
    { ok: true, state: 'None' },
    { ok: true, state: 'SmtpAccepted' }
  ];
  const fetch = async (url, options) => {
    calls.push({ url, options });
    return { ok: true, status: 200, redirected: false, json: async () => replies.shift() };
  };

  const testCsrf = 'csrf';
  ui.createController({ document, fetch, config: { enabled: true, token: testCsrf, receiptUrl: '/receipt', notifyUrl: '/notify' } }).init();
  assert.equal(buttons['[data-delivery-send]'].hidden, true, 'send remains unavailable before receipt lookup');
  buttons['[data-delivery-receipt]'].click();
  await new Promise(setImmediate);
  await new Promise(setImmediate);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, '/receipt');
  assert.equal(buttons['[data-delivery-send]'].hidden, false);

  buttons['[data-delivery-send]'].click();
  await new Promise(setImmediate);
  await new Promise(setImmediate);
  assert.equal(calls.length, 2);
  assert.equal(calls[1].url, '/notify');
  assert.equal(calls[1].options.method, 'POST');
  assert.equal(calls[1].options.credentials, 'same-origin');
  assert.match(calls[1].options.body, /rutaPedidoId=77/);
  assert.match(calls[1].options.body, /__RequestVerificationToken=csrf/);
  assert.doesNotMatch(calls[1].options.body, /OperationId|SyncGuid|recipient|html|proof/i);
  assert.equal(buttons['[data-delivery-send]'].hidden, true, 'accepted SMTP receipt removes the send action');
  assert.match(toast.textContent, /no confirma que llegó al buzón/i);
});

test('unknown results and opaque auth/not-found responses never expose resend', async () => {
  assert.match(ui.messageFor('Unknown'), /No se reenviará automáticamente/i);
  assert.match(source, /response\.status === 401 \|\| response\.status === 403 \|\| response\.status === 404/);
  assert.match(source, /send\.hidden = true; send\.disabled = true/);
  assert.doesNotMatch(source, /localStorage|sessionStorage|indexedDB|randomUUID|Math\.random/);
});
