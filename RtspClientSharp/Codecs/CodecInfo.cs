using System.Collections;
using System.Collections.Generic;

namespace RtspClientSharp.Codecs
{
    public abstract class CodecInfo
    {
        public IDictionary<string, string> Attributes { get; } = new Dictionary<string, string>(); 
    }
}