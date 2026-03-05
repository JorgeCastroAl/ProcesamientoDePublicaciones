namespace FluxAnswer.Services
{
    /// <summary>
    /// Represents the state of the video processing service.
    /// </summary>
    public enum ServiceState
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }
}

