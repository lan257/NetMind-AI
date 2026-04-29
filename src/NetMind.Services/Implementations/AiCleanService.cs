using System.Text.RegularExpressions;
using NetMind.Models.Dtos;
using NetMind.Services.Interfaces;

namespace NetMind.Services.Implementations;

public sealed partial class AiCleanService : IAiCleanService
{
    private static readonly IReadOnlyList<AiModelOptionDto> Models = new[]
    {
        new AiModelOptionDto
        {
            Id = "local-deepseek-placeholder",
            Name = "DeepSeek Local Placeholder",
            Provider = "local",
            Endpoint = "placeholder://local/deepseek",
            IsDefault = true,
            Status = "placeholder",
            Notes = "P1.1 uses this first hard-coded model option and runs a deterministic local cleaner."
        },
        new AiModelOptionDto
        {
            Id = "cloud-model-placeholder",
            Name = "Cloud Model Placeholder",
            Provider = "cloud",
            Endpoint = "placeholder://cloud/api",
            IsDefault = false,
            Status = "disabled",
            Notes = "Reserved for paid cloud model integration in a later phase."
        }
    };

    public IReadOnlyList<AiModelOptionDto> ListModels()
    {
        return Models;
    }

    public AiCleanResultDto Clean(AiCleanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NaturalLanguage))
        {
            throw new ArgumentException("Natural language input is required.", nameof(request));
        }

        var selectedModel = SelectModel(request.ModelId);
        var lines = NormalizeLines(request.NaturalLanguage);
        var rootTitle = ToTitle(lines[0], "AI cleaned mind map");
        var nodes = new List<MindMapTransferNodeDto>
        {
            new()
            {
                ClientId = "root",
                Title = rootTitle,
                Content = lines[0],
                OrderNo = 1
            }
        };

        var warnings = new List<string>();
        var itemLines = lines.Skip(1).ToList();
        if (itemLines.Count == 0)
        {
            itemLines = SplitIntoItems(lines[0]).ToList();
            warnings.Add("Only one paragraph was provided; the cleaner split it into topic items.");
        }

        var order = 1;
        foreach (var item in itemLines)
        {
            var title = ToTitle(item, $"Topic {order}");
            nodes.Add(new MindMapTransferNodeDto
            {
                ClientId = $"topic-{order}",
                ParentClientId = "root",
                Title = title,
                Content = item,
                OrderNo = order
            });
            order++;
        }

        if (nodes.Count == 1)
        {
            nodes.Add(new MindMapTransferNodeDto
            {
                ClientId = "topic-1",
                ParentClientId = "root",
                Title = "Clarify scope",
                Content = "Add more details before importing this structure.",
                OrderNo = 1
            });
            warnings.Add("No child topic was detected; a clarification node was added.");
        }

        var relations = nodes
            .Where(node => node.ClientId != "root")
            .Select(node => new MindMapTransferRelationDto
            {
                SourceClientId = "root",
                TargetClientId = node.ClientId,
                RelationType = "expands_to",
                Weight = 1
            })
            .ToList();

        return new AiCleanResultDto
        {
            SelectedModel = selectedModel,
            Prompt = BuildPrompt(request.NaturalLanguage),
            Transfer = new MindMapTransferDto
            {
                SchemaVersion = "netmind.mindmap.v1",
                Title = rootTitle,
                Nodes = nodes,
                Relations = relations
            },
            Warnings = warnings
        };
    }

    private static AiModelOptionDto SelectModel(string? requestedModelId)
    {
        if (string.IsNullOrWhiteSpace(requestedModelId))
        {
            return Models[0];
        }

        return Models.FirstOrDefault(model => string.Equals(model.Id, requestedModelId, StringComparison.Ordinal))
            ?? Models[0];
    }

    private static IReadOnlyList<string> NormalizeLines(string input)
    {
        return input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => BulletPrefixRegex().Replace(line.Trim(), string.Empty).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static IEnumerable<string> SplitIntoItems(string input)
    {
        return SentenceSplitRegex()
            .Split(input)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Take(6);
    }

    private static string ToTitle(string value, string fallback)
    {
        var cleaned = value.Trim();
        if (cleaned.Length == 0)
        {
            return fallback;
        }

        return cleaned.Length <= 36 ? cleaned : cleaned[..36];
    }

    private static string BuildPrompt(string naturalLanguage)
    {
        return "Convert the user's natural language into netmind.mindmap.v1 JSON with title, nodes and relations.\n\n"
            + naturalLanguage.Trim();
    }

    [GeneratedRegex(@"^([#>*\-\d\.\)\s]+)")]
    private static partial Regex BulletPrefixRegex();

    [GeneratedRegex(@"[。！？.!?；;]+")]
    private static partial Regex SentenceSplitRegex();
}
