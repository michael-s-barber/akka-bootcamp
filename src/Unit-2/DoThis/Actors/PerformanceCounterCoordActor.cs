using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;

namespace ChartApp.Actors
{
    public class PerformanceCounterCoordActor : ReceiveActor
    {
        #region Message types

        /// <summary>
        /// Subscribe the <see cref="ChartingActor"/> to 
        /// updates for <see cref="Counter"/>.
        /// </summary>
        public class Watch
        {
            public Watch(CounterType counter)
            {
                Counter = counter;
            }

            public CounterType Counter { get; private set; }
        }

        /// <summary>
        /// Unsubscribe the <see cref="ChartingActor"/> to 
        /// updates for <see cref="Counter"/>
        /// </summary>
        public class Unwatch
        {
            public Unwatch(CounterType counter)
            {
                Counter = counter;
            }

            public CounterType Counter { get; private set; }
        }

        #endregion

        // graph series creators
        private static readonly Dictionary<CounterType, Func<Series>> _counterSeriesCreateFunctions =
            new Dictionary<CounterType, Func<Series>>()
        {
            [CounterType.Cpu] = () => new Series(CounterType.Cpu.ToString()){
                 ChartType = SeriesChartType.SplineArea,
                 Color = Color.DarkGreen},
            [CounterType.Memory] = () => new Series(CounterType.Memory.ToString()){
                ChartType = SeriesChartType.FastLine,
                Color = Color.MediumBlue},
            [CounterType.Disk] = () => new Series(CounterType.Disk.ToString()){
                ChartType = SeriesChartType.SplineArea,
                Color = Color.DarkRed}
        };

        // performance counter creators
        private Dictionary<CounterType, Func<PerformanceCounter>> _counterCreateFunctions = 
            new Dictionary<CounterType, Func<PerformanceCounter>>()
        {
            [CounterType.Cpu] = () => new PerformanceCounter("Processor", "% Processor Time", "_Total", true),
            [CounterType.Memory] = () => new PerformanceCounter("Memory", "% Committed Bytes In Use", true),
            [CounterType.Disk] = () => new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true)
        };


        private Dictionary<CounterType, IActorRef> _counterActors;
        private IActorRef _chartingActor;

        public PerformanceCounterCoordActor(IActorRef chartingActor)
            : this(chartingActor, new Dictionary<CounterType, IActorRef>())
        {
        }

        public PerformanceCounterCoordActor(IActorRef chartingActor, Dictionary<CounterType, IActorRef> counterActors)
        {
            _counterActors = counterActors;
            _chartingActor = chartingActor;

            Receive<Watch>(w => {
                
                // retrieve or create the actor (should probably just use Lazy for this)
                if (!_counterActors.TryGetValue(w.Counter, out var actor))
                {
                    var createFn = _counterCreateFunctions[w.Counter];
                    actor = Context.ActorOf(Props.Create(() =>
                        new PerformanceCounterActor(w.Counter.ToString(), createFn)));
                    _counterActors[w.Counter] = actor;
                }

                // tell the chart about this series -- give it the create function to run
                var series = _counterSeriesCreateFunctions[w.Counter]();
                _chartingActor.Tell(new ChartingActor.AddSeries(series));

                // tell the counter actor to start publishing to the chart
                actor.Tell(new SubscribeCounter(w.Counter, _chartingActor));
            });

            Receive<Unwatch>(w => {

                if (!_counterActors.TryGetValue(w.Counter, out var actor))
                {
                    return;
                }

                // unsubscribe the chart from further updates
                actor.Tell(new UnsubscribeCounter(w.Counter, _chartingActor));

                // remove the series
                _chartingActor.Tell(new ChartingActor.RemoveSeries(w.Counter.ToString()));
            });


        }

       
    }
}
