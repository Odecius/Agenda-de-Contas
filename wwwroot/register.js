const form = document.querySelector("#registrationForm");
const feedback = document.querySelector("#feedback");
const submitButton = document.querySelector("#submitButton");
let antiforgeryToken = null;

initialize();

async function initialize() {
  const mode = await fetch("/api/multi-family/mode").catch(() => null);
  if (!mode?.ok || (await mode.json()).registrationEnabled !== true) {
    window.location.replace("/login.html");
    return;
  }
  const tokenResponse = await fetch("/api/multi-family/antiforgery/token");
  antiforgeryToken = (await tokenResponse.json()).token;
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  feedback.hidden = true;
  if (form.password.value !== form.confirmPassword.value) {
    showError("As senhas não coincidem.");
    return;
  }
  submitButton.disabled = true;
  try {
    const response = await fetch("/api/multi-family/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": antiforgeryToken },
      body: JSON.stringify({ email: form.email.value, password: form.password.value, familyName: form.familyName.value })
    });
    if (!response.ok) {
      showError(response.status === 429 ? "Muitas tentativas. Tente mais tarde." : "Não foi possível concluir o cadastro.");
      return;
    }
    form.password.value = "";
    form.confirmPassword.value = "";
    window.location.replace("/multi-family.html");
  } catch {
    showError("Não foi possível concluir o cadastro agora.");
  } finally {
    submitButton.disabled = false;
  }
});

function showError(message) {
  feedback.textContent = message;
  feedback.hidden = false;
}
