using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinect_Gallery.Exceptions
{
    class ExistsException: Exception
    {
        public ExistsException() : base() { }
        public ExistsException(string message) : base(message) { }
        public ExistsException(string message, Exception e) : base(message, e) { }
    }
}
