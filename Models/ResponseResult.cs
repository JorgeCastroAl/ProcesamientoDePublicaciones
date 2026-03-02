namespace VideoProcessingSystemV2.Models
{
    /// <summary>
    /// Result of a response generation operation.
    /// </summary>
    public class ResponseResult
    {
        public bool Success { get; set; }
        public string ResponseText { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
