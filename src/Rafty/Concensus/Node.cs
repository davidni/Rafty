namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Node : IDisposable, INode
    {
        private readonly List<Guid> _appendEntriesIdsReceived;
        private readonly ISendToSelf _sendToSelf;
        private Guid _appendEntriesAtPreviousHeartbeat;

        public Node(CurrentState initialState, ISendToSelf sendToSelf)
        {
            _appendEntriesIdsReceived = new List<Guid>();
            _sendToSelf = sendToSelf;
            State = new Follower(initialState, _sendToSelf);
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        public IState State { get; private set; }

        public void Handle(Message message)
        {
            //todo - could run middleware type functions here?
            //todo - these handlers should be in a dictionary
            if (message.GetType() == typeof(BeginElection))
            {
                Handle((BeginElection) message);
            }

            if (message.GetType() == typeof(Timeout))
            {
                Handle((Timeout) message);
            }
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            //Reply false if term < currentTerm (§5.1)
            if(appendEntries.Term < State.CurrentState.CurrentTerm)
            {
                return new AppendEntriesResponse(State.CurrentState.CurrentTerm, false);
            }

            // Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)
            var termAtPreviousLogIndex = State.CurrentState.Log.TermAtIndex(appendEntries.PreviousLogIndex);
            if(termAtPreviousLogIndex != appendEntries.PreviousLogTerm)
            {
                return new AppendEntriesResponse(State.CurrentState.CurrentTerm, false);
            }

            _appendEntriesIdsReceived.Add(appendEntries.MessageId);
            return new AppendEntriesResponse(State.CurrentState.CurrentTerm, true);
        }

        private void Handle(BeginElection beginElection)
        {
            State = State.Handle(beginElection);
        }

        private void Handle(Timeout timeout)
        {
            if (NoHeartbeatSinceLastTimeout())
            {
                State = State.Handle(timeout);

                if(State is Candidate)
                {
                    _sendToSelf.Publish(new BeginElection());
                }
            }

            if (AppendEntriesReceived())
            {
                _appendEntriesAtPreviousHeartbeat = _appendEntriesIdsReceived.Last();
            }
        }

        private bool AppendEntriesReceived()
        {
            return _appendEntriesIdsReceived.Any();
        }

        private bool NoHeartbeatSinceLastTimeout()
        {
            if (!_appendEntriesIdsReceived.Any())
            {
                return true;
            }

            return _appendEntriesIdsReceived.Last() == _appendEntriesAtPreviousHeartbeat;
        }
    }
}