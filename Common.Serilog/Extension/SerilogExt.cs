using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Common.Serilog.Extension;

public static class SerilogExt
{
    public static void AddSerilogExt(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog(
    (context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
);
    }
}
