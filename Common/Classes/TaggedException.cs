﻿#region Copyright
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
#endregion

using System;

namespace Media.Common
{
    /// <summary>
    /// In short,
    /// All exceptions can be recovered from, only some can be resumed. 
    /// This class provides [a scope when used in conjunction with the `using` construct] methods to share data through as well as pass messages during programming if required.
    ///
    /// Define a real class, e.g. myException : Exception<paramref name="T"/> to only mangle the typename once if possible.
    /// 
    /// This class provides a base class which is derived from <see cref="System.Exception"/>.
    ///
    /// It allows a construct of programming based on scopes and exceptions.
    /// 
    /// It is not marked abstract because it would be useless.
    /// </summary>
    /// <typeparam name="T">The type data in the Tag property</typeparam>
    public class TaggedException<T> : System.Exception, ITaggedException, IDisposed
    {
        #region Constants

        /// <summary>
        /// The string which represents the name of the property
        /// </summary>
        private const string TagPropertyString = nameof(Tag);

        #endregion

        #region Statics

        /// <summary>
        /// The string which will be used on all instances if no message was provided when instantiated.
        /// </summary>
        public const string ExceptionFormat = "A System.Exception occured related to the following System.Type: `{0}`. If there is related data it is located in the Tag property.";

        public static string DefaultExceptionTypeMessage() { return string.Format(TaggedException<T>.ExceptionFormat, typeof(T).FullName); }


        #endregion

        #region Fields

        private readonly Common.SuppressedFinalizerDisposable @base = new(true);

        #endregion

        #region Properties

        /// <summary>
        /// The element which corresponds to the underlying exception
        /// </summary>
        public virtual T Tag { get; protected set; }

        /// <summary>
        /// <see cref="Exception.InnerException"/>.
        /// </summary>
        System.Exception ITaggedException.InnerException
        {
            get { return base.InnerException; }
        }

        /// <summary>
        /// A boxed representation of the Tag property.
        /// </summary>
        object ITaggedException.Tag
        {
            get { return this.Tag; }
        }

        /// <summary>
        /// Indicates if the Exception has been previously disposed
        /// </summary>
        public bool IsDisposed { get { return @base.IsDisposed; } }

        public bool ShouldDispose { get { return @base.ShouldDispose; } }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the default for <typeparamref name="T"/> in the <see cref="Tag"/> property.
        /// </summary>
        public TaggedException()
            : base() { Tag = default; }

        /// <summary>
        /// Creates the default for <typeparamref name="T"/> from <paramref name="tag"/> in <see cref="Tag"/> and assigns <see cref="Exception.Message"/>
        /// </summary>
        /// <param name="tag">The value to store.</param>
        /// <param name="message">The message realted to the exception</param>
        public TaggedException(T tag, string message)
            : base(message) { Tag = tag; }

        /// <summary>
        /// Creates the default for <typeparamref name="T"/> from <paramref name="tag"/> in <see cref="Tag"/> and assigns <see cref="Exception.Message"/> and <see cref="Exception.InnerException"/>
        /// </summary>
        /// <param name="tag">The value to store.</param>
        /// <param name="message">The message realted to the exception</param>
        /// <param name="innerException">The exception which superceeds this exception</param>
        public TaggedException(T tag, string message, System.Exception innerException)
            : base(message, innerException) { Tag = tag; }


        /// <summary>
        /// Creates the default for <typeparamref name="T"/> from <paramref name="tag"/> in <see cref="Tag"/> and assigns a default message describing the <typeparamref name="T"/>.
        /// </summary>
        /// <param name="tag">The value to store.</param>
        public TaggedException(T tag) : this(tag, DefaultExceptionTypeMessage(), null) { }

        /// <summary>
        /// Facilitates the serialization.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="info"><see cref="System.Runtime.Serialization.SerializationInfo"/></param>
        /// <param name="context"><see cref="System.Runtime.Serialization.StreamingContext"/></param>
        [Obsolete]
        protected TaggedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
            //Get the Tag from the info and unbox.
            Tag = (T)info.GetValue(TagPropertyString, typeof(T));
        }

        /// <summary>
        /// Creates the default for <typeparamref name="T"/> from <paramref name="tag"/> in <see cref="Tag"/> and assigns <see cref="Exception.Message"/> and <see cref="Exception.InnerException"/> and optionally assigns any given Data.
        /// </summary>
        /// <param name="tag">The element related to the exception</param>
        /// <param name="message">The message realted to the exception, if not provided a default message will be used.</param>
        /// <param name="innerException">any <see cref="System.Exception"/> which is related to the exception being thrown</param>
        /// <param name="data">Any data which should be also stored with the exception</param>
        public TaggedException(T tag, string message, System.Exception innerException, params object[] data)
            : this(tag, message, innerException)
        {
            //If given any data 
            //Add any data related to the the Data IDictionary of the Exception using the Hashcode of the data as the key.
            if (data is not null)
                foreach (object key in data)
                    Data.Add(key.GetHashCode(), key);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Populates a <paramref name="info"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info"><see cref="System.Runtime.Serialization.SerializationInfo"/></param>
        /// <param name="context"><see cref="System.Runtime.Serialization.StreamingContext"/></param>
        [Obsolete]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            System.ArgumentNullException.ThrowIfNull(info);

            //Add the Tag to the info
            info.AddValue(TagPropertyString, Tag);

            //Get all other exception data from base.
            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Clears <see cref="Data"/>
        /// </summary>
        protected internal void ClearData() { Data.Clear(); }

        /// <summary>
        /// Adds to <see cref="Data"/>
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        protected internal void AddData(object key, object value) { Data.Add(key, value); }

        //Todo, T Key, T Value, Check for Serializable?

        #endregion

        /// <summary>
        /// Disposes the exception
        /// </summary>
        void System.IDisposable.Dispose()
        {
            if (ShouldDispose is false || @base.IsDisposed) return;

            @base.Dispose(ShouldDispose);

            //ClearData();
        }
    }

    /// <summary>
    /// Much more useful before you could catch an exception only when it was a specific type.
    /// Allows this functionality to be achieved lack thereof.
    /// </summary>
    public static class TaggedExceptionExtensions
    {
        /// <summary>
        /// Raises the given <see cref="TaggedException"/>
        /// </summary>
        /// <typeparam name="T">The type related to the exception.</typeparam>
        /// <param name="exception">The <see cref="System.Exception"/> which occured.</param>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void Raise<T>(this TaggedException<T> exception)
        {
            if (exception is not null)
                throw exception;
        }

        /// <summary>
        /// Tries to <see cref="Raise"/> the given <see cref="TaggedException"/>
        /// </summary>
        /// <typeparam name="T">The type related to the exception.</typeparam>
        /// <param name="exception">The <see cref="System.Exception"/> which occured.</param>

        public static void TryRaise<T>(this TaggedException<T> exception) //storeData
        {
            try { exception.Raise(); }
            catch { /*hide*/ }
        }

        /// <summary>
        /// Raises the given <see cref="TaggedException"/>
        /// </summary>
        /// <typeparam name="T">The type related to the exception.</typeparam>
        /// <param name="exception">The <see cref="System.Exception"/> which occured.</param>
        /// <param name="breakForResume">Indicates if the function should attach the debugger.</param>
        public static void RaiseAndAttachIfUnhandled<T>(this TaggedException<T> exception, bool breakForResume = true)
        {
            //If not attaching then fall back to TryRaise which hides the exception and return.
            if (breakForResume is false)
            {
                exception.TryRaise();

                return;
            }

            //Raise the exception
            try
            {
                exception.Raise();
            }
            catch //Handle it
            {
                //If the debugger is not attached and it cannot be then return
                if (Extensions.Debug.DebugExtensions.Attach() is false) return;

                //Break if still attached
                Extensions.Debug.DebugExtensions.BreakIfAttached();
            }
        }

        /// <summary>
        /// Raises an <see cref="Common.Exception"/> on the calling thread.
        /// </summary>
        /// <typeparam name="T">The type of the exception to raise.</typeparam>
        /// <param name="tag">The element related to the exception</param>
        /// <param name="message">The message realted to the exception, if not provided a default message will be used.</param>
        /// <param name="innerException">any <see cref="System.Exception"/> which is related to the exception being thrown</param>
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void RaiseTaggedException<T>(T tag, string message, System.Exception innerException = null)
        {
            new TaggedException<T>(tag, message ?? TaggedException<T>.DefaultExceptionTypeMessage(), innerException).Raise();
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T">The type of the exception to raise.</typeparam>
        /// <param name="tag">The element related to the exception</param>
        /// <param name="message">The message realted to the exception, if not provided a default message will be used.</param>
        /// <param name="innerException">any <see cref="System.Exception"/> which is related to the exception being thrown</param>
        public static void TryRaiseTaggedException<T>(T tag, string message, System.Exception innerException = null)
        {
            new TaggedException<T>(tag, message ?? TaggedException<T>.DefaultExceptionTypeMessage(), innerException).TryRaise();
        }
    }
}
