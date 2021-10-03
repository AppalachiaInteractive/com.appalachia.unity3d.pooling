#region

using UnityEngine.Assertions;

#endregion

namespace Appalachia.Core.Pooling.Arrays
{
    internal static class Utilities
    {
        internal static int SelectBucketIndex(int bufferSize)
        {
            Assert.IsTrue(bufferSize > 0);

            var bitsRemaining = ((uint) bufferSize - 1) >> 4;

            var poolIndex = 0;
            if (bitsRemaining > 0xFFFF)
            {
                bitsRemaining >>= 16;
                poolIndex = 16;
            }

            if (bitsRemaining > 0xFF)
            {
                bitsRemaining >>= 8;
                poolIndex += 8;
            }

            if (bitsRemaining > 0xF)
            {
                bitsRemaining >>= 4;
                poolIndex += 4;
            }

            if (bitsRemaining > 0x3)
            {
                bitsRemaining >>= 2;
                poolIndex += 2;
            }

            if (bitsRemaining > 0x1)
            {
                bitsRemaining >>= 1;
                poolIndex += 1;
            }

            return poolIndex + (int) bitsRemaining;
        }

        internal static int GetMaxSizeForBucket(int binIndex)
        {
            var maxSize = 16 << binIndex;
            Assert.IsTrue(maxSize >= 0);
            return maxSize;
        }
    }
}
