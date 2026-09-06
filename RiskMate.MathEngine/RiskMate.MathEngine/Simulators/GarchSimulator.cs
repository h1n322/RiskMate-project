using System;
using System.Threading.Tasks;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators
{
    public class GarchSimulator : RiskMate.MathEngine.Simulators.Interfaces.IGarchSimulator
    {
        public double[][] Simulate(
            AssetParameters parameters, 
            int simulationsCount, 
            int horizon,
            double omega = 0.00001, 
            double alpha = 0.1, 
            double beta = 0.85,
            IRandomProvider rng = null)
        {
            if (alpha + beta >= 1.0)
                throw new ArgumentException($"Нестаціонарні параметри GARCH: alpha ({alpha}) + beta ({beta}) має бути < 1");

            var allPaths = new double[simulationsCount][];
            double dt = 1.0;
            double sqrtDt = Math.Sqrt(dt);

            Parallel.For(0, simulationsCount, i =>
            {
                var pathRng = rng.Spawn(i);
                var path = new double[horizon + 1];
                path[0] = parameters.InitialPrice;
                double currentPrice = parameters.InitialPrice;
                double currentVariance = Math.Pow(parameters.Volatility, 2);

                for (int day = 1; day <= horizon; day++)
                {
                    double shock = pathRng.SampleNormal();
                    double currentDailyVolatility = Math.Sqrt(currentVariance);
                    
                    double returnForDay = parameters.Drift * dt + currentDailyVolatility * sqrtDt * shock;
                    currentPrice *= Math.Exp(returnForDay);
                    path[day] = currentPrice;

                    currentVariance = omega + alpha * Math.Pow(shock * currentDailyVolatility, 2) + beta * currentVariance;
                }
                
                allPaths[i] = path;
            });

            return allPaths;
        }
    }
}
