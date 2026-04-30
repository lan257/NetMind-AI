using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed class MindMapTransferService : IMindMapTransferService
{
    private const string SchemaVersion = "netmind.mindmap.v1";

    private readonly IMindMapService _mindMapService;
    private readonly INodeService _nodeService;
    private readonly INodeRelationService _nodeRelationService;

    public MindMapTransferService(
        IMindMapService mindMapService,
        INodeService nodeService,
        INodeRelationService nodeRelationService)
    {
        _mindMapService = mindMapService;
        _nodeService = nodeService;
        _nodeRelationService = nodeRelationService;
    }

    public async Task<MindMapStructureDto?> ExportAsync(long mapId)
    {
        var map = await _mindMapService.GetAsync(mapId);
        if (map is null)
        {
            return null;
        }

        var nodes = await _nodeService.ListByMapAsync(mapId);
        var relations = await _nodeRelationService.ListByMapAsync(mapId);
        var transfer = new MindMapTransferDto
        {
            SchemaVersion = SchemaVersion,
            Title = map.Title,
            Nodes = nodes
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.OrderNo)
                .ThenBy(node => node.Id)
                .Select(node => new MindMapTransferNodeDto
                {
                    ClientId = ToClientNodeId(node.Id),
                    ParentClientId = node.ParentId.HasValue ? ToClientNodeId(node.ParentId.Value) : null,
                    Title = node.Title,
                    Content = node.Content,
                    OrderNo = node.OrderNo
                })
                .ToList(),
            Relations = relations
                .OrderBy(relation => relation.Id)
                .Select(relation => new MindMapTransferRelationDto
                {
                    SourceClientId = ToClientNodeId(relation.SourceId),
                    TargetClientId = ToClientNodeId(relation.TargetId),
                    RelationType = relation.RelationType,
                    Weight = relation.Weight
                })
                .ToList()
        };

        return new MindMapStructureDto
        {
            Map = map,
            Nodes = nodes,
            Relations = relations,
            Transfer = transfer
        };
    }

    public async Task<ImportedMindMapDto> ImportAsync(ImportMindMapRequest request)
    {
        var source = ValidateTransfer(request.MindMap);
        var title = string.IsNullOrWhiteSpace(request.TitleOverride) ? source.Title.Trim() : request.TitleOverride.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Mind map title is required.", nameof(request));
        }

        var createdMap = await _mindMapService.CreateAsync(new CreateMindMapRequest { Title = title });
        var nodeByClientId = source.Nodes.ToDictionary(node => node.ClientId.Trim(), StringComparer.Ordinal);
        var createdNodeIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in source.Nodes.OrderBy(node => node.ParentClientId is null ? 0 : 1).ThenBy(node => node.OrderNo))
        {
            await ImportNodeAsync(node.ClientId.Trim());
        }

        foreach (var relation in source.Relations)
        {
            var sourceId = createdNodeIds[relation.SourceClientId.Trim()];
            var targetId = createdNodeIds[relation.TargetClientId.Trim()];
            await _nodeRelationService.CreateAsync(new CreateNodeRelationRequest
            {
                MapId = createdMap.Id,
                SourceId = sourceId,
                TargetId = targetId,
                RelationType = relation.RelationType.Trim(),
                Weight = relation.Weight
            });
        }

        var structure = await ExportAsync(createdMap.Id);
        return new ImportedMindMapDto
        {
            Structure = structure ?? throw new InvalidOperationException("Imported mind map cannot be loaded."),
            NodeIdMap = createdNodeIds
        };

        async Task<long> ImportNodeAsync(string clientId)
        {
            if (createdNodeIds.TryGetValue(clientId, out var existingId))
            {
                return existingId;
            }

            if (!visiting.Add(clientId))
            {
                throw new ArgumentException($"Node parent cycle detected at '{clientId}'.", nameof(request));
            }

            var sourceNode = nodeByClientId[clientId];
            long? parentId = null;
            if (!string.IsNullOrWhiteSpace(sourceNode.ParentClientId))
            {
                parentId = await ImportNodeAsync(sourceNode.ParentClientId.Trim());
            }

            var createdNode = await _nodeService.CreateAsync(new CreateNodeRequest
            {
                MapId = createdMap.Id,
                ParentId = parentId,
                Title = sourceNode.Title.Trim(),
                Content = sourceNode.Content,
                OrderNo = sourceNode.OrderNo
            });

            visiting.Remove(clientId);
            createdNodeIds[clientId] = createdNode.Id;
            return createdNode.Id;
        }
    }

    public MindMapTransferDto CreateTemplate()
    {
        return new MindMapTransferDto
        {
            SchemaVersion = SchemaVersion,
            Title = "Imported mind map",
            Nodes = new[]
            {
                new MindMapTransferNodeDto
                {
                    ClientId = "root",
                    Title = "Root topic",
                    Content = "Describe the central topic.",
                    OrderNo = 1
                },
                new MindMapTransferNodeDto
                {
                    ClientId = "child-1",
                    ParentClientId = "root",
                    Title = "Child topic",
                    Content = "Describe a child topic.",
                    OrderNo = 1
                }
            },
            Relations = new[]
            {
                new MindMapTransferRelationDto
                {
                    SourceClientId = "root",
                    TargetClientId = "child-1",
                    RelationType = "relates_to",
                    Weight = 1
                }
            }
        };
    }

    private static MindMapTransferDto ValidateTransfer(MindMapTransferDto transfer)
    {
        if (!string.Equals(transfer.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException($"SchemaVersion must be '{SchemaVersion}'.", nameof(transfer));
        }

        if (string.IsNullOrWhiteSpace(transfer.Title))
        {
            throw new ArgumentException("Mind map title is required.", nameof(transfer));
        }

        if (transfer.Nodes.Count == 0)
        {
            throw new ArgumentException("Mind map must contain at least one node.", nameof(transfer));
        }

        var clientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in transfer.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.ClientId))
            {
                throw new ArgumentException("Node clientId is required.", nameof(transfer));
            }

            var clientId = node.ClientId.Trim();
            if (!clientIds.Add(clientId))
            {
                throw new ArgumentException($"Duplicate node clientId '{clientId}'.", nameof(transfer));
            }

            if (string.IsNullOrWhiteSpace(node.Title))
            {
                throw new ArgumentException($"Node '{clientId}' title is required.", nameof(transfer));
            }
        }

        foreach (var node in transfer.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.ParentClientId)))
        {
            if (!clientIds.Contains(node.ParentClientId!.Trim()))
            {
                throw new ArgumentException($"Parent node '{node.ParentClientId}' does not exist.", nameof(transfer));
            }
        }

        foreach (var relation in transfer.Relations)
        {
            if (string.IsNullOrWhiteSpace(relation.SourceClientId) || string.IsNullOrWhiteSpace(relation.TargetClientId))
            {
                throw new ArgumentException("Relation source and target are required.", nameof(transfer));
            }

            if (!clientIds.Contains(relation.SourceClientId.Trim()) || !clientIds.Contains(relation.TargetClientId.Trim()))
            {
                throw new ArgumentException("Relation source and target must exist in nodes.", nameof(transfer));
            }

            if (string.Equals(relation.SourceClientId.Trim(), relation.TargetClientId.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Relation source and target cannot be the same.", nameof(transfer));
            }

            if (string.IsNullOrWhiteSpace(relation.RelationType))
            {
                throw new ArgumentException("Relation type is required.", nameof(transfer));
            }

            if (relation.Weight < 0)
            {
                throw new ArgumentException("Relation weight must be greater than or equal to 0.", nameof(transfer));
            }
        }

        return transfer;
    }

    private static string ToClientNodeId(long nodeId)
    {
        return $"node-{nodeId}";
    }
}
