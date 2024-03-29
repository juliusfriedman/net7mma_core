﻿using System;

namespace Media.Common
{
    /// <summary>
    /// Defines an interface [to a disposeable object] which allow access to an <see cref="Exception"/> and a user stored object which is related to the exception.
    /// </summary>
    public interface ITaggedException /*: IDisposed*/
    {
        /// <summary>
        /// <see cref="Exception.InnerException"/>.
        /// </summary>
        Exception InnerException { get; } //CurrentException

        /// <summary>
        /// The <see cref="object"/> which corresponds to the underlying exception.
        /// </summary>
        object Tag { get; }
    }

    //ITaggedExceptionExtensions
}
