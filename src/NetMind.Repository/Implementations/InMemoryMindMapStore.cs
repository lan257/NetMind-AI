using NetMind.Models.Entities;

namespace NetMind.Repository.Implementations;

public sealed class InMemoryMindMapStore
{
    private long _nextMapId = 1004;
    private long _nextNodeId = 2024;
    private long _nextRelationId = 3004;

    public InMemoryMindMapStore()
    {
        var now = DateTimeOffset.UtcNow;

        MindMaps.AddRange(new[]
        {
            new MindMapEntity { Id = 1001, Title = "产品需求知识图谱", RootNodeId = 2001, CreatedAt = now, UpdatedAt = now },
            new MindMapEntity { Id = 1002, Title = "技术方案知识图谱", RootNodeId = 2011, CreatedAt = now, UpdatedAt = now },
            new MindMapEntity { Id = 1003, Title = "项目计划知识图谱", RootNodeId = 2021, CreatedAt = now, UpdatedAt = now }
        });

        Nodes.AddRange(new[]
        {
            new NodeEntity { Id = 2001, MapId = 1001, Title = "需求总览", Content = "记录产品需求、用户确认和结构化导入流程。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2002, MapId = 1001, ParentId = 2001, Title = "用户确认", Content = "导入数据库前允许用户调整和校验结构化数据。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2003, MapId = 1001, ParentId = 2001, Title = "结构化导入", Content = "将清洗后的标准结构导入数据库持久化保存。", OrderNo = 2, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2011, MapId = 1002, Title = "技术总览", Content = "后端使用 WebAPI，前端使用 Vue3，数据库使用 PostgreSQL。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2012, MapId = 1002, ParentId = 2011, Title = "后端服务", Content = "负责数据管理、用户管理和 AI 调用。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2013, MapId = 1002, ParentId = 2011, Title = "前端展示", Content = "负责思维导图展示和基础交互。", OrderNo = 2, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2021, MapId = 1003, Title = "P0 数据闭环 Demo", Content = "完成手动创建、存储和展示思维导图的最小闭环。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2022, MapId = 1003, ParentId = 2021, Title = "P0.1 最小工程", Content = "建立 .NET 和 Vue3 最小可运行工程。", OrderNo = 1, CreatedAt = now, UpdatedAt = now },
            new NodeEntity { Id = 2023, MapId = 1003, ParentId = 2021, Title = "P0.3 增删改查", Content = "支持思维导图、节点和节点关联核心接口。", OrderNo = 2, CreatedAt = now, UpdatedAt = now }
        });

        Relations.AddRange(new[]
        {
            new NodeRelationEntity { Id = 3001, SourceId = 2002, TargetId = 2003, RelationType = "depends_on", Weight = 0.8, MapId = 1001, CreatedAt = now },
            new NodeRelationEntity { Id = 3002, SourceId = 2012, TargetId = 2013, RelationType = "supports", Weight = 0.7, MapId = 1002, CreatedAt = now },
            new NodeRelationEntity { Id = 3003, SourceId = 2022, TargetId = 2023, RelationType = "next_step", Weight = 1, MapId = 1003, CreatedAt = now }
        });
    }

    public object SyncRoot { get; } = new();

    public List<MindMapEntity> MindMaps { get; } = new();

    public List<NodeEntity> Nodes { get; } = new();

    public List<NodeRelationEntity> Relations { get; } = new();

    public long NextMapId()
    {
        return _nextMapId++;
    }

    public long NextNodeId()
    {
        return _nextNodeId++;
    }

    public long NextRelationId()
    {
        return _nextRelationId++;
    }
}
