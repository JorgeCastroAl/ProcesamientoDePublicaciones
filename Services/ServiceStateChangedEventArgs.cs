using System;

namespace VideoProcessingSystemV2.Services
{
    /// <summary>
    /// Event arguments for service state changes.
    /// </summary>
    public class ServiceStateChangedEventArgs : EventArgs
    {
        public ServiceState OldState { get; set; }
        public ServiceState NewState { get; set; }

        public ServiceStateChangedEventArgs(ServiceState oldState, ServiceState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }
}
