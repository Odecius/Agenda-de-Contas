const form = document.querySelector("#resetForm");
const password = document.querySelector("#password");
const confirmPassword = document.querySelector("#confirmPassword");
const feedback = document.querySelector("#feedback");
const params = new URLSearchParams(window.location.hash.slice(1));
const token = params.get("token");
const email = params.get("email");
window.history.replaceState(null, "", "/reset-password.html");

form.addEventListener("submit", async event => {
  event.preventDefault();
  if (!token || !email || password.value !== confirmPassword.value) {
    feedback.textContent = "Link invalido ou senhas diferentes.";
    feedback.hidden = false;
    return;
  }
  const tokenResponse = await fetch("/api/multi-family/antiforgery/token");
  const csrf = (await tokenResponse.json()).token;
  const response = await fetch("/api/multi-family/auth/reset-password", {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
    body: JSON.stringify({ email, token, newPassword: password.value })
  });
  password.value = "";
  confirmPassword.value = "";
  feedback.textContent = response.ok ? "Senha redefinida. Volte ao login." : "Nao foi possivel redefinir a senha.";
  feedback.hidden = false;
});
