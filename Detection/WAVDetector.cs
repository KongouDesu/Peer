using System;
using System.Collections.Generic;
using System.Linq;

namespace Peer.Detection {
    /// <summary>
    /// A Detector that looks for WAV audio 
    /// Searches for the magic 'RIFF????WAVEfmt' where '????' is 4 bytes indicating the length
    /// WAV format is Little Endian
    /// Note that since a 'length' concept is included, we can determine the start and end from the header
    /// </summary>
    class WAVDetector : BytewiseDetector {

        public string DisplayName => "WAV";
        public string Extension => ".wav";

        private List<(long, long)> detections = new List<(long, long)>();
        // Temporary list, tracking sequences that _might_ be a WAV when we get more bytes
        private List<PotentialWAV> potentialWAVs = new List<PotentialWAV>();
        private int currentIdx = 0;


        public static readonly byte[] MagicRIFF = { 0x52, 0x49, 0x46, 0x46 };
        public static readonly byte[] MagicWAVEfmt = { 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74 };

        public WAVDetector() {

        }

        public List<(long, long)> Detections() {
            return this.detections.ToList();
        }

        public void Process(byte b) {
            // If this byte might be the start of a file, add it to potential detections
            if (b == MagicRIFF[0]) {
                potentialWAVs.Add(new PotentialWAV(this.currentIdx));
            }
            // Update all potential detections with the new byte
            for (int i = potentialWAVs.Count - 1; i >= 0; i--) {
                if (!potentialWAVs[i].Process(b))
                    potentialWAVs.RemoveAt(i);
            }

            // Add all successfull ones to output
            foreach (var wav in potentialWAVs) {
                if (wav.done)
                    detections.Add((wav.detectedStartIdx, wav.endIndex));
            }
            // Remove finished elements
            for (int i = potentialWAVs.Count - 1; i >= 0; i--) {
                if (potentialWAVs[i].done)
                    potentialWAVs.RemoveAt(i);
            }

            this.currentIdx++;
        }

        public void Reset() {
            Console.WriteLine("WAVDetector reset with  {0} potentials remaining", potentialWAVs.Count);
            this.currentIdx = 0;
            potentialWAVs = new List<PotentialWAV>();
        }
    }

    class PotentialWAV {
        private int magicProgress = 0;
        private byte[] lenBuffer = new byte[4];
        public int detectedStartIdx;
        // Done is set to 'true' when we have successfully reached a valid end segment
        public bool done = false;
        public int endIndex = -1;


        public PotentialWAV(int start_idx) {
            detectedStartIdx = start_idx;
        }

        // Returns 'true' if it still looks valid, 'false' if it doesn't
        public bool Process(byte b) {
            // Check if the start is 'RIFF'
            if (magicProgress < WAVDetector.MagicRIFF.Length) {
                if (b == WAVDetector.MagicRIFF[magicProgress]) {
                    magicProgress++;
                    return true;
                }
                // Read length bytes
            } else if (magicProgress < WAVDetector.MagicRIFF.Length + 4) {
                int mp = magicProgress - WAVDetector.MagicRIFF.Length;
                lenBuffer[mp] = b;
                magicProgress++;
                return true;
                // Verify length is followed by 'WAVEfmt' 
            } else if (magicProgress < WAVDetector.MagicRIFF.Length + 4 + WAVDetector.MagicWAVEfmt.Length) {
                int mp = magicProgress - WAVDetector.MagicRIFF.Length - 4;
                if (b == WAVDetector.MagicWAVEfmt[mp]) {
                    if (mp == WAVDetector.MagicWAVEfmt.Length - 1) {
                        done = true;
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(lenBuffer);
                        endIndex = detectedStartIdx + BitConverter.ToInt32(lenBuffer, 0) + 8; // Add 8 (4 for 'RIFF', 4 for the length bytes)
                    }
                    magicProgress++;
                    return true;
                }
            }
            return false;
        }
    }

}
