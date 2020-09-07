using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Peer.Detection {
    /// <summary>
    /// A Detector that looks for PNG images
    /// Searches for the magic number and the IHDR chunk
    /// To detect the end of the image, it looks for the IEND chunk
    /// </summary>
    class PNGDetector : BytewiseDetector {

        public string DisplayName => "PNG";
        public string Extension => ".png";

        public Action<Stream> Display => Detection.Visualizers.ImageVisualizer.Display;

        public List<(long, long)> Detections { get; } = new List<(long, long)>();

        // Temporary list, tracking sequences that _might_ be a JPG when we get more bytes
        private List<PotentialPNG> potentialPNGs = new List<PotentialPNG>();
        private int currentIdx = 0;

        // Magic (in bytes) for the start and end of an image
        // Since the IHDR has to be the first chunk, we also check for that
        // A chunk has the format 
        // Length: 4 bytes
        // Chunk type: 4 bytes
        // Data: 'Length' bytes
        // CRC: 4 bytes
        // The IHDR chunk is always 13 bytes, thus the 'Length' is "00 00 00 0D"
        // IHDR is '49 48 44 52'
        // 
        // Encountering this entire sequence randomly is very low, so we don't bother to do further verification
        public static readonly byte[] MagicStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };

        // The IEND chunk has a Length of 0, thus the 'Length' is "00 00 00 00"
        // IEND is '49 45 4E 44'
        // We need to include the 4-byte CRC after this when saving a detection
        // The CRC is based on chunk type and data, since the data is 0, it is just the chunk, which is always IEND
        // Thus, the CRC is always the same - if we also check that, we get a fairly long sequence
        // The CRC is "AE 42 60 82"
        //
        // As with the MagicStart, this is long enough that we won't randomly encounter it
        // Note that most image-viewers WILL STILL READ THE FILE WITH THE CRC MISSING
        public static readonly byte[] MagicEnd = { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

        public PNGDetector() {

        }

        public void ProcessBytes(byte[] bytes) {
            foreach (byte b in bytes) {
                // If this byte might be the start of a PNG, add it to potential detections
                if (b == MagicStart[0]) {
                    potentialPNGs.Add(new PotentialPNG(this.currentIdx));
                }
                // Update all potential detections with the new byte
                for (int i = potentialPNGs.Count - 1; i >= 0; i--) {
                    if (!potentialPNGs[i].Process(b))
                        potentialPNGs.RemoveAt(i);
                }
                //potentialPNGs.RemoveAll(elem => !elem.Process(b));
                // Add all successfull ones to output
                foreach (var png in potentialPNGs) {
                    if (png.done)
                        Detections.Add((png.detectedStartIdx, currentIdx + 1));
                }
                // Remove finished elements
                //potentialPNGs.RemoveAll(elem => elem.done);
                for (int i = potentialPNGs.Count - 1; i >= 0; i--) {
                    if (potentialPNGs[i].done)
                        potentialPNGs.RemoveAt(i);
                }

                this.currentIdx++;
            }
        }

        public void Reset() {
            Detections.Clear();
            potentialPNGs.Clear();
            currentIdx = 0;
        }

        public void FinalizeDetection() {}
    }

    class PotentialPNG {
        private bool isLookingForStart = true;
        private int magicProgress = 0;
        public int detectedStartIdx;
        // Done is set to 'true' when we have successfully reached a valid end segment
        public bool done = false;

        public PotentialPNG(int start_idx) {
            detectedStartIdx = start_idx;
        }

        // Returns 'true' if it still looks valid, 'false' if it doesn't
        public bool Process(byte b) {
            if (this.isLookingForStart) {
                // Detect MagicStart
                if (b == PNGDetector.MagicStart[this.magicProgress]) {
                    this.magicProgress++;
                } else {
                    return false;
                }
                if (this.magicProgress == PNGDetector.MagicStart.Length) {
                    this.magicProgress = 0;
                    this.isLookingForStart = false;
                }
                return true;
            } else {
                // Detect MagicEnd
                if (b == PNGDetector.MagicEnd[this.magicProgress]) {
                    this.magicProgress++;
                } else {
                    this.magicProgress = 0;
                }
                if (this.magicProgress == PNGDetector.MagicEnd.Length) {
                    done = true;
                }
                return true;
            }
        }
    }

}
