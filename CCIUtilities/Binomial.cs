using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCIUtilities
{
    public class Binomial
    {
        /// <summary>
        /// Binomial probability function
        /// </summary>
        /// <param name="k">Number of successes</param>
        /// <param name="n">Number of trials</param>
        /// <param name="p">Probaility of success</param>
        /// <returns>Probability of k successes out of n trials</returns>
        public static double PDF(int k, int n, double p)
        {
            if (p < 0D || p > 1D || k < 0 || n < 0 || n < k)
                throw new ArgumentException($"In BinomialDistribution.PDF: Invalid argument set [k, n, p] = [{k:0}, {n:0}, {p}]");
            return Coefficient(n, k) * Math.Pow(p, k) * Math.Pow(1D - p, n - k);
        }

        /// <summary>
        /// Binomial cumulative distribution function
        /// </summary>
        /// <param name="k">Number of successes</param>
        /// <param name="n">Number of trials</param>
        /// <param name="p">Probaility of success</param>
        /// <returns>Probaility of k or less out of n</returns>
        public static double CDF(int k, int n, double p)
        {
            if (p < 0D || p > 1D || n < 0 )
                throw new ArgumentException($"In BinomialDistribution.CDF: Invalid argument set [k, n, p] = [{k:0}, {n:0}, {p}]");
            if (k < 0) return 0D;
            if (k >= n) return 1D;
            double result = 0D;
            if (k <= n >> 1)
            {
                for (int i = 0; i <= k; i++)
                    result += Coefficient(n, i) * Math.Pow(p, i) * Math.Pow(1D - p, n - i);
                return result;
            }
            for (int i = k + 1; i <= n; i++)
                result += Coefficient(n, i) * Math.Pow(p, i) * Math.Pow(1D - p, n - i);
            return 1D - result;
        }

        /// <summary>
        /// Upper tail of binomial CDF
        /// </summary>
        /// <param name="k">Number of successes</param>
        /// <param name="n">Number of trials</param>
        /// <param name="p">Probaility of success</param>
        /// <returns>Probability of k or more successes out of n</returns>
        public static double UpperCDF(int k, int n, double p) => 1D - CDF(k - 1, n, p);

        public static double Coefficient(int n, int k)
        {
            k = Math.Min(k, n - k);
            double K = (double)k;
            double N = (double)n;
            double result = 1D;
            for (double I = 1D; I <= K; I++)
                result *= (N - I + 1D) / I;
            return result;
        }

        public static ulong ExactCoefficient(uint n, uint k)
        {
            if (k == 0U || k == n) return 1U;
            k = Math.Min(k, n - k);
            return k == 1U ? n : ExactCoefficient(n - 1, k - 1) + ExactCoefficient(n - 1, k);
        }
    }
}
