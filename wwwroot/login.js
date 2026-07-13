const form = document.querySelector("#loginForm");
const username = document.querySelector("#username");
const password = document.querySelector("#password");
const feedback = document.querySelector("#feedback");
const submitButton = document.querySelector("#submitButton");

form.addEventListener("submit", async event => {
  event.preventDefault();
  feedback.hidden = true;
  submitButton.disabled = true;

  try {
    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: username.value,
        password: password.value
      })
    });

    if (!response.ok) {
      feedback.textContent = "Utilizador ou senha invalidos.";
      feedback.hidden = false;
      return;
    }

    window.location.href = "/";
  } catch {
    feedback.textContent = "Nao foi possivel entrar agora.";
    feedback.hidden = false;
  } finally {
    submitButton.disabled = false;
  }
});
