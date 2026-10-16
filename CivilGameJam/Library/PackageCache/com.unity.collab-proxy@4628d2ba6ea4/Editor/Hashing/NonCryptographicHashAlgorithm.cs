// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Ported from dotnet/runtime (release/9.0):
// src/libraries/System.IO.Hashing/src/System/IO/Hashing/NonCryptographicHashAlgorithm.cs
// Adapted for the Unity editor (Mono, netstandard2.1 API surface):
// - Removed async methods (AppendAsync, WriteAsync, BeginWrite, EndWrite).
// - Removed #if NET specific overloads (Write(ReadOnlySpan<byte>), etc.).
// - Replaced SR resource strings with inline constants.
// - Removed [DoesNotReturn] and other .NET-specific attributes.
// - Removed EditorBrowsable/Obsolete GetHashCode override (not needed for internal use).

using System;
using System.Diagnostics;
using System.IO;

namespace Unity.PlasticSCM.Editor.Hashing
{
    /// <summary>
    ///   Represents a non-cryptographic hash algorithm.
    /// </summary>
    internal abstract class NonCryptographicHashAlgorithm
    {
        /// <summary>
        ///   Gets the number of bytes produced from this hash algorithm.
        /// </summary>
        /// <value>The number of bytes produced from this hash algorithm.</value>
        public int HashLengthInBytes { get; }

        /// <summary>
        ///   Called from constructors in derived classes to initialize the
        ///   <see cref="NonCryptographicHashAlgorithm"/> class.
        /// </summary>
        /// <param name="hashLengthInBytes">
        ///   The number of bytes produced from this hash algorithm.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="hashLengthInBytes"/> is less than 1.
        /// </exception>
        protected NonCryptographicHashAlgorithm(int hashLengthInBytes)
        {
            if (hashLengthInBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(hashLengthInBytes));

            HashLengthInBytes = hashLengthInBytes;
        }

        /// <summary>
        ///   When overridden in a derived class,
        ///   appends the contents of <paramref name="source"/> to the data already
        ///   processed for the current hash computation.
        /// </summary>
        /// <param name="source">The data to process.</param>
        public abstract void Append(ReadOnlySpan<byte> source);

        /// <summary>
        ///   When overridden in a derived class,
        ///   resets the hash computation to the initial state.
        /// </summary>
        public abstract void Reset();

        /// <summary>
        ///   When overridden in a derived class,
        ///   writes the computed hash value to <paramref name="destination"/>
        ///   without modifying accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        protected abstract void GetCurrentHashCore(Span<byte> destination);

        /// <summary>
        ///   Appends the contents of <paramref name="source"/> to the data already
        ///   processed for the current hash computation.
        /// </summary>
        /// <param name="source">The data to process.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        public void Append(byte[] source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            Append(new ReadOnlySpan<byte>(source));
        }

        /// <summary>
        ///   Appends the contents of <paramref name="stream"/> to the data already
        ///   processed for the current hash computation.
        /// </summary>
        /// <param name="stream">The data to process.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        public void Append(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            stream.CopyTo(new CopyToDestinationStream(this));
        }

        /// <summary>
        ///   Gets the current computed hash value without modifying accumulated state.
        /// </summary>
        /// <returns>
        ///   The hash value for the data already provided.
        /// </returns>
        public byte[] GetCurrentHash()
        {
            byte[] ret = new byte[HashLengthInBytes];
            GetCurrentHashCore(ret);
            return ret;
        }

        /// <summary>
        ///   Attempts to write the computed hash value to <paramref name="destination"/>
        ///   without modifying accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <param name="bytesWritten">
        ///   On success, receives the number of bytes written to <paramref name="destination"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if <paramref name="destination"/> is long enough to receive
        ///   the computed hash value; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetCurrentHash(Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < HashLengthInBytes)
            {
                bytesWritten = 0;
                return false;
            }

            GetCurrentHashCore(destination.Slice(0, HashLengthInBytes));
            bytesWritten = HashLengthInBytes;
            return true;
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   without modifying accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <returns>
        ///   The number of bytes written to <paramref name="destination"/>,
        ///   which is always <see cref="HashLengthInBytes"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///   <paramref name="destination"/> is shorter than <see cref="HashLengthInBytes"/>.
        /// </exception>
        public int GetCurrentHash(Span<byte> destination)
        {
            if (destination.Length < HashLengthInBytes)
                ThrowDestinationTooShort();

            GetCurrentHashCore(destination.Slice(0, HashLengthInBytes));
            return HashLengthInBytes;
        }

        /// <summary>
        ///   Gets the current computed hash value and clears the accumulated state.
        /// </summary>
        /// <returns>
        ///   The hash value for the data already provided.
        /// </returns>
        public byte[] GetHashAndReset()
        {
            byte[] ret = new byte[HashLengthInBytes];
            GetHashAndResetCore(ret);
            return ret;
        }

        /// <summary>
        ///   Attempts to write the computed hash value to <paramref name="destination"/>.
        ///   If successful, clears the accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <param name="bytesWritten">
        ///   On success, receives the number of bytes written to <paramref name="destination"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> and clears the accumulated state
        ///   if <paramref name="destination"/> is long enough to receive
        ///   the computed hash value; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < HashLengthInBytes)
            {
                bytesWritten = 0;
                return false;
            }

            GetHashAndResetCore(destination.Slice(0, HashLengthInBytes));
            bytesWritten = HashLengthInBytes;
            return true;
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   then clears the accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <returns>
        ///   The number of bytes written to <paramref name="destination"/>,
        ///   which is always <see cref="HashLengthInBytes"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///   <paramref name="destination"/> is shorter than <see cref="HashLengthInBytes"/>.
        /// </exception>
        public int GetHashAndReset(Span<byte> destination)
        {
            if (destination.Length < HashLengthInBytes)
                ThrowDestinationTooShort();

            GetHashAndResetCore(destination.Slice(0, HashLengthInBytes));
            return HashLengthInBytes;
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   then clears the accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        protected virtual void GetHashAndResetCore(Span<byte> destination)
        {
            Debug.Assert(destination.Length == HashLengthInBytes);

            GetCurrentHashCore(destination);
            Reset();
        }

        private protected static void ThrowDestinationTooShort() =>
            throw new ArgumentException("Destination is too short.", "destination");

        /// <summary>Stream-derived type used to support copying from a source stream to this instance via CopyTo.</summary>
        private sealed class CopyToDestinationStream : Stream
        {
            private readonly NonCryptographicHashAlgorithm mHash;

            public CopyToDestinationStream(NonCryptographicHashAlgorithm hash)
            {
                mHash = hash;
            }

            public override bool CanWrite => true;

            public override void Write(byte[] buffer, int offset, int count)
            {
                mHash.Append(new ReadOnlySpan<byte>(buffer, offset, count));
            }

            public override void WriteByte(byte value)
            {
                Span<byte> span = stackalloc byte[1];
                span[0] = value;
                mHash.Append(span);
            }

            public override void Flush() { }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();
        }
    }
}
