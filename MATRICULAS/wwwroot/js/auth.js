// Se a sessão expirar, volta para o login
const _fetch = window.fetch;
window.fetch = async (...args) => {
  const resp = await _fetch(...args);
  if (resp.status === 401 && !String(args[0]).includes('/api/login')) {
    window.location.href = '/login.html';
  }
  return resp;
};

document.addEventListener('DOMContentLoaded', async () => {
  // mostra o nome de quem está logado no lugar de "Administrador"
  const selo = document.querySelector('.selo');
  if (selo) {
    try {
      const r = await fetch('/api/eu');
      if (r.ok) selo.textContent = (await r.json()).nome;
    } catch { /* mantém o texto */ }
  }

  // botão Sair
  const sair = document.getElementById('sair');
  if (sair) {
    sair.addEventListener('click', async () => {
      await fetch('/api/logout', { method: 'POST' });
      window.location.href = '/login.html';
    });
  }
});