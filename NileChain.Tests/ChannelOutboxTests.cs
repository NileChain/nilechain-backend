using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.Application.Interfaces;
using NileChain.Application.Services;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;

namespace NileChain.Tests;

public class ChannelOutboxTests
{
    [Fact]
    public async Task Enqueue_WritesLoggedRow_WithoutHttp()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var outbound = new LoggingOutboundChannel(
            new ChannelMessageRepository(harness.Db),
            new UnitOfWork(harness.Db),
            NullLogger<LoggingOutboundChannel>.Instance);

        var contractId = Guid.NewGuid();
        await outbound.EnqueueWhatsAppAsync(
            Guid.NewGuid(),
            "01550000001",
            ChannelTemplates.ContractSigned,
            "Both parties signed the supply contract.",
            "Contract",
            contractId);

        var rows = await harness.Db.ChannelMessages.AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("WhatsApp", rows[0].Channel);
        Assert.Equal(ChannelTemplates.ContractSigned, rows[0].TemplateKey);
        Assert.Equal(ChannelMessageStatus.Logged, rows[0].Status);
        Assert.Equal(contractId, rows[0].RelatedEntityId);
        Assert.Equal("01550000001", rows[0].ToPhone);
    }

    [Fact]
    public async Task ListForAdmin_ReturnsStubDisclaimer()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var outbound = new LoggingOutboundChannel(
            new ChannelMessageRepository(harness.Db),
            new UnitOfWork(harness.Db),
            NullLogger<LoggingOutboundChannel>.Instance);

        await outbound.EnqueueWhatsAppAsync(
            null, "01000000000", ChannelTemplates.Shipped, "Shipped.", "Contract", Guid.NewGuid());

        var listed = await outbound.ListForAdminAsync(null);
        Assert.True(listed.IsSuccess);
        Assert.Contains("does not send", listed.Value!.Disclaimer, StringComparison.OrdinalIgnoreCase);
        Assert.Single(listed.Value.Items);
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;

        private SqliteHarness(SqliteConnection keepAlive, NileChainDbContext db)
        {
            _keepAlive = keepAlive;
            Db = db;
        }

        public NileChainDbContext Db { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connectionString = $"DataSource=file:channel-{Guid.NewGuid():N}?mode=memory&cache=shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NileChainDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var db = new SqliteLifecycleDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteHarness(keepAlive, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }

    private sealed class SqliteLifecycleDbContext : NileChainDbContext
    {
        public SqliteLifecycleDbContext(DbContextOptions<NileChainDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Contract>()
                .Property(c => c.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<FarmMatch>()
                .Property(m => m.EligibilitySnapshotJson)
                .HasColumnType("TEXT");
        }
    }
}
