﻿using System.Collections.Generic;
using System.Linq;

namespace Media.Codec
{
    //Could be named CodecRegistry / CodecRepository etc.

    //Essentially a place for all codec types to register and unregister.

    //Confusing to type Codec.Codecs

    public sealed class Codecs
    {
        //Pattern for registering.
        private static readonly HashSet<Media.Codec.Interfaces.ICodec> m_RegisteredCodecs = [];

        public static IEnumerable<Media.Codec.Interfaces.ICodec> GetAllCodecs()
        {
            return m_RegisteredCodecs;
        }

        public static Media.Codec.Interfaces.ICodec GetCodec(string name)
        {
            return m_RegisteredCodecs.FirstOrDefault(c => string.Compare(c.Name, name, true) is 0);
        }

        public static bool TryRegisterCodec(Media.Codec.Interfaces.ICodec codec)
        {
            return m_RegisteredCodecs.Add(codec);
        }

        public static bool TryUnregisterCodec(Media.Codec.Interfaces.ICodec codec)
        {
            return m_RegisteredCodecs.Remove(codec);
        }
    }
}
