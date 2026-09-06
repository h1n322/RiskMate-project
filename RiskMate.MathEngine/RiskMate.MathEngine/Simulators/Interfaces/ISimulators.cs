using System.Collections.Generic;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators.Interfaces
{
    public interface IMonteCarloSimulator
    {
        double[][] Simulate(AssetParameters parameters, int simulationsCount, int horizon, IRandomProvider rng);
    }

    public interface IHistoricalSimulator
    {
        double[][] Simulate(double initialPrice, List<double> historicalReturns, int simulationsCount, int horizon, IRandomProvider rng);
    }

    public interface IGarchSimulator
    {
        double[][] Simulate(AssetParameters parameters, int simulationsCount, int horizon, double omega = 0.00001, double alpha = 0.1, double beta = 0.85, IRandomProvider rng = null);
    }

    public interface IMertonJumpSimulator
    {
        double[][] Simulate(AssetParameters parameters, int simulationsCount, int horizon, double jumpIntensity = 2.0, double jumpMean = 0.0, double jumpVolatility = 0.1, IRandomProvider rng = null);
    }

    public interface IStressTestSimulator
    {
        double[][] Simulate(AssetParameters parameters, int simulationsCount, int horizon, StressScenario scenario, double customShockPercentage = 0.0, IRandomProvider rng = null);
    }
}
