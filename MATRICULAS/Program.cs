using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
 
var builder = WebApplication.CreateBuilder(args);
 
// Login por cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "curso.auth";
        o.Cookie.HttpOnly = true;                                   // JavaScript não lê o cookie
        o.Cookie.SameSite = SameSiteMode.Strict;                    // protege contra pedidos vindos de outros sites
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // com HTTPS publicado: Always
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
 
var app = builder.Build();
 
string cs = builder.Configuration.GetConnectionString("Escola")!;
var hasher = new PasswordHasher<string>();
 
const string Colunas = "Id, Nome, Idade, Serie, Matriculado";
 
static AlunoDto Ler(SqlDataReader rd) => new(
    rd.GetInt32(0),
    rd.GetString(1),
    rd.IsDBNull(2) ? 0 : rd.GetInt32(2),
    rd.IsDBNull(3) ? "" : rd.GetString(3),
    rd.GetBoolean(4));
 
static string? Validar(AlunoEntrada a)
{
    if (string.IsNullOrWhiteSpace(a.Nome) || a.Nome.Trim().Length < 3) return "Informe o nome completo.";
    if (a.Idade < 5 || a.Idade > 100) return "Idade entre 5 e 100.";
    if (string.IsNullOrWhiteSpace(a.Serie)) return "Escolha a série.";
    return null;
}
 
async Task<SqlConnection> Abrir()
{
    var con = new SqlConnection(cs);
    await con.OpenAsync();
    return con;
}
 
// ===== Criar usuário pelo terminal (usado para o primeiro administrador) =====
// Uso: dotnet run -- criar-usuario
if (args.Length == 1 && args[0] == "criar-usuario")
{
    Console.Write("Nome: ");
    var nomeU = Console.ReadLine()?.Trim() ?? "";
    Console.Write("E-mail: ");
    var emailU = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
    Console.Write("Senha (mínimo 8 caracteres): ");
    var senhaU = Console.ReadLine() ?? "";
    Console.Write("Perfil (Admin ou Funcionario): ");
    var perfilU = Console.ReadLine()?.Trim() == "Admin" ? "Admin" : "Funcionario";
 
    if (nomeU.Length < 3 || !emailU.Contains('@') || senhaU.Length < 8)
    {
        Console.WriteLine("Dados inválidos. Nada foi criado.");
        return;
    }
 
    await using var conU = await Abrir();
    await using var cmdU = new SqlCommand(
        "INSERT INTO Usuario (Nome, Email, SenhaHash, Perfil) VALUES (@n, @e, @h, @p)", conU);
    cmdU.Parameters.AddWithValue("@n", nomeU);
    cmdU.Parameters.AddWithValue("@e", emailU);
    cmdU.Parameters.AddWithValue("@h", hasher.HashPassword("", senhaU));
    cmdU.Parameters.AddWithValue("@p", perfilU);
    try
    {
        await cmdU.ExecuteNonQueryAsync();
        Console.WriteLine("Usuário criado com sucesso.");
    }
    catch (SqlException ex) when (ex.Number is 2601 or 2627)
    {
        Console.WriteLine("Já existe um usuário com esse e-mail.");
    }
    return;
}
 
// ===== Porteiro: tudo exige login, menos a tela de login e a rota de login =====
app.UseAuthentication();
app.UseAuthorization();
 
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    bool logado = ctx.User.Identity?.IsAuthenticated ?? false;
    bool publico = path.Equals("/login.html", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/login", StringComparison.OrdinalIgnoreCase);
 
    if (!logado && !publico)
    {
        if (path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 401; return; }
        if (path.Value!.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Redirect("/login.html");
            return;
        }
    }
    await next();
});
 
app.UseStaticFiles();   // serve as páginas da pasta wwwroot
 
// Abre direto na tela de login
app.MapGet("/", () => Results.Redirect("/login.html"));
 
// ===== LOGIN =====
app.MapPost("/api/login", async (LoginEntrada e, HttpContext ctx) =>
{
    var email = (e.Email ?? "").Trim().ToLowerInvariant();
    string? hash = null;
    int id = 0;
    string nome = "", perfil = "";
 
    await using (var con = await Abrir())
    await using (var cmd = new SqlCommand(
        "SELECT Id, Nome, SenhaHash, Perfil FROM Usuario WHERE Email = @e AND Ativo = 1", con))
    {
        cmd.Parameters.AddWithValue("@e", email);
        await using var rd = await cmd.ExecuteReaderAsync();
        if (await rd.ReadAsync())
        {
            id = rd.GetInt32(0);
            nome = rd.GetString(1);
            hash = rd.GetString(2);
            perfil = rd.GetString(3);
        }
    }
 
    bool ok = hash != null &&
              hasher.VerifyHashedPassword("", hash, e.Senha ?? "") != PasswordVerificationResult.Failed;
 
    if (!ok)
    {
        await Task.Delay(700);   // atrapalha quem tenta adivinhar senhas em sequência
        // mesma mensagem para "e-mail não existe" e "senha errada"
        return Results.Json("E-mail ou senha incorretos.", statusCode: 401);
    }
 
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, id.ToString()),
        new(ClaimTypes.Name, nome),
        new(ClaimTypes.Email, email),
        new(ClaimTypes.Role, perfil)
    };
    var principal = new ClaimsPrincipal(
        new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Ok(new { nome, perfil });
});
 
// ===== SAIR =====
app.MapPost("/api/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});
 
// ===== QUEM ESTÁ LOGADO =====
app.MapGet("/api/eu", (ClaimsPrincipal u) =>
    Results.Ok(new { nome = u.Identity!.Name, perfil = u.FindFirst(ClaimTypes.Role)?.Value }));
 
// ===== CADASTRAR USUÁRIO (só administrador) =====
app.MapPost("/api/usuarios", async (UsuarioEntrada u, ClaimsPrincipal quem) =>
{
    if (!quem.IsInRole("Admin"))
        return Results.Json("Apenas administradores podem cadastrar usuários.", statusCode: 403);
 
    var email = (u.Email ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(u.Nome) || u.Nome.Trim().Length < 3) return Results.BadRequest("Informe o nome.");
    if (!email.Contains('@') || email.Length > 150) return Results.BadRequest("E-mail inválido.");
    if ((u.Senha ?? "").Length < 8) return Results.BadRequest("A senha deve ter pelo menos 8 caracteres.");
    var perfil = u.Perfil == "Admin" ? "Admin" : "Funcionario";
 
    try
    {
        await using var con = await Abrir();
        await using var cmd = new SqlCommand(
            "INSERT INTO Usuario (Nome, Email, SenhaHash, Perfil) VALUES (@n, @e, @h, @p)", con);
        cmd.Parameters.AddWithValue("@n", u.Nome.Trim());
        cmd.Parameters.AddWithValue("@e", email);
        cmd.Parameters.AddWithValue("@h", hasher.HashPassword("", u.Senha!));
        cmd.Parameters.AddWithValue("@p", perfil);
        await cmd.ExecuteNonQueryAsync();
        return Results.Created("/api/usuarios", null);
    }
    catch (SqlException ex) when (ex.Number is 2601 or 2627)
    {
        return Results.Conflict("Já existe um usuário com esse e-mail.");
    }
});
 
// ===== ALUNOS =====
 
// LISTAR
app.MapGet("/api/alunos", async () =>
{
    var lista = new List<AlunoDto>();
    await using var con = await Abrir();
    await using var cmd = new SqlCommand($"SELECT {Colunas} FROM Aluno ORDER BY Nome", con);
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync()) lista.Add(Ler(rd));
    return Results.Ok(lista);
});
 
// BUSCAR UM (usado pela tela de editar)
app.MapGet("/api/alunos/{id:int}", async (int id) =>
{
    await using var con = await Abrir();
    await using var cmd = new SqlCommand($"SELECT {Colunas} FROM Aluno WHERE Id = @id", con);
    cmd.Parameters.AddWithValue("@id", id);
    await using var rd = await cmd.ExecuteReaderAsync();
    return await rd.ReadAsync() ? Results.Ok(Ler(rd)) : Results.NotFound("Aluno não encontrado.");
});
 
// CADASTRAR
app.MapPost("/api/alunos", async (AlunoEntrada a) =>
{
    var erro = Validar(a);
    if (erro != null) return Results.BadRequest(erro);
 
    await using var con = await Abrir();
    await using var cmd = new SqlCommand(@"
        INSERT INTO Aluno (Nome, Idade, Serie, Matriculado)
        OUTPUT INSERTED.Id
        VALUES (@nome, @idade, @serie, @mat)", con);
    cmd.Parameters.AddWithValue("@nome", a.Nome.Trim());
    cmd.Parameters.AddWithValue("@idade", a.Idade);
    cmd.Parameters.AddWithValue("@serie", a.Serie);
    cmd.Parameters.AddWithValue("@mat", a.Matriculado);
    int novoId = (int)(await cmd.ExecuteScalarAsync())!;
    return Results.Created($"/api/alunos/{novoId}", new { id = novoId });
});
 
// EDITAR
app.MapPut("/api/alunos/{id:int}", async (int id, AlunoEntrada a) =>
{
    var erro = Validar(a);
    if (erro != null) return Results.BadRequest(erro);
 
    await using var con = await Abrir();
    await using var cmd = new SqlCommand(@"
        UPDATE Aluno SET Nome = @nome, Idade = @idade, Serie = @serie, Matriculado = @mat
        WHERE Id = @id", con);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.Parameters.AddWithValue("@nome", a.Nome.Trim());
    cmd.Parameters.AddWithValue("@idade", a.Idade);
    cmd.Parameters.AddWithValue("@serie", a.Serie);
    cmd.Parameters.AddWithValue("@mat", a.Matriculado);
    int linhas = await cmd.ExecuteNonQueryAsync();
    return linhas == 0 ? Results.NotFound("Aluno não encontrado.") : Results.NoContent();
});
 
// EXCLUIR
app.MapDelete("/api/alunos/{id:int}", async (int id) =>
{
    try
    {
        await using var con = await Abrir();
        await using var cmd = new SqlCommand("DELETE FROM Aluno WHERE Id = @id", con);
        cmd.Parameters.AddWithValue("@id", id);
        int linhas = await cmd.ExecuteNonQueryAsync();
        return linhas == 0 ? Results.NotFound("Aluno não encontrado.") : Results.NoContent();
    }
    catch (SqlException ex) when (ex.Number == 547)   // chave estrangeira
    {
        return Results.Conflict("Este aluno tem matrículas e não pode ser excluído.");
    }
});
 
// INÍCIO: números dos cards + alunos recentes
app.MapGet("/api/inicio", async () =>
{
    await using var con = await Abrir();
 
    int total = 0, matriculados = 0;
    await using (var cmd = new SqlCommand(@"
        SELECT COUNT(*),
               COALESCE(SUM(CASE WHEN Matriculado = 1 THEN 1 ELSE 0 END), 0)
        FROM Aluno", con))
    await using (var rd = await cmd.ExecuteReaderAsync())
    {
        if (await rd.ReadAsync())
        {
            total = rd.GetInt32(0);
            matriculados = rd.GetInt32(1);
        }
    }
 
    var recentes = new List<AlunoRecente>();
    await using (var cmd = new SqlCommand(
        "SELECT TOP 5 Nome, Serie, Matriculado FROM Aluno ORDER BY Id DESC", con))
    await using (var rd = await cmd.ExecuteReaderAsync())
    {
        while (await rd.ReadAsync())
            recentes.Add(new AlunoRecente(
                rd.GetString(0),
                rd.IsDBNull(1) ? "" : rd.GetString(1),
                rd.GetBoolean(2)));
    }
 
    return Results.Ok(new InicioDto(
        new ResumoDto(total, matriculados, total - matriculados),
        recentes));
});
 
app.Run();
 
record AlunoDto(int Id, string Nome, int Idade, string Serie, bool Matriculado);
record AlunoEntrada(string Nome, int Idade, string Serie, bool Matriculado);
record ResumoDto(int Total, int Matriculados, int NaoMatriculados);
record AlunoRecente(string Nome, string Serie, bool Matriculado);
record InicioDto(ResumoDto Resumo, List<AlunoRecente> Recentes);
record LoginEntrada(string Email, string Senha);
record UsuarioEntrada(string Nome, string Email, string Senha, string Perfil);
 