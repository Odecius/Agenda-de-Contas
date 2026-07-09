using System.Text.Json;
using AgendadorContas.Models;

namespace AgendadorContas.Services;

public sealed class ContaStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<ContaStore> _logger;

    public ContaStore(IConfiguration configuration, IWebHostEnvironment environment, ILogger<ContaStore> logger)
    {
        _logger = logger;
        var configuredPath = configuration["Data:FilePath"];
        _filePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "data", "contas.json")
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);
    }

    public async Task<IReadOnlyList<Conta>> ListarContasAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            return db.Contas.OrderBy(c => c.DiaVencimento).ThenBy(c => c.Nome).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Conta> CriarContaAsync(ContaCreateRequest request)
    {
        Validar(request);

        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            var conta = new Conta
            {
                Id = Guid.NewGuid(),
                Nome = request.Nome.Trim(),
                Valor = request.Valor,
                DiaVencimento = request.DiaVencimento,
                DataInicio = request.DataInicio,
                DuracaoMeses = request.DuracaoMeses,
                Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim(),
                Ativa = true
            };

            db.Contas.Add(conta);
            await WriteAsync(db);
            return conta;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Conta?> AtualizarContaAsync(Guid id, ContaCreateRequest request)
    {
        Validar(request);

        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            var conta = db.Contas.FirstOrDefault(c => c.Id == id);
            if (conta is null)
            {
                return null;
            }

            conta.Nome = request.Nome.Trim();
            conta.Valor = request.Valor;
            conta.DiaVencimento = request.DiaVencimento;
            conta.DataInicio = request.DataInicio;
            conta.DuracaoMeses = request.DuracaoMeses;
            conta.Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim();

            await WriteAsync(db);
            return conta;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AlternarAtivaAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            var conta = db.Contas.FirstOrDefault(c => c.Id == id);
            if (conta is null)
            {
                return false;
            }

            conta.Ativa = !conta.Ativa;
            await WriteAsync(db);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExcluirContaAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            var removed = db.Contas.RemoveAll(c => c.Id == id) > 0;
            if (!removed)
            {
                return false;
            }

            db.Pagamentos.RemoveAll(p => p.ContaId == id);
            await WriteAsync(db);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ContaVencimento>> ListarVencimentosAsync(DateOnly data)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            return db.Contas
                .Where(c => EstaAtivaNoMes(c, data))
                .Select(c => new ContaVencimento
                {
                    Conta = c,
                    DataVencimento = VencimentoNoMes(c, data),
                    Pago = db.Pagamentos.Any(p => p.ContaId == c.Id && p.Ano == data.Year && p.Mes == data.Month)
                })
                .OrderBy(v => v.DataVencimento)
                .ThenBy(v => v.Conta.Nome)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ContaVencimento>> ListarVencimentosDoDiaAsync(DateOnly data)
    {
        var vencimentos = await ListarVencimentosAsync(data);
        return vencimentos.Where(v => v.DataVencimento == data && !v.Pago).ToList();
    }

    public async Task<bool> MarcarPagamentoAsync(Guid contaId, int ano, int mes)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            if (db.Contas.All(c => c.Id != contaId))
            {
                return false;
            }

            if (db.Pagamentos.All(p => p.ContaId != contaId || p.Ano != ano || p.Mes != mes))
            {
                db.Pagamentos.Add(new Pagamento { ContaId = contaId, Ano = ano, Mes = mes });
                await WriteAsync(db);
            }

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DesmarcarPagamentoAsync(Guid contaId, int ano, int mes)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            var removed = db.Pagamentos.RemoveAll(p => p.ContaId == contaId && p.Ano == ano && p.Mes == mes) > 0;
            if (removed)
            {
                await WriteAsync(db);
            }

            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> LembreteJaEnviadoAsync(DateOnly data)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            return db.LembretesEnviados.Any(l => l.Data == data);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RegistrarLembreteEnviadoAsync(DateOnly data)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await ReadAsync();
            if (db.LembretesEnviados.All(l => l.Data != data))
            {
                db.LembretesEnviados.Add(new LembreteEnviado { Data = data });
                await WriteAsync(db);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void Validar(ContaCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            throw new ArgumentException("Informe o nome da conta.");
        }

        if (request.Valor <= 0)
        {
            throw new ArgumentException("O valor deve ser maior que zero.");
        }

        if (request.DiaVencimento is < 1 or > 31)
        {
            throw new ArgumentException("O dia de vencimento deve estar entre 1 e 31.");
        }

        if (request.DuracaoMeses < 0)
        {
            throw new ArgumentException("A duracao nao pode ser negativa. Use 0 para duracao indeterminada.");
        }
    }

    private static bool EstaAtivaNoMes(Conta conta, DateOnly data)
    {
        if (!conta.Ativa)
        {
            return false;
        }

        var mesesDesdeInicio = ((data.Year - conta.DataInicio.Year) * 12) + data.Month - conta.DataInicio.Month;
        if (mesesDesdeInicio < 0)
        {
            return false;
        }

        return conta.DuracaoMeses == 0 || mesesDesdeInicio < conta.DuracaoMeses;
    }

    private static DateOnly VencimentoNoMes(Conta conta, DateOnly data)
    {
        var ultimoDiaDoMes = DateTime.DaysInMonth(data.Year, data.Month);
        return new DateOnly(data.Year, data.Month, Math.Min(conta.DiaVencimento, ultimoDiaDoMes));
    }

    private async Task<StoreData> ReadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new StoreData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<StoreData>(stream, JsonOptions) ?? new StoreData();
        }
        catch (JsonException ex)
        {
            var backupPath = BackupCorruptedFile();
            _logger.LogError(ex, "Arquivo de dados invalido. Um backup foi criado em {BackupPath}.", backupPath);
            return new StoreData();
        }
    }

    private async Task WriteAsync(StoreData db)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, db, JsonOptions);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private string BackupCorruptedFile()
    {
        var backupPath = $"{_filePath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
        File.Move(_filePath, backupPath);
        return backupPath;
    }

    private sealed class StoreData
    {
        public List<Conta> Contas { get; set; } = [];
        public List<Pagamento> Pagamentos { get; set; } = [];
        public List<LembreteEnviado> LembretesEnviados { get; set; } = [];
    }
}
