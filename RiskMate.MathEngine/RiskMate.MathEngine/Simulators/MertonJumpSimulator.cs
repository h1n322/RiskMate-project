using System;
using System.Threading.Tasks;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators
{
    public class MertonJumpSimulator : RiskMate.MathEngine.Simulators.Interfaces.IMertonJumpSimulator
    {
        public double[][] Simulate(
            AssetParameters parameters, 
            int simulationsCount, 
            int horizon, 
            double jumpIntensity = 2.0, 
            double jumpMean = 0.0, 
            double jumpVolatility = 0.1,
            IRandomProvider rng = null)
        {
            var allPaths = new double[simulationsCount][];
            
            double dt = 1.0;
            double sqrtDt = Math.Sqrt(dt);
            double dailyJumpProbability = jumpIntensity / Constants.TradingDaysPerYear;
            double jumpCompensator = dailyJumpProbability * jumpMean;
            double adjustedDrift = parameters.Drift - jumpCompensator;

            Parallel.For(0, simulationsCount, i =>
            {
                var pathRng = rng.Spawn(i);
                var path = new double[horizon + 1];
                path[0] = parameters.InitialPrice;
                double currentPrice = parameters.InitialPrice;

                for (int day = 1; day <= horizon; day++)
                {
                    double normalShock = pathRng.SampleNormal();
                    double returnForDay = adjustedDrift * dt + parameters.Volatility * sqrtDt * normalShock;

                    if (pathRng.NextDouble() < dailyJumpProbability)
                    {
                        double jumpShock = pathRng.SampleNormal();
                        double jumpMagnitude = jumpMean + jumpVolatility * jumpShock;
                        returnForDay += jumpMagnitude;
                    }

                    currentPrice *= Math.Exp(returnForDay);
                    path[day] = currentPrice;
                }
                
                allPaths[i] = path;
            });

            return allPaths;
        }
    }
}
