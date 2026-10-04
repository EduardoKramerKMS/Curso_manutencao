// Se a sessão expirar, volta para o login
const _fetch = window.fetch;
window.fetch = async (...args) => {
  const resp = await _fetch(...args);
  if (resp.status === 401 && !String(args[0]).includes("/api/login")) {
    window.location.href = "/login.html";
  }
  return resp;
};

document.addEventListener("DOMContentLoaded", async () => {
  // nome de quem está logado + link "Usuários" só para administradores
  try {
    const r = await fetch("/api/eu");
    if (r.ok) {
      const u = await r.json();
      const selo = document.querySelector(".selo");
      if (selo) selo.textContent = u.nome;

      const nav = document.querySelector("aside nav");
      if (
        u.perfil === "Admin" &&
        nav &&
        !document.getElementById("menu-usuarios")
      ) {
        const a = document.createElement("a");
        a.id = "menu-usuarios";
        a.href = "consultar-usuarios.html";
        if (location.pathname.endsWith("usuarios.html")) a.className = "ativo";
        a.innerHTML =
          '<svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>Usuários';
        nav.appendChild(a);
      }
    }
  } catch {
    /* mantém o texto padrão */
  }

  // botão Sair
  const sair = document.getElementById("sair");
  if (sair) {
    sair.addEventListener("click", async () => {
      await fetch("/api/logout", { method: "POST" });
      window.location.href = "/login.html";
    });
  }
});
