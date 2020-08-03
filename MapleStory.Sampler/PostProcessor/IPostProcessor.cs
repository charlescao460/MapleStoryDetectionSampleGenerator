using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleStory.Sampler.PostProcessor
{
    /// <summary>
    /// Interface for post-processor of samples
    /// </summary>
    public interface IPostProcessor
    {
        /// <summary>
        /// Post-process the sample. Post-processing could be adding items, resizing, etc..
        /// </summary>
        /// <param name="sample"> Sample before processing. </param>
        /// <returns> Sample after processing. </returns>
        Sample Process(Sample sample);
    }
}
