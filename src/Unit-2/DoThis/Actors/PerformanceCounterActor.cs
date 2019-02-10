using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChartApp.Actors
{
    public class PerformanceCounterActor : UntypedActor
    {
        private readonly string _seriesName;
        private readonly Func<PerformanceCounter> _performanceCounterCreateFn;
        private PerformanceCounter _counter;
        private ICancelable _cancelPublishing;

        private readonly HashSet<IActorRef> _subscribers;
        

        public PerformanceCounterActor(string seriesName, Func<PerformanceCounter> performanceCounterCreateFn)
        {
            _seriesName = seriesName;
            _performanceCounterCreateFn = performanceCounterCreateFn;
            _subscribers = new HashSet<IActorRef>();

            // create new cancelable tied to the scheduler --  we get it from Context.System.Scheduler
            //_cancelPublishing = new Cancelable(Context.System.Scheduler);
        }

        protected override void PreStart()
        {
            _counter = _performanceCounterCreateFn();

            _cancelPublishing = Context.System.Scheduler
                .ScheduleTellRepeatedlyCancelable(
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromMilliseconds(250),
                    Self,
                    new GatherMetrics(),
                    Self);
        }

        protected override void PostStop()
        {
            try
            {
                // terminate the scheduled message
                _cancelPublishing?.Cancel(false);
                // and dispose of the performance counter
                _counter?.Dispose();
            }
            catch
            {
                // ignore any disposal exceptions
            }
            finally
            {
                base.PostStop();
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case GatherMetrics gather:
                    var metric = new Metric(_seriesName, _counter.NextValue());
                    foreach (var s in _subscribers) s.Tell(metric);
                    break;
                case SubscribeCounter subscribe:
                    _subscribers.Add(subscribe.Subscriber);
                    break;
                case UnsubscribeCounter unsubscribe:
                    _subscribers.Remove(unsubscribe.Subscriber);
                    break;
            }
        }
    }
}
