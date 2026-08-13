using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgendadorContas.Data;

public sealed class AgendadorDbContextFactory : IDesignTimeDbContextFactory<AgendadorDbContext>
{
    public AgendadorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENDADOR_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=agendador_design;Username=agendador_design";
        var options = new DbContextOptionsBuilder<AgendadorDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AgendadorDbContext(options);
    }
}
