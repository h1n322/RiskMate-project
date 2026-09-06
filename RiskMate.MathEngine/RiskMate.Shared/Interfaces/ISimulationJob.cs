using System.Threading.Tasks;
using Hangfire.Server;
using RiskMate.Api.DTOs;

namespace RiskMate.Shared.Interfaces
{
    public interface ISimulationJob
    {
        Task ExecuteAsync(SimulationRequestDto dto, PerformContext context = null);
    }
}
