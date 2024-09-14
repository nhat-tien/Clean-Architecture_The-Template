using Application.Common.Interfaces.Registers;
using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Services;
using Application.Common.Interfaces.Services.Identity;
using Application.Common.Interfaces.Services.Mail;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Services.Aws;
using Infrastructure.Services.Identity;
using Infrastructure.Services.Mail;
using Infrastructure.Services.Token;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDetection();
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        return services
            .AddScoped<IDbContext, TheDbContext>()
            .AddTransient<IUnitOfWork, UnitOfWork>()
            .AddSingleton<UpdateBaseEntityInterceptor>()
            .AddDbContext<TheDbContext>(
                (sp, options) =>
                    options
                        .UseNpgsql(configuration.GetConnectionString("default"))
                        .AddInterceptors(sp.GetRequiredService<UpdateBaseEntityInterceptor>())
            )
            .AddAmazonS3(configuration)
            .AddSingleton<ICurrentUser, CurrentUserService>()
            .AddScoped(typeof(IAvatarUpdateService<>), typeof(AvatarUpdateService<>))
            .AddTransient<KitMailService>()
            .AddTransient<IMailService, KitMailService>(provider =>
                provider.GetService<KitMailService>()!
            )
            .AddTransient<FluentMailService>()
            .AddTransient<IMailService, FluentMailService>(provider =>
                provider.GetService<FluentMailService>()!
            )
            .AddSingleton<Mailer>()
            .Scan(scan =>
                scan.FromCallingAssembly()
                    .AddClasses(classes => classes.AssignableTo<IScope>())
                    .AsImplementedInterfaces()
                    .WithScopedLifetime()
                    .AddClasses(classes => classes.AssignableTo<ISingleton>())
                    .AsImplementedInterfaces()
                    .WithSingletonLifetime()
                    .AddClasses(classes => classes.AssignableTo<ITransient>())
                    .AsImplementedInterfaces()
                    .WithTransientLifetime()
            )
            .AddSingleton<IActionContextAccessor, ActionContextAccessor>()
            .AddJwtAuth(configuration);
    }
}
