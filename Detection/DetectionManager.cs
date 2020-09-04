using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peer.Detection {
    /// <summary>
    /// Class responsible for managing any numbers of detectors and their outputs
    /// Detectors and output handlers can be registered
    /// </summary>
    class DetectionManager {

        private List<BytewiseDetector> detectors = new List<BytewiseDetector>();

        public DetectionManager() {

        }

        public int DetectionCount() {
            return detectors.Sum(elem => elem.Detections.Count);
        }

        public void RegisterDetector(BytewiseDetector detector) {
            this.detectors.Add(detector);
        }

        /// <summary>
        /// Makes all registered detectors process the supplied bytes
        /// </summary>
        /// <param name="bytes"></param>
        public void ProcessBytes(byte[] bytes) {
            foreach (var det in this.detectors) {
                det.ProcessBytes(bytes);
            }
        }

        /// <summary>
        /// Convert current detections into a human-readable list
        /// An entry foramt is "DisplayName \t Size" where 
        /// 'DisplayName' is what the BytewiseDetector defines it as
        /// 'Size' is filesize formatted with proper units
        /// </summary>
        public List<String> FormatDetections() {
            List<String> formattedDetections = new List<String>();
            foreach (var det in this.detectors) {
                foreach (var item in det.Detections) {
                    formattedDetections.Add(String.Format("{0} \t {1}", det.DisplayName, Program.format_bytes(item.Item2 - item.Item1)));
                }
            }
            return formattedDetections;
        }


        public ((long, long), BytewiseDetector) this[int key] {
            get {
                foreach (var det in this.detectors) {
                    if (key < det.Detections.Count) {
                        return (det.Detections[key],det);
                    } else {
                        key -= det.Detections.Count;
                    }
                }
                return ((0, 0), null);
            }
        }

    }

}
