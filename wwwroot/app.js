const state = {
  accounts: [],
  vencimentos: []
};

const formatMoney = new Intl.NumberFormat("pt-PT", {
  style: "currency",
  currency: "EUR"
});

const today = new Date();
const currentMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

document.querySelector("#startDate").value = today.toISOString().slice(0, 10);
document.querySelector("#monthPicker").value = currentMonth;

document.querySelector("#accountForm").addEventListener("submit", saveAccount);
document.querySelector("#cancelEdit").addEventListener("click", resetForm);
document.querySelector("#refreshButton").addEventListener("click", loadAll);
document.querySelector("#monthPicker").addEventListener("change", loadVencimentos);

loadAll();

async function loadAll() {
  const [accountsResponse] = await Promise.all([
    fetch("/api/contas"),
    loadVencimentos(),
    loadTodaySummary()
  ]);

  state.accounts = await accountsResponse.json();
  renderAccounts();
}

async function loadVencimentos() {
  const [year, month] = document.querySelector("#monthPicker").value.split("-");
  const response = await fetch(`/api/vencimentos?ano=${year}&mes=${Number(month)}`);
  state.vencimentos = await response.json();
  renderVencimentos();
}

async function loadTodaySummary() {
  const response = await fetch("/api/vencimentos/hoje");
  const dueToday = await response.json();
  const total = dueToday.reduce((sum, item) => sum + item.conta.valor, 0);
  const summary = document.querySelector("#todaySummary");

  if (dueToday.length === 0) {
    summary.textContent = "Hoje nao existem contas pendentes para pagar.";
    return;
  }

  summary.textContent = `Hoje existem ${dueToday.length} conta(s) pendente(s), totalizando ${formatMoney.format(total)}.`;
}

async function saveAccount(event) {
  event.preventDefault();

  const id = document.querySelector("#accountId").value;
  const payload = {
    nome: document.querySelector("#name").value,
    valor: Number(document.querySelector("#amount").value),
    diaVencimento: Number(document.querySelector("#dueDay").value),
    dataInicio: document.querySelector("#startDate").value,
    duracaoMeses: Number(document.querySelector("#duration").value),
    observacoes: document.querySelector("#notes").value
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

  document.querySelector("#formTitle").textContent = "Editar conta";
  document.querySelector("#accountId").value = account.id;
  document.querySelector("#name").value = account.nome;
  document.querySelector("#amount").value = account.valor;
  document.querySelector("#dueDay").value = account.diaVencimento;
  document.querySelector("#startDate").value = account.dataInicio;
  document.querySelector("#duration").value = account.duracaoMeses;
  document.querySelector("#notes").value = account.observacoes || "";
  document.querySelector("#cancelEdit").hidden = false;
}

function resetForm() {
  document.querySelector("#formTitle").textContent = "Cadastrar conta";
  document.querySelector("#accountForm").reset();
  document.querySelector("#accountId").value = "";
  document.querySelector("#startDate").value = today.toISOString().slice(0, 10);
  document.querySelector("#duration").value = 0;
  document.querySelector("#dueDay").value = 1;
  document.querySelector("#cancelEdit").hidden = true;
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
  await Promise.all([loadVencimentos(), loadTodaySummary()]);
}

function renderAccounts() {
  const tbody = document.querySelector("#accountsTable");
  tbody.innerHTML = "";

  if (state.accounts.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6" class="empty">Nenhuma conta cadastrada.</td></tr>`;
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
      <td><span class="badge ${account.ativa ? "ok" : "pending"}">${account.ativa ? "Ativa" : "Pausada"}</span></td>
      <td>
        <div class="row-actions">
          <button class="ghost" onclick="editAccount('${account.id}')">Editar</button>
          <button class="secondary" onclick="toggleActive('${account.id}')">${account.ativa ? "Pausar" : "Ativar"}</button>
          <button class="danger" onclick="deleteAccount('${account.id}')">Excluir</button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  }
}

function renderVencimentos() {
  const list = document.querySelector("#dueList");
  list.innerHTML = "";

  if (state.vencimentos.length === 0) {
    list.innerHTML = `<p class="empty">Nenhuma conta vence neste mes.</p>`;
    return;
  }

  for (const item of state.vencimentos) {
    const date = new Date(`${item.dataVencimento}T00:00:00`);
    const card = document.createElement("div");
    card.className = "due-item";
    card.innerHTML = `
      <div>
        <strong>${escapeHtml(item.conta.nome)}</strong>
        <div class="due-meta">
          Vence em ${date.toLocaleDateString("pt-PT")} · ${formatMoney.format(item.conta.valor)}
        </div>
      </div>
      <button class="${item.pago ? "secondary" : "primary"}" onclick="togglePayment('${item.conta.id}', ${date.getFullYear()}, ${date.getMonth() + 1}, ${item.pago})">
        ${item.pago ? "Desmarcar" : "Marcar pago"}
      </button>
    `;
    list.appendChild(card);
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
