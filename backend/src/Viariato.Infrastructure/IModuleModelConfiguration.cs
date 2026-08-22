using Microsoft.EntityFrameworkCore;

namespace Viariato.Infrastructure;

public interface IModuleModelConfiguration
{
    void Apply(ModelBuilder modelBuilder);
}
