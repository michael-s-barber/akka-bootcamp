﻿using Akka.Actor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WinTail
{
    public class FileValidatorActor : UntypedActor
    {
        private readonly IActorRef _consoleWriterActor;
        //private readonly IActorRef _tailCoordinatorActor;

        public FileValidatorActor(IActorRef consoleWriter)
        {
            _consoleWriterActor = consoleWriter;
            //_tailCoordinatorActor = tailCoordinatorActor;
        }

        protected override void OnReceive(object message)
        {
            var msg = message as string;
            if (string.IsNullOrEmpty(msg))
            {
                // signal that the user needs to supply an input
                _consoleWriterActor.Tell(new Messages.NullInputError("Input was blank. Please try again.\n"));

                // tell sender to continue doing its thing (whatever that may be,
                // this actor doesn't care)
                Sender.Tell(new Messages.ContinueProcessing());
            }
            else
            {
                var valid = IsFileUri(msg);
                if (valid)
                {
                    // feedback to console, and tell coordinator to watch the file
                    _consoleWriterActor.Tell(new Messages.InputSuccess($"Starting processing for {msg}"));
                    //_tailCoordinatorActor.Tell(new TailCoordinatorActor.StartTail(msg, _consoleWriterActor));
                    Context.ActorSelection("akka://MyActorSystem/user/tailCoordinatorActor")
                        .Tell(new TailCoordinatorActor.StartTail(msg, _consoleWriterActor));
                }
                else
                {
                    // tell us about the problem
                    _consoleWriterActor.Tell(new Messages.ValidationError($"{msg} is not a valid URI."));
                }
                Sender.Tell(new Messages.ContinueProcessing());
            }
        }

        private bool IsFileUri(string msg) => File.Exists(msg);
    }
}
