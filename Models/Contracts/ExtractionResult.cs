using System.Collections.Generic;

namespace FluxAnswer.Models
{
    /// <summary>
    /// Result of a video extraction operation for an account.
    /// </summary>
    public class ExtractionResult
    {
        public string AccountUsername { get; set; } = string.Empty;
        public int VideosFound { get; set; }
        public int VideosInserted { get; set; }
        public int DuplicatesSkipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public bool IsSuccess => Errors.Count == 0;
    }
}

