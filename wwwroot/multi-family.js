const api = "/api/multi-family";
const state = { token: null, families: [], current: null, accounts: [], payments: [], settings: null, members: [] };
const $ = id => document.getElementById(id);

initialize();

async function initialize() {
  try {
    const me = await request("/me");
    if (!me.authenticated) return redirectLogin();
    state.token = (await request("/antiforgery/token")).token;
    state.families = await request("/families");
    if (!state.families.length) throw new Error("Nenhuma família ativa autorizada.");
    renderFamilySelector();
    await loadCurrentFamily();
  } catch (error) { if (error.status === 401) redirectLogin(); else showError(error.message); }
}

async function loadCurrentFamily() {
  let response = await fetch(`${api}/family/current`);
  if (response.status === 409) {
    if (state.families.length === 1) await selectFamily(state.families[0].familyId);
    else { $("familySelect").hidden = false; $("familyStatus").textContent = "Selecione uma família"; return; }
    response = await fetch(`${api}/family/current`);
  }
  if (!response.ok) throw await apiError(response);
  state.current = await response.json();
  $("familySelect").value = state.current.familyId;
  $("familyStatus").textContent = `${state.families.find(x => x.familyId === state.current.familyId)?.name || "Família"} · ${state.current.role}`;
  $("roleStatus").textContent = `Role: ${state.current.role}`;
  await loadTenantData();
}

async function loadTenantData() {
  state.accounts = await request("/contas");
  state.payments = await request("/pagamentos");
  state.settings = await request("/settings");
  if (state.current.role !== "Member") state.members = await request("/members"); else state.members = [];
  renderAccounts(); renderSettings(); renderMembers(); applyRole();
}

function renderFamilySelector() {
  $("familySelect").innerHTML = state.families.map(x => `<option value="${x.familyId}">${escapeHtml(x.name)}</option>`).join("");
  $("familySelect").hidden = state.families.length < 2;
}

async function selectFamily(familyId) {
  await request("/family/select", { method: "POST", body: { familyId } });
  state.accounts = []; state.payments = []; state.settings = null; state.members = [];
}

$("familySelect").addEventListener("change", async event => { await selectFamily(event.target.value); await loadCurrentFamily(); });
$("logoutButton").addEventListener("click", async () => { await request("/auth/logout", { method: "POST" }); redirectLogin(); });
$("cancelEdit").addEventListener("click", () => $("accountForm").reset());

$("accountForm").addEventListener("submit", async event => {
  event.preventDefault();
  const id = $("accountId").value;
  const existing = state.accounts.find(x => x.id === id);
  const body = { nome: $("name").value, valor: Number($("amount").value), country: $("country").value, currency: $("currency").value, diaVencimento: Number($("dueDay").value), dataInicio: $("startDate").value, duracaoMeses: Number($("duration").value), ativa: existing?.ativa ?? true, observacoes: $("notes").value || null };
  await request(id ? `/contas/${id}` : "/contas", { method: id ? "PUT" : "POST", body });
  event.target.reset(); $("accountId").value = ""; await loadTenantData();
});

$("accountsTable").addEventListener("click", async event => {
  const button = event.target.closest("button[data-action]"); if (!button) return;
  const account = state.accounts.find(x => x.id === button.dataset.id); if (!account) return;
  if (button.dataset.action === "edit") { for (const [key, id] of [["nome","name"],["valor","amount"],["country","country"],["currency","currency"],["diaVencimento","dueDay"],["dataInicio","startDate"],["duracaoMeses","duration"],["observacoes","notes"]]) $(id).value = account[key] ?? ""; $("accountId").value = account.id; }
  if (button.dataset.action === "toggle") await request(`/contas/${account.id}`, { method: "PUT", body: { ...account, ativa: !account.ativa } });
  if (button.dataset.action === "delete" && confirm("Excluir esta conta?")) await request(`/contas/${account.id}`, { method: "DELETE" });
  if (button.dataset.action === "pay") { const now = new Date(); await request(`/contas/${account.id}/pagamentos`, { method: "POST", body: { ano: now.getFullYear(), mes: now.getMonth() + 1 } }); }
  if (button.dataset.action === "unpay") await request(`/pagamentos/${button.dataset.payment}`, { method: "DELETE" });
  await loadTenantData();
});

$("settingsForm").addEventListener("submit", async event => { event.preventDefault(); const [hour, minute] = $("reminderTime").value.split(":").map(Number); await request("/settings", { method: "PUT", body: { defaultCurrency: $("defaultCurrency").value, timeZoneId: $("timeZoneId").value, reminderHour: hour, reminderMinute: minute } }); await loadTenantData(); });
$("memberForm").addEventListener("submit", async event => { event.preventDefault(); await request("/members", { method: "POST", body: { email: $("memberEmail").value, role: $("memberRole").value } }); event.target.reset(); await loadTenantData(); });
$("membersList").addEventListener("click", async event => { const b = event.target.closest("button[data-user]"); if (!b) return; if (b.dataset.action === "remove") await request(`/members/${b.dataset.user}`, { method: "DELETE" }); else await request(`/members/${b.dataset.user}/role`, { method: "PUT", body: { role: b.dataset.action } }); await loadTenantData(); });

function renderAccounts() { const now = new Date(); $("accountsTable").innerHTML = state.accounts.map(x => { const payment = state.payments.find(p => p.contaId === x.id && p.ano === now.getFullYear() && p.mes === now.getMonth() + 1); return `<tr><td>${escapeHtml(x.nome)}</td><td>${x.currency} ${Number(x.valor).toFixed(2)}</td><td>${x.diaVencimento}</td><td>${x.ativa ? "Ativa" : "Pausada"}</td><td>${payment ? `<span>Pago</span> <button class="owner-only" data-action="unpay" data-id="${x.id}" data-payment="${payment.id}">Desmarcar</button>` : `<button data-action="pay" data-id="${x.id}">Pagar</button>`} <button class="manage-account" data-action="edit" data-id="${x.id}">Editar</button> <button class="manage-account" data-action="toggle" data-id="${x.id}">${x.ativa ? "Pausar" : "Ativar"}</button> <button class="owner-only" data-action="delete" data-id="${x.id}">Excluir</button></td></tr>`; }).join(""); }
function renderSettings() { $("defaultCurrency").value = state.settings.defaultCurrency; $("timeZoneId").value = state.settings.timeZoneId; $("reminderTime").value = `${String(state.settings.reminderHour).padStart(2,"0")}:${String(state.settings.reminderMinute).padStart(2,"0")}`; }
function renderMembers() { $("membersPanel").hidden = state.current.role === "Member"; $("memberForm").hidden = state.current.role !== "Owner"; $("membersList").innerHTML = state.members.map(x => `<p>${escapeHtml(x.email)} · ${x.role} · ${x.isActive ? "ativo" : "inativo"}${state.current.role === "Owner" ? ` <button data-user="${x.userId}" data-action="Admin">Admin</button> <button data-user="${x.userId}" data-action="Member">Member</button> <button data-user="${x.userId}" data-action="remove">Desativar</button>` : ""}</p>`).join(""); }
function applyRole() { const member = state.current.role === "Member"; document.querySelectorAll(".manage-account").forEach(x => x.hidden = member); document.querySelectorAll(".owner-only").forEach(x => x.hidden = state.current.role !== "Owner"); $("accountForm").hidden = member; $("settingsForm").querySelector("button").hidden = member; }

async function request(path, options = {}) { const init = { method: options.method || "GET", headers: {} }; if (options.body) { init.headers["Content-Type"] = "application/json"; init.body = JSON.stringify(options.body); } if (init.method !== "GET" && state.token) init.headers["X-CSRF-TOKEN"] = state.token; const response = await fetch(api + path, init); if (!response.ok) throw await apiError(response); return response.status === 204 ? null : response.json(); }
async function apiError(response) { const body = await response.json().catch(() => ({})); const error = new Error(body.erro || (response.status === 403 ? "Ação não autorizada." : "Erro ao processar solicitação.")); error.status = response.status; return error; }
function redirectLogin() { window.location.replace("/login.html"); }
function showError(message) { $("feedback").textContent = message; $("feedback").hidden = false; }
function escapeHtml(value) { const node = document.createElement("span"); node.textContent = value ?? ""; return node.innerHTML; }
