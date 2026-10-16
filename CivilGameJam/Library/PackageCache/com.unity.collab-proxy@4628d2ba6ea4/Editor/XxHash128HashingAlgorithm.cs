using System;
using System.IO;

using Codice.Utils;
using Unity.PlasticSCM.Editor.Hashing;

namespace Unity.PlasticSCM.Editor
{
    internal class XxHash128HashingAlgorithm : IHashingAlgorithm
    {
        public byte[] Hash => mHashValue;

        public byte[] ComputeHash(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            return ComputeHash(buffer, 0, buffer.Length);
        }

        public byte[] ComputeHash(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            mHasher.Reset();
            mHashValue = XxHash128.Hash(new ReadOnlySpan<byte>(buffer, offset, count));

            return mHashValue;
        }

        public byte[] ComputeHash(Stream inputStream)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));

            mHasher.Reset();
            mHasher.Append(inputStream);
            mHashValue = GetCurrentHashAndResetState(mHasher);

            return mHashValue;
        }

        public int TransformBlock(
            byte[] inputBuffer,
            int inputOffset,
            int inputCount,
            byte[] outputBuffer,
            int outputOffset)
        {
            if (inputBuffer == null)
                throw new ArgumentNullException(nameof(inputBuffer));

            if (inputOffset < 0 || inputOffset > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputOffset));

            if (inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputCount));

            if (inputCount > 0)
                mHasher.Append(new ReadOnlySpan<byte>(inputBuffer, inputOffset, inputCount));

            // Copy input to output if provided (supports null and in-place)
            if (outputBuffer == null)
                return inputCount;

            if (outputOffset < 0 || outputOffset > outputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(outputOffset));

            if (outputOffset + inputCount > outputBuffer.Length)
                throw new ArgumentException("Output buffer too small");

            if (inputCount > 0)
                Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);

            return inputCount;
        }

        public byte[] TransformFinalBlock(
            byte[] inputBuffer, int inputOffset, int inputCount)
        {
            if (inputBuffer == null)
                throw new ArgumentNullException(nameof(inputBuffer));

            if (inputOffset < 0 || inputOffset > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputOffset));

            if (inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputCount));

            if (inputCount > 0)
                mHasher.Append(new ReadOnlySpan<byte>(inputBuffer, inputOffset, inputCount));

            mHashValue = GetCurrentHashAndResetState(mHasher);

            // Return copy of input (HashAlgorithm contract)
            byte[] result = new byte[inputCount];

            if (inputCount > 0)
                Buffer.BlockCopy(inputBuffer, inputOffset, result, 0, inputCount);

            return result;
        }

        public void Initialize()
        {
            mHasher.Reset();
            mHashValue = Array.Empty<byte>();
        }

        public void Dispose()
        {
            // No need
        }

        static byte[] GetCurrentHashAndResetState(XxHash128 hasher)
        {
            // GetCurrentHash returns the digest in the conventional big-endian
            // representation, matching System.IO.Hashing used by the other clients.
            byte[] result = hasher.GetCurrentHash();
            hasher.Reset();
            return result;
        }

        byte[] mHashValue = Array.Empty<byte>();

        readonly XxHash128 mHasher = new XxHash128();
    }

    internal class XxHash128HashingAlgorithmFactory : IXxHash128HashingAlgorithmFactory
    {
        public IHashingAlgorithm Build()
        {
            return new XxHash128HashingAlgorithm();
        }
    }
}
