using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peer.Detection.Detectors {

    /// <summary>
    /// Ogg Audio file detector
    /// Looks for the "capture pattern" magic, then the EOS page
    /// </summary>
    class OggDetector : BytewiseDetector {
        public string DisplayName => "Ogg";
        public string Extension => ".ogg";

        public Action<Stream> Display => Detection.Visualizers.None.Display;

        public List<(long, long)> Detections => this.detections;

        private List<(long, long)> detections = new List<(long, long)>();
        private List<PotentialOgg> potentialOggs = new List<PotentialOgg>();
        private int currentIdx = 0;


        // Always starts with 'OggS'
        // The version field is required to be '0x0'
        // We want the start, so we only accept header type BOS (Beginning of Stream) which is '0x2'
        public static readonly byte[] MagicOggS = { 0x4F, 0x67, 0x67, 0x53, 0x0, 0x2 };

        // Magic for the last page, 'OggS', '0x0'
        // Note that we require an extra, final byte, which can vary
        // The header type EOS (End of Stream) is '0x4'
        // HOWEVER it is a _flag_ and can thus be combined with BOS (0x2) and Continuation (0x1)
        // This means  0x4, 0x5, 0x6 and 0x7 are all technically valid end bytes
        public static readonly byte[] MagicOggLast = { 0x4F, 0x67, 0x67, 0x53, 0x0 };
        public static readonly byte[] MagicOggValidFinal = { 0x4, 0x5, 0x6, 0x7 };

        public void FinalizeDetection() {

        }

        public void ProcessBytes(byte[] bytes) {
            foreach (byte b in bytes) {
                // If this byte might be the start of a file, add it to potential detections
                if (b == MagicOggS[0]) {
                    potentialOggs.Add(new PotentialOgg(this.currentIdx));
                }
                // Update all potential detections with the new byte
                for (int i = potentialOggs.Count - 1; i >= 0; i--) {
                    if (!potentialOggs[i].Process(b))
                        potentialOggs.RemoveAt(i);
                }

                // Add all successfull ones to output
                foreach (var ogg in potentialOggs) {
                    if (ogg.done)
                        detections.Add((ogg.detectedStartIdx, ogg.endIndex));
                }
                // Remove finished elements
                for (int i = potentialOggs.Count - 1; i >= 0; i--) {
                    if (potentialOggs[i].done)
                        potentialOggs.RemoveAt(i);
                }

                this.currentIdx++;
            }
        }

        public void Reset() {
            Detections.Clear();
            potentialOggs.Clear();
            currentIdx = 0;
        }
    }

    // The states of a potential ogg:
    // Start: find the start magic bytes
    // End: find the end magic bytes
    // SegmentNum: find the number of segments
    // SegmentTally: count number of bytes in segment
    // Once we've counted the number of bytes in the last one, we know where it ends and can stop
    enum OggState {
        Start,
        End,
        SegmentNum,
        SegmentTally,
    }

    class PotentialOgg {

        private int magicProgress = 0;
        private OggState state = OggState.Start;
        // Number of segments in the page
        private int numSegments = 0;
        // Size in bytes of those segments
        private int segmentsSize = 0;

        public int detectedStartIdx;
        // Done is set to 'true' when we have successfully reached a valid end segment
        public bool done = false;
        public int endIndex = -1;

        // Skip processing this many bytes
        public int skip = 0;

        public PotentialOgg(int start_idx) {
            detectedStartIdx = start_idx;
            endIndex = start_idx;
        }

        public bool Process(byte b) {
            endIndex++;
            if (skip > 0) {
                skip--;
                return true;
            }

            if (state == OggState.Start) {
                if (magicProgress < OggDetector.MagicOggS.Length) {
                    if (b == OggDetector.MagicOggS[magicProgress]) {
                        magicProgress++;
                        if (magicProgress == OggDetector.MagicOggS.Length) {
                            magicProgress = 0;
                            state = OggState.End;
                        }
                        return true;
                    }
                }
                if (detectedStartIdx == 0)
                    Console.WriteLine("Failed at 0x{0:X}", b);
                return false;
            } else if (state == OggState.End) {
                // Note: we use <= instead of < s.t. we can check the MagicOggValidFinal byte
                if (magicProgress <= OggDetector.MagicOggLast.Length) {
                    if (magicProgress < OggDetector.MagicOggLast.Length) {
                        if (b == OggDetector.MagicOggLast[magicProgress])
                            magicProgress++;
                        else
                            magicProgress = 0;
                    } else {
                        if (OggDetector.MagicOggValidFinal.Contains(b))
                            magicProgress++;
                        else
                            magicProgress = 0;
                    }
                    if (magicProgress == OggDetector.MagicOggLast.Length+1) { // +1 for the 'MagicOggValidFinal' byte 
                        skip = 20; // Page segment is 20 bytes after the magic, since magic ends at 'header type'
                        state = OggState.SegmentNum;
                    }
                }
                return true;
            } else if (state == OggState.SegmentNum) {
                numSegments = (int)b;
                state = OggState.SegmentTally;
                return true;
            } else {
                segmentsSize += (int)b;
                numSegments--;
                if (numSegments <= 0) {
                    endIndex += segmentsSize;
                    done = true;
                }
                return true;
            }
        }

    }
}
