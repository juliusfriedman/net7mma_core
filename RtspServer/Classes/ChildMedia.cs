/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://github.com/juliusfriedman/net7mma_core
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. https://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

using System;

namespace Media.Rtsp.Server
{
    /// <summary>
    /// A Source Stream which is a facade` to another.
    /// </summary>
    public class ChildMedia : SourceMedia
    {
        internal SourceMedia m_Parent;

        public ChildMedia(SourceMedia source)
            : base(source.Name, source.Source)
        {
            if (source.IsParent is false)
                throw new ArgumentException("Cannot make a Child of a Child.", nameof(source));

            m_Parent = source;
            //m_Child = true;
            IsReady = m_Parent.IsReady;
        }

        public override Uri Source { get { return m_Parent.Source; } set { m_Parent.Source = value; } }

        public override bool IsReady
        {
            get
            {
                return base.IsReady && m_Parent.IsReady;
            }
            protected set
            {
                base.IsReady = value;
            }
        }
    }
}
