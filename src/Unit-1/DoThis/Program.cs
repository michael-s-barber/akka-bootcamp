using System;
﻿using Akka.Actor;

namespace WinTail
{
    #region Program
    class Program
    {
        public static ActorSystem MyActorSystem;

        static void Main(string[] args)
        {
            // initialize MyActorSystem
            // YOU NEED TO FILL IN HERE
            MyActorSystem = ActorSystem.Create("MyActorSystem");

            // create the props and actor separately
            var consoleWriterActorProps = Props.Create<ConsoleWriterActor>();
            var consoleWriterActor = MyActorSystem.ActorOf(consoleWriterActorProps, "consoleWriterActor"); // named

            var tailCoordActorProps = Props.Create<TailCoordinatorActor>();
            var tailCoordActor = MyActorSystem.ActorOf(tailCoordActorProps, "tailCoordinatorActor");

            var validationActorProps = Props.Create(() => new FileValidatorActor(consoleWriterActor));
            var validationActor = MyActorSystem.ActorOf(validationActorProps, "validationActor");

            var consoleReaderProps = Props.Create<ConsoleReaderActor>(() => new ConsoleReaderActor());
            var consoleReaderActor = MyActorSystem.ActorOf(consoleReaderProps, "consoleReaderActor");

            // tell console reader to begin
            consoleReaderActor.Tell(ConsoleReaderActor.StartCommand); 

            // blocks the main thread from exiting until the actor system is shut down
            MyActorSystem.WhenTerminated.Wait();
        }

        
    }
    #endregion
}
