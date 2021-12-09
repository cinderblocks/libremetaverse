namespace OpenMetaverse.Interfaces
{
    /// <summary>
    /// Interface to a class that can supply cached byte arrays
    /// </summary>
    public interface IByteBufferPool
    {
        /// <summary>
        /// Leases a byte array from the pool
        /// </summary>
        /// <param name="minSize">Minimum size the array must be to satisfy this request</param>
        /// <returns>A poooled array that is at least minSize</returns>
        byte[] LeaseBytes(int minSize);

        /// <summary>
        /// Returns a byte array to the pool
        /// </summary>
        /// <param name="bytes">The bytes being returned</param>
        void ReturnBytes(byte[] bytes);
    }
}
