using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    internal class BiQuadFilter : IFilter
    {
        public NAudio.Dsp.BiQuadFilter Filter { get; set; }
        public float Transform(float input)
        {
            return Filter.Transform(input);
        }
    }
}
