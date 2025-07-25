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

using System;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using System.Windows.Media;

#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	
    public class Strategy_FFF_two : Strategy
    {
        private EMA fastEMA;
        private EMA slowEMA;
        private ATR atr;
 		private Account account;
        private double atrStopPrice = 0.0;
        private double entryPrice = 0.0;
        private DateTime entryTime;
        private string direction;
		private double sessionPnL = 0;// back to zero default here
        private string filePath;
        private double lastPnL = 0;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Fast EMA Period", GroupName = "Parameters", Order = 1)]
        public int FastEMAPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Slow EMA Period", GroupName = "Parameters", Order = 2)]
        public int SlowEMAPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", GroupName = "Parameters", Order = 3)]
        public int ATRPeriod { get; set; }
		
		// minimum tickes or minimum points for the TP or SL to avoid too fast exits if ATR is too low
	 	
		       [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "minimumTicks", GroupName = "Parameters", Order = 4)]
        public int minimumTicks { get; set; }
		
			 
		
		
        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "ATR Multiplier", GroupName = "Parameters", Order = 5)]
        public double ATRMultiplier { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Position Size", GroupName = "Parameters", Order = 6)]
		public int PositionSize { get; set; }

				[NinjaScriptProperty]
		[Range(100, int.MaxValue)]
		[Display(Name = "dailyProfitTarget", GroupName = "Parameters", Order = 7)]
		public int dailyProfitTarget { get; set; }
		
				[NinjaScriptProperty]
		[Range(400, int.MaxValue)]
		[Display(Name = "dailyStopLoss", GroupName = "Parameters", Order = 8)]
		public int dailyStopLoss { get; set; }
	
						[NinjaScriptProperty]
		[Range(100, int.MaxValue)]
		[Display(Name = "tradeProfitTarget", GroupName = "Parameters", Order = 9)]
		public int tradeProfitTarget { get; set; }
		
				[NinjaScriptProperty]
		[Range(100, int.MaxValue)]
		[Display(Name = "tradeStopLoss", GroupName = "Parameters", Order = 10)]
		public int  tradeStopLoss { get; set; }
	
 
		
 
		 
		
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "Strategy_FFF_two";
              //  Calculate = Calculate.OnBarClose;
				Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                FastEMAPeriod = 9;
                SlowEMAPeriod = 21;
                ATRPeriod = 14;
                ATRMultiplier = 1.5;
				minimumTicks = 10;
    // 10 for NQ, like 2 or 3 for ES
				PositionSize = 1;	// change to more for prop firms
				dailyStopLoss=999;
				dailyProfitTarget=2500;
				tradeProfitTarget=400;
				tradeStopLoss = 400;
	
            }
            else if (State == State.DataLoaded)
            {
				PositionSize = 3;
		//		DefaultQuantity = PositionSize;
                fastEMA = EMA(FastEMAPeriod);
                slowEMA = EMA(SlowEMAPeriod);
                atr = ATR(ATRPeriod);
       			account = Account;
                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "FFF-1-trade_log.csv");

                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, "EntryTime,Direction,EntryPrice,ExitTime,ExitPrice,ProfitLoss\n");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(Math.Max(FastEMAPeriod, SlowEMAPeriod), ATRPeriod))
                return;

			
				//		DefaultQuantity = PositionSize;
			            // Get account values
            double cashValue = account.Get(AccountItem.CashValue, Currency.UsDollar);
            double realizedPnL = account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            double unrealizedPnLLL = account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
            double totalPnL = realizedPnL + unrealizedPnLLL;
            double netLiquidation = account.Get(AccountItem.NetLiquidation, Currency.UsDollar);
            
            // Get strategy-specific performance
            double strategyPnL = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
            int totalTrades = SystemPerformance.AllTrades.Count;
            int winningTrades = SystemPerformance.AllTrades.WinningTrades.Count;
            int losingTrades = SystemPerformance.AllTrades.LosingTrades.Count;
            double winRate = totalTrades > 0 ? (double)winningTrades / totalTrades * 100 : 0;
            
 
			
		 
			
            // Create comprehensive display text
            string accountInfo = $"==STRAT FFF = ACCOUNT INFO ===\n" +
                                $"Cash: {cashValue:C}\n" +
                                $"Net Liquidation: {netLiquidation:C}\n" +
                                $"Realized PnL: {realizedPnL:C}\n" +
                                $"Unrealized PnL: {unrealizedPnLLL:C}\n" +
                                $"Total PnL: {totalPnL:C}\n" +
                                $"\n=== STRATEGY STATS ===\n" +
                    //          $"Strategy PnL: {strategyPnL:C}\n" +
                    //          $"Total Trades: {totalTrades}\n" +
                     //           $"Win Rate: {winRate:F1}%\n" +
                                $"Position: {Position.MarketPosition}\n";
	 
           
            // Choose color based on current trade PnL
            Brush textColor = unrealizedPnLLL >= 0 ? Brushes.LimeGreen : Brushes.Red;
            
            
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (CrossAbove(fastEMA, slowEMA, 1))
                {
                    EnterLong(PositionSize,"GoLong");
                    entryTime = Time[0];
                    entryPrice = Close[0];
                    direction = "Long";
					double distance = Math.Max(atr[0] * ATRMultiplier, minimumTicks);
                    atrStopPrice = entryPrice - (distance);

                    Draw.ArrowUp(this, "LongEntry_" + CurrentBar, false, 0, Low[0] - 2 * TickSize, Brushes.LimeGreen);
					Draw.HorizontalLine(this, "entryLine", entryPrice, Brushes.Lime);
                }
                else if (CrossBelow(fastEMA, slowEMA, 1))
                {
                    EnterShort(PositionSize,"GoShort");
                    entryTime = Time[0];
                    entryPrice = Close[0];
                    direction = "Short";
					double distance = Math.Max(atr[0] * ATRMultiplier, minimumTicks);
                    atrStopPrice = entryPrice + (distance);

                    Draw.ArrowDown(this, "ShortEntry_" + CurrentBar, false, 0, High[0] + 2 * TickSize, Brushes.Red);
					Draw.HorizontalLine(this, "entryLine", entryPrice, Brushes.Red);
                }
            }

            // TRAILING STOP + LABEL + EXIT LOGIC
            if (Position.MarketPosition == MarketPosition.Long)
            {
				double distance = Math.Max(atr[0] * ATRMultiplier,minimumTicks);
                double newStop = Close[0] - (distance);
                if (newStop > atrStopPrice)
                    atrStopPrice = newStop;

                double unrealizedPnL = (Close[0] - entryPrice) * Position.Quantity;
          //      Draw.TextFixed(this, "Short Trade info", $"Direction: Long\nATR Stop: {atrStopPrice:F2}\nPnL: {unrealizedPnL:F2}", TextPosition.TopLeft);
				Draw.TextFixed(this, "PNL FFF: LONG ", $"{unrealizedPnLLL:F2} ", TextPosition.BottomLeft);	

                Draw.Dot(this, "ATR_Stop_L" + CurrentBar, false, 0, atrStopPrice, Brushes.Orange);

                if (Close[0] < atrStopPrice)
                {
                    double exitPrice = Close[0];
                    DateTime exitTime = Time[0];
                    double profitLoss = (exitPrice - entryPrice) * Position.Quantity;

                    ExitLong("GoLong");

                    Brush markerColor = profitLoss >= 0 ? Brushes.Green : Brushes.Red;
                    Draw.Text(this, "ExitLong_" + CurrentBar, "❌", 0, High[0] + 2 * TickSize, markerColor);

                    string line = $"EXIT LONG {entryTime},{direction},{entryPrice},{exitTime},{exitPrice},{profitLoss}";
                    File.AppendAllText(filePath, line + "\n");
				Print(line);
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
				double distance = Math.Max(atr[0] * ATRMultiplier,minimumTicks);
                double newStop = Close[0] + (distance);
                if (newStop < atrStopPrice)
                    atrStopPrice = newStop;
			double distanceinticks=atr[0] * ATRMultiplier;
                double unrealizedPnL = (entryPrice - Close[0]) * Position.Quantity;
     //           Draw.TextFixed(this, "Long Trade Info", $"Direction: Short\nATR Stop: {atrStopPrice:F2}\nPnL: {unrealizedPnL:F2}", TextPosition.BottomLeft);

                Draw.Dot(this, "ATR_Stop_S" + CurrentBar, false, 0, atrStopPrice, Brushes.Yellow);

		//		Draw.TextFixed(this, "ATR: ", $"{atr[0]:F2} Coef: {ATRMultiplier:F2} distance: {distance:F2}", TextPosition.TopRight);
		//		Draw.TextFixed(this, "position:", $"{Position.MarketPosition:F2} ok?", TextPosition.BottomLeft);
				Draw.TextFixed(this, "PNL FFF: SHORT ", $"{unrealizedPnLLL:F2} ", TextPosition.BottomLeft);			
				
				
                if (Close[0] > atrStopPrice)
                {
                    double exitPrice = Close[0];
                    DateTime exitTime = Time[0];
                    double profitLoss = (entryPrice - exitPrice) * Position.Quantity;

                    ExitShort("GoShort");

                    Brush markerColor = profitLoss >= 0 ? Brushes.Green : Brushes.Red;
                    Draw.Text(this, "ExitShort_" + CurrentBar, "❌", 0, Low[0] - 2 * TickSize, markerColor);
					 

                    string line = $"EXIT SHORT {entryTime},{direction},{entryPrice},{exitTime},{exitPrice},{profitLoss}";
                    File.AppendAllText(filePath, line + "\n");
					Print(line);
                }
            }
			

			
        }
    }
}
