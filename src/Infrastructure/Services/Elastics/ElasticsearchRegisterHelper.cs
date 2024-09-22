using System.Reflection;
using Contracts.Dtos.Models;
using Contracts.Extensions;
using Domain.Aggregates.AuditLogs;
using Domain.Aggregates.AuditLogs.Enums;
using Domain.Aggregates.Users.Enums;
using Domain.Common.ElasticConfigurations;
using Elastic.Clients.Elasticsearch;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Serilog;

namespace Infrastructure.Services.Elastics;

public class ElasticsearchRegisterHelper
{
    /// <summary>
    /// Execute connection mapping config
    /// </summary>
    /// <param name="connectionSettings"></param>
    /// <param name="elsConfigs"></param>
    public static void ConfigureConnectionSettings(
        ref ElasticsearchClientSettings connectionSettings,
        IEnumerable<ElasticConfigureResult> elsConfigs
    )
    {
        foreach (var elsConfig in elsConfigs)
        {
            object? connectionSettingEvaluator = Activator.CreateInstance(
                typeof(ConnectionSettingEvaluator),
                [connectionSettings]
            );

            var evaluateMethodInfo = typeof(ConnectionSettingEvaluator)
                .GetMethod(nameof(IEvaluator.Evaluate))!
                .MakeGenericMethod(elsConfig.Type);

            evaluateMethodInfo.Invoke(connectionSettingEvaluator, [elsConfig.Configs]);
        }
    }

    /// <summary>
    /// execute config classes by reflection
    /// </summary>
    /// <param name="elasticClient"></param>
    /// <param name="elsConfigs"></param>
    /// <returns></returns>
    public static async Task ElasticFluentConfigAsync(
        ElasticsearchClient elasticClient,
        IEnumerable<ElasticConfigureResult> elsConfigs
    )
    {
        foreach (var elsConfig in elsConfigs)
        {
            object? elasticsearchClientEvaluator = Activator.CreateInstance(
                typeof(ElasticsearchClientEvaluator),
                [elasticClient]
            );

            var evaluateMethodInfo = typeof(ElasticsearchClientEvaluator)
                .GetMethod(nameof(IEvaluator.Evaluate))!
                .MakeGenericMethod(elsConfig.Type);

            await (Task)evaluateMethodInfo.Invoke(elasticsearchClientEvaluator, [elsConfig.Configs])!;
        }
    }

    public static async Task SeedingAsync(ElasticsearchClient elasticsearchClient)
    {
        var auditLog = await elasticsearchClient.SearchAsync<AuditLog>();
        if (auditLog.Documents.Count > 0)
        {
            return;
        }
        string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        List<AuditLog> auditLogs = [];
        for (int i = 0; i < 10; i++)
        {
            string entity =
                $"{StringExtension.GenerateRandomString(4, allowedChars)} {StringExtension.GenerateRandomString(4, allowedChars)} {i}";

            int[] types = Enum.GetValues(typeof(AuditLogType)).Cast<int>().ToArray();
            int index = new Random().Next(0, types.Length - 1);

            auditLogs.Add(
                new()
                {
                    Id = Ulid.NewUlid().ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Entity = entity,
                    Type = (AuditLogType)index,
                    ActionPerformBy = Ulid.NewUlid().ToString(),
                    Agent = new()
                    {
                        Id = Ulid.NewUlid().ToString(),
                        CreatedAt = DateTimeOffset.UtcNow,
                        Gender = (Gender)new Random().Next(0, 1),
                        FirstName = $"{StringExtension.GenerateRandomString(4, allowedChars)} {i}",
                        LastName = $"{StringExtension.GenerateRandomString(4, allowedChars)} {i}",
                        Email = $"anna.kim{i}@gmail.com",
                        DayOfBirth = new DateTime(1990, 1, 1 + i),
                    },
                }
            );
        }

        BulkResponse response = await elasticsearchClient.BulkAsync(x =>
            x.Index(ElsIndexExtension.GetName<AuditLog>()).IndexMany(auditLogs)
        );

        if (response.IsSuccess())
        {
            Log.Information("Elasticsearch has seeded.");
        }
        else
        {
            Log.Information(
                "Elasticsearch has been failed in seeding with {debug}",
                response.DebugInformation
            );
        }
    }

    /// <summary>
    /// get all of config classes by reflection
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static IEnumerable<ElasticConfigureResult> GetElasticsearchConfigBuilder(
        Assembly assembly
    )
    {
        var configuringTypes = GetConfiguringTypes(assembly);

        foreach (var (type, iType) in configuringTypes)
        {
            var method = GetConfigureMethod(type);
            if (method == null)
                continue;

            var elasticsearchConfigBuilder = CreateElasticsearchConfigBuilder(iType);
            var elsConfig = Activator.CreateInstance(type);

            method.Invoke(elsConfig, [elasticsearchConfigBuilder]);

            yield return new ElasticConfigureResult(elasticsearchConfigBuilder!, iType);
        }
    }

    private static IEnumerable<(Type type, Type iType)> GetConfiguringTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type =>
                type.GetInterfaces().Any(@interface => IsElasticsearchDocumentConfigure(@interface))
            )
            .Select(type =>
                (
                    type,
                    iType: type.GetInterfaces()
                        .First(@interface => IsElasticsearchDocumentConfigure(@interface))
                        .GenericTypeArguments[0]
                )
            );
    }

    private static bool IsElasticsearchDocumentConfigure(Type @interface)
    {
        return @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == typeof(IElasticsearchDocumentConfigure<>);
    }

    private static MethodInfo? GetConfigureMethod(Type type)
    {
        return type.GetMethod(nameof(IElasticsearchDocumentConfigure<object>.Configure));
    }

    private static object CreateElasticsearchConfigBuilder(Type documentType)
    {
        var builderType = typeof(ElasticsearchConfigBuilder<>).MakeGenericType(documentType);
        return Activator.CreateInstance(builderType)!;
    }
}
