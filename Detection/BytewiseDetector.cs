using System;
using System.Collections.Generic;

namespace Peer {
    interface BytewiseDetector {
        /// <summary>
        /// Handle the next n bytes
        /// Processes arrays of bytes at a time to avoid function call overhead
        /// </summary>
        /// <param name="b">The next byte in the sequence</param>
        void ProcessBytes(byte[] bytes);

        /// <summary>
        /// Called to indicate that the input stream is done
        /// In some cases, we can only find out where a file ends by adding it when the stream ends, if it appears valid at that point
        /// </summary>
        void FinalizeDetection();

        /// <summary>
        /// Reset all internal state
        /// Useful when we want to reuse the same detectors on a new stream
        /// </summary>
        void Reset();

        /// <summary>
        /// Returns all the detections as (start,end) indices into the file
        /// </summary>
        /// <returns>List of (start,end) tuples</returns>
        List<(long, long)> Detections { get; }

        /// <summary>
        /// Get the human-readable name for what this detector detects, e.g. "PNG" or "WAV"
        /// </summary>
        /// <returns>Identifying name</returns>
        string DisplayName { get; }

        /// <summary>
        /// Get the standard file extension for this detector, e.g. ".png" or ".wav"
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// The Action (method) to execute when we want to display a detection
        /// </summary>
        Action<System.IO.Stream> Display { get; }
    }
}
