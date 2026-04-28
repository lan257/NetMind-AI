using NetMind.Models.Entities;

namespace NetMind.Repository.Interfaces;

public interface IMindMapRepository
{
    Task<IReadOnlyList<MindMapEntity>> ListAsync();

    Task<MindMapEntity?> GetAsync(long id);

    Task<MindMapEntity> CreateAsync(string title);

    Task<MindMapEntity?> UpdateAsync(long id, string title, long? rootNodeId);

    Task<int> DeleteAsync(long id, bool cascade);
}
