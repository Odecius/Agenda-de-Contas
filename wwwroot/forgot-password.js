const form = document.querySelector("#forgotForm");
const email = document.querySelector("#email");
const feedback = document.querySelector("#feedback");

form.addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const tokenResponse = await fetch("/api/multi-family/antiforgery/token");
    const csrf = (await tokenResponse.json()).token;
    await fetch("/api/multi-family/auth/forgot-password", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
      body: JSON.stringify({ email: email.value })
    });
  } finally {
    feedback.textContent = "Se existir uma conta elegivel, enviaremos instrucoes para recuperacao.";
    feedback.hidden = false;
  }
});
