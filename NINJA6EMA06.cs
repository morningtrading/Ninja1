#region Using declarations
using System; // System namespace for base types
using System.Collections.Generic; // For generic collections
using System.ComponentModel; // For component model attributes
using System.ComponentModel.DataAnnotations; // For data annotations
using System.Linq; // For LINQ queries
using System.Text; // For string manipulation
using System.Threading.Tasks; // For async tasks
using System.Windows; // For WPF types
using System.Windows.Input; // For input types
using System.Windows.Media; // For drawing/brushes
using System.Xml.Serialization; // For XML serialization
using NinjaTrader.Cbi; // NinjaTrader core business interfaces
using NinjaTrader.Gui; // NinjaTrader GUI
using NinjaTrader.Gui.Chart; // NinjaTrader charting
using NinjaTrader.Gui.SuperDom; // NinjaTrader SuperDOM
using NinjaTrader.Gui.Tools; // NinjaTrader GUI tools
using NinjaTrader.Data; // NinjaTrader data types
using NinjaTrader.NinjaScript; // NinjaTrader scripting
using NinjaTrader.Core.FloatingPoint; // For floating point helpers
using NinjaTrader.NinjaScript.Indicators; // For indicators
using NinjaTrader.NinjaScript.DrawingTools; // For drawing tools
using System.IO; // For file handling
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class NINJA6EMA06 : Strategy // Main strategy class
    {
        private SMA fastMA; // Fast moving average
        private SMA slowMA; // Slow moving average
        //private SMA HTFMA;                    
		private EMA emaHTF; // 5-min EMA for higher time frame filter
        private ATR atr; // ATR indicator for volatility-based trailing stop
        private double trailStopPrice = 0; // Current trailing stop price
        private bool longTrailActive = false; // Is long trailing stop active
        private bool shortTrailActive = false; // Is short trailing stop active

        // Variables for PnL tracking
        private double totalPnL = 0; // Total profit and loss
        private double currentPositionPnL = 0; // PnL for current open position
        public double entryPrice = 0; // Entry price for current position
        private int totalTrades = 0; // Total number of trades
        private int winningTrades = 0; // Number of winning trades
        private int losingTrades = 0; // Number of losing trades

        // Flags to prevent immediate re-entry after exit
        private bool justExitedShort = false; // Prevents immediate short re-entry
        private bool justExitedLong = false; // Prevents immediate long re-entry

        private MarketPosition lastPosition = MarketPosition.Flat; // Last position direction
        private double lastEntryPrice = 0; // Last entry price
        private int lastQuantity = 0; // Last position quantity

        private Series<double> emaHTFOnPrimary;

        private int lastTradeBar = -1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults) // Set default strategy properties
            {
                Description = @"MA crossing with trailing stops and filters"; // Description
                Name = "NINJA6EMA06"; // Strategy name
                Calculate = Calculate.OnEachTick; // Calculate on each tick
                EntriesPerDirection = 1; // Only one entry per direction
                EntryHandling = EntryHandling.AllEntries; // Handle all entries
                IsExitOnSessionCloseStrategy = true; // Exit on session close
                ExitOnSessionCloseSeconds = 30; // Seconds before session close to exit
                IsFillLimitOnTouch = false; // Do not fill limit on touch
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix; // Max bars lookback
                OrderFillResolution = OrderFillResolution.Standard; // Standard fill resolution
                Slippage = 0; // No slippage
                StartBehavior = StartBehavior.WaitUntilFlat; // Wait until flat before starting
                TimeInForce = TimeInForce.Gtc; // Good till cancelled
                TraceOrders = false; // Do not trace orders
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose; // Stop on error
                StopTargetHandling = StopTargetHandling.PerEntryExecution; // Per entry execution
                BarsRequiredToTrade = 50; // Bars required before trading
                IsInstantiatedOnEachOptimizationIteration = true; // Instantiate on each optimization

                // Default parameter values
                FastPeriod = 5; // Fast MA period
                SlowPeriod = 55; // Slow MA period
                                 //  TrailAmount = 200; // Trailing stop amount in ticks (legacy, not used if ATR-based)
                Quantity = 1; // Default trade quantity
                UseTimeFilter = false; // Use time filter or not
                StartTime = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture); // Trading start time
                EndTime = DateTime.Parse("15:30", System.Globalization.CultureInfo.InvariantCulture); // Trading end time

                // ATR-based trailing stop settings
                Trail_with_ATR = true; // Use ATR-based trailing stop
                ATRMultiplier = 2.5; // Default ATR multiplier for trailing stop
                //BreakEvenTriggerCurrency = 40; // Default break-even trigger in ticks ??? curreny CHANGE
                TralingStartDeltaCurrency = 5; // Default trailing start delta in currency units
                BreakEvenTriggerCurrency = 10; // Default break-even trigger in currency units
                TakeProfitDollar = 250; // Default take profit in dollars
            }
            else if (State == State.Configure) // Configure additional data series
            {
                AddDataSeries(Data.BarsPeriodType.Minute, 1); // Add 1-min bars for higher time frame
            }
            else if (State == State.DataLoaded) // Initialize indicators after data is loaded
            {
                fastMA = SMA(FastPeriod); // Initialize fast MA
                slowMA = SMA(SlowPeriod); // Initialize slow MA
                AddChartIndicator(fastMA); // Add fast MA to chart
                AddChartIndicator(slowMA); // Add slow MA to chart

                emaHTF = EMA(BarsArray[1], 100); // 100-period EMA on 1-min bars
                AddChartIndicator(emaHTF); // Add higher time frame EMA to chart

               // atr = ATR(14); // 14-period ATR on primary series
               // AddChartIndicator(atr); // Add ATR to chart

                emaHTFOnPrimary = new Series<double>(this);
            }
        }

        private void UpdatePnLDisplay()
        {
            if (State != State.Realtime && State != State.Historical) // Only update in real/historical
                return;

            double winRate = totalTrades > 0 ? (winningTrades / (double)totalTrades) * 100 : 0; // Calculate win rate
            Brush pnlColor = totalPnL >= 0 ? Brushes.Green : Brushes.Red; // Color for total PnL
            Brush currentPnlColor = currentPositionPnL >= 0 ? Brushes.Green : Brushes.Red; // Color for current PnL

            Draw.TextFixed(this, "TotalPnL", // Draw total PnL
                string.Format("\n \n PNL Total: {0:C2}", totalPnL),
                TextPosition.TopLeft,
                pnlColor,
                new SimpleFont("Arial", 16),
                Brushes.Transparent,
                Brushes.Transparent,
                0);

            if (Position.MarketPosition != MarketPosition.Flat) // If in a position, show current PnL
            {
                string positionType = Position.MarketPosition == MarketPosition.Long ? "LONG" :
                      Position.MarketPosition == MarketPosition.Short ? "SHORT" : "FLAT";
                Draw.TextFixed(this, "CurrentPnL",
                    string.Format("Position {0}: {1:C2}", positionType, currentPositionPnL),
                    TextPosition.TopRight,
                    currentPnlColor,
                    new SimpleFont("Arial", 16),
                    Brushes.Transparent,
                    Brushes.Transparent,
                    20);
            }
            else
            {
                RemoveDrawObject("CurrentPnL"); // Remove current PnL display if flat
            }

            Draw.TextFixed(this, "Stats", // Draw trade statistics
                string.Format("Trades: {0} | W: {1} | L: {2} | WR: {3:F1}%",
                    totalTrades, winningTrades, losingTrades, winRate),
                TextPosition.BottomLeft,
                Brushes.White,
                new SimpleFont("Arial", 9),
                Brushes.Transparent,
                Brushes.Transparent,
                40);

            if (Position.MarketPosition != MarketPosition.Flat && trailStopPrice > 0) // Show trailing stop if active
            {
                Draw.TextFixed(this, "TrailStop",
                    string.Format("Trailing Stop: {0:F2}", trailStopPrice),
                    TextPosition.TopLeft,
                    Brushes.Yellow,
                    new SimpleFont("Arial", 9),
                    Brushes.Transparent,
                    Brushes.Transparent,
                    60);

                Draw.HorizontalLine(this, "TSLine", trailStopPrice, Brushes.Yellow, DashStyleHelper.Dash, 3); // Thicker line (width 3)
                //Print("We have a trailing stop bar at " + trailStopPrice); // Print debug info
            }
            else
            {
                RemoveDrawObject("TrailStop"); // Remove trailing stop display if not active
                RemoveDrawObject("TSLine"); // Remove trailing stop line
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) // Only process primary series (main chart)
                return;

                // At the start of OnBarUpdate()
            if (CurrentBar == lastTradeBar)
                 return; // Skip if already traded this bar

            // Draw.TextFixed(this, "tradePnL", $"This trade PnL: {tradePnL:F2}", TextPosition.TopLeft, Brushes.Yellow, new SimpleFont("Arial", 16), Brushes.Transparent, Brushes.Transparent, 0);
            //   Draw.TextFixed(this, "TotalPnL", $"Total PnL: {totalPnL:F2}", TextPosition.TopRight, Brushes.Lime, new SimpleFont("Arial", 16), Brushes.Transparent, Brushes.Transparent, 0);
            // Draw.TextFixed(this, "EMAdiff ", $"CrossWaiting:{WaitingforCrossing} \n ADX Val={adxVal} EMA diff: {emaDiff:F2} | {MinEMADiff} | fastEMASlope {fastEMASlope}", TextPosition.BottomRight, Brushes.Lime, new SimpleFont("Arial", 16), Brushes.Transparent, Brushes.Transparent, 0);




            if (CurrentBars[0] < Math.Max(FastPeriod, SlowPeriod) || CurrentBars[1] < 50) // Wait for enough bars
                return;

            if (UseTimeFilter && (Time[0].TimeOfDay < StartTime.TimeOfDay || Time[0].TimeOfDay > EndTime.TimeOfDay)) // Time filter
                return;

            if (Position.MarketPosition != MarketPosition.Flat && entryPrice != 0) // If in a position, update current PnL
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    currentPositionPnL = (Close[0] - entryPrice) * Position.Quantity * Instrument.MasterInstrument.PointValue;
                else
                    currentPositionPnL = (entryPrice - Close[0]) * Position.Quantity * Instrument.MasterInstrument.PointValue;
            }
            else
            {
                currentPositionPnL = 0; // Reset current PnL if flat
            }

            UpdatePnLDisplay(); // Update PnL and stats display

            // --- HTF EMA Direction Info ---
            string htfDirection = "";
            Brush htfColor = Brushes.Gray;

            if (emaHTF[0] > emaHTF[1])
            {
                htfDirection = "HTF EMA Bullish: Only LONG entries allowed";
                htfColor = Brushes.LimeGreen;
            }
            else if (emaHTF[0] < emaHTF[1])
            {
                htfDirection = "HTF EMA Bearish: Only SHORT entries allowed";
                htfColor = Brushes.OrangeRed;
            }
            else
            {
                htfDirection = "HTF EMA Flat: No directional bias";
                htfColor = Brushes.Gray;
            }

            Draw.TextFixed(this, "HTFInfo", htfDirection, TextPosition.BottomRight, htfColor, new SimpleFont("Arial", 14), Brushes.Transparent, Brushes.Transparent, 0);

            // -  Trailing Stop for Long Positions ---
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double atrStop;
                double breakEvenPrice;
              
               
                
                    //  trailing stop
                    atrStop = Close[0] - TralingStartDeltaCurrency;
                    breakEvenPrice = entryPrice + BreakEvenTriggerCurrency;
                     Draw.HorizontalLine(this, "BEline", breakEvenPrice, Brushes.Red, DashStyleHelper.Dash, 1); // Thicker line (width 3)
                     
                  
                
                // Initialize trailing stop if not already active
                if (!longTrailActive)
                {
                    trailStopPrice = atrStop;
                    longTrailActive = true;
                    PrintOut1("===== |Long trailing stop initialized at " + trailStopPrice); // Print debug info
                   

                  //  Print("ZZZZ entry = " + entryPrice); // Print debug info
                }
                else
                {
                    // Move stop up
                    if (atrStop > trailStopPrice)
                    {
                        double deltacash= Math.Round(atrStop - entryPrice,0); // Calculate delta cash
                        trailStopPrice = Math.Round(trailStopPrice,2);
                        PrintOut1(">>>>>  |Long trailing MOVED UP at " + trailStopPrice + " Delta Cash: " + deltacash +" points"); // Print debug info
                        trailStopPrice = atrStop;
                     //   Print("ZZZZ entry = " + entryPrice);

                    }

                    // Move to break-even if profit threshold reached
                    if (Close[0] >= breakEvenPrice && trailStopPrice < entryPrice)
                    {
                        trailStopPrice = entryPrice;
                        PrintOut1("***** |Long trailing stop moved to break-even at " + trailStopPrice + " Entry " + entryPrice); // Print debug info
                    //    Print("ZZZZ entry = " + entryPrice);
                    }
                }

                if (Low[0] <= trailStopPrice)
                {
                    ExitLong("MABuy");
                    Draw.ArrowDown(this, "ExitLong" + CurrentBar, false, 0, High[0] + 5 * TickSize, Brushes.Red);
                    justExitedLong = true;
                    // Entry price and PnL will be printed in OnPositionUpdate when position is closed
                    PrintOut1("***** |Long exit at " + trailStopPrice + " Entry " + entryPrice); // Print debug info

                }
            }

            // --- Trailing Stop for Short Positions ---
            if (Position.MarketPosition == MarketPosition.Short)
            {
                double atrStop = Math.Round(Close[0]  + TralingStartDeltaCurrency,0); //  Trailing stop for short positions
                double breakEvenPrice = entryPrice - BreakEvenTriggerCurrency; // Break-even trigger
                  Draw.HorizontalLine(this, "BEline", breakEvenPrice, Brushes.Red, DashStyleHelper.Dash, 1); // Thicker line (width 3)
         

                if (!shortTrailActive)
                {
                    trailStopPrice = atrStop;
                    shortTrailActive = true;
                    PrintOut1("===== |Short trailing stop initialized at " + trailStopPrice); // Print debug info
                }
                else
                {
                    // Move stop down  
                    if (atrStop < trailStopPrice)
                    {
                        trailStopPrice = atrStop;
                        double deltacash= Math.Round(-atrStop + entryPrice,2); // Calculate delta points
                        PrintOut1(">>>>>  |Short trailing MOVED down at " + trailStopPrice + " Delta Cash: " + deltacash +" points"); // Print debug info
                      


                     
                    }


                    // Move to break-even if profit threshold reached
                    if (Close[0] <= breakEvenPrice && trailStopPrice > entryPrice)
                    {   trailStopPrice = Math.Round(entryPrice-1,0); // Move stop to break-even
						PrintOut1("**BE** |Short trailing stop moved to break-even at " + entryPrice); // Print debug info


						
                      
                        // the -1 is to cover fees
                        
                    }

                }

                if (Close[0] >= trailStopPrice)
                {
                    ExitShort("MASell");
                    Draw.ArrowUp(this, "ExitShort" + CurrentBar, false, 0, Low[0] - 5 * TickSize, Brushes.Red);
                    justExitedShort = true;
                    PrintOut1(">>> Exited SHORT from trailing  " + Close[0] ); // Print exit info
        
                }
            }

            // Entry signals 
            // Check if we are in a position and update entry price
            // Entry signals WITH 30-min EMA filter
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                // Long entry: fastMA crosses above slowMA AND 2-min close above 2-min EMA
                if (CrossAbove(fastMA, slowMA, 1) && !justExitedLong && Closes[1][0] > emaHTF[0])
                {
                    EnterLong(Quantity, "MABuy");
                    lastTradeBar = CurrentBar; // Mark trade for this bar
                    Draw.ArrowUp(this, "BuySignal" + CurrentBar, false, 0, Low[0] - 10 * TickSize, Brushes.Green);
                    longTrailActive = true;
                    trailStopPrice = 0;
                    justExitedShort = false;
                }
                // Short entry: fastMA crosses below slowMA AND 2-min close below 2-min EMA
                else if (CrossBelow(fastMA, slowMA, 1) && !justExitedShort && Closes[1][0] < emaHTF[0])
                {
                    EnterShort(Quantity, "MASell");
                    lastTradeBar = CurrentBar; // Mark trade for this bar
                    Draw.ArrowDown(this, "SellSignal" + CurrentBar, false, 0, High[0] + 10 * TickSize, Brushes.Orange);
                    shortTrailActive = false;
                    trailStopPrice = 0;
                    justExitedLong = false;
                }
            }

            // Example: Save CSV with day of year in file name
            string baseFileName = "trade_log";
            int dayOfYear = DateTime.Now.DayOfYear;
            string fileName = $"{baseFileName}_{dayOfYear}.csv"; // e.g., trade_log_215.csv

            // Use fileName when saving your CSV
            // File.WriteAllText(fileName, csvContent);

            if (BarsInProgress == 0 && CurrentBars[1] > 50)
            {
                emaHTFOnPrimary[0] = emaHTF[0];
                       
            }
    
            if (emaHTF[0] > emaHTF[1])
            {
             //   BackBrush = Brushes.DarkBlue; // Bullish: dark blue background
                BackBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 139)); // 128 = 50% transparent, 0,0,139 = DarkBlue   
            }
            else if (emaHTF[0] < emaHTF[1])
            {
               // BackBrush = Brushes.Maroon; // Bearish: maroon background
                BackBrush = new SolidColorBrush(Color.FromArgb(128, 128, 0, 0)); // 128 = 50% transparent, 128,0,0 = Maroon
            }
            else
            {
                BackBrush = null; // Neutral: default background
            }

            // --- TAKE PROFIT LOGIC & PLOT FOR LONG POSITIONS ---
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double tradePnL = (Close[0] - entryPrice) * Position.Quantity * Instrument.MasterInstrument.PointValue;
                double takeProfitPrice = entryPrice + (TakeProfitDollar / (Position.Quantity * Instrument.MasterInstrument.PointValue));
                // Draw horizontal line for take profit
                Draw.HorizontalLine(this, "TPLong", takeProfitPrice, Brushes.HotPink, DashStyleHelper.Solid, 2);

                if (tradePnL >= TakeProfitDollar)
                {
                    ExitLong("MABuy"); // Corrected order name
                    PrintOut1($"Take profit hit for LONG at {Close[0]} PnL: {tradePnL}");
                }
            }
            else
            {
                RemoveDrawObject("TPLong");
            }

            // --- TAKE PROFIT LOGIC & PLOT FOR SHORT POSITIONS ---
            if (Position.MarketPosition == MarketPosition.Short)
            {
                double tradePnL = (entryPrice - Close[0]) * Position.Quantity * Instrument.MasterInstrument.PointValue;
                double takeProfitPrice = entryPrice - (TakeProfitDollar / (Position.Quantity * Instrument.MasterInstrument.PointValue));
                // Draw horizontal line for take profit
                Draw.HorizontalLine(this, "TPShort", takeProfitPrice, Brushes.HotPink, DashStyleHelper.Solid, 2);

                if (tradePnL >= TakeProfitDollar)
                {
                    ExitShort("MASell"); // Corrected order name
                    PrintOut1($"Take profit hit for SHORT at {Close[0]} PnL: {tradePnL}");
                }
            }
            else
            {
                RemoveDrawObject("TPShort");
            }

            // --- EXIT ON PRICE CROSSING  SLOW MA BY 2 BARS WITH SLOPE CONDITION ---

            // Exit long if price crosses below SlowMA in the last 2 bars AND fastMA is sloping down
            if (Position.MarketPosition == MarketPosition.Long && CrossBelow(Close, slowMA, 2)  )
            {
                ExitLong("MABuy");
                lastTradeBar = CurrentBar; // Mark trade for this bar
                PrintOut1("Exit LONG: Price crossed below slowMA (2 bars) ");
            }

            // Exit short if price crosses above slowMA in the last 2 bars AND slowMA is sloping up
            if (Position.MarketPosition == MarketPosition.Short && CrossAbove(Close, slowMA, 2)  )
            {
                ExitShort("MASell");
                lastTradeBar = CurrentBar; // Mark trade for this bar
                PrintOut1("Exit SHORT: Price crossed above slowMA (2 bars)");
            }
 

            
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            if (order.Name == "MABuy" && orderState == OrderState.Filled) // On long order fill
            {
                entryPrice = averageFillPrice; // Set entry price to fill price
                lastEntryPrice = averageFillPrice; // Set last entry price
                lastPosition = MarketPosition.Long; // Set last position
                lastQuantity = filled; // Set last quantity
                PrintOut1(string.Format("TBC long opened at "+entryPrice+" {0} time {1}", entryPrice, time)); // Debug print
            }
            else if (order.Name == "MASell" && orderState == OrderState.Filled) // On short order fill
            {
                entryPrice = averageFillPrice; // Set entry price to fill price
                lastEntryPrice = averageFillPrice; // Set last entry price
                lastPosition = MarketPosition.Short; // Set last position
                lastQuantity = filled; // Set last quantity
                PrintOut1(string.Format("TBC Short Opened at " +entryPrice+ " {0} time {1}", averageFillPrice, time)); // Debug print
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            if (marketPosition == MarketPosition.Flat && lastPosition != MarketPosition.Flat)
            {
                double tradePnL = 0; // Initialize trade PnL
                if (lastPosition == MarketPosition.Long)
                    tradePnL = (Close[0] - lastEntryPrice) * Math.Abs(lastQuantity) * Instrument.MasterInstrument.PointValue; // Long PnL
                else if (lastPosition == MarketPosition.Short)
                    tradePnL = (lastEntryPrice - Close[0] ) * Math.Abs(lastQuantity) * Instrument.MasterInstrument.PointValue; // Short PnL

                totalPnL += tradePnL; // Update total PnL
                totalTrades++; // Increment trade count

                if (tradePnL > 0)
                    winningTrades++; // Increment win count
                else if (tradePnL < 0)
                    losingTrades++; // Increment loss count

                //PrintOut2($"ENTRY PRICE: {lastEntryPrice}, EXIT PRICE: {Close[0] }, QTY: {lastQuantity}");
                PrintOut2($"TotalPNL: {totalPnL:C2}, PNL : {tradePnL:C2}"); // Debug print

                lastPosition = MarketPosition.Flat; // Reset last position
                lastEntryPrice = 0; // Reset last entry price
                lastQuantity = 0; // Reset last quantity
            }

            if (marketPosition == MarketPosition.Flat)
            {
                longTrailActive = false; // Deactivate long trailing stop
                shortTrailActive = false; // Deactivate short trailing stop
                trailStopPrice = 0; // Reset trailing stop price
                entryPrice = 0; // Reset entry price
                currentPositionPnL = 0; // Reset current PnL
   //             Print(string.Format("Afer flat longTrailActive  shortTrailActive: {0:C2}, {1:C2}", longTrailActive, shortTrailActive)); // Debug print
            }

            if (marketPosition == MarketPosition.Flat)
            {
                // barsSinceTrade = 0; // Reset cool off after exit
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Fast MA Period", Description = "Période de la moyenne mobile rapide", Order = 1, GroupName = "Moyennes Mobiles")]
        public int FastPeriod { get; set; } // Fast MA period property

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Slow MA Period", Description = "Période de la moyenne mobile lente", Order = 2, GroupName = "Moyennes Mobiles")]
        public int SlowPeriod { get; set; } // Slow MA period property


        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Quantity", Description = "Quantité par position", Order = 4, GroupName = "Gestion du Risque")]
        public int Quantity { get; set; } // Trade quantity property

        [NinjaScriptProperty]
        [Display(Name = "Use Time Filter", Description = "Utiliser un filtre temporel", Order = 5, GroupName = "Filtre Temporel")]
        public bool UseTimeFilter { get; set; } // Use time filter property

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Start Time", Description = "Heure de début du trading", Order = 6, GroupName = "Filtre Temporel")]
        public DateTime StartTime { get; set; } // Trading start time property

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "End Time", Description = "Heure de fin du trading", Order = 7, GroupName = "Filtre Temporel")]
        public DateTime EndTime { get; set; } // Trading end time property

        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "ATR Multiplier", Description = "ATR multiplier for trailing stop", Order = 8, GroupName = "Trailing Stop")]
        public double ATRMultiplier { get; set; } // ATR multiplier for trailing stop

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Break Even (Currency)", Description = "Profit in currency to move stop to break-even", Order = 9, GroupName = "Trailing Stop")]
        public double BreakEvenTriggerCurrency { get; set; } // Profit in currency to move stop to break-even

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trailing Start Delta (currency)", Description = "Trailing start delta in currency", Order = 10, GroupName = "Trailing Stop")]
        public int TralingStartDeltaCurrency { get; set; } // Trailing start delta value in currency

[NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trail_with_ATR", Description = "Trail with ATR or not", Order = 11, GroupName = "Trailing Stop")]
        public bool Trail_with_ATR { get; set; } // Basic stop trail value in currency

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take Profit ($)", Description = "Take profit in dollars per trade", Order = 12, GroupName = "Trade Management")]
        public double TakeProfitDollar { get; set; } = 400;

        
        #endregion

        private void PrintOut1(string message)
        {
            string timeStamp = Time[0].ToString("yyyy-MM-dd HH:mm:ss");
            NinjaTrader.Code.Output.Process($"{timeStamp} | {message}", PrintTo.OutputTab1);
        }

        private void PrintOut2(string message)
        {
            string timeStamp = Time[0].ToString("yyyy-MM-dd HH:mm:ss");
            NinjaTrader.Code.Output.Process($"{timeStamp} | {message}", PrintTo.OutputTab2);
        }
    }
}
/*
Strategy Overview
NINJA04EMA01 is a NinjaTrader 8 strategy that implements a moving average crossover system with a trailing stop and higher time frame trend filter.

Key Features
Moving Average Crossover:
Trades are triggered when a fast SMA crosses a slow SMA on the primary chart.

Trailing Stop:
Each trade uses a dynamic trailing stop that moves in favor of the position and exits when price hits the stop.

Higher Time Frame EMA Filter:
Entries are only allowed if the 30-minute close is above (for longs) or below (for shorts) a 30-minute EMA, aligning trades with the higher time frame trend.

Trade Management:
Prevents immediate re-entry after an exit using flags.

PnL and Statistics Display:
Shows total PnL, current position PnL, trade count, win/loss count, and win rate directly on the chart.

Time Filter:
Optional trading window can be set by the user.

User Parameters
Fast MA Period
Slow MA Period
Trailing Stop Amount (in ticks)
Trade Quantity
Use Time Filter (on/off)
Start Time / End Time (for trading window)
ATR Multiplier (for trailing stop)
Break Even (Ticks) (for break-even stop adjustment)
*/