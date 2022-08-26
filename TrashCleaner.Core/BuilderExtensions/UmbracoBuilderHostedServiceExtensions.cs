using Microsoft.Extensions.DependencyInjection;
using TrashCleaner.Core.Scheduling;
using Umbraco.Cms.Core.DependencyInjection;

namespace TrashCleaner.Core.BuilderExtensions;

public static class UmbracoBuilderHostedServiceExtensions
{
    public static IUmbracoBuilder AddCustomHostedServices(this IUmbracoBuilder builder)
    {
        builder.Services.AddHostedService<TrashCleaningTask>();
        return builder;
    }
}