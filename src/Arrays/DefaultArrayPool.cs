#region

using System;
using System.Threading;
using Unity.Profiling;

#endregion

namespace Appalachia.Core.Pooling.Arrays
{
    public sealed class DefaultArrayPool<T> : ArrayPool<T>
    {
        /// <summary>The default maximum length of each array in the pool (2^20).</summary>
        private const int DefaultMaxArrayLength = 1024 * 1024;

        /// <summary>The default maximum number of arrays per bucket that are available for rent.</summary>
        private const int DefaultMaxNumberOfArraysPerBucket = 50;

        /// <summary>Lazily-allocated empty array used when arrays of length 0 are requested.</summary>
        private static T[] s_emptyArray; // we support contracts earlier than those with Array.Empty<T>()

        private readonly Bucket[] _buckets;

        public DefaultArrayPool() : this(DefaultMaxArrayLength, DefaultMaxNumberOfArraysPerBucket)
        {
        }

        private static readonly ProfilerMarker _PRF_DefaultArrayPool_DefaultArrayPool = new ProfilerMarker("DefaultArrayPool.DefaultArrayPool");
        public DefaultArrayPool(int maxArrayLength, int maxArraysPerBucket)
        {
            using (_PRF_DefaultArrayPool_DefaultArrayPool.Auto())
            {
                if (maxArrayLength <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxArrayLength));
                }

                if (maxArraysPerBucket <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxArraysPerBucket));
                }

                // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
                // Constrain the actual max used to those values.
                const int MinimumArrayLength = 0x10, MaximumArrayLength = 0x40000000;
                if (maxArrayLength > MaximumArrayLength)
                {
                    maxArrayLength = MaximumArrayLength;
                }
                else if (maxArrayLength < MinimumArrayLength)
                {
                    maxArrayLength = MinimumArrayLength;
                }

                // Create the buckets.
                var poolId = Id;
                var maxBuckets = Utilities.SelectBucketIndex(maxArrayLength);
                var buckets = new Bucket[maxBuckets + 1];
                for (var i = 0; i < buckets.Length; i++)
                {
                    buckets[i] = new Bucket(Utilities.GetMaxSizeForBucket(i), maxArraysPerBucket, poolId);
                }

                _buckets = buckets;
            }
        }

        /// <summary>Gets an ID for the pool to use with events.</summary>
        private int Id => GetHashCode();

        private static readonly ProfilerMarker _PRF_DefaultArrayPool_Rent = new ProfilerMarker("DefaultArrayPool.Rent");
        public override T[] Rent(int minimumLength)
        {
            using (_PRF_DefaultArrayPool_Rent.Auto())
            {
                // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
                // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
                // to be usable in general instead of using `new`, even for computed lengths.
                if (minimumLength < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(minimumLength));
                }

                if (minimumLength == 0)
                {
                    // No need for events with the empty array.  Our pool is effectively infinite
                    // and we'll never allocate for rents and never store for returns.
                    return s_emptyArray ?? (s_emptyArray = new T[0]);
                }

                //var log = ArrayPoolEventSource.Log;
                T[] buffer = null;

                var index = Utilities.SelectBucketIndex(minimumLength);
                if (index < _buckets.Length)
                {
                    // Search for an array starting at the 'index' bucket. If the bucket is empty, bump up to the
                    // next higher bucket and try that one, but only try at most a few buckets.
                    const int MaxBucketsToTry = 2;
                    var i = index;
                    do
                    {
                        // Attempt to rent from the bucket.  If we get a buffer from it, return it.
                        buffer = _buckets[i].Rent();
                        if (buffer != null)
                        {
                            //log.BufferRented(buffer.GetHashCode(), buffer.Length, Id, _buckets[i].Id);
                            return buffer;
                        }
                    } while ((++i < _buckets.Length) && (i != (index + MaxBucketsToTry)));

                    // The pool was exhausted for this buffer size.  Allocate a new buffer with a size corresponding
                    // to the appropriate bucket.
                    buffer = new T[_buckets[index]._bufferLength];
                }
                else
                {
                    // The request was for a size too large for the pool.  Allocate an array of exactly the requested length.
                    // When it's returned to the pool, we'll simply throw it away.
                    buffer = new T[minimumLength];
                }

                var bufferId = buffer.GetHashCode(); // no bucket for an on-demand allocated buffer

                //log.BufferRented(bufferId, buffer.Length, Id, bucketId);
                //log.BufferAllocated(bufferId, buffer.Length, Id, bucketId, index >= _buckets.Length ?
                //ArrayPoolEventSource.BufferAllocatedReason.OverMaximumSize : ArrayPoolEventSource.BufferAllocatedReason.PoolExhausted);

                return buffer;
            }
        }

        private static readonly ProfilerMarker _PRF_DefaultArrayPool_Return = new ProfilerMarker("DefaultArrayPool.Return");
        public override void Return(T[] array, bool clearArray = false)
        {
            using (_PRF_DefaultArrayPool_Return.Auto())
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if (array.Length == 0)
                {
                    // Ignore empty arrays.  When a zero-length array is rented, we return a singleton
                    // rather than actually taking a buffer out of the lowest bucket.
                    return;
                }

                // Determine with what bucket this array length is associated
                var bucket = Utilities.SelectBucketIndex(array.Length);

                // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
                if (bucket < _buckets.Length)
                {
                    // Clear the array if the user requests
                    if (clearArray)
                    {
                        Array.Clear(array, 0, array.Length);
                    }

                    // Return the buffer to its bucket.  In the future, we might consider having Return return false
                    // instead of dropping a bucket, in which case we could try to return to a lower-sized bucket,
                    // just as how in Rent we allow renting from a higher-sized bucket.
                    _buckets[bucket].Return(array);
                }

                // Log that the buffer was returned
                //var log = ArrayPoolEventSource.Log;
                //log.BufferReturned(array.GetHashCode(), array.Length, Id);
            }
        }/// <summary>Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.</summary>
        private sealed class Bucket
        {
            internal readonly int _bufferLength;
            private readonly T[][] _buffers;
            private readonly int _poolId;

            private object _lock; // do not make this readonly; it's a mutable struct
            private int _index;

            /// <summary>
            ///     Creates the pool with numberOfBuffers arrays where each buffer is of bufferLength length.
            /// </summary>
            internal Bucket(int bufferLength, int numberOfBuffers, int poolId)
            {
                _lock = new object();
                _buffers = new T[numberOfBuffers][];
                _bufferLength = bufferLength;
                _poolId = poolId;
            }

            /// <summary>Gets an ID for the bucket to use with events.</summary>
            internal int Id => GetHashCode();

            /// <summary>Takes an array from the bucket.  If the bucket is empty, returns null.</summary>
            internal T[] Rent()
            {
                var buffers = _buffers;
                T[] buffer = null;

                // While holding the lock, grab whatever is at the next available index and
                // update the index.  We do as little work as possible while holding the spin
                // lock to minimize contention with other threads.  The try/finally is
                // necessary to properly handle thread aborts on platforms which have them.
                var allocateBuffer = false;
                try
                {
                    Monitor.Enter(_lock);

                    if (_index < buffers.Length)
                    {
                        buffer = buffers[_index];
                        buffers[_index++] = null;
                        allocateBuffer = buffer == null;
                    }
                }
                finally
                {
                    Monitor.Exit(_lock);
                }

                // While we were holding the lock, we grabbed whatever was at the next available index, if
                // there was one.  If we tried and if we got back null, that means we hadn't yet allocated
                // for that slot, in which case we should do so now.
                if (allocateBuffer)
                {
                    buffer = new T[_bufferLength];

                    //var log = ArrayPoolEventSource.Log;

                    //log.BufferAllocated(buffer.GetHashCode(), _bufferLength, _poolId, Id, ArrayPoolEventSource.BufferAllocatedReason.Pooled);
                }

                return buffer;
            }

            /// <summary>
            ///     Attempts to return the buffer to the bucket.  If successful, the buffer will be stored
            ///     in the bucket and true will be returned; otherwise, the buffer won't be stored, and false
            ///     will be returned.
            /// </summary>
            internal void Return(T[] array)
            {
                // Check to see if the buffer is the correct size for this bucket
                if (array.Length != _bufferLength)
                {
                    return;

                    //throw new ArgumentException("BufferNotFromPool",nameof(array));
                }

                // While holding the spin lock, if there's room available in the bucket,
                // put the buffer into the next available slot.  Otherwise, we just drop it.
                // The try/finally is necessary to properly handle thread aborts on platforms
                // which have them.
                try
                {
                    Monitor.Enter(_lock);

                    if (_index != 0)
                    {
                        _buffers[--_index] = array;
                    }
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }
    }
}