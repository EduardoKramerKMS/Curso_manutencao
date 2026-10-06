/* ============ TEMA CLARO / ESCURO ============
   Roda logo no início (o script fica no <head>), para a página já abrir no tema certo
   e não piscar branca. A escolha fica salva no navegador. */
(function () {
  let salvo = null;
  try { salvo = localStorage.getItem("tema"); } catch { }
  const escuro = salvo
    ? salvo === "escuro"
    : window.matchMedia("(prefers-color-scheme: dark)").matches;
  document.documentElement.dataset.tema = escuro ? "escuro" : "claro";

  const E = 'html[data-tema="escuro"]';
  const css = `
    ${E}{color-scheme:dark;--fundo:#0b1220;--texto:#e5e7eb;--suave:#94a3b8;--borda:#243049;--azul-claro:#13234a;--azul-escuro:#93c5fd}
    ${E} body{background:#0b1220;color:#e5e7eb}
    ${E} :is(aside,header,.cartao,dialog){background:#111a2e;color:#e5e7eb;border-color:#243049}
    ${E} :is(h1,h2,h3,b,strong,.nome,.detalhe){color:#f1f5f9}
    ${E} :is(p,small,.caminho,.rodape,.vazio td){color:#94a3b8}
    ${E} :is(label,legend,nav a,.submenu a,td,th){color:#cbd5e1}
    ${E} nav a:hover{background:#1a2540}
    ${E} nav a.ativo{background:#13234a;color:#93c5fd}
    ${E} .caminho b{color:#60a5fa}
    ${E} :is(input,select,textarea){background:#0f172a;color:#e5e7eb;border-color:#2b3a57}
    ${E} ::placeholder{color:#64748b}
    ${E} .busca{background:#111a2e;border-color:#243049}
    ${E} .busca input{background:transparent}
    ${E} .filtros{background:#0f172a}
    ${E} .filtros button{color:#cbd5e1}
    ${E} .filtros button.ativo{background:#1e293b;color:#f1f5f9}
    ${E} :is(.sino,.cancelar){background:#1e293b;color:#e5e7eb}
    ${E} .selo{background:#13234a;color:#93c5fd}
    ${E} :is(thead,thead tr,thead th){background:#0f172a;color:#94a3b8}
    ${E} :is(td,th,tbody tr,.acoes,.rodape,aside,header){border-color:#243049}
    ${E} .aviso.ok,${E} .selo-situacao.ok{background:#064e3b;color:#a7f3d0}
    ${E} .aviso.falha,${E} .selo-situacao.falha{background:#4c1d1d;color:#fecaca}
    ${E} .icone.azul{background:#13234a;color:#93c5fd}
    ${E} .icone.verde{background:#064e3b;color:#a7f3d0}
    ${E} .icone.vermelho{background:#4c1d1d;color:#fecaca}
    ${E} .btn-excluir-linha{background:transparent;border-color:#7f1d1d;color:#fca5a5}
    ${E} dialog::backdrop{background:rgba(0,0,0,.65)}
  `;
  const estilo = document.createElement("style");
  estilo.textContent = css;
  document.head.appendChild(estilo);
})();

/* ============ SESSÃO EXPIRADA ============ */
const _fetch = window.fetch;
window.fetch = async (...args) => {
  const resp = await _fetch(...args);
  if (resp.status === 401 && !String(args[0]).includes("/api/login")) {
    window.location.href = "/login.html";
  }
  return resp;
};

document.addEventListener("DOMContentLoaded", async () => {
  /* ----- botão de tema (ao lado do nome, no cabeçalho) ----- */
  const area = document.querySelector("header .usuario");
  if (area && !document.getElementById("tema")) {
    const lua = '<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>';
    const sol =
      '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>';
    const botao = document.createElement("button");
    botao.type = "button";
    botao.id = "tema";
    botao.className = "sino";

    const pintar = () => {
      const escuro = document.documentElement.dataset.tema === "escuro";
      botao.innerHTML = '<svg viewBox="0 0 24 24">' + (escuro ? sol : lua) + "</svg>";
      botao.setAttribute("aria-label", escuro ? "Usar tema claro" : "Usar tema escuro");
      botao.title = escuro ? "Tema claro" : "Tema escuro";
    };
    botao.addEventListener("click", () => {
      const novo = document.documentElement.dataset.tema === "escuro" ? "claro" : "escuro";
      document.documentElement.dataset.tema = novo;
      try { localStorage.setItem("tema", novo); } catch { }
      pintar();
    });
    pintar();
    area.insertBefore(botao, area.firstChild);
  }

  /* ----- nome de quem está logado + links do menu ----- */
  try {
    const r = await fetch("/api/eu");
    if (r.ok) {
      const u = await r.json();
      const selo = document.querySelector(".selo");
      if (selo) selo.textContent = u.nome;

      const nav = document.querySelector("aside nav");

      // link "Presença": aparece para todos os usuários
      if (nav && !document.getElementById("menu-presenca")) {
        const p = document.createElement("a");
        p.id = "menu-presenca";
        p.href = "presenca.html";
        if (location.pathname.endsWith("presenca.html")) p.className = "ativo";
        p.innerHTML =
          '<svg viewBox="0 0 24 24"><path d="M9 11l3 3 8-8"/><path d="M20 12v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h9"/></svg>Presen&ccedil;a';
        nav.appendChild(p);
      }

      // link "Usuários": só para administradores
      if (u.perfil === "Admin" && nav && !document.getElementById("menu-usuarios")) {
        const a = document.createElement("a");
        a.id = "menu-usuarios";
        a.href = "consultar-usuarios.html";
        if (location.pathname.endsWith("usuarios.html")) a.className = "ativo";
        a.innerHTML =
          '<svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>Usu&aacute;rios';
        nav.appendChild(a);
      }
    }
  } catch {
    /* mantém o texto padrão */
  }

  /* ----- botão Sair ----- */
  const sair = document.getElementById("sair");
  if (sair) {
    sair.addEventListener("click", async () => {
      await fetch("/api/logout", { method: "POST" });
      window.location.href = "/login.html";
    });
  }
});