const form = document.querySelector("#invitationForm");
const email = document.querySelector("#email");
const password = document.querySelector("#password");
const feedback = document.querySelector("#feedback");
const submitButton = document.querySelector("#submitButton");
const invitationToken = new URLSearchParams(window.location.hash.slice(1)).get("token");

window.history.replaceState(null, "", "/invite.html");

if (!invitationToken) {
  feedback.textContent = "Link de convite invalido.";
  feedback.hidden = false;
  submitButton.disabled = true;
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  if (!invitationToken) return;
  feedback.hidden = true;
  submitButton.disabled = true;

  try {
    const tokenResponse = await fetch("/api/multi-family/antiforgery/token");
    const antiforgeryToken = (await tokenResponse.json()).token;
    const response = await fetch("/api/multi-family/invitations/accept", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": antiforgeryToken },
      body: JSON.stringify({ token: invitationToken, email: email.value, password: password.value })
    });

    password.value = "";
    if (!response.ok) {
      feedback.textContent = "Convite invalido, expirado ou ja utilizado.";
      feedback.hidden = false;
      return;
    }

    window.location.replace("/multi-family.html");
  } catch {
    password.value = "";
    feedback.textContent = "Nao foi possivel aceitar o convite agora.";
    feedback.hidden = false;
  } finally {
    submitButton.disabled = false;
  }
});
