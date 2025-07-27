using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
	

    public class EMACrossATR_TrailingStop : Strategy
    {
		private	bool waitForNewCross = true;
		private EMA emaFast;
		private EMA emaSlow;
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "AAAEMACrossATR_TrailingStop";
        //        Calculate = MarketDataType.Last;
				Calculate = Calculate.OnBarClose; // or Calculate.OnEachTick if needed
                IsOverlay = true;
		int test =3;
            }
            else if (State == State.Configure)
            {
				
		emaFast = EMA(10);
        emaSlow = EMA(50);

        AddChartIndicator(emaFast);
        AddChartIndicator(emaSlow);
				
		 
                AddChartIndicator(ATR(14));
				
				        // 🎨 Set custom colors
        emaFast.Plots[0].Brush = Brushes.Orange;
        emaSlow.Plots[0].Brush = Brushes.Red;
	
            }
		
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 50) return;

				
			if (Position.MarketPosition == MarketPosition.Flat && (CrossAbove(EMA(10), EMA(50), 1) || CrossBelow(EMA(10), EMA(50), 1)))
			{
			    waitForNewCross = false;
			    Print("Flag reset at " + Time[0]);
			}
				
			
			
            double fastEMA = EMA(10)[0];
            double slowEMA = EMA(50)[0];
            double atrVal = ATR(14)[0];
            double trailDistance = atrVal * 1.5+10;

	 

			
			
			//----------------
			
			if (!waitForNewCross && CrossAbove(EMA(10), EMA(50), 1) && ATR(14)[0] > 3 && Position.MarketPosition == MarketPosition.Flat)
			{
			    EnterLong();
					    Print("EnterLong at " + Time[0]);
			    SetTrailStop(CalculationMode.Price, trailDistance);
			    SetProfitTarget(CalculationMode.Ticks, 100);
		
				
				
				
				
			    waitForNewCross = true; // Block further entries until next cross
			}
			
			if (!waitForNewCross && CrossBelow(EMA(10), EMA(50), 1) && ATR(14)[0] > 3 && Position.MarketPosition == MarketPosition.Flat)
			{
			    EnterShort();		   
				Print("EnterShort at " + Time[0]);
			    SetTrailStop(CalculationMode.Price, trailDistance);
			    SetProfitTarget(CalculationMode.Ticks, 100);
					    waitForNewCross = true;
			}

			
			//++++++++++++++++
			
		

            if (Position.MarketPosition == MarketPosition.Long && CrossBelow(EMA(10), EMA(50), 1))
            {
                ExitLong();
            }

            if (Position.MarketPosition == MarketPosition.Short && CrossAbove(EMA(10), EMA(50), 1))
            {
                ExitShort();
            }
        }
    }
}
