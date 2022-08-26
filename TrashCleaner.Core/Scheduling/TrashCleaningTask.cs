using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;

namespace TrashCleaner.Core.Scheduling;

public class TrashCleaningTask : RecurringHostedServiceBase
{
    private readonly ILogger<TrashCleaningTask>? _logger;
    private readonly IRuntimeState _runtimeState;
    private readonly IScopeProvider _scopeProvider;
    private readonly IContentService _contentService;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly ISqlContext _sqlContext;

    public TrashCleaningTask(
        ILogger<TrashCleaningTask>? logger,
        IRuntimeState runtimeState,
        IScopeProvider scopeProvider,
        IContentService contentService,
        IServerRoleAccessor serverRoleAccessor,
        ISqlContext sqlContext) : base(logger, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(0)) // TODO: Change to higher values when done testing
    {
        _logger = logger;
        _runtimeState = runtimeState;
        _scopeProvider = scopeProvider;
        _contentService = contentService;
        _serverRoleAccessor = serverRoleAccessor;
        _sqlContext = sqlContext;
    }

    public override Task PerformExecuteAsync(object? state)
    {
        // Fail if the site is not running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogInformation($"Skipped recycle bin cleanup due to the site not currently running");
            return Task.CompletedTask;
        }
        
        // Fail for LB sites that aren't the scheduler
        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
            case ServerRole.Unknown:
                _logger.LogInformation($"Skipped recycle bin cleanup due to this site having the ServerRole: {_serverRoleAccessor.CurrentServerRole}");
                return Task.CompletedTask;
        }

        // Wrap the three content service calls in a scope to do it all in one transaction.
        using (var scope = _scopeProvider.CreateScope(autoComplete:true))
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var query = new Query<IContent>(_sqlContext).Where(x => x.UpdateDate < thirtyDaysAgo);
            var recycledContent = _contentService.GetPagedContentInRecycleBin(0, 50, out long totalRecords, query);
            
            _logger.LogInformation($"Starting TrashCleaner, there is a total of {totalRecords} content nodes older than 30 days in the recycle bin.");

            foreach (var node in recycledContent)
            {
                var nodeName = node.Name;
                var nodeId = node.Id;
                var result = _contentService.Delete(node);
                if(result.Success)
                    _logger.LogInformation($"Successfully deleted content node with name: {nodeName} & id: {nodeId}");
            }
            
            _logger.LogInformation("Finished TrashCleaner");
        }

        return Task.CompletedTask;
    }
}