using System;
using System.Collections.Generic;

namespace Peer {
    interface BytewiseDetector {
        /// <summary>
        /// Handle the next incoming byte
        /// </summary>
        /// <param name="b">The next byte in the sequence</param>
        void Process(byte b);

        /// <summary>
        /// Called to indicate that the stream has changed and all state should be reset
        /// </summary>
        void Reset();

        /// <summary>
        /// Returns all the detections as (start,end) indices into the file
        /// </summary>
        /// <returns>List of (start,end) tuples</returns>
        List<(long, long)> Detections();

        /// <summary>
        /// Get the human-readable name for what this detector detects, e.g. "PNG" or "WAV"
        /// </summary>
        /// <returns>Identifying name</returns>
        string DisplayName { get; }

        /// <summary>
        /// Get the standard file extension for this detector, e.g. ".png" or ".wav"
        /// </summary>
        string Extension { get; }
    }
}
