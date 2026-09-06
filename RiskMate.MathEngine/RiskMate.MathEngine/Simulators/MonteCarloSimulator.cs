using System;
using System.Threading.Tasks;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators
{
    public class MonteCarloSimulator : RiskMate.MathEngine.Simulators.Interfaces.IMonteCarloSimulator
    {
        public double[][] Simulate(AssetParameters parameters, int simulationsCount, int horizon, IRandomProvider rng)
        {
            var allPaths = new double[simulationsCount][];
            double dt = 1.0;
            double sqrtDt = Math.Sqrt(dt);

            Parallel.For(0, simulationsCount, i =>
            {
                var pathRng = rng.Spawn(i);
                var path = new double[horizon + 1];
                path[0] = parameters.InitialPrice;
                double currentPrice = parameters.InitialPrice;

                for (int day = 1; day <= horizon; day++)
                {
                    double randomShock = pathRng.SampleNormal();
                    currentPrice *= Math.Exp(parameters.Drift * dt + parameters.Volatility * sqrtDt * randomShock);
                    path[day] = currentPrice;
                }
                
                allPaths[i] = path;
            });

            return allPaths;
        }
    }
}
