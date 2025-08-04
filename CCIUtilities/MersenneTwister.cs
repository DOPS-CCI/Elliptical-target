using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace CCIUtilities
{
    public class MersenneTwister
    {
        private const int N = 624;
        private const int M = 397;
        private const UInt32 MATRIX_A = 0x9908B0DF;
        private const UInt32 UPPER_MASK = 0x80000000;
        private const UInt32 LOWER_MASK = 0x7FFFFFFF;
        private const ulong MASK32 = 0xFFFFFFFF;
        private const UInt32 TEMPERING_MASK_B = 0x9D2C5680;
        private const UInt32 TEMPERING_MASK_C = 0xEFC60000;
        private const double FINAL_CONSTANT = (double)UInt32.MaxValue + 1D;
        private static readonly ulong[] mag01 = { 0U, (ulong)MATRIX_A };

        private static volatile MersenneTwister instance = null;
        private static readonly object sync = new object();

        public static MersenneTwister Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (sync)
                    {
                        if (instance == null)
                            instance = new MersenneTwister();
                    }
                }
                return instance;
            }
        }

        private readonly ulong[] mt = new ulong[N];
        private int mti = 0;

        private MersenneTwister()
        {
            ulong s = (ulong)DateTime.Now.Ticks | 0x01;
            sgenrand(s);
        }

        private static ulong TEMPERING_SHIFT_U(ulong y)
        {
            return y >> 11;
        }

        private static ulong TEMPERING_SHIFT_S(ulong y)
        {
            return y << 7;
        }

        private static ulong TEMPERING_SHIFT_T(ulong y)
        {
            return y << 15;
        }

        private static ulong TEMPERING_SHIFT_L(ulong y)
        {
            return y >> 18;
        }

        private void sgenrand(ulong seed)
        {
            mt[0] = seed & MASK32;
            for (mti = 1; mti < N; mti++)
                mt[mti] = (69069 * mt[mti - 1]) & MASK32;
        }

        public uint GenerateUint()
        {
            ulong y;

            lock (sync)
            {
                if (mti >= N)
                {
                    int kk;
                    for (kk = 0; kk < N - M; kk++)
                    {
                        y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                        mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1];
                    }
                    for (; kk < N - 1; kk++)
                    {
                        y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                        mt[kk] = mt[kk + M - N] ^ (y >> 1) ^ mag01[y & 0x1];
                    }
                    y = (mt[kk] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                    mt[kk] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1];
                    mti = 0;
                }

                y = mt[mti++];
            }
            y ^= TEMPERING_SHIFT_U(y);
            y ^= TEMPERING_SHIFT_S(y) & TEMPERING_MASK_B;
            y ^= TEMPERING_SHIFT_T(y) & TEMPERING_MASK_C;
            y ^= TEMPERING_SHIFT_L(y);
            return (uint)(y & MASK32);
        }

        public double Generate()
        {
            return (double)GenerateUint() / FINAL_CONSTANT;
        }

        /// <summary>
        /// Generate random integer using Mersenne Twister pseudo ramdom number generator
        /// </summary>
        /// <param name="lowerBound">lower bound</param>
        /// <param name="higherBound">upper bound</param>
        /// <returns>Random integer between lowerBound and upperBound inclusinve</returns>
        /// <exception cref="ArgumentException">if lowerBound is higher than upperBound</exception>
        public int Generate(int lowerBound, int higherBound)
        {
            if (higherBound < lowerBound)
                throw new ArgumentException("In MersenneTwister.Generate(int,int): invalid arguments");
            return (int)Math.Floor(Generate() * (double)(higherBound - lowerBound + 1)) + lowerBound;
        }

        /// <summary>
        /// Generate random number using Mersenne Twister pseudo random number generator
        /// </summary>
        /// <param name="lowerBound">lower bound</param>
        /// <param name="higherBound">upper bound</param>
        /// <returns>Random number between lowerBound and upperBound</returns>
        /// <exception cref="ArgumentException">if lowerBound is higher than upperBound</exception>
        public double Generate(double lowerBound, double higherBound)
        {
            if (higherBound < lowerBound)
                throw new ArgumentException("In MersenneTwister.Generate(double,double): invalid arguments");
            return Generate() * (higherBound - lowerBound) + lowerBound;
        }
    }

    public class MTBitStream
    {
        private readonly MersenneTwister mt = MersenneTwister.Instance;
        private ulong rBits = 0U;
        private int nextBit = 0;

        public MTBitStream()
        {
            rBits = mt.GenerateUint();
            nextBit = 32;
        }
        public uint Generate(int nbits)
        {
            if (nbits <= 0 | nbits > 32)
                throw new ArgumentException("In MTBitStream.Generate: argument out of range.");
            if (nextBit < nbits)
            {
                rBits |= ((ulong)mt.GenerateUint()) << nextBit;
                nextBit += 32;
            }
            uint b = (uint)(rBits & (0xFFFFFFFF >> (32 - nbits)));
            rBits >>= nbits;
            nextBit -= nbits;
            return b;
        }
    }
}
