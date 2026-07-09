const state = {
  accounts: [],
  accountFilter: "all",
  today: [],
  vencimentos: []
};

let toastTimeoutId;

const formatMoney = new Intl.NumberFormat("pt-PT", {
  style: "currency",
  currency: "EUR"
});

const formatDate = new Intl.DateTimeFormat("pt-PT");
const today = new Date();
const currentMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

const selectors = {
  accountForm: document.querySelector("#accountForm"),
  accountFilters: document.querySelectorAll("[data-filter]"),
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
  formFeedback: document.querySelector("#formFeedback"),
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
  todayTotal: document.querySelector("#todayTotal"),
  toast: document.querySelector("#toast")
};

selectors.startDate.value = today.toISOString().slice(0, 10);
selectors.monthPicker.value = currentMonth;

selectors.accountForm.addEventListener("submit", saveAccount);
selectors.cancelEdit.addEventListener("click", resetForm);
selectors.refreshButton.addEventListener("click", loadAll);
selectors.monthPicker.addEventListener("change", loadAll);
selectors.accountFilters.forEach(button => {
  button.addEventListener("click", () => changeAccountFilter(button.dataset.filter));
});

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
    showToast(error.message || "Erro ao carregar dados.", "error");
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
    nome: selectors.name.value.trim(),
    valor: Number(selectors.amount.value),
    diaVencimento: Number(selectors.dueDay.value),
    dataInicio: selectors.startDate.value,
    duracaoMeses: Number(selectors.duration.value),
    observacoes: selectors.notes.value.trim()
  };

  if (!validateAccountPayload(payload)) {
    return;
  }

  try {
    const response = await fetch(id ? `/api/contas/${id}` : "/api/contas", {
      method: id ? "PUT" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ erro: "Erro ao guardar conta." }));
      showFormFeedback(error.erro || "Erro ao guardar conta.", "error");
      return;
    }

    resetForm();
    showToast(id ? "Conta atualizada com sucesso." : "Conta cadastrada com sucesso.", "success");
    await loadAll();
  } catch (error) {
    showFormFeedback(error.message || "Erro ao guardar conta.", "error");
  }
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
  showFormFeedback(`Editando ${account.nome}.`, "info");
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
  clearFieldStates();
  hideFormFeedback();
}

async function toggleActive(id) {
  const account = state.accounts.find(item => item.id === id);
  const response = await fetch(`/api/contas/${id}/alternar-ativa`, { method: "POST" });

  if (!response.ok) {
    showToast("Nao foi possivel alterar o status da conta.", "error");
    return;
  }

  showToast(account?.ativa ? "Conta pausada." : "Conta ativada.", "success");
  await loadAll();
}

async function deleteAccount(id) {
  const account = state.accounts.find(item => item.id === id);
  const accountName = account ? ` "${account.nome}"` : "";

  if (!confirm(`Excluir a conta${accountName}? Esta acao nao pode ser desfeita.`)) return;

  const response = await fetch(`/api/contas/${id}?confirm=true`, { method: "DELETE" });
  if (!response.ok) {
    showToast("Nao foi possivel excluir a conta.", "error");
    return;
  }

  showToast("Conta excluida com sucesso.", "success");
  await loadAll();
}

async function togglePayment(accountId, year, month, paid) {
  const response = await fetch(`/api/contas/${accountId}/pagamentos/${year}/${month}`, {
    method: paid ? "DELETE" : "POST"
  });

  if (!response.ok) {
    showToast("Nao foi possivel atualizar o pagamento.", "error");
    return;
  }

  showToast(paid ? "Pagamento desmarcado." : "Pagamento marcado como pago.", "success");
  await loadAll();
}

function renderAccounts() {
  selectors.accountsTable.innerHTML = "";
  updateFilterButtons();

  const accounts = filterAccounts(state.accounts);
  selectors.accountsCaption.textContent = buildAccountsCaption(accounts.length);

  if (accounts.length === 0) {
    selectors.accountsTable.innerHTML = `<tr><td colspan="6" class="empty">${getEmptyAccountsMessage()}</td></tr>`;
    return;
  }

  for (const account of accounts) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td data-label="Conta">
        <strong>${escapeHtml(account.nome)}</strong>
        ${account.observacoes ? `<div class="due-meta">${escapeHtml(account.observacoes)}</div>` : ""}
      </td>
      <td data-label="Valor">${formatMoney.format(account.valor)}</td>
      <td data-label="Vencimento">Dia ${account.diaVencimento}</td>
      <td data-label="Duracao">${account.duracaoMeses === 0 ? "Indeterminada" : `${account.duracaoMeses} meses`}</td>
      <td data-label="Status">${renderAccountStatus(account)}</td>
      <td data-label="Acoes">
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

function changeAccountFilter(filter) {
  state.accountFilter = filter;
  renderAccounts();
}

function filterAccounts(accounts) {
  if (state.accountFilter === "active") {
    return accounts.filter(account => account.ativa);
  }

  if (state.accountFilter === "paused") {
    return accounts.filter(account => !account.ativa);
  }

  return accounts;
}

function updateFilterButtons() {
  selectors.accountFilters.forEach(button => {
    const isActive = button.dataset.filter === state.accountFilter;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-pressed", String(isActive));
  });
}

function buildAccountsCaption(visibleCount) {
  const total = state.accounts.length;
  const suffix = state.accountFilter === "all" ? "" : `, ${visibleCount} visivel(is) no filtro`;
  return `${total} conta(s) cadastrada(s)${suffix}`;
}

function getEmptyAccountsMessage() {
  if (state.accounts.length === 0) {
    return "Nenhuma conta cadastrada.";
  }

  return "Nenhuma conta encontrada neste filtro.";
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

function validateAccountPayload(payload) {
  clearFieldStates();

  if (payload.nome.length < 2) {
    markFieldError(selectors.name);
    showFormFeedback("Informe um nome com pelo menos 2 caracteres.", "error");
    selectors.name.focus();
    return false;
  }

  if (!Number.isFinite(payload.valor) || payload.valor <= 0) {
    markFieldError(selectors.amount);
    showFormFeedback("Informe um valor maior que zero.", "error");
    selectors.amount.focus();
    return false;
  }

  if (!Number.isInteger(payload.diaVencimento) || payload.diaVencimento < 1 || payload.diaVencimento > 31) {
    markFieldError(selectors.dueDay);
    showFormFeedback("Informe um dia de vencimento entre 1 e 31.", "error");
    selectors.dueDay.focus();
    return false;
  }

  if (!payload.dataInicio) {
    markFieldError(selectors.startDate);
    showFormFeedback("Informe a data de inicio da conta.", "error");
    selectors.startDate.focus();
    return false;
  }

  if (!Number.isInteger(payload.duracaoMeses) || payload.duracaoMeses < 0) {
    markFieldError(selectors.duration);
    showFormFeedback("Informe uma duracao valida. Use 0 para contas sem fim definido.", "error");
    selectors.duration.focus();
    return false;
  }

  hideFormFeedback();
  return true;
}

function markFieldError(field) {
  field.classList.add("is-invalid");
}

function clearFieldStates() {
  [selectors.name, selectors.amount, selectors.dueDay, selectors.startDate, selectors.duration].forEach(field => {
    field.classList.remove("is-invalid");
  });
}

function showFormFeedback(message, type) {
  selectors.formFeedback.textContent = message;
  selectors.formFeedback.className = `form-feedback ${type}`;
  selectors.formFeedback.hidden = false;
}

function hideFormFeedback() {
  selectors.formFeedback.hidden = true;
  selectors.formFeedback.textContent = "";
}

function showToast(message, type = "info") {
  clearTimeout(toastTimeoutId);
  selectors.toast.textContent = message;
  selectors.toast.className = `toast ${type}`;
  selectors.toast.hidden = false;

  toastTimeoutId = setTimeout(() => {
    selectors.toast.hidden = true;
  }, 3600);
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
