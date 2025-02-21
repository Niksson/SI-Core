﻿using SIPackages.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SIPackages.PlatformSpecific
{
    /// <summary>
    /// Defines a package source.
    /// </summary>
    public interface ISIPackage: IDisposable
    {
        /// <summary>
        /// Gets source entries by category.
        /// </summary>
        /// <param name="category">Category name.</param>
        /// <returns></returns>
        string[] GetEntries(string category);

        /// <summary>
        /// Gets object stream.
        /// </summary>
        /// <param name="name">Object name.</param>
        /// <param name="read">Should a stream be read-only.</param>
        StreamInfo GetStream(string name, bool read = true);

        /// <summary>
        /// Gets object stream.
        /// </summary>
        /// <param name="category">Object category.</param>
        /// <param name="name">Object name.</param>
        /// <param name="read">Should a stream be read-only.</param>
        /// <returns></returns>
        StreamInfo GetStream(string category, string name, bool read = true);

        /// <summary>
        /// Creates an object.
        /// </summary>
        /// <param name="name">Object name.</param>
        /// <param name="contentType">Object content type.</param>
        void CreateStream(string name, string contentType);

        /// <summary>
        /// Creates an object.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="name">Object name.</param>
        /// <param name="contentType">Object content type.</param>
        void CreateStream(string category, string name, string contentType);

        /// <summary>
        /// Creates an object.
        /// </summary>
        /// <param name="category">Object category.</param>
        /// <param name="name">Object name.</param>
        /// <param name="contentType">Object content type.</param>
        /// <param name="stream">Object stream.</param>
        Task CreateStreamAsync(string category, string name, string contentType, Stream stream);

        /// <summary>
        /// Deletes an object.
        /// </summary>
        /// <param name="category">Object category.</param>
        /// <param name="name">Object name.</param>
        void DeleteStream(string category, string name);

        /// <summary>
        /// Copies the whole source to the stream.
        /// </summary>
        /// <param name="stream">Target stream.</param>
        /// <param name="close">Should this object be closed.</param>
        /// <param name="isNew">Has a new source been created.</param>
        /// <returns>Created copy.</returns>
        ISIPackage CopyTo(Stream stream, bool close, out bool isNew);

        /// <summary>
        /// Flushes source changes.
        /// </summary>
        void Flush();
    }
}
