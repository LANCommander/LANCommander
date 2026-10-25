using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's taxonomy records without exposing the database model.</summary>
internal sealed class TaxonomyProvider(
    CategoryService categories,
    CollectionService collections,
    CompanyService companies,
    EngineService engines,
    GenreService genres,
    PlatformService platforms,
    TagService tags) : ITaxonomyProvider
{
    public Task<IReadOnlyList<TaxonomyInfo>> GetAsync(TaxonomyKind kind, CancellationToken cancellationToken = default) =>
        For(kind).GetAsync(cancellationToken);

    public Task<TaxonomyInfo?> GetAsync(TaxonomyKind kind, Guid id, CancellationToken cancellationToken = default) =>
        For(kind).GetAsync(id, cancellationToken);

    public Task<TaxonomyInfo?> FindAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default) =>
        For(kind).FindAsync(name, cancellationToken);

    public async Task<TaxonomyInfo> GetOrAddAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default) =>
        await FindAsync(kind, name, cancellationToken) ?? await AddAsync(kind, name, cancellationToken);

    public Task<TaxonomyInfo> AddAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default) =>
        For(kind).AddAsync(name, cancellationToken);

    public Task<TaxonomyInfo> RenameAsync(TaxonomyKind kind, Guid id, string name, CancellationToken cancellationToken = default) =>
        For(kind).RenameAsync(id, name, cancellationToken);

    public Task DeleteAsync(TaxonomyKind kind, Guid id, CancellationToken cancellationToken = default) =>
        For(kind).DeleteAsync(id, cancellationToken);

    ITaxonomyStore For(TaxonomyKind kind) => kind switch
    {
        TaxonomyKind.Category => new TaxonomyStore<Category>(categories, kind),
        TaxonomyKind.Collection => new TaxonomyStore<Collection>(collections, kind),
        TaxonomyKind.Company => new TaxonomyStore<Company>(companies, kind),
        TaxonomyKind.Engine => new TaxonomyStore<Engine>(engines, kind),
        TaxonomyKind.Genre => new TaxonomyStore<Genre>(genres, kind),
        TaxonomyKind.Platform => new TaxonomyStore<Platform>(platforms, kind),
        TaxonomyKind.Tag => new TaxonomyStore<Tag>(tags, kind),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    interface ITaxonomyStore
    {
        Task<IReadOnlyList<TaxonomyInfo>> GetAsync(CancellationToken cancellationToken);
        Task<TaxonomyInfo?> GetAsync(Guid id, CancellationToken cancellationToken);
        Task<TaxonomyInfo?> FindAsync(string name, CancellationToken cancellationToken);
        Task<TaxonomyInfo> AddAsync(string name, CancellationToken cancellationToken);
        Task<TaxonomyInfo> RenameAsync(Guid id, string name, CancellationToken cancellationToken);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }

    /// <summary>One kind of taxonomy record; they differ only in their table.</summary>
    sealed class TaxonomyStore<T>(BaseDatabaseService<T> service, TaxonomyKind kind) : ITaxonomyStore
        where T : BaseTaxonomyModel, new()
    {
        public async Task<IReadOnlyList<TaxonomyInfo>> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var records = await service.GetAsync(query => query.Select(record => new { record.Id, record.Name }));

            return records
                .Select(record => Info(record.Id, record.Name))
                .OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<TaxonomyInfo?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await service.GetAsync(id);

            return record == null ? null : Info(record.Id, record.Name);
        }

        public async Task<TaxonomyInfo?> FindAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lowered = name.Trim().ToLower();
            var record = await service.FirstOrDefaultAsync(record => record.Name.ToLower() == lowered);

            return record == null ? null : Info(record.Id, record.Name);
        }

        public async Task<TaxonomyInfo> AddAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await service.AddAsync(new T { Name = Required(name) });

            return Info(record.Id, record.Name);
        }

        public async Task<TaxonomyInfo> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = RecordGuard.Found(await service.Include(r => r.CreatedBy).GetAsync(id), kind.ToString().ToLowerInvariant(), id);

            record.Name = Required(name);

            await service.UpdateAsync(record);

            return Info(record.Id, record.Name);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await service.GetAsync(id);

            if (record != null)
                await service.DeleteAsync(record);
        }

        TaxonomyInfo Info(Guid id, string name) => new() { Id = id, Kind = kind, Name = name };

        static string Required(string name) =>
            string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A name is required.", nameof(name)) : name.Trim();
    }
}
