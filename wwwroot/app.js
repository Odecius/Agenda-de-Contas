const state = {
  accounts: [],
  accountFilter: "all",
  backups: [],
  dueFilter: "all",
  today: [],
  vencimentos: []
};

let toastTimeoutId;

const formatDate = new Intl.DateTimeFormat("pt-PT");
const formatDateTime = new Intl.DateTimeFormat("pt-PT", {
  dateStyle: "short",
  timeStyle: "short"
});
const today = new Date();
const currentMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

const currencyLocales = {
  GBP: "en-GB",
  EUR: "pt-PT",
  BRL: "pt-BR"
};

const countryLabels = {
  UnitedKingdom: "United Kingdom",
  Portugal: "Portugal",
  Brazil: "Brazil"
};

const selectors = {
  accountForm: document.querySelector("#accountForm"),
  accountFilters: document.querySelectorAll("[data-filter]"),
  accountId: document.querySelector("#accountId"),
  accountsCaption: document.querySelector("#accountsCaption"),
  accountsTable: document.querySelector("#accountsTable"),
  activeAccounts: document.querySelector("#activeAccounts"),
  amount: document.querySelector("#amount"),
  backupList: document.querySelector("#backupList"),
  backupsCaption: document.querySelector("#backupsCaption"),
  cancelEdit: document.querySelector("#cancelEdit"),
  createBackupButton: document.querySelector("#createBackupButton"),
  country: document.querySelector("#country"),
  currency: document.querySelector("#currency"),
  dueDay: document.querySelector("#dueDay"),
  dueFilters: document.querySelectorAll("[data-due-filter]"),
  dueList: document.querySelector("#dueList"),
  duration: document.querySelector("#duration"),
  formTitle: document.querySelector("#formTitle"),
  formFeedback: document.querySelector("#formFeedback"),
  logoutButton: document.querySelector("#logoutButton"),
  monthCaption: document.querySelector("#monthCaption"),
  monthPaidCount: document.querySelector("#monthPaidCount"),
  monthPendingCount: document.querySelector("#monthPendingCount"),
  monthPendingTotal: document.querySelector("#monthPendingTotal"),
  monthOverviewCaption: document.querySelector("#monthOverviewCaption"),
  monthPicker: document.querySelector("#monthPicker"),
  monthProgressBar: document.querySelector("#monthProgressBar"),
  monthProgressPercent: document.querySelector("#monthProgressPercent"),
  monthTotal: document.querySelector("#monthTotal"),
  name: document.querySelector("#name"),
  overviewPaid: document.querySelector("#overviewPaid"),
  overviewPending: document.querySelector("#overviewPending"),
  overviewTotal: document.querySelector("#overviewTotal"),
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
selectors.createBackupButton.addEventListener("click", createBackup);
selectors.logoutButton.addEventListener("click", logout);
selectors.refreshButton.addEventListener("click", loadAll);
selectors.monthPicker.addEventListener("change", loadAll);
selectors.accountFilters.forEach(button => {
  button.addEventListener("click", () => changeAccountFilter(button.dataset.filter));
});
selectors.dueFilters.forEach(button => {
  button.addEventListener("click", () => changeDueFilter(button.dataset.dueFilter));
});

initialize();

async function initialize() {
  await loadAuthStatus();
  await loadAll();
}

async function loadAuthStatus() {
  const response = await fetch("/api/auth/status");
  if (!response.ok) {
    window.location.href = "/login.html";
    return;
  }

  const status = await response.json();
  selectors.logoutButton.hidden = !status.enabled;

  if (status.enabled && !status.authenticated) {
    window.location.href = "/login.html";
  }
}

async function loadAll() {
  setLoading(true);

  try {
    const [accountsResponse, vencimentosResponse, todayResponse, backupsResponse] = await Promise.all([
      fetch("/api/contas"),
      fetchMonthVencimentos(),
      fetch("/api/vencimentos/hoje"),
      fetch("/api/backups")
    ]);

    state.accounts = await readJson(accountsResponse);
    state.vencimentos = await readJson(vencimentosResponse);
    state.today = await readJson(todayResponse);
    state.backups = await readJson(backupsResponse);

    renderDashboard();
    renderAccounts();
    renderVencimentos();
    renderBackups();
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
  if (response.status === 401) {
    window.location.href = "/login.html";
    throw new Error("Sessao expirada.");
  }

  if (!response.ok) {
    throw new Error("A API devolveu um erro ao carregar os dados.");
  }

  return response.json();
}

async function logout() {
  await fetch("/api/auth/logout", { method: "POST" });
  window.location.href = "/login.html";
}

async function createBackup() {
  selectors.createBackupButton.disabled = true;

  try {
    const response = await fetch("/api/backups", { method: "POST" });
    if (!response.ok) {
      throw new Error("Nao foi possivel criar backup.");
    }

    showToast("Backup criado com sucesso.", "success");
    await loadAll();
  } catch (error) {
    showToast(error.message || "Erro ao criar backup.", "error");
  } finally {
    selectors.createBackupButton.disabled = false;
  }
}

async function restoreBackup(fileName) {
  const backupFileName = decodeURIComponent(fileName);
  if (!confirm(`Restaurar o backup ${backupFileName}? Os dados atuais serao substituidos.`)) {
    return;
  }

  const response = await fetch(`/api/backups/${encodeURIComponent(backupFileName)}/restaurar?confirm=true`, {
    method: "POST"
  });

  if (!response.ok) {
    showToast("Nao foi possivel restaurar o backup.", "error");
    return;
  }

  showToast("Backup restaurado com sucesso.", "success");
  await loadAll();
}

function renderDashboard() {
  const todayTotals = groupTotalsByCurrency(state.today);
  const monthTotals = groupTotalsByCurrency(state.vencimentos);
  const monthPending = state.vencimentos.filter(item => !item.pago);
  const monthPendingTotals = groupTotalsByCurrency(monthPending);
  const monthPaid = state.vencimentos.filter(item => item.pago);
  const monthPaidTotals = groupTotalsByCurrency(monthPaid);
  const progressPercent = state.vencimentos.length === 0
    ? 0
    : Math.round((monthPaid.length / state.vencimentos.length) * 100);
  const activeAccounts = state.accounts.filter(account => account.ativa);
  const pausedAccounts = state.accounts.length - activeAccounts.length;

  selectors.todayCount.textContent = state.today.length;
  selectors.todayTotal.innerHTML = formatCurrencyTotals(todayTotals);
  selectors.monthPendingCount.textContent = monthPending.length;
  selectors.monthPendingTotal.innerHTML = formatCurrencyTotals(monthPendingTotals);
  selectors.monthTotal.innerHTML = formatCurrencyTotals(monthTotals);
  selectors.monthPaidCount.textContent = `${monthPaid.length} pagas`;
  selectors.activeAccounts.textContent = activeAccounts.length;
  selectors.pausedAccounts.textContent = `${pausedAccounts} pausadas`;
  selectors.overviewTotal.innerHTML = formatCurrencyTotals(monthTotals);
  selectors.overviewPaid.innerHTML = formatCurrencyTotals(monthPaidTotals);
  selectors.overviewPending.innerHTML = formatCurrencyTotals(monthPendingTotals);
  selectors.monthProgressPercent.textContent = `${progressPercent}%`;
  selectors.monthProgressBar.style.width = `${progressPercent}%`;
  selectors.monthOverviewCaption.textContent = buildMonthOverviewCaption(monthPending.length, monthPaid.length, state.vencimentos.length);

  selectors.todaySummary.textContent = state.today.length === 0
    ? "Hoje nao existem contas pendentes para pagar."
    : `Hoje existem ${state.today.length} conta(s) pendente(s), totalizando ${formatCurrencyTotalsText(todayTotals)}.`;

  selectors.accountsCaption.textContent = `${state.accounts.length} conta(s) cadastradas`;
}

async function saveAccount(event) {
  event.preventDefault();

  const id = selectors.accountId.value;
  const payload = {
    nome: selectors.name.value.trim(),
    valor: Number(selectors.amount.value),
    country: selectors.country.value,
    currency: selectors.currency.value,
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
  selectors.country.value = account.country || "UnitedKingdom";
  selectors.currency.value = account.currency || "GBP";
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
  selectors.country.value = "UnitedKingdom";
  selectors.currency.value = "GBP";
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
    selectors.accountsTable.innerHTML = `<tr><td colspan="8" class="empty">${getEmptyAccountsMessage()}</td></tr>`;
    return;
  }

  for (const account of accounts) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td data-label="Conta">
        <strong>${escapeHtml(account.nome)}</strong>
        ${account.observacoes ? `<div class="due-meta">${escapeHtml(account.observacoes)}</div>` : ""}
      </td>
      <td data-label="Valor">${formatCurrency(account.valor, account.currency)}</td>
      <td data-label="Pais">${formatCountry(account.country)}</td>
      <td data-label="Moeda">${escapeHtml(account.currency || "GBP")}</td>
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
  updateDueFilterButtons();

  const vencimentos = filterVencimentos(state.vencimentos);
  selectors.monthCaption.textContent = buildVencimentosCaption(vencimentos.length);

  if (vencimentos.length === 0) {
    selectors.dueList.innerHTML = `<p class="empty">${getEmptyVencimentosMessage()}</p>`;
    return;
  }

  for (const item of vencimentos) {
    const date = new Date(`${item.dataVencimento}T00:00:00`);
    const card = document.createElement("div");
    card.className = item.pago ? "due-item paid" : "due-item pending";
    card.innerHTML = `
      <div>
        <div class="due-title">
          <strong>${escapeHtml(item.conta.nome)}</strong>
          ${renderPaymentStatus(item)}
        </div>
        <div class="due-meta">
          Vence em ${formatDate.format(date)} - ${formatCurrency(item.conta.valor, item.conta.currency)}
        </div>
        ${renderPaymentDetails(item)}
      </div>
      <button class="${item.pago ? "secondary" : "primary"}" onclick="togglePayment('${item.conta.id}', ${date.getFullYear()}, ${date.getMonth() + 1}, ${item.pago})">
        ${item.pago ? "Desmarcar" : "Marcar pago"}
      </button>
    `;
    selectors.dueList.appendChild(card);
  }
}

function renderBackups() {
  selectors.backupList.innerHTML = "";
  selectors.backupsCaption.textContent = `${state.backups.length} backup(s) disponivel(is)`;

  if (state.backups.length === 0) {
    selectors.backupList.innerHTML = `<p class="empty">Nenhum backup criado ainda.</p>`;
    return;
  }

  for (const backup of state.backups) {
    const item = document.createElement("div");
    const createdAt = new Date(backup.createdAtUtc);
    item.className = "backup-item";
    item.innerHTML = `
      <div>
        <strong>${escapeHtml(backup.fileName)}</strong>
        <div class="due-meta">
          ${formatDateTime.format(createdAt)} - ${formatBytes(backup.sizeBytes)}
        </div>
      </div>
      <button class="secondary" type="button" onclick="restoreBackup('${encodeURIComponent(backup.fileName)}')">Restaurar</button>
    `;
    selectors.backupList.appendChild(item);
  }
}

function changeDueFilter(filter) {
  state.dueFilter = filter;
  renderVencimentos();
}

function filterVencimentos(vencimentos) {
  if (state.dueFilter === "pending") {
    return vencimentos.filter(item => !item.pago);
  }

  if (state.dueFilter === "paid") {
    return vencimentos.filter(item => item.pago);
  }

  return vencimentos;
}

function updateDueFilterButtons() {
  selectors.dueFilters.forEach(button => {
    const isActive = button.dataset.dueFilter === state.dueFilter;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-pressed", String(isActive));
  });
}

function buildVencimentosCaption(visibleCount) {
  const total = state.vencimentos.length;
  const paidCount = state.vencimentos.filter(item => item.pago).length;
  const pendingCount = total - paidCount;
  const suffix = state.dueFilter === "all" ? "" : `, ${visibleCount} visivel(is) no filtro`;
  return `${total} vencimento(s): ${pendingCount} pendente(s), ${paidCount} pago(s)${suffix}`;
}

function getEmptyVencimentosMessage() {
  if (state.vencimentos.length === 0) {
    return "Nenhuma conta vence neste mes.";
  }

  return "Nenhum vencimento encontrado neste filtro.";
}

function buildMonthOverviewCaption(pendingCount, paidCount, totalCount) {
  if (totalCount === 0) {
    return "Sem vencimentos previstos para o mes selecionado.";
  }

  if (pendingCount === 0) {
    return `Mes fechado: ${paidCount} vencimento(s) pago(s).`;
  }

  return `${pendingCount} vencimento(s) ainda pendente(s) neste mes.`;
}

function groupTotalsByCurrency(items) {
  return items.reduce((totals, item) => {
    const currency = item.conta.currency || "GBP";
    totals[currency] = (totals[currency] || 0) + item.conta.valor;
    return totals;
  }, {});
}

function formatCurrencyTotals(totals) {
  const entries = Object.entries(totals);
  if (entries.length === 0) {
    return formatCurrency(0, "GBP");
  }

  return entries
    .sort(([currencyA], [currencyB]) => currencyA.localeCompare(currencyB))
    .map(([currency, amount]) => `<span>${formatCurrency(amount, currency)}</span>`)
    .join("");
}

function formatCurrencyTotalsText(totals) {
  const entries = Object.entries(totals);
  if (entries.length === 0) {
    return formatCurrency(0, "GBP");
  }

  return entries
    .sort(([currencyA], [currencyB]) => currencyA.localeCompare(currencyB))
    .map(([currency, amount]) => formatCurrency(amount, currency))
    .join(", ");
}

function formatCurrency(value, currency = "GBP") {
  return new Intl.NumberFormat(currencyLocales[currency] || "en-GB", {
    style: "currency",
    currency
  }).format(value);
}

function formatCountry(country = "UnitedKingdom") {
  return countryLabels[country] || country;
}

function formatBytes(value) {
  if (value < 1024) {
    return `${value} B`;
  }

  return `${(value / 1024).toFixed(1)} KB`;
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

function renderPaymentDetails(item) {
  if (!item.pago || !item.pagoEm) {
    return "";
  }

  const paidAt = new Date(item.pagoEm);
  return `<div class="payment-meta">Pago em ${formatDateTime.format(paidAt)}</div>`;
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

  if (!countryLabels[payload.country]) {
    markFieldError(selectors.country);
    showFormFeedback("Selecione um pais suportado.", "error");
    selectors.country.focus();
    return false;
  }

  if (!currencyLocales[payload.currency]) {
    markFieldError(selectors.currency);
    showFormFeedback("Selecione uma moeda suportada.", "error");
    selectors.currency.focus();
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
  [selectors.name, selectors.amount, selectors.country, selectors.currency, selectors.dueDay, selectors.startDate, selectors.duration].forEach(field => {
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

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
