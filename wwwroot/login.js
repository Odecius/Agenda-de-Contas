const form = document.querySelector("#loginForm");
const username = document.querySelector("#username");
const password = document.querySelector("#password");
const feedback = document.querySelector("#feedback");
const submitButton = document.querySelector("#submitButton");

let multiFamily = false;
let antiforgeryToken = null;

initializeLogin();

async function initializeLogin() {
  const mode = await fetch("/api/multi-family/mode").catch(() => null);
  multiFamily = mode?.ok === true;
  if (multiFamily) {
    username.previousSibling.textContent = "Email ";
    username.type = "email";
    const tokenResponse = await fetch("/api/multi-family/antiforgery/token");
    antiforgeryToken = (await tokenResponse.json()).token;
  }
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  feedback.hidden = true;
  submitButton.disabled = true;

  try {
    const response = await fetch(multiFamily ? "/api/multi-family/auth/login" : "/api/auth/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(multiFamily ? { "X-CSRF-TOKEN": antiforgeryToken } : {})
      },
      body: JSON.stringify({
        ...(multiFamily ? { email: username.value } : { username: username.value }),
        password: password.value
      })
    });

    if (!response.ok) {
      feedback.textContent = "Utilizador ou senha invalidos.";
      feedback.hidden = false;
      return;
    }

    password.value = "";
    window.location.href = multiFamily ? "/multi-family.html" : "/";
  } catch {
    feedback.textContent = "Nao foi possivel entrar agora.";
    feedback.hidden = false;
  } finally {
    submitButton.disabled = false;
  }
});
