using System;
using System.Collections.Generic;
using System.Linq;

namespace Peer.Detection {
    /// <summary>
    /// A Detector that looks for JPG images
    /// </summary>
    class JPGDetector : BytewiseDetector {

        public string DisplayName => "JPG";
        public string Extension => ".jpg";

        // Detected elements
        private List<(long, long)> detections = new List<(long, long)>();
        // Temporary list, tracking sequences that _might_ be a JPG when we get more bytes
        public List<PotentialJPG> potentialJPGs = new List<PotentialJPG>();

        // The index into the file we're currently processing
        public int currentIdx = 0;

        // Magic (in bytes) for the start and end of an image
        // Always starts with "ff d8", followed by a segment
        // A segment always starts with "ff", thus it is "ff d8 ff"
        public static readonly byte[] MagicStart = { 0xFF, 0xD8, 0xFF };
        // Only some bytes are valid for the first segment
        // Checking it helps reduce false positives
        // The fourth byte has to be one of the following:
        public static readonly byte[] ValidMarkerBytes =
            { 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, // SOF0, SOF1, SOF2, SOF3, DHT, SOF5, SOF6
            // 0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, // RSTn (only exists in entropy-coded data, which we don't care about)
            0xDA, 0xDB, 0xDD, // SOS, DQT, DRI
            0xE0, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF, // APPn
            0xFE // COM
        };

        // The end segment is always "ff d9" 
        //public static readonly byte[] MagicEnd = { 0xFF, 0xD9 };

        public JPGDetector() {

        }

        public List<(long, long)> Detections() {
            return this.detections.ToList();
        }

        public void Process(byte b) {
            // If this byte might be the start of a JPG, add it to potential detections
            if (b == MagicStart[0]) {
                potentialJPGs.Add(new PotentialJPG(this.currentIdx));
            }
            // Update all potential detections with the new byte
            for (int i = potentialJPGs.Count - 1; i >= 0; i--) {
                if (!potentialJPGs[i].Process(b))
                    potentialJPGs.RemoveAt(i);
            }

            // Add all successfull ones to output
            // In order to remove some false-positives, consider the following:
            // https://stackoverflow.com/questions/2253404/what-is-the-smallest-valid-jpeg-file-size-in-bytes
            // If the detection is less than 119 bytes, it is very likely a fall positive
            foreach (var jpg in potentialJPGs) {
                if (jpg.done && (currentIdx + 1 - jpg.detectedStartIdx) > 119)
                    detections.Add((jpg.detectedStartIdx, currentIdx + 1));
            }

            // Remove finished elements
            potentialJPGs.RemoveAll(elem => elem.done);
            this.currentIdx++;
        }

        public void Reset() {
            Console.WriteLine("JPGDetector reset with  {0} potentials remaining", potentialJPGs.Count);
            this.currentIdx = 0;
            this.potentialJPGs = new List<PotentialJPG>();
        }

        // The first byte of a marker is always 0xFF
        // The second one identifies the marker type and thus how the length is defined
        // The length can be none, fixed or variable
        // None and fixed are returned as a number, variable is returned as null
        public static int? GetMarkerLengthFromByte(byte b) {
            // DRI is always 4
            if (b == 0xDD) {
                return 4;
            }
            // SOI, EOI and RSTn are 0
            if (b == 0xD8 || b == 0xD9 || (b >= 0xD0 && b <= 0xD7)) {
                return 0;
            }
            // All others are variable
            return null;
        }
    }

    // When we've found the 'magic start' we have to find a valid end segment
    // We can be in one of three states during this process:
    // FindMarker: keep going till we get a valid marker, i.e. 0xFF and one of "ValidMarkerBytes"
    // GetLength: if the marker has a variable length, use the next two bytes to find the length and set state of 'Wait'
    // Wait: skip the next n bytes depending on the marker length
    public enum JPGDetectionState {
        FindMarker,
        GetLength,
        Wait,
    }

    class PotentialJPG {
        private bool isLookingForStart = true;
        private bool isVerifyingFourthByte = false;
        private int magicProgress = 0;
        public int detectedStartIdx;
        // Done is set to 'true' when we have successfully reached a valid end segment
        public bool done = false;

        // Keep track of our state
        // Note that entropy-coded data byte-stuffs s.t. a 0xFF byte is always followed by 0x00 and thus not a valid marker
        private JPGDetectionState state;
        private int payloadRemaining = -1;
        private byte? firstLengthByte;

        // Some markers may be followed by entropy-coded data
        // In our case, we only consider SOS (0xFF 0xDA)
        // After it's variable-size payload, we may have any amount of entropy-coded data
        // If we run into what looks like entropy-coded data unexpectedly, we likely do not have an actual JPG
        // 
        // That is to say, after checking one sequence, the next byte should be 0xFF (next marker) UNLESS the previous sequence was SOS
        // If that isn't the case, stop
        // If it was the case (expectEntropy is 'true'), keep going until the next marker and reset this to 'false'
        private bool expectEntropy = false;

        public PotentialJPG(int start_idx) {
            detectedStartIdx = start_idx;
        }

        // Returns 'true' if it still looks valid, 'false' if it doesn't
        public bool Process(byte b) {
            if (isLookingForStart) {
                if (isVerifyingFourthByte) {
                    if (JPGDetector.ValidMarkerBytes.Contains(b)) {
                        // Swap to looking for EOI
                        magicProgress = 0;
                        isLookingForStart = false;

                        // True if the marker we just found is SOS, false otherwise
                        expectEntropy = b == 0xDA;

                        // Set initial 'state'
                        // This depends on the marker we just verified (and thus the current byte)
                        var len = JPGDetector.GetMarkerLengthFromByte(b);
                        if (len.HasValue) {
                            if (len.Value == 0) {
                                state = JPGDetectionState.FindMarker;
                            } else {
                                state = JPGDetectionState.Wait;
                                payloadRemaining = len.Value;
                            }
                        } else {
                            state = JPGDetectionState.GetLength;
                            firstLengthByte = null;
                        }
                        return true;
                    }
                    return false;
                } else {
                    if (b == JPGDetector.MagicStart[magicProgress]) {
                        magicProgress++;
                    } else {
                        return false;
                    }
                    if (magicProgress == JPGDetector.MagicStart.Length) {
                        isVerifyingFourthByte = true;
                    }
                    return true;
                }
            } else {
                // We keep looking until we find a valid end
                // Due to the short magic start (3 bytes + 1 from a list) we may get a lot of false-positives
                // If we detect a malformed marker, we return false

                // Wait: Do nothing, just count down until we're no longer in a payload
                if (state == JPGDetectionState.Wait) {
                    payloadRemaining--;
                    if (payloadRemaining <= 0)
                        state = JPGDetectionState.FindMarker;
                    return true;
                }

                // GetLength: Get the 2 bytes indicating payload length, then switch to wait state
                if (state == JPGDetectionState.GetLength) {
                    if (firstLengthByte.HasValue) {
                        int len = (firstLengthByte.Value << 8) + b;
                        payloadRemaining = len - 2; // Subtract the 2 bytes that make up the length indicator
                        state = JPGDetectionState.Wait;
                        return true;
                    } else {
                        firstLengthByte = b;
                        return true;
                    }
                }

                // FindMarker: look for the next marker and repeat previous checks
                if (magicProgress == 1) {
                    if (b == 0xD9) { // EOI
                        done = true;
                        return true;
                    } else {
                        magicProgress = 0;
                        // Check if it's a valid marker, stopping if it isn't
                        if (!JPGDetector.ValidMarkerBytes.Contains(b)) {
                            // Note that the sequence 0xFF 0xFF can't appear where we expect a marker
                            // so there is no chance the byte after a 0xFF is the "real" start of a marker
                            // i.e. We'd never see "0xFF 0xFF 0xC0" for a SOF0 (0xFF 0xC0) marker

                            // We know the previous byte was 0xFF
                            // If the current byte wasn't valid, it may be because it's 0x00 and we are in entropy-coded data
                            // In this case, it is working as expected and we skip the byte, otherwise this is an error
                            if (b == 0x00 && expectEntropy)
                                return true;
                            return false;
                        }
                        // Reset expectEntropy
                        // True if the marker we just found is SOS, false otherwise
                        expectEntropy = b == 0xDA;

                        var len = JPGDetector.GetMarkerLengthFromByte(b);
                        if (len.HasValue) {
                            if (len.Value == 0) {
                                state = JPGDetectionState.FindMarker;
                            } else {
                                state = JPGDetectionState.Wait;
                                payloadRemaining = len.Value;
                            }
                        } else {
                            state = JPGDetectionState.GetLength;
                            firstLengthByte = null;
                        }
                    }
                } else {
                    if (b == 0xFF)
                        magicProgress = 1;
                    else {
                        // We finished a 'Wait' state but didn't find a 0xFF for the next marker
                        // This means we _may_ have entropy-encoded data
                        // If we are not expecting is (i.e. the previous marker was SOS), this is an ERROR
                        magicProgress = 0;
                        if (!expectEntropy) {
                            return false;
                        }
                    }

                }

                return true;
            }
        }
    }
}
