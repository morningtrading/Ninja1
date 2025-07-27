#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class NINJA1EMA01 : Strategy
	{
		   private EMA EMAfast;
    private EMA EMAslow;
    private ATR ATR;
		private	bool waitForNewCross = true;
	

		double EnterPrice=0;
		double exitPrice=0;
		
								[NinjaScriptProperty]
		[Range(100, int.MaxValue)]
		[Display(Name = "takeprofit", GroupName = "Parameters", Order = 1)]
		public int takeprofit { get; set; }
		
				[NinjaScriptProperty]
		[Range(100, int.MaxValue)]
		[Display(Name = "stoploss", GroupName = "Parameters", Order = 2)]
		public int  stoploss { get; set; }
		
						[NinjaScriptProperty]
		[Range(4, int.MaxValue)]
		[Display(Name = "EMAfastPeriod", GroupName = "Parameters", Order = 3)]
		public int  EMAfastPeriod { get; set; }

						[NinjaScriptProperty]
		[Range(5, int.MaxValue)]
		[Display(Name = "EMAslowPeriod", GroupName = "Parameters", Order = 4)]
		public int  EMAslowPeriod { get; set; }
		
						[NinjaScriptProperty]
		[Range(5, int.MaxValue)]
		[Display(Name = "ATRPeriod", GroupName = "Parameters", Order = 5)]
		public int  ATRPeriod { get; set; }


		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Enter the description for your new custom Strategy here.";
				Name = "NINJA1EMA01";
				//        Calculate = MarketDataType.Last;
				//				Calculate = Calculate.OnBarClose; // or Calculate.OnEachTick if needed
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
				TimeInForce = TimeInForce.Gtc;
				TraceOrders = false;
				RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration = true;
				//******************* MAIN DEFAULT SETTING ****************
				takeprofit = 120;// ticks
				stoploss = 120;// ticks
				EMAfastPeriod = 9;
				EMAslowPeriod = 50;
				ATRPeriod = 14; // ATR period, default is 14





			}
			else if (State == State.Configure)
			{
				ClearOutputWindow();   // OK????

	 


				//Remove AddChartIndicator from here!




			}
			
			    else if (State == State.DataLoaded)
				{
				EMAfast = EMA(EMAfastPeriod);
				EMAslow = EMA(EMAslowPeriod);
				ATR = ATR(ATRPeriod);

				AddChartIndicator(EMAfast);
				AddChartIndicator(EMAslow);
				AddChartIndicator(ATR);
				}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade) return;

    // Use indicator objects for cross detection
    if (Position.MarketPosition == MarketPosition.Flat &&
        (CrossAbove(EMAfast, EMAslow, 1) || CrossBelow(EMAfast, EMAslow, 1)))
    {
        waitForNewCross = false;
        Print("Flag reset at " + Time[0]);
    }

    double fastEMA = EMAfast[0];
    double slowEMA = EMAslow[0];
    double atrVal = ATR[0];
    double trailDistance = Math.Round(atrVal * 1.5 + 10, 2);

    if (!waitForNewCross && CrossAbove(EMAfast, EMAslow, 1) && atrVal > 3 && Position.MarketPosition == MarketPosition.Flat)
    {
        EnterLong();
        EnterPrice = Close[0];
        Print("EnterLong at " + Time[0] + " @ " + EnterPrice);
        SetStopLoss(CalculationMode.Ticks, stoploss);
        SetProfitTarget(CalculationMode.Ticks, takeprofit);
        Print("SetStopLossLong at :" + stoploss);
        waitForNewCross = true;
    }

    if (!waitForNewCross && CrossBelow(EMAfast, EMAslow, 1) && atrVal > 3 && Position.MarketPosition == MarketPosition.Flat)
    {
        EnterShort();
        EnterPrice = Close[0];
        Print("EnterShort at " + Time[0] + " @ " + EnterPrice);
        SetStopLoss(CalculationMode.Ticks, stoploss);
        Print("SetStopLossShort at :" + stoploss);
        SetProfitTarget(CalculationMode.Ticks, takeprofit);
        waitForNewCross = true;
    }

    if (Position.MarketPosition == MarketPosition.Long && CrossBelow(EMAfast, EMAslow, 1))
    {
        double exitPrice = Close[0];
        double delta = exitPrice - EnterPrice;
        ExitLong();
        Print("ExitLong at " + Time[0] + " @ " + exitPrice + "delta=" + delta);
    }

    if (Position.MarketPosition == MarketPosition.Short && CrossAbove(EMAfast, EMAslow, 1))
    {
        double exitPrice = Close[0];
        double delta = exitPrice - EnterPrice;
        ExitShort();
        Print("ExitShort at " + Time[0] + " @ " + exitPrice + "delta=" + delta);
    }
}
	}
}
