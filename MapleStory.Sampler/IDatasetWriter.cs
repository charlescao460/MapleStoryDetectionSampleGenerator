using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleStory.Sampler
{
    public interface IDatasetWriter
    {
        void Write(Sample sample);

        void Finish();
    }
}
