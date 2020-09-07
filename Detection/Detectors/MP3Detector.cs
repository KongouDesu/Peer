using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peer.Detection.Detectors {

    /// <summary>
    /// A detector that can find MP3 files
    /// First detects an ID3v2 tag, then keeps looking for MP3 frames till it runs out
    /// </summary>
    class MP3Detector : BytewiseDetector {

        // Using http://id3lib.sourceforge.net/id3/id3v2com-00.html#sec3 as reference for ID3v2
        // ID3v2 is in little-endian
        // Uses http://www.mpgedit.org/mpgedit/mpeg_format/mpeghdr.htm as reference for MP3 header format

        public List<(long, long)> Detections { get; } = new List<(long, long)>();

        public string DisplayName => "MP3";
        public string Extension => ".mp3";
        public Action<Stream> Display => Detection.Visualizers.None.Display;

        private List<PotentialMP3> potentialMP3s = new List<PotentialMP3>();
        private int currentIdx = 0;

        public static readonly byte[] MagicStart = { 0x49, 0x44, 0x33 }; // 'ID3' - used by ID3v2
        public static readonly byte[] MagicTag = { 0x54, 0x41, 0x47 }; // 'TAG' - used by ID3v1

        /// <summary>
        /// Get the bitrate of the frame
        /// </summary>
        /// <param name="version">MPEG version, either 0b10 or 0b11</param>
        /// <param name="layer">Layer, one of 0b01, 0b10 or 0b11</param>
        /// <param name="bitrateIndex">Anywhere from 0b0000 to 0b1111</param>
        /// <returns>Bitrate in kbps</returns>
        // The 'free' field is not technically supported, so 'free' and 'bad' are both -1
        public static int GetBitrate(int version, int layer, int bitrateIndex) {
            // Invalid index: return -1
            if (bitrateIndex > 0b1111 || bitrateIndex < 0)
                return -1;
            if (version == 0b11) { // V1
                switch (layer) {
                    case 0b11: // V1L1
                        int[] bitratesV1L1 = {-1, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, -1};
                        return bitratesV1L1[bitrateIndex];
                    case 0b10: // V1L2
                        int[] bitratesV1L2 = { -1, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, -1 };
                        return bitratesV1L2[bitrateIndex];
                    case 0b01: // V1L3
                        int[] bitratesV1L3 = { -1, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, -1 };
                        return bitratesV1L3[bitrateIndex];
                    default:
                        return -1;
                }
            } else if (version == 0b10) { // V2
                if (layer == 0b11) {
                    int[] bitratesV2L1 = { -1, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, -1 };
                    return bitratesV2L1[bitrateIndex];
                } else if (layer == 0b10 || layer == 0b01) {
                    int[] bitratesV2L2L3 = { -1, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, -1 };
                    return bitratesV2L2L3[bitrateIndex];
                } else {
                    return -1;
                }
            }
            // Invalid version: return -1
            return -1;
        }

        /// <summary>
        /// Gets the sampling rate frequency associated with a version for the given index
        /// Note MPEG2.5 is not supported
        /// </summary>
        /// <param name="version">MPEG version</param>
        /// <param name="samplingIndex">Valid indices are 00, 01 and 10 (11 is reserved)</param>
        /// <returns>Sampling rate in Hz; Returns -1 on invalid input</returns>
        public static int GetSamplingRate(int version, int samplingIndex) {
            // Return -1 on invalid index
            if (samplingIndex >= 0b11 || samplingIndex < 0)
                return -1;

            if (version == 0b11) { // V1
                int[] ratesV1 = { 44100, 48000, 32000 };
                return ratesV1[samplingIndex];
            } else if (version == 0b10) { // V2
                int[] ratesV2 = { 22050, 24000, 16000 };
                return ratesV2[samplingIndex];
            } else { // Invalid version, return -1
                return -1;
            }
        }

        public void ProcessBytes(byte[] bytes) {
            foreach (byte b in bytes) {
                // If this byte might be the start of a MP3, add it to potential detections
                // This is either the 'I' in 'ID3' for an ID3v2 header OR a 0xFF byte for an MP3 with no ID3v2
                if (b == MagicStart[0]) { // The 'I' in 'ID3'
                    potentialMP3s.Add(new PotentialMP3(this.currentIdx, MP3State.ID3v2));
                }
                // TODO: enable this after adding de-duplication
                // Problem: of course  there is always a valid mp3 inside a valid ID3v2 detection
                // Thus we'd get two detections for an mp3 with an ID3v2 header: one with and one without the header
                // We need to add some logic to remove the non-header one if a header version is found
                // This can be done by checking if two detections end on the same index and only keeping the one that starts earlier
                //else if (b == 0xFF) {
                //    potentialMP3s.Add(new PotentialMP3(this.currentIdx, MP3State.Frame));
                //}
                // Update all potential detections with the new byte
                for (int i = potentialMP3s.Count - 1; i >= 0; i--) {
                    if (!potentialMP3s[i].Process(b))
                        potentialMP3s.RemoveAt(i);
                }
                // Add all successfull ones to output
                foreach (var mp3 in potentialMP3s) {
                    if (mp3.done)
                        Detections.Add((mp3.detectedStartIdx, mp3.detectedStartIdx+mp3.processedBytes));
                }
                // Remove finished elements
                for (int i = potentialMP3s.Count - 1; i >= 0; i--) {
                    if (potentialMP3s[i].done)
                        potentialMP3s.RemoveAt(i);
                }

                //if (currentIdx % 10000 == 0)
                //    Console.WriteLine(potentialMP3s.Count);

                this.currentIdx++;
            }
        }

        public void Reset() {
            Detections.Clear();
            potentialMP3s.Clear();
            currentIdx = 0;
        }

        // We might not know our MP3 ended, as it may end just as the stream ends
        // Thus, if we have a potential MP3 that is 'potentiallyValid', we should add it to the output
        public void FinalizeDetection() {
            foreach (var mp3 in potentialMP3s) {
                if (mp3.potentiallyValid) {
                    Detections.Add((mp3.detectedStartIdx, currentIdx));
                }
                    
            }
        }
    }

    // Three possible states of an MP3 detection
    // ID3v2, verifying the tag header
    // Frame, verifies MP3 frames until it fails, then goes to ID3
    // ID3 looks for an ID3 tag at the end
    enum MP3State {
        ID3v2,
        Frame,
        ID3
    }

    class PotentialMP3 {

        private MP3State state = MP3State.ID3v2;

        // If the ID3v2 was valid and we've seen at least 1 valid frame, then this may be a valid MP3 file
        // If this is true when we encounter an invalid frame, assume the file has ended and mark this done
        // If this is false however, it is not a valid MP3 file
        public bool potentiallyValid = false;

        private int progress = 0;
        private int skip = 0; // Skip this many bytes, returning true immediately
        public int detectedStartIdx;
        public int processedBytes = 0; // How many bytes this has processed, used to compute end index
        // Done is set to 'true' when we have successfully reached a valid end segment
        public bool done = false;

        // Holds the 4 bytes that determine the length of the ID3 header
        // Note that the first bit of each byte is ignored
        private byte[] lengthBytes = new byte[4];

        // Holds the MP3 Header until we have all bytes
        private byte[] frameBytes = new byte[4];

        public PotentialMP3(int start_idx, MP3State startState) {
            detectedStartIdx = start_idx;
            state = startState;
        }

        // Returns 'true' if it still looks valid, 'false' if it doesn't
        public bool Process(byte b) {
            processedBytes++;
            if (skip > 0) {
                skip--;
                return true;
            }
            if (state == MP3State.ID3v2) { // Check magic bytes and get ID3v2 length
                if (progress < 3) {
                    if (b == MP3Detector.MagicStart[progress]) {
                        progress++;
                        return true;
                    }
                    return false;
                } else if (progress < 6) { // TODO We can perform extra verification on 'version' and 'flags' here
                    progress++;
                    return true;
                } else if (progress < 10) {
                    lengthBytes[progress - 6] = b;
                    progress++;
                    return true;
                } else {
                    // Determine length
                    // This is given by the 4 length bytes, where the first bit in each of them is ignored (28 bits)
                    // Note that since ID3v2 is LE, the 'first' bit is bit 7

                    // Convert to integers in system-endian 
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);
                    // Bit 0 is _never_ high in the ID3v2 size field
                    foreach (byte lb in lengthBytes) {
                        if ((lb & 0b10000000) == 0b10000000) {
                            return false;
                        }
                    }

                    int[] lengthInts = new int[4];
                    lengthInts[0] = (int)lengthBytes[0];
                    lengthInts[1] = (int)lengthBytes[1];
                    lengthInts[2] = (int)lengthBytes[2];
                    lengthInts[3] = (int)lengthBytes[3];

                    // Consider the length ints to be A B C and D
                    // The length is then given by A*2^21 + B*2^14 + C^*2^7 + D
                    // We use << for powers, which gives us this expression
                    // 
                    int len = lengthInts[0] * (2 << 20) + lengthInts[1] * (2 << 13) + lengthInts[2] * (2 << 6) + lengthInts[3];
                    //Console.WriteLine("ID3v2 Tag Length: {0}", len);
                    progress = 0;
                    state = MP3State.Frame;
                    skip = len-1; // -1 to account for the current byte

                    return true;
                } 
            } else if (state == MP3State.Frame) { // Keep parsing MP3 frame headers
                if (progress < 3) {
                    frameBytes[progress] = b;
                    progress++;
                    return true;
                } else {
                    frameBytes[progress] = b;
                    progress = 0; // Reset

                    ////// Verify header

                    //string bits1 = Convert.ToString(frameBytes[0], 2);
                    //string bits2 = Convert.ToString(frameBytes[1], 2);
                    //string bits3 = Convert.ToString(frameBytes[2], 2);
                    //string bits4 = Convert.ToString(frameBytes[3], 2);
                    //Console.WriteLine("Global index: {0}", detectedStartIdx+processedBytes-1);
                    //Console.WriteLine("{0} {1} {2} {3}", bits1, bits2, bits3, bits4);
                    //Console.WriteLine("0x{0:X} 0x{1:X} 0x{2:X} 0x{3:X}", frameBytes[0], frameBytes[1], frameBytes[2], frameBytes[3]);

                    // Check frame sync
                    // First 11 bits are all 1
                    if (!(frameBytes[0] == 0xFF && (frameBytes[1] & 0b11100000) == 0b11100000)) {
                        if (potentiallyValid) {
                            state = MP3State.ID3;
                            return true;
                        } else {
                            //Console.WriteLine("Reject: sync");
                            return false;
                        }
                    }
                    // Check version
                    // This is either 11 for v1 or 10 for v2
                    // 01 is reserved and 00 is unofficial
                    int version = (frameBytes[1] & 0b00011000) >> 3;
                    if ( version != 0b11 && version != 0b10 ) {
                        if (potentiallyValid) {
                            state = MP3State.ID3;
                            return true;
                        } else {
                            //Console.WriteLine("Reject: version");
                            return false;
                        }
                    }
                    // Check layer
                    int layer = (frameBytes[1] & 0b00000110) >> 1;
                    if (layer == 0b00) { // 00 is reserved; 01, 10 and 11 are valid layers
                        if (potentiallyValid) {
                            state = MP3State.ID3;
                            return true;
                        } else {
                            //Console.WriteLine("Reject: layer");
                            return false;
                        }
                    }

                    // Get bitrate index
                    byte bitrateIndex = (byte)((frameBytes[2] & 0b11110000) >> 4);
                    // Get sampling rate index
                    byte samplingIndex = (byte)((frameBytes[2] & 0b00001100) >> 2);

                    int bitrate = MP3Detector.GetBitrate(version, layer, bitrateIndex) * 1000; // Multiply by 1000 (convert kbps -> bps)
                    int samplingRate = MP3Detector.GetSamplingRate(version, samplingIndex);
                    if (bitrate == -1) {
                        if (potentiallyValid) {
                            state = MP3State.ID3;
                            return true;
                        } else {
                            //Console.WriteLine("Reject: bitrate");
                            return false;
                        }
                    }
                    if (samplingRate == -1) {
                        if (potentiallyValid) {
                            state = MP3State.ID3;
                            return true;
                        } else {
                            //Console.WriteLine("Reject: sampling");
                            //Console.WriteLine("{0} - {1}", samplingIndex, version);
                            return false;
                        }
                    }
                    //Console.WriteLine("Got bitrate: {0}", bitrate);
                    //Console.WriteLine("Got sam: {0}", samplingRate);

                    // Get padding bit
                    // This is either 0 (not padded) or 1 (padded)
                    int paddingBit = (frameBytes[2] & 0b00000010) >> 1;
                    //Console.WriteLine("Padding: {0}", paddingBit);

                    // Compute length
                    int length;
                    if (layer == 0b11) {
                        length = (12 * bitrate / samplingRate + paddingBit * 4)*4; // 4 byte pad
                    } else {
                        length = 144 * bitrate / samplingRate + paddingBit; // 1 byte pad
                    }
                    
                    //Console.WriteLine("Computed length: {0}", length);
                    skip = length-4;
                    potentiallyValid = true;
                    return true;
                }
            } else { // MP3State.ID3;
            
                // NOTICE: BY THE TIME WE SWITCH HERE WE'VE ALREADY READ 4 BYTES!!!
                // Those 4 bytes were read in the 'Frame' state which was found invalid
                // Thus, we already have all the bytes needed to check for ID3v1 'TAG'

                if (frameBytes[0] == 0x54 && frameBytes[1] == 0x41 && frameBytes[2] == 0x47) {
                    processedBytes += 123; // ID3v1 tag is always 128; -4 we already read; -1 the current byte we just read
                } else {
                    processedBytes--; // Substract 1 for the byte we just read, since it isn't used
                }

                done = true;
                return true;
            }
        }
    }
}
