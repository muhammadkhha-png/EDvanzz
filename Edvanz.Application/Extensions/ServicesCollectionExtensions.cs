using Edvanz.Application.Services;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Application.Extensions;

public static class ServicesCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITutorService, TutorService>();
      
       
    }
}
