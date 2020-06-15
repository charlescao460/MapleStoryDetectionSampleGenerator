using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MapleStory.Common.Exceptions
{
    /// <summary>
    /// Simple exception class indicating specified Wz Img cannot be found.
    /// </summary>
    public class WzImgNotFoundException : Exception
    {
        public WzImgNotFoundException()
        {
        }

        public WzImgNotFoundException(string message) : base(message)
        {
        }

        public WzImgNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected WzImgNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
