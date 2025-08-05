using System;

namespace CCIUtilities
{
    public static class IrwinHall
    {
        /// <summary>
        /// CDF of Irwin-Hall distribution
        /// </summary>
        /// <param name="x">Sum of samples</param>
        /// <param name="n">Number of samples</param>
        /// <returns>CDF value</returns>
        /// <remarks>Expect approximately 0.1% accuracy close to x = n/2; expect "tail" accuracy far better</remarks>
        public static double CDF(double x, int n)
        {
            
            if (x <= 0D) return 0D;
            double N = (double)n;
            if (x >= N) return 1D;
            if (n > 30) return NormalDistribution.CDF(x, N / 2D, Math.Sqrt(N / 12D)); //Use normal approx for n > 30
            double sum;
            double s;
            if (x < N / 2D)
            {
                sum = 0;
                s = 1D;
            }
            else
            {
                x = N - x;
                sum = 1D;
                s = -1D;
            }
            double xf = Math.Floor(x);
            for (double K = 0D; K <= xf; K++, s = -s)
                sum += s * IHterm(x, N, K);
            return sum;
        }

        private static double IHterm(double x, double N, double K)
        {
            double p = 1D;
            double d = x - K;
            for (double j = 1D; j <= K; j++) p *= d / j;
            for (double j = 1D; j <= N - K; j++) p *= d / j;
            return p;
        }
    }
}
