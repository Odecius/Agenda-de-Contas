const state = {
  accounts: [],
  today: [],
  vencimentos: []
};

const formatMoney = new Intl.NumberFormat("pt-PT", {
  style: "currency",
  currency: "EUR"
});

const formatDate = new Intl.DateTimeFormat("pt-PT");
const today = new Date();
const currentMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

const selectors = {
  accountForm: document.querySelector("#accountForm"),
  accountId: document.querySelector("#accountId"),
  accountsCaption: document.querySelector("#accountsCaption"),
  accountsTable: document.querySelector("#accountsTable"),
  activeAccounts: document.querySelector("#activeAccounts"),
  amount: document.querySelector("#amount"),
  cancelEdit: document.querySelector("#cancelEdit"),
  dueDay: document.querySelector("#dueDay"),
  dueList: document.querySelector("#dueList"),
  duration: document.querySelector("#duration"),
  formTitle: document.querySelector("#formTitle"),
  monthCaption: document.querySelector("#monthCaption"),
  monthPaidCount: document.querySelector("#monthPaidCount"),
  monthPendingCount: document.querySelector("#monthPendingCount"),
  monthPendingTotal: document.querySelector("#monthPendingTotal"),
  monthPicker: document.querySelector("#monthPicker"),
  monthTotal: document.querySelector("#monthTotal"),
  name: document.querySelector("#name"),
  notes: document.querySelector("#notes"),
  pausedAccounts: document.querySelector("#pausedAccounts"),
  refreshButton: document.querySelector("#refreshButton"),
  startDate: document.querySelector("#startDate"),
  todayCount: document.querySelector("#todayCount"),
  todaySummary: document.querySelector("#todaySummary"),
  todayTotal: document.querySelector("#todayTotal")
};

selectors.startDate.value = today.toISOString().slice(0, 10);
selectors.monthPicker.value = currentMonth;

selectors.accountForm.addEventListener("submit", saveAccount);
selectors.cancelEdit.addEventListener("click", resetForm);
selectors.refreshButton.addEventListener("click", loadAll);
selectors.monthPicker.addEventListener("change", loadAll);

loadAll();

async function loadAll() {
  setLoading(true);

  try {
    const [accountsResponse, vencimentosResponse, todayResponse] = await Promise.all([
      fetch("/api/contas"),
      fetchMonthVencimentos(),
      fetch("/api/vencimentos/hoje")
    ]);

    state.accounts = await readJson(accountsResponse);
    state.vencimentos = await readJson(vencimentosResponse);
    state.today = await readJson(todayResponse);

    renderDashboard();
    renderAccounts();
    renderVencimentos();
  } catch (error) {
    alert(error.message || "Erro ao carregar dados.");
  } finally {
    setLoading(false);
  }
}

function fetchMonthVencimentos() {
  const [year, month] = selectors.monthPicker.value.split("-");
  return fetch(`/api/vencimentos?ano=${year}&mes=${Number(month)}`);
}

async function readJson(response) {
  if (!response.ok) {
    throw new Error("A API devolveu um erro ao carregar os dados.");
  }

  return response.json();
}

function renderDashboard() {
  const todayTotal = sum(state.today, item => item.conta.valor);
  const monthTotal = sum(state.vencimentos, item => item.conta.valor);
  const monthPending = state.vencimentos.filter(item => !item.pago);
  const monthPendingTotal = sum(monthPending, item => item.conta.valor);
  const activeAccounts = state.accounts.filter(account => account.ativa);
  const pausedAccounts = state.accounts.length - activeAccounts.length;

  selectors.todayCount.textContent = state.today.length;
  selectors.todayTotal.textContent = formatMoney.format(todayTotal);
  selectors.monthPendingCount.textContent = monthPending.length;
  selectors.monthPendingTotal.textContent = formatMoney.format(monthPendingTotal);
  selectors.monthTotal.textContent = formatMoney.format(monthTotal);
  selectors.monthPaidCount.textContent = `${state.vencimentos.length - monthPending.length} pagas`;
  selectors.activeAccounts.textContent = activeAccounts.length;
  selectors.pausedAccounts.textContent = `${pausedAccounts} pausadas`;

  selectors.todaySummary.textContent = state.today.length === 0
    ? "Hoje nao existem contas pendentes para pagar."
    : `Hoje existem ${state.today.length} conta(s) pendente(s), totalizando ${formatMoney.format(todayTotal)}.`;

  selectors.monthCaption.textContent = `${state.vencimentos.length} vencimento(s) encontrados`;
  selectors.accountsCaption.textContent = `${state.accounts.length} conta(s) cadastradas`;
}

async function saveAccount(event) {
  event.preventDefault();

  const id = selectors.accountId.value;
  const payload = {
    nome: selectors.name.value,
    valor: Number(selectors.amount.value),
    diaVencimento: Number(selectors.dueDay.value),
    dataInicio: selectors.startDate.value,
    duracaoMeses: Number(selectors.duration.value),
    observacoes: selectors.notes.value
  };

  const response = await fetch(id ? `/api/contas/${id}` : "/api/contas", {
    method: id ? "PUT" : "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ erro: "Erro ao guardar conta." }));
    alert(error.erro || "Erro ao guardar conta.");
    return;
  }

  resetForm();
  await loadAll();
}

function editAccount(id) {
  const account = state.accounts.find(item => item.id === id);
  if (!account) return;

  selectors.formTitle.textContent = "Editar conta";
  selectors.accountId.value = account.id;
  selectors.name.value = account.nome;
  selectors.amount.value = account.valor;
  selectors.dueDay.value = account.diaVencimento;
  selectors.startDate.value = account.dataInicio;
  selectors.duration.value = account.duracaoMeses;
  selectors.notes.value = account.observacoes || "";
  selectors.cancelEdit.hidden = false;
  selectors.name.focus();
}

function resetForm() {
  selectors.formTitle.textContent = "Cadastrar conta";
  selectors.accountForm.reset();
  selectors.accountId.value = "";
  selectors.startDate.value = today.toISOString().slice(0, 10);
  selectors.duration.value = 0;
  selectors.dueDay.value = 1;
  selectors.cancelEdit.hidden = true;
}

async function toggleActive(id) {
  await fetch(`/api/contas/${id}/alternar-ativa`, { method: "POST" });
  await loadAll();
}

async function deleteAccount(id) {
  if (!confirm("Excluir esta conta?")) return;
  await fetch(`/api/contas/${id}`, { method: "DELETE" });
  await loadAll();
}

async function togglePayment(accountId, year, month, paid) {
  await fetch(`/api/contas/${accountId}/pagamentos/${year}/${month}`, {
    method: paid ? "DELETE" : "POST"
  });
  await loadAll();
}

function renderAccounts() {
  selectors.accountsTable.innerHTML = "";

  if (state.accounts.length === 0) {
    selectors.accountsTable.innerHTML = `<tr><td colspan="6" class="empty">Nenhuma conta cadastrada.</td></tr>`;
    return;
  }

  for (const account of state.accounts) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>
        <strong>${escapeHtml(account.nome)}</strong>
        ${account.observacoes ? `<div class="due-meta">${escapeHtml(account.observacoes)}</div>` : ""}
      </td>
      <td>${formatMoney.format(account.valor)}</td>
      <td>Dia ${account.diaVencimento}</td>
      <td>${account.duracaoMeses === 0 ? "Indeterminada" : `${account.duracaoMeses} meses`}</td>
      <td>${renderAccountStatus(account)}</td>
      <td>
        <div class="row-actions">
          <button class="ghost" onclick="editAccount('${account.id}')">Editar</button>
          <button class="secondary" onclick="toggleActive('${account.id}')">${account.ativa ? "Pausar" : "Ativar"}</button>
          <button class="danger" onclick="deleteAccount('${account.id}')">Excluir</button>
        </div>
      </td>
    `;
    selectors.accountsTable.appendChild(tr);
  }
}

function renderVencimentos() {
  selectors.dueList.innerHTML = "";

  if (state.vencimentos.length === 0) {
    selectors.dueList.innerHTML = `<p class="empty">Nenhuma conta vence neste mes.</p>`;
    return;
  }

  for (const item of state.vencimentos) {
    const date = new Date(`${item.dataVencimento}T00:00:00`);
    const card = document.createElement("div");
    card.className = "due-item";
    card.innerHTML = `
      <div>
        <div class="due-title">
          <strong>${escapeHtml(item.conta.nome)}</strong>
          ${renderPaymentStatus(item)}
        </div>
        <div class="due-meta">
          Vence em ${formatDate.format(date)} - ${formatMoney.format(item.conta.valor)}
        </div>
      </div>
      <button class="${item.pago ? "secondary" : "primary"}" onclick="togglePayment('${item.conta.id}', ${date.getFullYear()}, ${date.getMonth() + 1}, ${item.pago})">
        ${item.pago ? "Desmarcar" : "Marcar pago"}
      </button>
    `;
    selectors.dueList.appendChild(card);
  }
}

function renderAccountStatus(account) {
  return account.ativa
    ? `<span class="badge ok">Ativa</span>`
    : `<span class="badge paused">Pausada</span>`;
}

function renderPaymentStatus(item) {
  return item.pago
    ? `<span class="badge ok">Pago</span>`
    : `<span class="badge pending">Pendente</span>`;
}

function setLoading(isLoading) {
  selectors.refreshButton.disabled = isLoading;
  selectors.refreshButton.innerHTML = isLoading ? "..." : "&#8635;";
}

function sum(items, selector) {
  return items.reduce((total, item) => total + selector(item), 0);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
