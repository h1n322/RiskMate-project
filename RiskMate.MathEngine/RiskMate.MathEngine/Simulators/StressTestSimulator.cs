using System;
using System.Threading.Tasks;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine.Simulators
{
    public class StressTestSimulator : RiskMate.MathEngine.Simulators.Interfaces.IStressTestSimulator
    {
        public double[][] Simulate(
            AssetParameters parameters, 
            int simulationsCount, 
            int horizon, 
            StressScenario scenario, 
            double customShockPercentage = 0.0,
            IRandomProvider rng = null)
        {
            double shockModifier = GetShockModifier(scenario, customShockPercentage);
            double crisisVolatility = parameters.Volatility * 2.5; 
            double crisisDrift = -0.40 / 252.0; 

            if (scenario == StressScenario.CustomShock)
            {
                crisisDrift = parameters.Drift; 
                crisisVolatility = parameters.Volatility * 1.5;
            }

            var allPaths = new double[simulationsCount][];

            Parallel.For(0, simulationsCount, i =>
            {
                var pathRng = rng.Spawn(i);
                var path = new double[horizon + 1];
                path[0] = parameters.InitialPrice;

                double currentPrice = parameters.InitialPrice * shockModifier;
                path[1] = currentPrice;

                for (int day = 2; day <= horizon; day++)
                {
                    double randomShock = pathRng.SampleNormal();
                    currentPrice *= Math.Exp(crisisDrift + crisisVolatility * randomShock);
                    path[day] = currentPrice;
                }
                
                allPaths[i] = path;
            });

            return allPaths;
        }

        private double GetShockModifier(StressScenario scenario, double customShockPercentage)
        {
            return scenario switch
            {
                StressScenario.Covid19Crash => 0.75,
                StressScenario.FinancialCrisis08 => 0.65,
                StressScenario.DotComBubble00 => 0.50,
                StressScenario.BlackMonday87 => 0.774,
                StressScenario.CustomShock => 1.0 - Math.Clamp(Math.Abs(customShockPercentage), 0, 0.99), 
                _ => 1.0
            };
        }
    }
}
