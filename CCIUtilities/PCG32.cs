using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CCIUtilities
{
    public class PCG32
    {
        private const ulong multiplier = 6364136223846793005U;
        private const double FINAL_CONSTANT = (double)UInt32.MaxValue + 1D;

        private ulong state = 0x4d595df4d0f33173;
        private readonly ulong increment = 1442695040888963407U;

        private static PCG32 instance = null;
        private static readonly object sync = new object();

        public static PCG32 Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (sync)
                    {
                        if (instance == null)
                            instance = new PCG32();
                    }
                }
                return instance;
            }
        }

        private PCG32()
        {
//            increment = (ulong)sync.GetHashCode() | 0x01;
            state = ((ulong)DateTime.Now.Ticks + increment) | 0x01;
            GenerateUint(); //throw out first RN
        }

        private static uint rotr32(uint x, int r)
        {
            return x >> r | x << (32 - r); //((-r) & 0x1F)
        }

        public uint GenerateUint()
        {
            ulong x;
            lock (sync)
            {
                x = state;
                state = x * multiplier + increment;
            }
            int count = (int)(x >> 59); // 59 = 64 - 5
            x ^= x >> 18; // 18 = (64 - 27)/2
            return rotr32((uint)(x >> 27), count); // 27 = 32 - 5
        }

        public double Generate()
        {
            return (double)GenerateUint() / FINAL_CONSTANT;
        }

        /// <summary>
        /// Generate random integer using PCG32 pseudo ramdom number generator
        /// </summary>
        /// <param name="lowerBound">lower bound</param>
        /// <param name="higherBound">upper bound</param>
        /// <returns>Random integer between lowerBound and upperBound inclusinve</returns>
        /// <exception cref="ArgumentException">if lowerBound is higher than upperBound</exception>
        public int Generate(int lowerBound, int higherBound)
        {
            if (higherBound < lowerBound)
                throw new ArgumentException("In PCG32.Generate(int,int): invalid arguments");
            return (int)Math.Floor(Generate() * (double)(higherBound - lowerBound + 1)) + lowerBound;
        }

        /// <summary>
        /// Generate random number using PCG32 pseudo random number generator
        /// </summary>
        /// <param name="lowerBound">lower bound</param>
        /// <param name="higherBound">upper bound</param>
        /// <returns>Random number between lowerBound and upperBound</returns>
        /// <exception cref="ArgumentException">if lowerBound is higher than upperBound</exception>
        public double Generate(double lowerBound, double higherBound)
        {
            if (higherBound < lowerBound)
                throw new ArgumentException("In PCG32.Generate(double,double): invalid arguments");
            return Generate() * (higherBound - lowerBound) + lowerBound;
        }

    }

    public class PCG32BitStream
    {
        private readonly PCG32 pcg = PCG32.Instance;
        private ulong rBits = 0;
        private int nextBit = 0;

        public PCG32BitStream()
        {
            rBits = pcg.GenerateUint();
            nextBit = 32;
        }

        public uint Generate(int nbits, bool rev = false)
        {
            if (nbits <= 0 | nbits > 32)
                throw new ArgumentException("In PCG32BitStream.Generate: argument out of range.");
            if (nextBit < nbits)
            {
                rBits |= ((ulong)PCG32.Instance.GenerateUint()) << nextBit;
                nextBit += 32;
            }
            uint b = (uint)(rBits & (0xFFFFFFFF >> (32 - nbits)));
            rBits >>= nbits;
            nextBit -= nbits;
            if (!rev)
                return b;
            b = ((b >> 1) & 0x55555555) | ((b & 0x55555555) << 1);
            b = ((b >> 2) & 0x33333333) | ((b & 0x33333333) << 2);
            b = ((b >> 4) & 0x0F0F0F0F) | ((b & 0x0F0F0F0F) << 4);
            b = ((b >> 8) & 0x00FF00FF) | ((b & 0x00FF00FF) << 8);
            b = (b >> 16) | (b << 16);
            return b >> (32 - nbits);
        }
    }
}
