using System;

namespace VideoProcessingSystemV2.Services
{
    /// <summary>
    /// Event arguments for service errors.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; set; }
        public Exception? Exception { get; set; }

        public ErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }
}
