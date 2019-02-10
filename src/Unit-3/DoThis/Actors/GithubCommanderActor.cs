using System;
using System.Linq;
using Akka.Actor;
using Akka.Routing;

namespace GithubActors.Actors
{
    /// <summary>
    /// Top-level actor responsible for coordinating and launching repo-processing jobs
    /// </summary>
    public class GithubCommanderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Message classes

        public class CanAcceptJob
        {
            public CanAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class AbleToAcceptJob
        {
            public AbleToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        public class UnableToAcceptJob
        {
            public UnableToAcceptJob(RepoKey repo)
            {
                Repo = repo;
            }

            public RepoKey Repo { get; private set; }
        }

        #endregion

        private IActorRef _coordinator;
        private IActorRef _canAcceptJobSender;

        private RepoKey _repoJob;

        public IStash Stash { get; set; }

        private int pendingJobReplies;

        public GithubCommanderActor()
        {
            Ready();
        }

        private void Ready()
        {
            Receive<CanAcceptJob>(job =>
            {
                _coordinator.Tell(job);
                _repoJob = job.Repo;
                BecomeAsking();
            });
        }

        private void BecomeAsking()
        {
            _canAcceptJobSender = Sender;

            // Block, but ask the router for the number of routees. 
            // (that bit comes from configuration, so this makes more sense)
            // We can't really proceed otherwise, so we do this synchronously.
            pendingJobReplies = _coordinator.Ask<Routees>(new GetRoutees()).Result.Members.Count();
            
            Become(Asking);

            // send ourselves a ReceiveTimeout message if no message within 3 seconds
            Context.SetReceiveTimeout(TimeSpan.FromSeconds(3));
        }

        private void Asking()
        {
            // stash any subsequent requests
            Receive<CanAcceptJob>(job => Stash.Stash());

            // handle timeout while waiting
            // means at least one actor failed to respond
            Receive<ReceiveTimeout>(timeout =>
            {
                _canAcceptJobSender.Tell(new UnableToAcceptJob(_repoJob));
                BecomeReady();
            });

            Receive<UnableToAcceptJob>(job =>
            {
                pendingJobReplies--;
                if (pendingJobReplies == 0)
                {
                    _canAcceptJobSender.Tell(job);
                    BecomeReady();
                }
            });

            Receive<AbleToAcceptJob>(job =>
            {
                _canAcceptJobSender.Tell(job);

                // start processing messages
                Sender.Tell(new GithubCoordinatorActor.BeginJob(job.Repo));

                // launch the new window to view results of the processing
                Context.ActorSelection(ActorPaths.MainFormActor.Path).Tell(
                    new MainFormActor.LaunchRepoResultsWindow(job.Repo, Sender));

                BecomeReady();
            });
        }

        private void BecomeReady()
        {
            Become(Ready);
            Stash.UnstashAll();

            // cancel timeout
            Context.SetReceiveTimeout(null);
        }



        protected override void PreStart()
        {
            _coordinator = Context.ActorOf(Props.Create(() => new GithubCoordinatorActor()).WithRouter(FromConfig.Instance),
                ActorPaths.GithubCoordinatorActor.Name);
            
            base.PreStart();
        }
    }
}
