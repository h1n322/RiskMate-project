using System;
using System.Collections.Generic;
using Xunit;
using RiskMate.MathEngine;
using RiskMate.MathEngine.Models;

namespace RiskMate.MathEngine.Tests.Regression
{
    public class RiskEngineRegressionTests
    {
        private List<double> GetMockHistoricalPrices()
        {
            var prices = new List<double>();
            double current = 100.0;
            for (int i = 0; i < 50; i++)
            {
                prices.Add(current);
                current *= (1.0 + 0.02 * Math.Sin(i));
            }
            return prices;
        }

        [Fact]
        public void Gbm_Simulation_ShouldMatchBaseline()
        {
            var engine = new RiskEngine(new RiskMate.MathEngine.Simulators.MonteCarloSimulator(), new RiskMate.MathEngine.Simulators.HistoricalSimulator(), new RiskMate.MathEngine.Simulators.StressTestSimulator(), new RiskMate.MathEngine.Simulators.MertonJumpSimulator(), new RiskMate.MathEngine.Simulators.GarchSimulator(), null);
            var prices = GetMockHistoricalPrices();
            var result = engine.RunSimulation(prices, SimulationAlgorithm.Gbm, 1000, 10);

            Assert.Equal(102.1318, result.ExpectedPrice, 0);
            Assert.Equal(7.0491, result.ValueAtRisk, 0);
            Assert.Equal(8.7796, result.ConditionalValueAtRisk, 0);
            Assert.Equal(22.5014, result.Volatility, 0);
            Assert.Equal(0.2016, result.SharpeRatio, 0);
        }

        [Fact]
        public void Garch_Simulation_ShouldMatchBaseline()
        {
            var engine = new RiskEngine(new RiskMate.MathEngine.Simulators.MonteCarloSimulator(), new RiskMate.MathEngine.Simulators.HistoricalSimulator(), new RiskMate.MathEngine.Simulators.StressTestSimulator(), new RiskMate.MathEngine.Simulators.MertonJumpSimulator(), new RiskMate.MathEngine.Simulators.GarchSimulator(), null);
            var prices = GetMockHistoricalPrices();
            var result = engine.RunSimulation(prices, SimulationAlgorithm.Garch, 1000, 10);

            Assert.Equal(102.1219, result.ExpectedPrice, 0);
            Assert.Equal(6.9602, result.ValueAtRisk, 0);
            Assert.Equal(9.1135, result.ConditionalValueAtRisk, 0);
        }

        [Fact]
        public void Merton_Simulation_ShouldMatchBaseline()
        {
            var engine = new RiskEngine(new RiskMate.MathEngine.Simulators.MonteCarloSimulator(), new RiskMate.MathEngine.Simulators.HistoricalSimulator(), new RiskMate.MathEngine.Simulators.StressTestSimulator(), new RiskMate.MathEngine.Simulators.MertonJumpSimulator(), new RiskMate.MathEngine.Simulators.GarchSimulator(), null);
            var prices = GetMockHistoricalPrices();
            var result = engine.RunSimulation(prices, SimulationAlgorithm.Merton, 1000, 10);

            Assert.Equal(102.2001, result.ExpectedPrice, 0);
            Assert.Equal(8.0660, result.ValueAtRisk, 0);
            Assert.Equal(11.1076, result.ConditionalValueAtRisk, 0);
        }

        [Fact]
        public void Historical_Simulation_ShouldMaintainInvariants()
        {
            var engine = new RiskEngine(new RiskMate.MathEngine.Simulators.MonteCarloSimulator(), new RiskMate.MathEngine.Simulators.HistoricalSimulator(), new RiskMate.MathEngine.Simulators.StressTestSimulator(), new RiskMate.MathEngine.Simulators.MertonJumpSimulator(), new RiskMate.MathEngine.Simulators.GarchSimulator(), null);
            var prices = GetMockHistoricalPrices();
            
            var result = engine.RunSimulation(prices, SimulationAlgorithm.Historical, 1000, 10);

            Assert.True(result.ExpectedPrice > 0, "Expected price should be positive");
            Assert.True(result.ValueAtRisk >= 0, "VaR should be non-negative");
            Assert.True(result.ConditionalValueAtRisk >= result.ValueAtRisk, "CVaR should be >= VaR");
            Assert.True(result.Volatility > 0, "Volatility should be positive");
        }
    }
}
