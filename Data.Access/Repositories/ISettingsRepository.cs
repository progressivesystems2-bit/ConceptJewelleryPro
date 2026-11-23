using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface ISettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    Task SaveAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task SaveManyAsync(IEnumerable<KeyValuePair<string, string?>> settings, CancellationToken cancellationToken = default);
}
