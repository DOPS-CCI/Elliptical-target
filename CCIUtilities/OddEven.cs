namespace CCIUtilities
{
    static class OddEven
    {
        /// <summary>
        ///Query if odd or even
        /// </summary>
        /// <param name="n">Input number</param>
        /// <returns>True if even number, false if odd</returns>
        public static bool IsEven(int n)
        {
            return n == (n >> 1) << 1;
        }

        /// <summary>
        /// Query if odd or even
        /// </summary>
        /// <param name="n">Input number</param>
        /// <returns>YTrue if odd number, false if even</returns>
        public static bool IsOdd(int n)
        {
            return n != (n >> 1) << 1;
        }
    }
}
