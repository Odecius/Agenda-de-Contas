const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'wwwroot/multi-family.js'), 'utf8');

function page(role, paid = false) {
  const elements = new Map();
  const element = id => {
    if (!elements.has(id)) elements.set(id, {
      hidden: true, value: '', innerHTML: '', button: { hidden: false },
      addEventListener() {}, querySelector() { return this.button; }
    });
    return elements.get(id);
  };
  const controls = [];
  const document = {
    getElementById: element,
    createElement: () => ({ set textContent(value) { this.innerHTML = String(value); } }),
    querySelectorAll: selector => controls.filter(c => c.className.split(' ').includes(selector.slice(1)))
  };
  const context = vm.createContext({ document, console });
  // Execute the real UI functions without starting authentication/network initialization.
  vm.runInContext(source.replace(/^initialize\(\);$/m, ''), context);
  const now = new Date();
  vm.runInContext(`state.current = {role: ${JSON.stringify(role)}};
    state.accounts = [{id:'account', nome:'Internet', currency:'EUR', valor:30, diaVencimento:5, ativa:true}];
    state.payments = ${JSON.stringify(paid ? [{ id: 'payment', contaId: 'account', ano: now.getFullYear(), mes: now.getMonth() + 1 }] : [])};
    renderAccounts(); renderMembers();`, context);
  // Build the controls from the actual rendered markup before applying visibility rules.
  for (const match of element('accountsTable').innerHTML.matchAll(/<button\b([^>]*)>/g)) {
    const attrs = match[1];
    controls.push({
      className: /class="([^"]*)"/.exec(attrs)?.[1] || '',
      action: /data-action="([^"]*)"/.exec(attrs)?.[1], hidden: false
    });
  }
  vm.runInContext('applyRole();', context);
  return { element, controls };
}

for (const role of ['Member', 'Admin', 'Owner']) {
  test(`${role}: account form, edit and payment are available`, () => {
    const { element, controls } = page(role);
    assert.equal(element('accountForm').hidden, false);
    for (const action of ['edit', 'pay']) {
      assert.equal(controls.find(c => c.action === action)?.hidden, false);
    }
  });
  test(`${role}: destructive and administrative controls retain role restrictions`, () => {
    const unpaid = page(role);
    const paid = page(role, true);
    assert.equal(unpaid.controls.find(c => c.action === 'delete')?.hidden, role !== 'Owner');
    assert.equal(paid.controls.find(c => c.action === 'unpay')?.hidden, role !== 'Owner');
    assert.equal(unpaid.element('settingsForm').button.hidden, role === 'Member');
    assert.equal(unpaid.element('membersPanel').hidden, role === 'Member');
    assert.equal(unpaid.element('memberForm').hidden, role !== 'Owner');
    assert.equal(paid.controls.some(c => c.action === 'pay'), false);
  });
}

test('HTML loads the updated real UI script', () => {
  const html = fs.readFileSync(path.join(root, 'wwwroot/multi-family.html'), 'utf8');
  assert.match(html, /id="accountForm"/);
  assert.match(html, /\/multi-family\.js\?v=1\.0\.7/);
});
