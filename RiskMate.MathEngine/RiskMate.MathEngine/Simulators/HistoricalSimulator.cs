using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators
{
    public class HistoricalSimulator : RiskMate.MathEngine.Simulators.Interfaces.IHistoricalSimulator
    {
        public double[][] Simulate(double initialPrice, List<double> historicalReturns, int simulationsCount, int horizon, IRandomProvider rng)
        {
            var allPaths = new double[simulationsCount][];
            double[] histRetArray = historicalReturns.ToArray();
            int histCount = histRetArray.Length;

            Parallel.For(0, simulationsCount, i =>
            {
                var pathRng = rng.Spawn(i);
                var path = new double[horizon + 1];
                path[0] = initialPrice;
                double currentPrice = initialPrice;

                for (int day = 1; day <= horizon; day++)
                {
                    int randomIndex = pathRng.Next(histCount);
                    double sampledReturn = histRetArray[randomIndex];
                    
                    currentPrice *= Math.Exp(sampledReturn);
                    path[day] = currentPrice;
                }
                allPaths[i] = path;
            });

            return allPaths;
        }
    }
}
