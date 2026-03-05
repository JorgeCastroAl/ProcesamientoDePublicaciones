using System;
using System.Collections.Generic;
using FluxAnswer.Models;

namespace FluxAnswer.Extraction
{
    /// <summary>
    /// Event arguments for extraction cycle completion.
    /// </summary>
    public class ExtractionCompletedEventArgs : EventArgs
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int AccountsProcessed { get; set; }
        public int TotalVideosFound { get; set; }
        public int TotalVideosInserted { get; set; }
        public int TotalDuplicatesSkipped { get; set; }
        public List<ExtractionResult> Results { get; set; } = new List<ExtractionResult>();

        public TimeSpan Duration => EndTime - StartTime;
        public bool HasErrors => Results.Exists(r => !r.IsSuccess);
    }
}

