#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.Gui.Tools;
using System.ComponentModel.DataAnnotations;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    // Main strategy class
    public class NINJA1EMA01 : Strategy
    {
        // Indicator objects
        private EMA EMAfast;
        private EMA EMAslow;
        private ATR ATR;

        // Control flag for entry logic
        private bool waitForNewCross = true;

        // Track entry and exit prices for trades
        double EnterPrice = 0;
        double exitPrice = 0;

        // Strategy parameters exposed to the UI
        [NinjaScriptProperty]
        [Range(10, int.MaxValue)]
        [Display(Name = "takeprofit", GroupName = "Parameters", Order = 1)]
        public int takeprofit { get; set; }

        [NinjaScriptProperty]
        [Range(10, int.MaxValue)]
        [Display(Name = "stoploss", GroupName = "Parameters", Order = 2)]
        public int stoploss { get; set; }

        [NinjaScriptProperty]
        [Range(4, int.MaxValue)]
        [Display(Name = "EMAfastPeriod", GroupName = "Parameters", Order = 3)]
        public int EMAfastPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, int.MaxValue)]
        [Display(Name = "EMAslowPeriod", GroupName = "Parameters", Order = 4)]
        public int EMAslowPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, int.MaxValue)]
        [Display(Name = "ATRPeriod", GroupName = "Parameters", Order = 5)]
        public int ATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "ATRmultiplier", GroupName = "Parameters", Order = 6)]
        public double ATRmultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "ATRoffset", GroupName = "Parameters", Order = 7)]
        public double ATRoffset { get; set; }

        // Called when the strategy state changes (initialization, configuration, data loaded, etc.)
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                // Set default strategy properties and parameter values
                Description = @"Enter the description for your new custom Strategy here.";
                Name = "NINJA1EMA01";
                IsOverlay = true;
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
      //          TimeInForce tif= TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                IsInstantiatedOnEachOptimizationIteration = true;

                // Default parameter values
                takeprofit = 80; // ticks
                stoploss = 135;   // ticks
                EMAfastPeriod = 6;
                EMAslowPeriod = 34;
                ATRPeriod = 10;   // ATR period, default is 14
                ATRmultiplier = 0.5; // Default multiplier for ATR-based calculations
                ATRoffset = 3; // Default offset for ATR-based calculations
            }
            else if (State == State.Configure)
            {
                // Clear output window for easier debugging
                ClearOutputWindow();
                // Chart indicators are added in DataLoaded state
				
				
				
				
            }
            else if (State == State.DataLoaded)
            {
                // Instantiate indicator objects with user-defined periods
                EMAfast = EMA(EMAfastPeriod);
                EMAslow = EMA(EMAslowPeriod);
                ATR = ATR(ATRPeriod);

                // Add indicators to chart for visualization
                AddChartIndicator(EMAfast);
                AddChartIndicator(EMAslow);
                AddChartIndicator(ATR);
            }
        }

        // Output window variable for PrintMessage
        private int outputWindow = 1;

        // Helper function to print messages to a specific output window
        private void PrintMessage(string message, int window)
        {
		string	timedMessage= "Strat-" + Time[0] + message;
			Print(timedMessage);

           // NinjaTrader.Code.Output.Process(timedMessage, window);
        }

        // Main strategy logic executed on each bar update
        protected override void OnBarUpdate()
        {
			Print("Current MarketPosition: " + Position.MarketPosition);

            // Ensure enough bars have loaded before trading
            if (CurrentBar < BarsRequiredToTrade) return;

            // Reset entry flag when a new cross occurs and no position is open
            if (Position.MarketPosition == MarketPosition.Flat &&
                (CrossAbove(EMAfast, EMAslow, 1) || CrossBelow(EMAfast, EMAslow, 1)))
            {
                waitForNewCross = false;
                PrintMessage("Flag reset at " + Time[0], outputWindow);
            }

            // Get current indicator values
            double fastEMA = EMAfast[0];
            double slowEMA = EMAslow[0];
            double atrVal = ATR[0];
            double trailDistance = Math.Round((atrVal * ATRmultiplier) + ATRoffset, 2);

            // Entry logic for long position
            if (!waitForNewCross && CrossAbove(EMAfast, EMAslow, 1) && atrVal > 3 && Position.MarketPosition == MarketPosition.Flat)
            {
                EnterLong();
                EnterPrice = Close[0];
                PrintMessage("EnterLong at " + Time[0] + " @ " + EnterPrice, outputWindow);
                SetStopLoss(CalculationMode.Ticks, stoploss);
                SetProfitTarget(CalculationMode.Ticks, takeprofit);
                PrintMessage("SetStopLossLong at :" + stoploss, outputWindow);
                waitForNewCross = true;
            }

            // Entry logic for short position
            if (!waitForNewCross && CrossBelow(EMAfast, EMAslow, 1) && atrVal > 3 && Position.MarketPosition == MarketPosition.Flat)
            {
                EnterShort();
                EnterPrice = Close[0];
                PrintMessage("EnterShort at " + Time[0] + " @ " + EnterPrice, outputWindow);
                SetStopLoss(CalculationMode.Ticks, stoploss);
                PrintMessage("SetStopLossShort at :" + stoploss, outputWindow);
                SetProfitTarget(CalculationMode.Ticks, takeprofit);
                waitForNewCross = true;
            }

            // Exit logic for long position on EMA cross below
            if (Position.MarketPosition == MarketPosition.Long && CrossBelow(EMAfast, EMAslow, 1))
            {
                double exitPrice = Close[0];
                double delta = exitPrice - EnterPrice;
                ExitLong();
                PrintMessage("ExitLong at " + Time[0] + " @ " + exitPrice + " delta=" + delta, outputWindow);
            }

            // Exit logic for short position on EMA cross above
            if (Position.MarketPosition == MarketPosition.Short && CrossAbove(EMAfast, EMAslow, 1))
            {
                double exitPrice = Close[0];
                double delta = exitPrice - EnterPrice;
                ExitShort();
                PrintMessage("ExitShort at " + Time[0] + " @ " + exitPrice + " delta=" + delta, outputWindow);
            }
        }
    }
}
